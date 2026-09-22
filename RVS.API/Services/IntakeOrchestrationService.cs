using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Packets;
using RVS.Domain.Security;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Core 8-step intake orchestration sequence that creates up to 5 Cosmos documents
/// (GlobalCustomerAcct, CustomerProfile, ServiceRequest, AssetLedgerEntry, updated linkages)
/// in a single intake request, then enqueues packet generation (<c>Spec A-8</c>, <c>B-1</c>)
/// without blocking the response.
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
    private readonly INotificationOrchestrator _notificationOrchestrator;
    private readonly IPacketGenerationQueue _packetGenerationQueue;
    private readonly IIntakeInviteRepository _intakeInviteRepository;
    private readonly IntakeUrlOptions _intakeUrlOptions;
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
        INotificationOrchestrator notificationOrchestrator,
        IPacketGenerationQueue packetGenerationQueue,
        IIntakeInviteRepository intakeInviteRepository,
        IOptions<IntakeUrlOptions> intakeUrlOptions,
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
        _notificationOrchestrator = notificationOrchestrator;
        _packetGenerationQueue = packetGenerationQueue;
        _intakeInviteRepository = intakeInviteRepository;
        _intakeUrlOptions = intakeUrlOptions.Value;
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

        // A-14 (issue #664): an advisor invite that is still good attributes the request to that
        // advisor and invite. Anything else is ignored rather than refused — a spent, expired or
        // unreadable invite costs the customer the attribution, never the submission.
        var invite = await FindRedeemableInviteAsync(tenantId, locationId, request.InviteToken, cancellationToken);

        // ── Step 2: Resolve GlobalCustomerAcct by email (create if absent) ───
        var normalizedEmail = request.Customer.Email.Trim().ToLowerInvariant();
        var globalAcct = await _globalCustomerAcctRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        var globalAcctCreated = false;

        if (globalAcct is null)
        {
            // The id is derived from the email, so a concurrent first submission for the same
            // address collides here instead of leaving a second account behind (issue #679).
            try
            {
                globalAcct = await _globalCustomerAcctRepository.CreateAsync(new GlobalCustomerAcct
                {
                    Id = GlobalCustomerAcct.IdForEmail(normalizedEmail),
                    Email = normalizedEmail,
                    FirstName = request.Customer.FirstName.Trim(),
                    LastName = request.Customer.LastName.Trim(),
                    Phone = request.Customer.Phone?.Trim(),
                    CreatedByUserId = "intake",
                }, cancellationToken);
                globalAcctCreated = true;
                _logger.LogInformation("Intake Step 2: Created new GlobalCustomerAcct {AcctId} for {Email}",
                    globalAcct.Id, normalizedEmail);
            }
            catch (ConflictException ex)
            {
                globalAcct = await _globalCustomerAcctRepository.GetByEmailAsync(normalizedEmail, cancellationToken)
                    ?? throw new ConflictException("The customer account was created concurrently and could not be read back.", ex);
                _logger.LogInformation("Intake Step 2: GlobalCustomerAcct {AcctId} was created concurrently; using it",
                    globalAcct.Id);
            }
        }

        if (!globalAcctCreated)
        {
            globalAcct.Phone = request.Customer.Phone?.Trim();
            _logger.LogInformation("Intake Step 2: Resolved existing GlobalCustomerAcct {AcctId} for {Email}",
                globalAcct.Id, normalizedEmail);
        }

        // ── Step 3: Resolve CustomerProfile + asset ownership ────────────────
        var profile = await _customerProfileRepository.GetByEmailAsync(tenantId, normalizedEmail, cancellationToken);
        var profileCreated = false;

        if (profile is null)
        {
            var newProfile = new CustomerProfile
            {
                TenantId = tenantId,
                Email = normalizedEmail,
                FirstName = request.Customer.FirstName.Trim(),
                LastName = request.Customer.LastName.Trim(),
                Phone = request.Customer.Phone?.Trim(),
                // The matchable form of the number: an inbound STOP arrives with a phone and no
                // tenant, so a lookup needs E.164 rather than what the customer typed (#665).
                PhoneE164 = PhoneNumberNormalizer.Normalize(request.Customer.Phone),
                Name = $"{request.Customer.FirstName.Trim()} {request.Customer.LastName.Trim()}",
                GlobalCustomerAcctId = globalAcct.Id,
                CreatedByUserId = "intake",
            };
            newProfile.ApplyIntakeOptOuts(request.SmsOptOut, request.EmailOptOut, DateTime.UtcNow);

            // The [/tenantId, /email] unique key rejects a concurrent second create; carry on with
            // the profile that won rather than fail the submission (issue #679).
            try
            {
                profile = await _customerProfileRepository.CreateAsync(newProfile, cancellationToken);
                profileCreated = true;
                _logger.LogInformation("Intake Step 3: Created new CustomerProfile {ProfileId} in tenant {TenantId}",
                    profile.Id, tenantId);
            }
            catch (ConflictException ex)
            {
                profile = await _customerProfileRepository.GetByEmailAsync(tenantId, normalizedEmail, cancellationToken)
                    ?? throw new ConflictException("The customer profile was created concurrently and could not be read back.", ex);
                _logger.LogInformation("Intake Step 3: CustomerProfile {ProfileId} in tenant {TenantId} was created concurrently; using it",
                    profile.Id, tenantId);
            }
        }

        if (!profileCreated)
        {
            profile.Phone = request.Customer.Phone?.Trim();
            profile.PhoneE164 = PhoneNumberNormalizer.Normalize(request.Customer.Phone);
            // Intake sets an opt-out, never clears one: the form never shows the stored value, so
            // an unticked box is not a choice to opt back in (Spec A-2, issue #673).
            profile.ApplyIntakeOptOuts(request.SmsOptOut, request.EmailOptOut, DateTime.UtcNow);
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
        // A-5: the category the customer submitted is authoritative. The AI suggestion is
        // advisory and was already offered (and accepted or overridden) in the intake wizard;
        // the server does not re-run categorization and override the choice here. Coerce to
        // the controlled vocabulary so an unrecognised value can never reach the packet.
        var issueCategory = IssueCategoryVocabulary.Normalize(request.IssueCategory);

        // A-13: the channel the customer arrived through, as forwarded by the intake app from
        // the go.rvintake.com redirect. Normalised rather than validated — an unrecognised or
        // malformed tag costs the request its channel, never the submission.
        var intakeSource = IntakeSourceVocabulary.Normalize(request.IntakeSource);

        // A redeemed invite is the proof of the channel, whatever src survived the trip.
        if (invite is not null)
        {
            intakeSource = IntakeSourceVocabulary.Advisor;
        }

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
            IssueDescriptionVerbatim = NullIfBlank(request.IssueDescriptionVerbatim),
            TechnicianSummary = technicianSummary,
            Urgency = request.Urgency?.Trim(),
            RvUsage = request.RvUsage?.Trim(),
            HasExtendedWarranty = request.HasExtendedWarranty?.Trim(),
            ApproxPurchaseDate = request.ApproxPurchaseDate?.Trim(),
            IntakeSource = intakeSource,
            IntakeInviteId = invite?.Id,
            AdvisorUserId = invite?.AdvisorUserId,
            CustomerSnapshot = new CustomerSnapshotEmbedded
            {
                FirstName = request.Customer.FirstName.Trim(),
                LastName = request.Customer.LastName.Trim(),
                Email = request.Customer.Email.Trim(),
                Phone = request.Customer.Phone?.Trim(),
                PreferredContact = PreferredContactMethod.Normalize(request.Customer.PreferredContact),
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
            // The client uploads and confirms its attachments after this call returns, so record
            // how many to expect: packet generation waits for them rather than rendering a
            // photo-less packet (issue #516). Clamped so a bad value cannot stall generation.
            PacketGeneration = new PacketGenerationEmbedded
            {
                ExpectedAttachmentCount = Math.Max(0, request.ExpectedAttachmentCount),
            },
            CreatedByUserId = "intake",
        };

        serviceRequest = await _serviceRequestRepository.CreateAsync(serviceRequest, cancellationToken);
        _logger.LogInformation("Intake Step 4: Created ServiceRequest {ServiceRequestId} in tenant {TenantId}",
            serviceRequest.Id, tenantId);

        // Spend the invite only now that the request it produced exists (Spec A-14).
        if (invite is not null)
        {
            await RedeemInviteAsync(invite, serviceRequest.Id, cancellationToken);
        }

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
        // Spec section B / issue #496: the confirmation thanks the customer by dealer name,
        // points them at their status page, and includes the dealer phone when known.
        var location = await _locationRepository.GetByIdAsync(tenantId, locationId, cancellationToken);
        var statusUrl = $"{_intakeUrlOptions.BaseUrl.TrimEnd('/')}/status/{globalAcct.MagicLinkToken}";

        // ACS only accepts E.164 (issue #661). A number that doesn't normalise is dropped from the
        // notification rather than sent raw; the profile keeps it as entered.
        // The preference chooses the channel; the opt-outs veto it (Spec A-2, issue #662). They are
        // the profile's stored opt-outs, not this submission's boxes (issue #673): a Text preference
        // against a stored SMS opt-out is accepted, and the orchestrator confirms by email instead.
        _ = FireAndForgetNotificationAsync(
            tenantId,
            locationId,
            serviceRequest.CustomerSnapshot.PreferredContact,
            profile.SmsOptOut,
            profile.EmailOptOut,
            request.Customer.Email.Trim(),
            PhoneNumberNormalizer.Normalize(request.Customer.Phone),
            serviceRequest.Id,
            slugLookup.DealershipName,
            statusUrl,
            location?.Phone);

        // ── Step 8: Enqueue packet generation (never blocks the 201) ─────────
        // Spec A-8 / B-1 / X-7: the packet is generated asynchronously; nothing here may delay
        // or roll back the submission. The request already carries PacketGeneration = Pending.
        try
        {
            if (!_packetGenerationQueue.TryEnqueue(new PacketGenerationJob(tenantId, serviceRequest.Id, "intake")))
            {
                _logger.LogWarning(
                    "Intake Step 8: packet generation queue full; SR {ServiceRequestId} stays Pending and can be regenerated on demand",
                    serviceRequest.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intake Step 8: failed to enqueue packet generation for SR {ServiceRequestId}", serviceRequest.Id);
        }

        return (serviceRequest, globalAcct.MagicLinkToken);
    }

    /// <summary>
    /// Builds the preliminary-assessment seed text: the capability-mismatch note, when one was
    /// raised, and nothing else (issue #601). It must never echo the issue description — the
    /// packet renders that verbatim in its own "Complaint" section, and this text renders above
    /// it labelled "AI-generated"; repeating the same words in both would misrepresent the
    /// literal text as an AI summary. The diagnostic Q&amp;A is deliberately not repeated here
    /// either — the packet renders it in full in its own "Reported symptoms &amp; diagnostic
    /// Q&amp;A" section (issue #492 item 6).
    /// </summary>
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildTechnicianSummary(ServiceRequestCreateRequestDto request) =>
        string.IsNullOrWhiteSpace(request.CapabilityMismatchNote)
            ? null
            : request.CapabilityMismatchNote.Trim();

    /// <summary>
    /// Point-reads the invite named by <paramref name="token"/> and returns it only when it can
    /// still be redeemed at this location (issue #664). A malformed token never reaches storage,
    /// and a failed read is logged and treated as no invite: the invite path must never break
    /// the intake form or its submission.
    /// </summary>
    private async Task<IntakeInvite?> FindRedeemableInviteAsync(
        string tenantId, string locationId, string? token, CancellationToken cancellationToken)
    {
        if (!InviteToken.IsWellFormed(token))
        {
            return null;
        }

        IntakeInvite? invite;
        try
        {
            invite = await _intakeInviteRepository.GetByIdAsync(tenantId, InviteToken.Hash(token!), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Intake invite lookup failed in tenant {TenantId}; continuing without the invite", tenantId);
            return null;
        }

        var now = DateTime.UtcNow;
        if (invite is null || invite.LocationId != locationId || !invite.IsRedeemableAt(now))
        {
            _logger.LogInformation(
                "Intake invite not usable in tenant {TenantId}: found={Found}, redeemed={Redeemed}, expired={Expired}, otherLocation={OtherLocation}",
                tenantId,
                invite is not null,
                invite?.RedeemedAtUtc is not null,
                invite is not null && invite.ExpiresAtUtc <= now,
                invite is not null && invite.LocationId != locationId);
            return null;
        }

        return invite;
    }

    /// <summary>
    /// Marks <paramref name="invite"/> redeemed by <paramref name="serviceRequestId"/>. The request
    /// already exists and already carries the attribution, so a failure here is logged, not thrown.
    /// </summary>
    private async Task RedeemInviteAsync(IntakeInvite invite, string serviceRequestId, CancellationToken cancellationToken)
    {
        try
        {
            invite.MarkRedeemed(serviceRequestId, DateTime.UtcNow, "intake");
            await _intakeInviteRepository.UpdateAsync(invite, cancellationToken);
            _logger.LogInformation("Intake Step 4: Redeemed intake invite {InviteId} with SR {ServiceRequestId}",
                invite.Id, serviceRequestId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Intake Step 4: Failed to mark intake invite {InviteId} redeemed for SR {ServiceRequestId}",
                invite.Id, serviceRequestId);
        }
    }

    /// <summary>
    /// Sends a confirmation notification via the orchestrator without blocking the caller.
    /// Exceptions are caught and logged as warnings.
    /// </summary>
    private async Task FireAndForgetNotificationAsync(
        string tenantId, string locationId, string? preferredContact,
        bool smsOptOut, bool emailOptOut,
        string email, string? phone, string serviceRequestId, string dealershipName,
        string statusUrl, string? dealerPhone)
    {
        try
        {
            await _notificationOrchestrator.SendServiceRequestConfirmationAsync(
                tenantId,
                locationId,
                preferredContact,
                smsOptOut,
                emailOptOut,
                email,
                phone,
                serviceRequestId,
                dealershipName,
                statusUrl,
                dealerPhone,
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

        // A-7 returning-customer prefill is deferred (Spec A-7, #673). Nothing RVS sends puts ?token=
        // on the intake URL, so this branch is unreachable; it stays for when A-7 returns.
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
            LocationPhone = location?.Phone,
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
    public async Task<IntakeInvitePrefillResponseDto?> GetInvitePrefillAsync(string slug, string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var slugLookup = await _slugLookupRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new KeyNotFoundException($"Location slug '{slug}' not found.");

        // Read-only on purpose: link previews fetch this URL too, so opening never redeems.
        var invite = await FindRedeemableInviteAsync(slugLookup.TenantId, slugLookup.LocationId, token, cancellationToken);

        return invite is null
            ? null
            : new IntakeInvitePrefillResponseDto { FirstName = invite.FirstName, Phone = invite.Phone, Email = invite.Email };
    }

    /// <inheritdoc />
    public async Task<CapabilityAssessmentResponseDto> AssessCapabilitiesAsync(
        string slug,
        string issueDescription,
        string? issueCategory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueDescription);

        var slugLookup = await _slugLookupRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new KeyNotFoundException($"Location slug '{slug}' not found.");

        var location = await _locationRepository.GetByIdAsync(slugLookup.TenantId, slugLookup.LocationId, cancellationToken);
        var enabledCapabilities = location?.EnabledCapabilities ?? [];
        var locationPhone = location?.Phone;

        // Use the supplied category when present; otherwise derive one via the AI categorization service.
        // A categorization failure is treated as "unknown" (no specific capability requirement → match).
        string? resolvedCategory = string.IsNullOrWhiteSpace(issueCategory) ? null : issueCategory.Trim();
        if (resolvedCategory is null)
        {
            try
            {
                resolvedCategory = await _categorizationService.CategorizeAsync(issueDescription, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Capability assessment: categorization failed for slug={Slug}; treating as unknown category.", slug);
            }
        }

        var requiredCapabilities = IssueCategoryCapabilityMap.GetRequiredCapabilities(resolvedCategory);
        var missing = requiredCapabilities
            .Where(req => !enabledCapabilities.Contains(req, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var matched = missing.Count == 0;

        _logger.LogInformation(
            "Capability assessment: slug={Slug}, category={Category}, required=[{Required}], missing=[{Missing}], matched={Matched}",
            slug, resolvedCategory ?? "(none)", string.Join(",", requiredCapabilities), string.Join(",", missing), matched);

        return new CapabilityAssessmentResponseDto
        {
            Matched = matched,
            IssueCategory = resolvedCategory,
            RequiredCapabilities = [.. requiredCapabilities],
            MissingCapabilities = missing,
            LocationPhone = locationPhone
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
