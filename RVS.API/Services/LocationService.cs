using System.Net;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="Location"/> entities with atomic slug management.
/// Creates a <see cref="SlugLookup"/> entry before the location to guarantee slug uniqueness,
/// and rolls back the slug entry if location creation fails.
/// </summary>
public sealed class LocationService : ILocationService
{
    /// <summary>Logged when one packet recipient is disabled after a hard bounce (<c>Spec B-4</c>, #439).</summary>
    private static readonly EventId RecipientHardBounced = new(439_001, nameof(RecipientHardBounced));

    /// <summary>Logged when a hard bounce leaves a location with no working packet recipients (<c>Spec B-4</c>, #439).</summary>
    private static readonly EventId AllRecipientsBounced = new(439_002, nameof(AllRecipientsBounced));

    private readonly ILocationRepository _locationRepository;
    private readonly ISlugLookupRepository _slugLookupRepository;
    private readonly IDealershipRepository _dealershipRepository;
    private readonly IUserContextAccessor _userContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<LocationService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="LocationService"/>.
    /// </summary>
    public LocationService(
        ILocationRepository locationRepository,
        ISlugLookupRepository slugLookupRepository,
        IDealershipRepository dealershipRepository,
        IUserContextAccessor userContext,
        INotificationService notificationService,
        ILogger<LocationService> logger)
    {
        _locationRepository = locationRepository;
        _slugLookupRepository = slugLookupRepository;
        _dealershipRepository = dealershipRepository;
        _userContext = userContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Location> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await _locationRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{id}' not found.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Location>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return await _locationRepository.ListByTenantAsync(tenantId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Location> CreateAsync(string tenantId, Location entity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entity);

        ValidatePacketConfig(entity);

        // Auto-generate a unique slug from the dealership ("org") slug + location name when
        // the caller did not supply one. This keeps slugs uniform, human-readable, and unique
        // per tenant without requiring the UI to pick a slug.
        if (string.IsNullOrWhiteSpace(entity.Slug))
        {
            entity.Slug = await GenerateUniqueSlugAsync(tenantId, entity.Name, cancellationToken);
        }
        else
        {
            // Caller supplied a slug — make sure it is not already taken.
            var existing = await _slugLookupRepository.GetBySlugAsync(entity.Slug, cancellationToken);
            if (existing is not null)
            {
                throw new ArgumentException($"Slug '{entity.Slug}' is already in use.", nameof(entity));
            }
        }

        // Step 1: Create slug lookup entry first to reserve the slug
        var slugLookup = new SlugLookup
        {
            Id = $"slug_{entity.Slug}",
            TenantId = tenantId,
            Slug = entity.Slug,
            LocationId = entity.Id,
            DealershipName = string.Empty,
            LocationName = entity.Name,
            CreatedByUserId = _userContext.UserId
        };

        await _slugLookupRepository.UpsertAsync(slugLookup, cancellationToken);

        try
        {
            // Step 2: Create the location document
            return await _locationRepository.CreateAsync(entity, cancellationToken);
        }
        catch
        {
            // Rollback: delete the slug entry if location creation fails
            try
            {
                await _slugLookupRepository.DeleteAsync(entity.Slug, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to rollback slug '{Slug}' after location creation failure", entity.Slug);
            }

            throw;
        }
    }

    /// <summary>
    /// Builds a slug shaped like <c>{dealership-slug}-{location-name}</c> and probes the slug-lookup
    /// store for collisions, appending <c>-2</c>, <c>-3</c>, … until a free slug is found.
    /// Falls back to just the location-name slug when the tenant has no dealership yet.
    /// </summary>
    private async Task<string> GenerateUniqueSlugAsync(string tenantId, string locationName, CancellationToken cancellationToken)
    {
        var dealerships = await _dealershipRepository.ListByTenantAsync(tenantId, cancellationToken);
        var orgSlug = dealerships.FirstOrDefault()?.Slug;

        var baseSlug = SlugGenerator.ForLocation(orgSlug, locationName);
        if (string.IsNullOrEmpty(baseSlug))
        {
            // Defensive fallback: a location-id-derived slug is always non-empty and unique.
            baseSlug = $"location-{Guid.NewGuid():N}"[..32];
        }

        // Cap the base so suffix variants stay within MaxSlugLength.
        const int suffixReserve = 4; // supports up to "-9999"
        if (baseSlug.Length > SlugGenerator.MaxSlugLength - suffixReserve)
        {
            baseSlug = baseSlug[..(SlugGenerator.MaxSlugLength - suffixReserve)].TrimEnd('-');
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await _slugLookupRepository.GetBySlugAsync(candidate, cancellationToken) is not null)
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    /// <inheritdoc />
    public async Task<Location> UpdateAsync(string tenantId, string id, Location entity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(entity);

        ValidatePacketConfig(entity);

        var existing = await _locationRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{id}' not found.");

        var oldSlug = existing.Slug;
        var newSlug = entity.Slug;

        // If slug changed, manage slug lookup entries atomically
        if (!string.Equals(oldSlug, newSlug, StringComparison.Ordinal))
        {
            var slugLookup = new SlugLookup
            {
                Id = $"slug_{newSlug}",
                TenantId = tenantId,
                Slug = newSlug,
                LocationId = existing.Id,
                DealershipName = string.Empty,
                LocationName = entity.Name,
                CreatedByUserId = _userContext.UserId
            };

            await _slugLookupRepository.UpsertAsync(slugLookup, cancellationToken);

            // Delete old slug entry
            try
            {
                await _slugLookupRepository.DeleteAsync(oldSlug, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old slug '{Slug}' during location update", oldSlug);
            }
        }

        // The disabled-recipient list (Spec B-4, #439) is owned by the hard-bounce flow, not the
        // settings payload — carry it across the update. But an address the caller has put back
        // into the active recipient list is an explicit re-enable, so drop it from the disabled
        // list rather than letting it sit in both.
        var carriedDisabled = existing.PacketConfig.DisabledRecipients
            .Where(d => !entity.PacketConfig.Recipients.Any(r =>
                string.Equals(r?.Trim(), d.Email?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        existing.Name = entity.Name;
        existing.Slug = entity.Slug;
        existing.Phone = entity.Phone;
        existing.Address = entity.Address;
        existing.IntakeConfig = entity.IntakeConfig;
        existing.EnabledCapabilities = entity.EnabledCapabilities;
        existing.PacketConfig = entity.PacketConfig;
        existing.PacketConfig.DisabledRecipients = carriedDisabled;
        existing.MarkAsUpdated(_userContext.UserId);

        ValidatePacketConfig(existing);

        return await _locationRepository.UpdateAsync(existing, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Location> DisableRecipientForBounceAsync(
        string tenantId, string id, string recipientEmail, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);

        var location = await _locationRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{id}' not found.");

        var disabled = location.PacketConfig.DisableRecipient(recipientEmail, reason, DateTime.UtcNow);
        if (!disabled)
        {
            _logger.LogInformation(
                "Hard bounce for {Recipient} at location {LocationId} in tenant {TenantId}: not an active packet recipient, nothing to disable",
                recipientEmail, id, tenantId);
            return location;
        }

        location.MarkAsUpdated(_userContext.UserId);
        var saved = await _locationRepository.UpdateAsync(location, cancellationToken);

        var remaining = saved.PacketConfig.Recipients
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        if (remaining.Count == 0)
        {
            _logger.LogCritical(
                AllRecipientsBounced,
                "Every packet recipient for location {LocationId} in tenant {TenantId} has now hard-bounced; no packets can be delivered for this location until an address is fixed in its settings",
                id, tenantId);
        }
        else
        {
            _logger.LogWarning(
                RecipientHardBounced,
                "Packet recipient at location {LocationId} in tenant {TenantId} disabled after a hard bounce; notifying {RemainingCount} remaining recipient(s)",
                id, tenantId, remaining.Count);

            await NotifyRemainingRecipientsAsync(saved, recipientEmail, reason, remaining, cancellationToken);
        }

        return saved;
    }

    /// <inheritdoc />
    public async Task<Location> ReEnableRecipientAsync(
        string tenantId, string id, string recipientEmail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);

        var location = await _locationRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{id}' not found.");

        if (!location.PacketConfig.ReEnableRecipient(recipientEmail))
        {
            _logger.LogInformation(
                "Re-enable request for {Recipient} at location {LocationId} in tenant {TenantId}: not a disabled recipient, nothing to do",
                recipientEmail, id, tenantId);
            return location;
        }

        ValidatePacketConfig(location);
        location.MarkAsUpdated(_userContext.UserId);
        return await _locationRepository.UpdateAsync(location, cancellationToken);
    }

    /// <summary>
    /// Tells the still-active recipients that one address was dropped from packet delivery after a
    /// hard bounce (<c>Spec B-4</c>, issue #439). One send per recipient; a send that throws is
    /// logged and skipped so a second bad address does not stop the rest. The body carries no
    /// customer data (<c>Spec X-7</c>).
    /// </summary>
    private async Task NotifyRemainingRecipientsAsync(
        Location location, string disabledEmail, string? reason, IReadOnlyList<string> remaining, CancellationToken cancellationToken)
    {
        var reasonSuffix = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $" ({WebUtility.HtmlEncode(reason.Trim())})";
        var subject = "[RVS] A packet email recipient was disabled after a hard bounce";
        var htmlBody =
            $"<p>The address <strong>{WebUtility.HtmlEncode(disabledEmail)}</strong> was removed from the "
            + $"service-packet recipients for <strong>{WebUtility.HtmlEncode(location.Name)}</strong> because "
            + $"email to it hard-bounced{reasonSuffix}.</p>"
            + "<p>Packets will keep going to the remaining recipients. Once the address is fixed, re-add it "
            + "in the location's packet settings.</p>";

        foreach (var recipient in remaining)
        {
            try
            {
                await _notificationService.SendEmailAsync(recipient, subject, htmlBody, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Could not notify remaining packet recipient at location {LocationId} that an address was disabled after a hard bounce",
                    location.Id);
            }
        }
    }

    /// <summary>
    /// Rejects a location whose packet configuration breaks a <c>Spec B-6</c> rule — most notably
    /// the 0–<see cref="PacketConfigEmbedded.MaxRecipients"/> recipient bound. Surfaces as a
    /// <see cref="ArgumentException"/> (HTTP 400) via <c>ExceptionHandlingMiddleware</c>.
    /// </summary>
    private static void ValidatePacketConfig(Location entity)
    {
        var result = PacketConfigValidator.Validate(entity.PacketConfig);
        if (!result.IsValid)
        {
            throw new ArgumentException(result.ErrorMessage, nameof(entity));
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var existing = await _locationRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{id}' not found.");

        // Delete location first, then clean up slug
        await _locationRepository.DeleteAsync(tenantId, id, cancellationToken);

        try
        {
            await _slugLookupRepository.DeleteAsync(existing.Slug, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete slug '{Slug}' during location delete", existing.Slug);
        }
    }
}
