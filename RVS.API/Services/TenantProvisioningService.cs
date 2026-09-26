using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Provisioning;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Orchestrates the platform-admin provisioning tool (Spec P-1 … P-12, issues #563 and #647):
/// creates a tenant's Cosmos documents and first Auth0 user, adds, lists, edits, disables and
/// deletes users, adds locations, and flips the access gate.
/// <para>
/// <b>Safe to retry (P-6).</b> Cosmos writes run first, with fixed ids; Auth0 runs last. Every
/// step checks for what an earlier attempt left behind before writing, so re-submitting a
/// partially failed run finishes it without duplicates.
/// </para>
/// <para>
/// <b>Audit (P-7).</b> Every admin write is logged with the admin's user id, what changed and the
/// tenant id. Emails and set-password URLs are never logged.
/// </para>
/// </summary>
public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private static readonly EventId TenantProvisioned = new(563_001, nameof(TenantProvisioned));
    private static readonly EventId TenantUpdated = new(563_002, nameof(TenantUpdated));
    private static readonly EventId UserProvisioned = new(563_003, nameof(UserProvisioned));
    private static readonly EventId PasswordTicketIssued = new(563_004, nameof(PasswordTicketIssued));
    private static readonly EventId AccessGateChanged = new(563_005, nameof(AccessGateChanged));
    private static readonly EventId LocationProvisioned = new(563_006, nameof(LocationProvisioned));
    private static readonly EventId ProvisioningStepFailed = new(563_007, nameof(ProvisioningStepFailed));
    private static readonly EventId UserUpdated = new(647_001, nameof(UserUpdated));
    private static readonly EventId UserAccessChanged = new(647_002, nameof(UserAccessChanged));
    private static readonly EventId UserDeleted = new(647_003, nameof(UserDeleted));

    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantConfigService _tenantConfigService;
    private readonly IDealershipService _dealershipService;
    private readonly ILocationService _locationService;
    private readonly ISlugLookupRepository _slugLookupRepository;
    private readonly IIdentityProvisioner _identityProvisioner;
    private readonly IUserContextAccessor _userContext;
    private readonly ILogger<TenantProvisioningService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TenantProvisioningService"/>.
    /// </summary>
    public TenantProvisioningService(
        ITenantRepository tenantRepository,
        ITenantConfigService tenantConfigService,
        IDealershipService dealershipService,
        ILocationService locationService,
        ISlugLookupRepository slugLookupRepository,
        IIdentityProvisioner identityProvisioner,
        IUserContextAccessor userContext,
        ILogger<TenantProvisioningService> logger)
    {
        _tenantRepository = tenantRepository;
        _tenantConfigService = tenantConfigService;
        _dealershipService = dealershipService;
        _locationService = locationService;
        _slugLookupRepository = slugLookupRepository;
        _identityProvisioner = identityProvisioner;
        _userContext = userContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TenantOverview>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _tenantRepository.ListAllAsync(cancellationToken);

        var overviews = new List<TenantOverview>(tenants.Count);
        foreach (var tenant in tenants.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var config = await _tenantConfigService.GetTenantConfigAsync(tenant.Id, cancellationToken);
            var locations = await _locationService.ListByTenantAsync(tenant.Id, cancellationToken);
            overviews.Add(new TenantOverview(tenant, config.AccessGate, locations, config.AvailableCapabilities));
        }

        return overviews;
    }

    /// <inheritdoc />
    public async Task<TenantOverview> UpdateTenantAsync(
        string tenantId, TenantUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfInvalid(TenantProvisioningValidator.ValidateUpdateTenant(request));

        var tenant = await GetTenantOrThrowAsync(tenantId, cancellationToken);
        tenant.ApplyUpdate(request, _userContext.UserId);
        var saved = await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        _logger.LogInformation(
            TenantUpdated,
            "Platform admin {AdminUserId} updated tenant {TenantId}: status {Status}, plan {Plan}",
            _userContext.UserId, tenantId, saved.Status, saved.Plan);

        var config = await _tenantConfigService.GetTenantConfigAsync(tenantId, cancellationToken);
        var locations = await _locationService.ListByTenantAsync(tenantId, cancellationToken);
        return new TenantOverview(saved, config.AccessGate, locations, config.AvailableCapabilities);
    }

    /// <inheritdoc />
    public async Task<TenantProvisioningResult> CreateTenantAsync(
        TenantCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfInvalid(TenantProvisioningValidator.ValidateCreateTenant(request));

        var name = request.Name.Trim();
        var tenantId = string.IsNullOrWhiteSpace(request.TenantId)
            ? TenantIdGenerator.FromName(name)
            : request.TenantId.Trim();

        var tenantIdCheck = TenantProvisioningValidator.ValidateTenantId(tenantId);
        if (!tenantIdCheck.IsValid)
        {
            throw new ArgumentException(
                $"Could not use a tenant id for '{name}': {tenantIdCheck.ErrorMessage} Enter a tenant id explicitly.");
        }

        // Same id, same name: a retry that fills in whatever is missing. Same id, different name:
        // someone else's tenant — reject before writing anything.
        var existingTenant = await _tenantRepository.GetAsync(tenantId, cancellationToken);
        if (existingTenant is not null
            && !string.Equals(existingTenant.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                $"Tenant id '{tenantId}' is already used by '{existingTenant.Name}'. Choose a different tenant id.");
        }

        var run = new StepRun(_logger, tenantId);
        Dealership? dealership = null;
        Location? location = null;
        var locationCreatedThisRun = false;
        IdentityUserResult? user = null;
        PasswordTicket? ticket = null;

        await run.StepAsync(ProvisioningStepNames.Tenant, async () =>
        {
            if (existingTenant is not null)
            {
                return ProvisioningStepStatus.AlreadyExisted;
            }

            await _tenantRepository.CreateAsync(request.ToEntity(tenantId, _userContext.UserId), cancellationToken);
            return ProvisioningStepStatus.Created;
        });

        await run.StepAsync(ProvisioningStepNames.TenantConfig, async () =>
        {
            if (await TenantConfigExistsAsync(tenantId, cancellationToken))
            {
                return ProvisioningStepStatus.AlreadyExisted;
            }

            // New configs start with the access gate on (LoginsEnabled = true).
            await _tenantConfigService.CreateTenantConfigAsync(tenantId, new TenantConfigCreateRequestDto(), cancellationToken);
            return ProvisioningStepStatus.Created;
        });

        await run.StepAsync(ProvisioningStepNames.Dealership, async () =>
        {
            dealership = (await _dealershipService.ListByTenantAsync(tenantId, cancellationToken)).FirstOrDefault();
            if (dealership is not null)
            {
                return ProvisioningStepStatus.AlreadyExisted;
            }

            dealership = await _dealershipService.CreateAsync(tenantId, new Dealership
            {
                Id = TenantIdGenerator.DealershipIdFor(tenantId),
                TenantId = tenantId,
                Name = name,
                Slug = SlugGenerator.Slugify(name),
                CreatedByUserId = _userContext.UserId
            }, cancellationToken);
            return ProvisioningStepStatus.Created;
        });

        await run.StepAsync(ProvisioningStepNames.Location, async () =>
        {
            var firstLocationId = TenantIdGenerator.FirstLocationIdFor(tenantId);
            var locations = await _locationService.ListByTenantAsync(tenantId, cancellationToken);
            location = locations.FirstOrDefault(l => l.Id == firstLocationId) ?? locations.FirstOrDefault();
            if (location is not null)
            {
                return ProvisioningStepStatus.AlreadyExisted;
            }

            // LocationService reserves the slug (generating one when none was given) and fills in
            // the dealership name on the slug lookup before it writes the location.
            location = await _locationService.CreateAsync(tenantId, new Location
            {
                Id = firstLocationId,
                TenantId = tenantId,
                Name = LocationNameGenerator.ForBusiness(existingTenant?.Name ?? name, request.LocationName),
                Slug = string.IsNullOrWhiteSpace(request.LocationSlug) ? string.Empty : request.LocationSlug.Trim(),
                Phone = TrimToNull(request.LocationPhone),
                PacketConfig = new PacketConfigEmbedded { Recipients = [request.OwnerEmail.Trim()] },
                CreatedByUserId = _userContext.UserId
            }, cancellationToken);
            locationCreatedThisRun = true;
            return ProvisioningStepStatus.Created;
        });

        await run.StepAsync(ProvisioningStepNames.SlugLookup, async () =>
        {
            if (locationCreatedThisRun)
            {
                return ProvisioningStepStatus.Created;
            }

            var slug = location!.Slug;
            var lookup = await _slugLookupRepository.GetBySlugAsync(slug, cancellationToken);
            if (lookup is null)
            {
                await _slugLookupRepository.CreateAsync(new SlugLookup
                {
                    Id = $"slug_{slug}",
                    TenantId = tenantId,
                    Slug = slug,
                    LocationId = location.Id,
                    DealershipName = dealership?.Name ?? name,
                    LocationName = location.Name,
                    CreatedByUserId = _userContext.UserId
                }, cancellationToken);
                return ProvisioningStepStatus.Created;
            }

            if (!string.Equals(lookup.TenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(lookup.LocationId, location.Id, StringComparison.Ordinal))
            {
                throw new ConflictException($"Slug '{slug}' points at a different location.");
            }

            return ProvisioningStepStatus.AlreadyExisted;
        });

        await run.StepAsync(ProvisioningStepNames.IdentityUser, async () =>
        {
            IReadOnlyList<string> locationIds = TenantProvisioningValidator.IsLocationScopedRole(request.OwnerRole)
                ? [location!.Id]
                : [];

            user = await _identityProvisioner.EnsureUserAsync(new IdentityUserRequest(
                request.OwnerEmail.Trim(),
                request.OwnerDisplayName.Trim(),
                tenantId,
                existingTenant?.Name ?? name,
                locationIds,
                request.OwnerRole), cancellationToken);

            ticket = await _identityProvisioner.CreatePasswordTicketAsync(user.UserId, cancellationToken);
            return user.Created ? ProvisioningStepStatus.Created : ProvisioningStepStatus.AlreadyExisted;
        });

        _logger.LogInformation(
            TenantProvisioned,
            "Platform admin {AdminUserId} provisioned tenant {TenantId}: {Steps}",
            _userContext.UserId,
            tenantId,
            string.Join(", ", run.Steps.Select(s => $"{s.Name}={s.Status}")));

        return new TenantProvisioningResult(tenantId, run.Steps, location, user?.UserId, ticket);
    }

    /// <inheritdoc />
    public async Task<TenantUserProvisioningResult> AddUserAsync(
        string tenantId, TenantUserCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfInvalid(TenantProvisioningValidator.ValidateAddUser(request));

        var tenant = await GetTenantOrThrowAsync(tenantId, cancellationToken);
        var locationIds = await ResolveLocationIdsAsync(tenantId, request.Role, request.LocationIds, cancellationToken);

        var email = request.Email.Trim();
        var user = await _identityProvisioner.EnsureUserAsync(new IdentityUserRequest(
            email,
            request.DisplayName.Trim(),
            tenantId,
            tenant.Name,
            locationIds,
            request.Role), cancellationToken);

        var ticket = await _identityProvisioner.CreatePasswordTicketAsync(user.UserId, cancellationToken);

        _logger.LogInformation(
            UserProvisioned,
            "Platform admin {AdminUserId} {Outcome} user {UserId} with role {Role} in tenant {TenantId}",
            _userContext.UserId, user.Created ? "created" : "updated", user.UserId, request.Role, tenantId);

        return new TenantUserProvisioningResult(tenantId, user.UserId, email, request.Role, user.Created, ticket);
    }

    /// <inheritdoc />
    public async Task<PasswordTicket> CreatePasswordTicketAsync(
        string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await GetTenantOrThrowAsync(tenantId, cancellationToken);
        await GetTenantUserOrThrowAsync(tenantId, userId, cancellationToken);

        var ticket = await _identityProvisioner.CreatePasswordTicketAsync(userId, cancellationToken);

        _logger.LogInformation(
            PasswordTicketIssued,
            "Platform admin {AdminUserId} issued a set-password link for user {UserId} in tenant {TenantId}",
            _userContext.UserId, userId, tenantId);

        return ticket;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IdentityUser>> ListUsersAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        await GetTenantOrThrowAsync(tenantId, cancellationToken);

        // The provider filters by tenant already; filtering again keeps a loose search from ever
        // showing one tenant another tenant's users.
        var users = await _identityProvisioner.ListUsersAsync(tenantId, cancellationToken);
        return users
            .Where(u => string.Equals(u.TenantId, tenantId, StringComparison.Ordinal))
            .OrderBy(u => u.DisplayName ?? u.Email, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IdentityUser> UpdateUserAsync(
        string tenantId, string userId, TenantUserUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfInvalid(TenantProvisioningValidator.ValidateUpdateUser(request));

        await GetTenantOrThrowAsync(tenantId, cancellationToken);
        await GetTenantUserOrThrowAsync(tenantId, userId, cancellationToken);
        var locationIds = await ResolveLocationIdsAsync(tenantId, request.Role, request.LocationIds, cancellationToken);

        var user = await _identityProvisioner.UpdateUserAsync(
            userId,
            new IdentityUserUpdate(request.DisplayName.Trim(), locationIds, request.Role),
            cancellationToken);

        _logger.LogInformation(
            UserUpdated,
            "Platform admin {AdminUserId} updated user {UserId} to role {Role} with {LocationCount} location(s) in tenant {TenantId}",
            _userContext.UserId, userId, request.Role, locationIds.Count, tenantId);

        return user;
    }

    /// <inheritdoc />
    public async Task<IdentityUser> SetUserLoginsEnabledAsync(
        string tenantId, string userId, TenantUserAccessUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);

        await GetTenantOrThrowAsync(tenantId, cancellationToken);
        await GetTenantUserOrThrowAsync(tenantId, userId, cancellationToken);

        var user = await _identityProvisioner.SetBlockedAsync(userId, blocked: !request.LoginsEnabled, cancellationToken);

        _logger.LogInformation(
            UserAccessChanged,
            "Platform admin {AdminUserId} {Action} logins for user {UserId} in tenant {TenantId}",
            _userContext.UserId, request.LoginsEnabled ? "enabled" : "disabled", userId, tenantId);

        return user;
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await GetTenantOrThrowAsync(tenantId, cancellationToken);
        await GetTenantUserOrThrowAsync(tenantId, userId, cancellationToken);

        await _identityProvisioner.DeleteUserAsync(userId, cancellationToken);

        _logger.LogInformation(
            UserDeleted,
            "Platform admin {AdminUserId} deleted user {UserId} from tenant {TenantId}",
            _userContext.UserId, userId, tenantId);
    }

    /// <inheritdoc />
    public async Task<TenantOverview> SetAccessGateAsync(
        string tenantId, TenantAccessGateUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfInvalid(TenantProvisioningValidator.ValidateAccessGate(request));

        var tenant = await GetTenantOrThrowAsync(tenantId, cancellationToken);
        var reason = request.LoginsEnabled ? null : request.Reason?.Trim();

        // Access only: Tenant.Status (the commercial state) is deliberately left alone.
        var config = await _tenantConfigService.SetAccessGateAsync(tenantId, request.LoginsEnabled, reason, cancellationToken);

        _logger.LogInformation(
            AccessGateChanged,
            "Platform admin {AdminUserId} {Action} logins for tenant {TenantId} (reason: {Reason})",
            _userContext.UserId, request.LoginsEnabled ? "enabled" : "disabled", tenantId, reason ?? "none");

        var locations = await _locationService.ListByTenantAsync(tenantId, cancellationToken);
        return new TenantOverview(tenant, config.AccessGate, locations, config.AvailableCapabilities);
    }

    /// <inheritdoc />
    public async Task<Location> AddLocationAsync(
        string tenantId, TenantLocationCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfInvalid(TenantProvisioningValidator.ValidateAddLocation(request));

        var tenant = await GetTenantOrThrowAsync(tenantId, cancellationToken);

        var location = await _locationService.CreateAsync(tenantId, new Location
        {
            TenantId = tenantId,
            Name = LocationNameGenerator.ForBusiness(tenant.Name, request.Name),
            Slug = string.IsNullOrWhiteSpace(request.Slug) ? string.Empty : request.Slug.Trim(),
            Phone = TrimToNull(request.Phone),
            TimeZoneId = TrimToNull(request.TimeZoneId),
            PacketConfig = new PacketConfigEmbedded { Recipients = [.. request.Recipients.Select(r => r.Trim())] },
            EnabledCapabilities = request.EnabledCapabilities is not null ? [.. request.EnabledCapabilities] : [],
            CreatedByUserId = _userContext.UserId
        }, cancellationToken);

        _logger.LogInformation(
            LocationProvisioned,
            "Platform admin {AdminUserId} added location {LocationId} ({Slug}) to tenant {TenantId}",
            _userContext.UserId, location.Id, location.Slug, tenantId);

        return location;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<Tenant> GetTenantOrThrowAsync(string tenantId, CancellationToken cancellationToken) =>
        await _tenantRepository.GetAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' not found.");

    /// <summary>
    /// Gets a user of the tenant. A user in another tenant is reported as not found rather than
    /// forbidden: the admin is acting on this tenant, and the answer should not depend on who
    /// else exists.
    /// </summary>
    private async Task<IdentityUser> GetTenantUserOrThrowAsync(string tenantId, string userId, CancellationToken cancellationToken)
    {
        var user = await _identityProvisioner.GetUserAsync(userId, cancellationToken);
        if (user is null || !string.Equals(user.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException($"User '{userId}' not found in tenant '{tenantId}'.");
        }

        return user;
    }

    /// <summary>
    /// The trimmed, de-duplicated locations for a location-scoped role, each checked to belong to
    /// the tenant; empty for <c>dealer:owner</c>, which is tenant-wide.
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolveLocationIdsAsync(
        string tenantId, string role, IEnumerable<string> locationIds, CancellationToken cancellationToken)
    {
        if (!TenantProvisioningValidator.IsLocationScopedRole(role))
        {
            return [];
        }

        var requested = locationIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var known = (await _locationService.ListByTenantAsync(tenantId, cancellationToken))
            .Select(l => l.Id)
            .ToHashSet(StringComparer.Ordinal);

        var unknown = requested.Where(id => !known.Contains(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"Location(s) not found in tenant '{tenantId}': {string.Join(", ", unknown)}.");
        }

        return requested;
    }

    private async Task<bool> TenantConfigExistsAsync(string tenantId, CancellationToken cancellationToken)
    {
        try
        {
            await _tenantConfigService.GetTenantConfigAsync(tenantId, cancellationToken);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private static void ThrowIfInvalid(ValidationResult result)
    {
        if (!result.IsValid)
        {
            throw new ArgumentException(result.ErrorMessage);
        }
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Runs provisioning steps in order and records each outcome. After the first failure the
    /// remaining steps are recorded as skipped rather than attempted, because each depends on the
    /// ones before it. Cancellation is never swallowed.
    /// </summary>
    private sealed class StepRun(ILogger logger, string tenantId)
    {
        private readonly List<ProvisioningStep> _steps = [];
        private bool _failed;

        public IReadOnlyList<ProvisioningStep> Steps => _steps;

        public async Task StepAsync(string name, Func<Task<string>> step)
        {
            if (_failed)
            {
                _steps.Add(new ProvisioningStep(name, ProvisioningStepStatus.Skipped));
                return;
            }

            try
            {
                _steps.Add(new ProvisioningStep(name, await step()));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _failed = true;
                logger.LogWarning(ProvisioningStepFailed, ex,
                    "Provisioning step {Step} failed for tenant {TenantId}", name, tenantId);
                _steps.Add(new ProvisioningStep(name, ProvisioningStepStatus.Failed, ex.Message));
            }
        }
    }
}
