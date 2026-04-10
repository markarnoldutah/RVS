using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Core 7-step intake orchestration sequence that creates up to 5 Cosmos documents
/// (GlobalCustomerAcct, CustomerProfile, ServiceRequest, AssetLedgerEntry, updated linkages)
/// in a single intake request.
/// </summary>
public sealed class IntakeOrchestrationService : IIntakeOrchestrationService
{
    private readonly ISlugLookupRepository _slugLookupRepository;
    private readonly IGlobalCustomerAcctRepository _globalCustomerAcctRepository;
    private readonly ICustomerProfileRepository _customerProfileRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IAssetLedgerRepository _assetLedgerRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly ICategorizationService _categorizationService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<IntakeOrchestrationService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeOrchestrationService"/>.
    /// </summary>
    public IntakeOrchestrationService(
        ISlugLookupRepository slugLookupRepository,
        IGlobalCustomerAcctRepository globalCustomerAcctRepository,
        ICustomerProfileRepository customerProfileRepository,
        IServiceRequestRepository serviceRequestRepository,
        IAssetLedgerRepository assetLedgerRepository,
        ILocationRepository locationRepository,
        ILookupRepository lookupRepository,
        ICategorizationService categorizationService,
        INotificationService notificationService,
        ILogger<IntakeOrchestrationService> logger)
    {
        _slugLookupRepository = slugLookupRepository;
        _globalCustomerAcctRepository = globalCustomerAcctRepository;
        _customerProfileRepository = customerProfileRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _assetLedgerRepository = assetLedgerRepository;
        _locationRepository = locationRepository;
        _lookupRepository = lookupRepository;
        _categorizationService = categorizationService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(ServiceRequest ServiceRequest, string? MagicLinkToken)> ExecuteAsync(string slug, ServiceRequestCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(request);

        // ── Step 1: Resolve slug → tenantId + locationId ─────────────────────
        var slugLookup = await _slugLookupRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new KeyNotFoundException($"Location slug '{slug}' not found.");

        var tenantId = slugLookup.TenantId;
        var locationId = slugLookup.LocationId;

        _logger.LogInformation("Intake Step 1 complete: slug={Slug} → tenantId={TenantId}, locationId={LocationId}",
            slug, tenantId, locationId);

        // ── Step 2: Resolve GlobalCustomerAcct by email (create if absent) ───
        var normalizedEmail = request.Customer.Email.Trim().ToLowerInvariant();
        var globalAcct = await _globalCustomerAcctRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (globalAcct is null)
        {
            globalAcct = new GlobalCustomerAcct
            {
                Email = normalizedEmail,
                FirstName = request.Customer.FirstName.Trim(),
                LastName = request.Customer.LastName.Trim(),
                Phone = request.Customer.Phone?.Trim(),
                CreatedByUserId = "intake",
            };
            globalAcct = await _globalCustomerAcctRepository.CreateAsync(globalAcct, cancellationToken);
            _logger.LogInformation("Intake Step 2: Created new GlobalCustomerAcct {AcctId} for {Email}",
                globalAcct.Id, normalizedEmail);
        }
        else
        {
            globalAcct.Phone = request.Customer.Phone?.Trim();
            _logger.LogInformation("Intake Step 2: Resolved existing GlobalCustomerAcct {AcctId} for {Email}",
                globalAcct.Id, normalizedEmail);
        }

        // ── Step 3: Resolve CustomerProfile + asset ownership ────────────────
        var profile = await _customerProfileRepository.GetByEmailAsync(tenantId, normalizedEmail, cancellationToken);

        if (profile is null)
        {
            profile = new CustomerProfile
            {
                TenantId = tenantId,
                Email = normalizedEmail,
                FirstName = request.Customer.FirstName.Trim(),
                LastName = request.Customer.LastName.Trim(),
                Phone = request.Customer.Phone?.Trim(),
                Name = $"{request.Customer.FirstName.Trim()} {request.Customer.LastName.Trim()}",
                GlobalCustomerAcctId = globalAcct.Id,
                CreatedByUserId = "intake",
            };
            profile = await _customerProfileRepository.CreateAsync(profile, cancellationToken);
            _logger.LogInformation("Intake Step 3: Created new CustomerProfile {ProfileId} in tenant {TenantId}",
                profile.Id, tenantId);
        }
        else
        {
            profile.Phone = request.Customer.Phone?.Trim();
            _logger.LogInformation("Intake Step 3: Resolved existing CustomerProfile {ProfileId} in tenant {TenantId}",
                profile.Id, tenantId);
        }

        var assetId = request.Asset.AssetId.Trim();

        var existingOwner = await _customerProfileRepository.GetByActiveAssetIdAsync(tenantId, assetId, cancellationToken);
        if (existingOwner is not null && existingOwner.Id != profile.Id)
        {
            existingOwner.DeactivateAsset(assetId);
            existingOwner.MarkAsUpdated("intake");
            await _customerProfileRepository.UpdateAsync(existingOwner, cancellationToken);
            _logger.LogInformation("Intake Step 3: Transferred asset {AssetId} ownership from profile {OldProfileId} to {NewProfileId}",
                assetId, existingOwner.Id, profile.Id);
        }

        profile.ActivateOrRefreshAsset(assetId, request.Asset.Manufacturer?.Trim(), request.Asset.Model?.Trim(), request.Asset.Year);
        profile.MarkAsUpdated("intake");
        profile = await _customerProfileRepository.UpdateAsync(profile, cancellationToken);

        // ── Step 4: Create ServiceRequest ────────────────────────────────────
        string? aiCategory = null;
        try
        {
            aiCategory = await _categorizationService.CategorizeAsync(request.IssueDescription, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI categorization failed; falling back to request-provided category");
        }

        var issueCategory = aiCategory ?? request.IssueCategory.Trim();

        var technicianSummary = BuildTechnicianSummary(request);

        var priorRequestCount = profile.TotalRequestCount;

        var serviceRequest = new ServiceRequest
        {
            TenantId = tenantId,
            LocationId = locationId,
            CustomerProfileId = profile.Id,
            Status = "New",
            IssueCategory = issueCategory,
            IssueDescription = request.IssueDescription.Trim(),
            TechnicianSummary = technicianSummary,
            Urgency = request.Urgency?.Trim(),
            RvUsage = request.RvUsage?.Trim(),
            CustomerSnapshot = new CustomerSnapshotEmbedded
            {
                FirstName = request.Customer.FirstName.Trim(),
                LastName = request.Customer.LastName.Trim(),
                Email = request.Customer.Email.Trim(),
                Phone = request.Customer.Phone?.Trim(),
                IsReturningCustomer = priorRequestCount > 0,
                PriorRequestCount = priorRequestCount,
            },
            AssetInfo = new AssetInfoEmbedded
            {
                AssetId = assetId,
                Manufacturer = request.Asset.Manufacturer?.Trim(),
                Model = request.Asset.Model?.Trim(),
                Year = request.Asset.Year,
            },
            DiagnosticResponses = request.DiagnosticResponses?
                .Select(d => new DiagnosticResponseEmbedded
                {
                    QuestionText = d.QuestionText.Trim(),
                    SelectedOptions = d.SelectedOptions,
                    FreeTextResponse = d.FreeTextResponse?.Trim(),
                })
                .ToList() ?? [],
            CreatedByUserId = "intake",
        };

        serviceRequest = await _serviceRequestRepository.CreateAsync(serviceRequest, cancellationToken);
        _logger.LogInformation("Intake Step 4: Created ServiceRequest {ServiceRequestId} in tenant {TenantId}",
            serviceRequest.Id, tenantId);

        // ── Step 5: Append AssetLedgerEntry (non-blocking on failure) ────────
        try
        {
            var ledgerEntry = new AssetLedgerEntry
            {
                AssetId = assetId,
                TenantId = tenantId,
                DealershipName = slugLookup.DealershipName,
                ServiceRequestId = serviceRequest.Id,
                GlobalCustomerAcctId = globalAcct.Id,
                Manufacturer = request.Asset.Manufacturer?.Trim(),
                Model = request.Asset.Model?.Trim(),
                Year = request.Asset.Year,
                IssueCategory = issueCategory,
                IssueDescription = request.IssueDescription.Trim(),
                SubmittedAtUtc = serviceRequest.CreatedAtUtc,
            };

            await _assetLedgerRepository.AppendAsync(ledgerEntry, cancellationToken);
            _logger.LogInformation("Intake Step 5: Appended AssetLedgerEntry for asset {AssetId}, SR {ServiceRequestId}",
                assetId, serviceRequest.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intake Step 5: Failed to append AssetLedgerEntry for asset {AssetId}, SR {ServiceRequestId}. Continuing intake.",
                assetId, serviceRequest.Id);
        }

        // ── Step 6: Update linkages ──────────────────────────────────────────
        profile.TotalRequestCount++;
        profile.ServiceRequestIds.Add(serviceRequest.Id);
        profile.MarkAsUpdated("intake");
        await _customerProfileRepository.UpdateAsync(profile, cancellationToken);
        _logger.LogInformation("Intake Step 6: Updated CustomerProfile {ProfileId} requestCount={Count}",
            profile.Id, profile.TotalRequestCount);

        var tokenIsAbsent = globalAcct.MagicLinkToken is null;
        var tokenIsExpired = globalAcct.MagicLinkExpiresAtUtc.HasValue &&
                             globalAcct.MagicLinkExpiresAtUtc.Value <= DateTime.UtcNow;

        if (tokenIsAbsent || tokenIsExpired)
        {
            globalAcct.MagicLinkToken = GlobalCustomerAcctService.GenerateMagicLinkToken(normalizedEmail);
            globalAcct.MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(90);
        }
        if (!globalAcct.AllKnownAssetIds.Contains(assetId))
        {
            globalAcct.AllKnownAssetIds.Add(assetId);
        }

        var alreadyLinked = globalAcct.LinkedProfiles
            .Any(lp => lp.TenantId == tenantId && lp.ProfileId == profile.Id);
        if (!alreadyLinked)
        {
            globalAcct.LinkedProfiles.Add(new LinkedProfileEmbedded
            {
                TenantId = tenantId,
                ProfileId = profile.Id,
                DealershipName = slugLookup.DealershipName,
                FirstSeenAtUtc = DateTime.UtcNow,
                RequestCount = 1,
            });
        }
        else
        {
            var linked = globalAcct.LinkedProfiles
                .First(lp => lp.TenantId == tenantId && lp.ProfileId == profile.Id);
            linked.RequestCount++;
        }

        globalAcct.MarkAsUpdated("intake");
        await _globalCustomerAcctRepository.UpdateAsync(globalAcct, cancellationToken);
        _logger.LogInformation(
            tokenIsAbsent || tokenIsExpired
                ? "Intake Step 6: Generated new magic-link token for GlobalCustomerAcct {AcctId}"
                : "Intake Step 6: Reused existing magic-link token for GlobalCustomerAcct {AcctId}",
            globalAcct.Id);

        // ── Step 7: Fire-and-forget notification ─────────────────────────────
        _ = FireAndForgetNotificationAsync(request.Customer.Email.Trim(), serviceRequest.Id);

        return (serviceRequest, globalAcct.MagicLinkToken);
    }

    /// <summary>
    /// Builds a technician summary from the issue description and diagnostic responses.
    /// </summary>
    private static string BuildTechnicianSummary(ServiceRequestCreateRequestDto request)
    {
        var parts = new List<string> { $"Issue: {request.IssueDescription.Trim()}" };

        if (request.DiagnosticResponses is { Count: > 0 })
        {
            parts.Add("Diagnostic Responses:");
            foreach (var response in request.DiagnosticResponses)
            {
                var answer = response.SelectedOptions.Count > 0
                    ? string.Join(", ", response.SelectedOptions)
                    : response.FreeTextResponse ?? "No response";
                parts.Add($"  Q: {response.QuestionText.Trim()} → A: {answer}");
            }
        }

        return string.Join("\n", parts);
    }

    /// <summary>
    /// Sends a confirmation notification without blocking the caller.
    /// Exceptions are caught and logged as warnings.
    /// </summary>
    private async Task FireAndForgetNotificationAsync(string email, string serviceRequestId)
    {
        try
        {
            await _notificationService.SendServiceRequestConfirmationAsync(
                email,
                serviceRequestId,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intake Step 7: Failed to send confirmation notification for SR {ServiceRequestId}",
                serviceRequestId);
        }
    }

    /// <inheritdoc />
    public async Task<IntakeConfigResponseDto> GetIntakeConfigAsync(string slug, string? magicLinkToken = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var slugLookup = await _slugLookupRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new KeyNotFoundException($"Location slug '{slug}' not found.");

        var location = await _locationRepository.GetByIdAsync(slugLookup.TenantId, slugLookup.LocationId, cancellationToken);

        var intakeConfig = location?.IntakeConfig ?? new IntakeFormConfigEmbedded();

        var issueCategories = new List<LookupItemDto>();
        var lookupSet = await _lookupRepository.GetGlobalAsync("IssueCategory", cancellationToken);
        if (lookupSet is not null)
        {
            issueCategories = lookupSet.Items
                .Where(i => i.IsSelectable)
                .Select(i => new LookupItemDto(i.Code, i.Name, i.Description, i.SortOrder, i.IsSelectable))
                .ToList();
        }

        CustomerInfoDto? prefillCustomer = null;
        AssetInfoDto? prefillAsset = null;
        var knownAssets = new List<AssetInfoDto>();
        var tokenExpired = false;
        if (!string.IsNullOrWhiteSpace(magicLinkToken))
        {
            var acct = await _globalCustomerAcctRepository.GetByMagicLinkTokenAsync(magicLinkToken, cancellationToken);
            if (acct is not null && acct.MagicLinkExpiresAtUtc > DateTime.UtcNow)
            {
                prefillCustomer = new CustomerInfoDto
                {
                    FirstName = acct.FirstName,
                    LastName = acct.LastName,
                    Email = acct.Email,
                    Phone = acct.Phone
                };

                // Resolve known vehicles for one-tap selection (capped to avoid excessive lookups;
                // RV customers typically own 1–3 vehicles)
                if (acct.AllKnownAssetIds is { Count: > 0 })
                {
                    const int maxAssetLookups = 10;
                    AssetInfoDto? lastEnrichedAsset = null;
                    foreach (var assetId in acct.AllKnownAssetIds.TakeLast(maxAssetLookups))
                    {
                        var entries = await _assetLedgerRepository.GetByAssetIdAsync(assetId, cancellationToken);
                        var mostRecent = entries.LastOrDefault();
                        if (mostRecent is not null)
                        {
                            var assetDto = new AssetInfoDto
                            {
                                AssetId = mostRecent.AssetId,
                                Manufacturer = mostRecent.Manufacturer,
                                Model = mostRecent.Model,
                                Year = mostRecent.Year,
                            };
                            knownAssets.Add(assetDto);
                            lastEnrichedAsset = assetDto;
                        }
                        else
                        {
                            knownAssets.Add(new AssetInfoDto { AssetId = assetId });
                        }
                    }

                    // Prefill the most recently used vehicle that has full details
                    prefillAsset = lastEnrichedAsset;
                }
            }
            else if (acct is not null && acct.MagicLinkExpiresAtUtc.HasValue && acct.MagicLinkExpiresAtUtc.Value <= DateTime.UtcNow)
            {
                tokenExpired = true;
            }
        }

        return new IntakeConfigResponseDto
        {
            LocationName = slugLookup.LocationName,
            LocationSlug = slugLookup.Slug,
            DealershipName = slugLookup.DealershipName,
            AcceptedFileTypes = intakeConfig.AcceptedFileTypes,
            MaxFileSizeMb = intakeConfig.MaxFileSizeMb,
            MaxAttachments = intakeConfig.MaxAttachments,
            AllowAnonymousIntake = intakeConfig.AllowAnonymousIntake,
            IssueCategories = issueCategories,
            PrefillCustomer = prefillCustomer,
            PrefillAsset = prefillAsset,
            KnownAssets = knownAssets,
            TokenExpired = tokenExpired,
        };
    }

    /// <inheritdoc />
    public async Task<string> ResolveSlugToTenantIdAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var slugLookup = await _slugLookupRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new KeyNotFoundException($"Location slug '{slug}' not found.");

        return slugLookup.TenantId;
    }
}
