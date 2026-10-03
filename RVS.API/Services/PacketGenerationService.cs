using System.Diagnostics;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.API.Packets;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Packets;

namespace RVS.API.Services;

/// <summary>
/// Composes and renders the one-page service packet for a request, then stores the PDF
/// (<c>Spec B-1</c> … <c>B-3</c>, issue #434).
///
/// This runs off the intake request thread, driven by <see cref="Workers.PacketGenerationWorker"/>.
/// It gathers what the composer needs that is not on the request (location, photo read URLs),
/// hands one <see cref="ServicePacket"/> to both renderers so HTML and PDF cannot diverge, and
/// records progress on the request's <see cref="PacketGenerationEmbedded"/> block. A generation
/// failure is caught and recorded — it never rolls back or deletes the service request — and
/// after <see cref="PacketGenerationEmbedded.MaxAttempts"/> failed attempts an alert is logged
/// once for the manager app to surface.
/// </summary>
public sealed class PacketGenerationService : IPacketGenerationService
{
    /// <summary>Blob container holding service-request attachments and generated packet PDFs.</summary>
    private const string AttachmentsContainer = "rvs-attachments";

    /// <summary>
    /// Lifetime of the manager app's packet PDF link (issue #443). The detail dialog pre-fetches
    /// it on load, so it matches the 1-hour attachment read links shown alongside it.
    /// </summary>
    private static readonly TimeSpan PdfLinkLifetime = TimeSpan.FromHours(1);

    /// <summary>Event id for the exhausted-retries alert (<c>Spec B-1</c>).</summary>
    private static readonly EventId PacketGenerationExhausted = new(434_001, nameof(PacketGenerationExhausted));

    /// <summary>Event id for the exhausted packet-email-delivery alert (<c>Spec B-4</c>, issue #438).</summary>
    private static readonly EventId PacketEmailDeliveryExhausted = new(438_001, nameof(PacketEmailDeliveryExhausted));

    /// <summary>
    /// Event id for a packet email that could not carry its PDF within the email size budget
    /// (<c>Spec B-4</c>, issue #521). The email still goes out; the PDF does not.
    /// </summary>
    private static readonly EventId PacketEmailOversized = new(521_001, nameof(PacketEmailOversized));

    private const string SystemUserId = "system";
    private const int MaxErrorLength = 500;

    /// <summary>
    /// How long after a request is created generation will wait for intake's promised
    /// attachments to finish uploading (issue #516). The intake client uploads photos after the
    /// submission that creates the request, so a packet rendered the instant the job is
    /// enqueued carries none of them. Once this window closes the packet is rendered with
    /// whatever arrived — a browser upload that failed must never cost the service department
    /// its packet.
    /// </summary>
    internal static readonly TimeSpan AttachmentUploadWindow = TimeSpan.FromMinutes(2);

    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IPacketPhotoUrlResolver _photoUrlResolver;
    private readonly IBlobStorageService _blobStorage;
    private readonly IPacketGenerationQueue _queue;
    private readonly IUserContextAccessor _userContext;
    private readonly INotificationService _notificationService;
    private readonly IPreliminaryAssessmentService _assessmentService;
    private readonly ILocationLogoFetcher _logoFetcher;
    private readonly PacketEmailOptions _packetEmailOptions;
    private readonly ManagerAppUrlOptions _managerAppUrlOptions;
    private readonly IntakeUrlOptions _intakeUrlOptions;
    private readonly ILogger<PacketGenerationService> _logger;

    /// <summary>Creates the packet generation orchestrator with its repositories, blob storage, queue, notification transport, assessment generator, and logo fetcher.</summary>
    public PacketGenerationService(
        IServiceRequestRepository serviceRequestRepository,
        ILocationRepository locationRepository,
        IPacketPhotoUrlResolver photoUrlResolver,
        IBlobStorageService blobStorage,
        IPacketGenerationQueue queue,
        IUserContextAccessor userContext,
        INotificationService notificationService,
        IPreliminaryAssessmentService assessmentService,
        ILocationLogoFetcher logoFetcher,
        IOptions<PacketEmailOptions> packetEmailOptions,
        IOptions<ManagerAppUrlOptions> managerAppUrlOptions,
        IOptions<IntakeUrlOptions> intakeUrlOptions,
        ILogger<PacketGenerationService> logger)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _locationRepository = locationRepository;
        _photoUrlResolver = photoUrlResolver;
        _blobStorage = blobStorage;
        _queue = queue;
        _userContext = userContext;
        _notificationService = notificationService;
        _assessmentService = assessmentService;
        _logoFetcher = logoFetcher;
        _packetEmailOptions = packetEmailOptions.Value;
        _managerAppUrlOptions = managerAppUrlOptions.Value;
        _intakeUrlOptions = intakeUrlOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PacketGenerationOutcome> GenerateAsync(string tenantId, string serviceRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        var request = await _serviceRequestRepository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        // A multi-issue submission is one packet (Spec A-17, B-2, issue #806): whichever issue
        // the job names, the whole submission is generated together, led by its first request.
        var members = await LoadSubmissionAsync(request, cancellationToken);
        var lead = members[0];
        var isMultiIssue = members.Count > 1;

        // Intake's photos land after the 201 that created the requests (issue #516). Hold off
        // while any issue has fewer than promised — before MarkGenerating, so polling never eats
        // one of the three attempts — and render regardless once the window closes.
        var waiting = members.FirstOrDefault(IsWaitingForAttachments);
        if (waiting is not null)
        {
            _logger.LogInformation(
                "Packet generation deferred for SR {ServiceRequestId}: {ArrivedCount} of {ExpectedCount} attachment(s) uploaded, still inside the {WindowSeconds}s upload window",
                waiting.Id, waiting.Attachments.Count, waiting.PacketGeneration.ExpectedAttachmentCount,
                AttachmentUploadWindow.TotalSeconds);

            return PacketGenerationOutcome.WaitingForAttachments;
        }

        // Record the attempt before doing the work so a mid-render crash still shows it was tried.
        foreach (var member in members)
        {
            member.PacketGeneration.MarkGenerating();
            member.MarkAsUpdated(SystemUserId);
            await _serviceRequestRepository.UpdateAsync(member, cancellationToken);
        }

        try
        {
            var location = await _locationRepository.GetByIdAsync(tenantId, lead.LocationId, cancellationToken);

            // The dealer's logo (Spec A-16, issue #470). The PDF needs its bytes; the HTML only
            // names its URL, and does so only when the fetch succeeded, so the two renderings show
            // the logo or both leave it out. A logo that will not load never fails the packet.
            var logoUrl = location?.Branding is { HasLogo: true } branding ? branding.LogoUrl : null;
            var logoImage = logoUrl is null ? null : await _logoFetcher.FetchAsync(logoUrl, cancellationToken);
            if (logoImage is null)
            {
                logoUrl = null;
            }

            var parts = new List<ComposedPacket>(members.Count);
            foreach (var member in members)
            {
                parts.Add(await ComposeAsync(member, location, logoUrl, cancellationToken));
            }

            IReadOnlyList<ServicePacket> packets = [.. parts.Select(p => p.Packet)];

            // Render the HTML now too: it is the primary artifact and shares failure modes with
            // the PDF, so a broken packet is caught here rather than at email time (#437).
            var html = PacketHtmlRenderer.RenderCombined(packets);
            _logger.LogDebug("Packet generation: composed HTML packet ({Length} chars) for SR {ServiceRequestId}", html.Length, lead.Id);

            var pdf = PacketPdfRenderer.RenderCombined(packets, MergePhotoImages(parts), logoImage);

            // One stored document for the submission, kept under its first request and linked
            // from every issue, so each issue's manager download opens the same packet.
            var nextVersion = lead.PacketGeneration.PacketVersion + 1;
            var pdfBlobPath = $"packets/{tenantId}/{lead.Id}/v{nextVersion}.pdf";
            using (var pdfStream = new MemoryStream(pdf, writable: false))
            {
                await _blobStorage.UploadAsync(AttachmentsContainer, pdfBlobPath, pdfStream, "application/pdf", cancellationToken);
            }

            var generatedAtUtc = DateTime.UtcNow;
            foreach (var member in members)
            {
                member.PacketGeneration.MarkSucceeded(pdfBlobPath, generatedAtUtc);
                member.MarkAsUpdated(SystemUserId);
                await _serviceRequestRepository.UpdateAsync(member, cancellationToken);
            }

            _logger.LogInformation(
                "Packet generation succeeded for SR {ServiceRequestId} with {IssueCount} issue(s) (version {PacketVersion}, attempt {AttemptCount})",
                lead.Id, members.Count, lead.PacketGeneration.PacketVersion, lead.PacketGeneration.AttemptCount);

            // Deliver the packet by email (Spec B-4, #437 send, #438 idempotency + retry). A
            // delivery failure never fails generation: the packet is generated and stored. A
            // multi-issue packet is emailed once; a regeneration re-renders it without sending
            // it again, and the manager app can download it to forward by hand (Spec A-17).
            if (isMultiIssue && lead.PacketEmailDelivery.DeliveredPacketVersion > 0)
            {
                _logger.LogInformation(
                    "Packet email skipped for SR {ServiceRequestId} v{PacketVersion}: a regenerated multi-issue packet is not emailed again",
                    lead.Id, lead.PacketGeneration.PacketVersion);
            }
            else
            {
                await DeliverPacketEmailAsync(lead, location, parts, html, pdf, members, cancellationToken);
            }

            return PacketGenerationOutcome.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = TrimError($"{ex.GetType().Name}: {ex.Message}");
            foreach (var member in members)
            {
                member.PacketGeneration.MarkFailed(error);
            }

            var exhausted = lead.PacketGeneration.AttemptCount >= PacketGenerationEmbedded.MaxAttempts;
            if (exhausted && !lead.PacketGeneration.AlertRaised)
            {
                _logger.LogCritical(
                    PacketGenerationExhausted, ex,
                    "Packet generation exhausted after {AttemptCount} attempts for SR {ServiceRequestId} ({IssueCount} issue(s)) in tenant {TenantId}",
                    lead.PacketGeneration.AttemptCount, lead.Id, members.Count, tenantId);
                foreach (var member in members)
                {
                    member.PacketGeneration.MarkAlertRaised();
                }
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Packet generation attempt {AttemptCount} failed for SR {ServiceRequestId} ({IssueCount} issue(s)) in tenant {TenantId}",
                    lead.PacketGeneration.AttemptCount, lead.Id, members.Count, tenantId);
            }

            foreach (var member in members)
            {
                member.MarkAsUpdated(SystemUserId);
                await _serviceRequestRepository.UpdateAsync(member, cancellationToken);
            }

            return exhausted ? PacketGenerationOutcome.Exhausted : PacketGenerationOutcome.Retry;
        }
    }

    /// <inheritdoc />
    public async Task RequestRegenerationAsync(string tenantId, string serviceRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        var request = await _serviceRequestRepository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        // Regenerating any issue of a multi-issue submission regenerates its one packet (Spec A-17).
        var members = await LoadSubmissionAsync(request, cancellationToken);
        foreach (var member in members)
        {
            member.PacketGeneration.ResetForRegeneration();
            member.MarkAsUpdated(_userContext.UserId);
            await _serviceRequestRepository.UpdateAsync(member, cancellationToken);
        }

        var lead = members[0];
        if (!_queue.TryEnqueue(new PacketGenerationJob(tenantId, lead.Id, "regeneration")))
        {
            _logger.LogWarning(
                "Packet regeneration for SR {ServiceRequestId} could not be enqueued; it stays Pending and will need another regenerate request",
                lead.Id);
        }
    }

    /// <summary>
    /// The requests that share <paramref name="request"/>'s packet, first request first: every
    /// issue of its submission (<c>Spec A-17</c>), or just <paramref name="request"/> when it was
    /// reported on its own or its submission cannot be read back.
    /// </summary>
    private async Task<IReadOnlyList<ServiceRequest>> LoadSubmissionAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsInMultiIssueSubmission)
        {
            return [request];
        }

        var members = await _serviceRequestRepository.GetBySubmissionIdAsync(request.TenantId, request.SubmissionId!, cancellationToken);
        if (members.Count == 0 || members[0].Id != request.SubmissionId)
        {
            _logger.LogWarning(
                "Submission {SubmissionId} of SR {ServiceRequestId} could not be read back whole; generating the request on its own",
                request.SubmissionId, request.Id);
            return [request];
        }

        return members;
    }

    /// <summary>Every issue's downloaded photos, keyed by read URL; the URLs are unique across issues.</summary>
    private static Dictionary<string, byte[]> MergePhotoImages(IEnumerable<ComposedPacket> parts)
    {
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (url, bytes) in parts.SelectMany(p => p.PhotoImages))
        {
            images[url] = bytes;
        }

        return images;
    }

    /// <inheritdoc />
    public async Task<PacketPdfLinkDto> GetPdfLinkAsync(string tenantId, string serviceRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        var request = await _serviceRequestRepository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        var packet = request.PacketGeneration;
        if (packet.PacketVersion == 0 || string.IsNullOrEmpty(packet.PdfBlobPath))
        {
            throw new KeyNotFoundException($"No packet has been generated for service request '{serviceRequestId}' yet.");
        }

        var sasUrl = await _blobStorage.GenerateReadSasUrlAsync(
            AttachmentsContainer, packet.PdfBlobPath, PdfLinkLifetime, cancellationToken);

        return new PacketPdfLinkDto
        {
            SasUrl = sasUrl,
            ExpiresAtUtc = DateTime.UtcNow.Add(PdfLinkLifetime),
            PacketVersion = packet.PacketVersion
        };
    }

    /// <summary>
    /// A composed packet with what delivery needs beside it: the request it came from, its photos'
    /// read URLs by attachment id, and the downloaded image bytes by read URL.
    /// </summary>
    private sealed record ComposedPacket(
        ServiceRequest Request,
        ServicePacket Packet,
        IReadOnlyDictionary<string, string> PhotoUrls,
        IReadOnlyDictionary<string, byte[]> PhotoImages);

    /// <summary>
    /// Resolves and downloads <paramref name="request"/>'s photos, runs its preliminary assessment
    /// if it has none yet, and composes its packet — one issue's part of the document.
    /// </summary>
    private async Task<ComposedPacket> ComposeAsync(
        ServiceRequest request, Location? location, string? logoUrl, CancellationToken cancellationToken)
    {
        var photoUrls = await _photoUrlResolver.ResolveAsync(request, cancellationToken);

        // One download feeds both the assessment, which reads the photos (issue #772), and
        // the PDF, which embeds them. It used to happen after the assessment, for the PDF only.
        var downloadedPhotos = await DownloadPhotosAsync(request, photoUrls, cancellationToken);

        await EnsurePreliminaryAssessmentAsync(request, downloadedPhotos, cancellationToken);

        var pasteBlockCap = location?.PacketConfig.PasteBlockCharacterCap
            ?? PacketConfigEmbedded.DefaultPasteBlockCharacterCap;

        var context = new PacketCompositionContext
        {
            LocationName = location?.Name,
            LocationPhone = location?.Phone,
            LocationTimeZoneId = location?.TimeZoneId,
            // SpecifyKind, not a bare widening: CreatedAtUtc is a DateTime, and a Cosmos
            // round-trip can hand it back with Kind=Unspecified, which DateTimeOffset reads
            // as server-local. Harmless while the Received line was re-normalised to UTC;
            // a wrong wall-clock hour once it is converted into a named zone (issue #506).
            SubmittedAtUtc = new DateTimeOffset(
                DateTime.SpecifyKind(request.CreatedAtUtc, DateTimeKind.Utc)),
            StatusLinkUrl = null,   // minted by #427
            ManagerLinks = ManagerDeepLinks.Build(_managerAppUrlOptions.BaseUrl, request.Id),
            PasteBlock = PasteBlockGenerator.Generate(
                request.IssueCategory,
                request.IssueDescription,
                statusLinkUrl: null,   // supplied by #427 once the status token is minted
                characterCap: pasteBlockCap,
                equipmentLines: PhotoFindingText.PasteLines(request.PreliminaryAssessment?.PhotoFindings)),
            PhotoUrls = photoUrls,
            LogoUrl = logoUrl,
            PoweredByLogoUrl = PacketBranding.PoweredByLogoUrlFor(_intakeUrlOptions.BaseUrl),
        };

        var photoImages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var photo in downloadedPhotos)
        {
            photoImages[photo.Url] = photo.Bytes;
        }

        return new ComposedPacket(request, PacketComposer.Compose(request, context), photoUrls, photoImages);
    }

    /// <summary>
    /// Generates the structured preliminary assessment the first time a packet is built and
    /// stores it on <paramref name="request"/>, persisted by the attempt's next save (issue #507).
    /// A regeneration reuses the stored result, so the packet's content stays stable and the
    /// model is called once per request. The assessment is advisory: an unexpected failure is
    /// logged and the packet renders without it rather than costing a generation attempt.
    ///
    /// The photos already downloaded for the PDF go with it, in attachment order (issue #772). A
    /// photo that arrives after the first generation is therefore never assessed — regeneration
    /// reuses the stored result.
    /// </summary>
    private async Task EnsurePreliminaryAssessmentAsync(
        ServiceRequest request, IReadOnlyList<DownloadedPhoto> photos, CancellationToken cancellationToken)
    {
        if (request.PreliminaryAssessment is not null)
        {
            return;
        }

        try
        {
            AssessmentPhoto[] assessmentPhotos =
                [.. photos.Select(p => new AssessmentPhoto(p.AttachmentId, p.ContentType, p.Bytes))];
            request.PreliminaryAssessment = await _assessmentService.AssessAsync(request, assessmentPhotos, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Preliminary assessment failed for SR {ServiceRequestId}; the packet renders without it",
                request.Id);
        }
    }

    /// <summary>
    /// <c>true</c> while intake has promised more attachments than have been confirmed onto
    /// <paramref name="request"/> and <see cref="AttachmentUploadWindow"/> has not yet elapsed
    /// since the request was created (issue #516).
    ///
    /// Anchoring the deadline on <see cref="EntityBase.CreatedAtUtc"/> means an on-demand
    /// regeneration — always long after creation — never waits: it renders immediately with
    /// whatever attachments the request actually has.
    /// </summary>
    private static bool IsWaitingForAttachments(ServiceRequest request) =>
        request.Attachments.Count < request.PacketGeneration.ExpectedAttachmentCount
        && DateTime.UtcNow - request.CreatedAtUtc < AttachmentUploadWindow;

    /// <summary>One image attachment's downloaded bytes and the read URL the packet shows it by.</summary>
    private sealed record DownloadedPhoto(string AttachmentId, string ContentType, string Url, byte[] Bytes);

    /// <summary>
    /// Downloads the image bytes for each resolved photo, in attachment order, so
    /// <see cref="PacketPdfRenderer"/> can embed them (<c>Spec B-3</c>) and the preliminary
    /// assessment can read them (issue #772). A single photo that cannot be fetched is logged and
    /// skipped — it renders as a labelled placeholder rather than failing the whole packet. Videos
    /// are skipped here: <see cref="PacketPdfRenderer"/> renders them as a text/hyperlink
    /// placeholder, not a raster embed, so downloading their (potentially 25 MB) bytes would be
    /// wasted work (issue <c>#583</c>).
    /// </summary>
    private async Task<List<DownloadedPhoto>> DownloadPhotosAsync(
        ServiceRequest request, IReadOnlyDictionary<string, string> photoUrls, CancellationToken cancellationToken)
    {
        var images = new List<DownloadedPhoto>();

        foreach (var attachment in request.Attachments)
        {
            var isImage = attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            if (!isImage)
            {
                continue;
            }

            if (!photoUrls.TryGetValue(attachment.AttachmentId, out var url) || string.IsNullOrWhiteSpace(attachment.BlobUri))
            {
                continue;
            }

            try
            {
                var bytes = await _blobStorage.DownloadAsync(AttachmentsContainer, attachment.BlobUri, cancellationToken);
                images.Add(new DownloadedPhoto(attachment.AttachmentId, attachment.ContentType!, url, bytes));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Packet generation: could not download photo {AttachmentId} for SR {ServiceRequestId}; rendering a placeholder",
                    attachment.AttachmentId, request.Id);
            }
        }

        return images;
    }

    /// <summary>
    /// Delivers the finished packet to the location's configured recipients (<c>Spec B-4</c>,
    /// issues #437 and #438). No-ops when the location has no packet config, delivery is disabled,
    /// or no recipient is set. Attachments follow the location's <c>attachPdf</c> /
    /// <c>includePhotos</c> flags.
    ///
    /// Delivery is <b>idempotent</b> per <c>(serviceRequestId, packetVersion)</c>: if this exact
    /// packet version is already recorded as delivered, the send is skipped. Otherwise it is
    /// attempted up to <see cref="PacketEmailDeliveryEmbedded.MaxAttempts"/> times with an
    /// exponential backoff between tries; every attempt logs under a delivery scope carrying the
    /// correlation id, and exhausting all attempts logs a <c>LogCritical</c> alert once. A delivery
    /// failure never fails generation — the packet is already generated and stored.
    ///
    /// A multi-issue packet (<c>Spec A-17</c>) goes out as one message led by
    /// <paramref name="request"/>, its first issue: <paramref name="html"/> and
    /// <paramref name="pdf"/> already carry every issue, and each issue's photos are attached.
    /// A successful send is recorded on every one of <paramref name="members"/>.
    /// </summary>
    private async Task DeliverPacketEmailAsync(
        ServiceRequest request,
        Location? location,
        IReadOnlyList<ComposedPacket> parts,
        string html,
        byte[] pdf,
        IReadOnlyList<ServiceRequest> members,
        CancellationToken cancellationToken)
    {
        var packet = parts[0].Packet;
        IReadOnlyList<ServicePacket> packets = [.. parts.Select(p => p.Packet)];

        var config = location?.PacketConfig;
        if (config is null || !config.Enabled)
        {
            _logger.LogInformation(
                "Packet email skipped for SR {ServiceRequestId}: no location config or delivery disabled",
                request.Id);
            return;
        }

        var recipients = (config.Recipients ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();
        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Packet email skipped for SR {ServiceRequestId}: location {LocationId} has no recipients configured",
                request.Id, location!.LocationId);
            return;
        }

        var packetVersion = request.PacketGeneration.PacketVersion;

        // Idempotency (Spec B-4): this exact packet has already been emailed — never double-send.
        if (request.PacketEmailDelivery.IsDeliveredFor(packetVersion))
        {
            _logger.LogInformation(
                "Packet email skipped for SR {ServiceRequestId} v{PacketVersion}: already delivered",
                request.Id, packetVersion);
            return;
        }

        var attachments = new List<PacketEmailAttachment>();
        if (config.AttachPdf)
        {
            attachments.Add(new PacketEmailAttachment
            {
                FileName = $"service-packet-{packet.Origin.ReferenceCode}.pdf",
                ContentType = "application/pdf",
                Content = pdf,
            });
        }

        if (config.IncludePhotos)
        {
            foreach (var part in parts)
            {
                attachments.AddRange(BuildPhotoAttachments(part.Request, part.PhotoImages, part.PhotoUrls));
            }
        }

        // Trim the attachment set to the email size budget (Spec B-4, #521). Spec A-6 allows ten
        // 25 MB uploads, so a photo-heavy submission's original photos can exceed the 10 MB
        // request ceiling on their own; before this it failed every attempt and left the shop a
        // request with no packet. The PDF is not the problem (QuestPDF resamples embedded images,
        // keeping it at roughly 1.5–3 MB) and outranks the photos.
        var plainTextBody = PacketEmailComposer.BuildCombinedPlainTextBody(packets);
        var fit = PacketEmailSizeFitter.Fit(attachments, html, plainTextBody, _packetEmailOptions.MaxRequestBytes);

        if (fit.AnythingDropped)
        {
            _logger.LogWarning(
                "Packet email for SR {ServiceRequestId} v{PacketVersion} exceeded the {BudgetBytes}-byte email budget: attaching {KeptCount} of {CandidateCount} file(s) at ~{EstimatedBytes} bytes, dropping {DroppedFiles}",
                request.Id, packetVersion, _packetEmailOptions.MaxRequestBytes,
                fit.Attachments.Count, attachments.Count, fit.EstimatedRequestBytes,
                string.Join(", ", fit.Dropped.Select(d => d.FileName)));
        }

        // A dropped photo attachment is no longer visible inline (issue #580: the HTML body
        // lists photos by name only, it no longer embeds them by SAS URL) — point the reader
        // at the Manager app instead of silently losing the photo. Re-rendering after Fit()
        // costs a few dozen bytes against the budget's 500 KB margin, which is negligible.
        //
        // The pointer is the packet's own /sr/{id} link, not a second hand-built URL: it lands on
        // the board's detail dialog, the one place in the manager app that renders photos
        // (issue #581). Null when no base URL is configured, and the renderer then omits the note
        // rather than emitting a dead href.
        var droppedAPhoto = fit.Dropped.Any(
            d => d.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
        if (droppedAPhoto)
        {
            html = PacketHtmlRenderer.RenderCombined(packets, photosDropped: true);
        }

        if (fit.PdfDropped)
        {
            // The printable artifact could not be emailed. This should be impossible: even a
            // ten-photo PDF is about 3.8 MB on the wire, and startup validation keeps
            // MaxRequestBytes at 5 MB or more. So it signals a regression — the renderer stopped resampling images, or
            // something large landed in the HTML body. Any photos that fit were still attached,
            // and the packet is stored and downloadable from the manager app.
            _logger.LogCritical(
                PacketEmailOversized,
                "Packet email for SR {ServiceRequestId} v{PacketVersion} in tenant {TenantId} went out with no PDF: it did not fit the {BudgetBytes}-byte email budget on its own",
                request.Id, packetVersion, request.TenantId, _packetEmailOptions.MaxRequestBytes);
        }

        var message = PacketEmailComposer.ComposeCombined(
            packets, html, request.CustomerSnapshot.LastName, recipients, fit.Attachments);

        // One correlation id spans every retry of this delivery. Packet generation runs off the
        // HTTP request thread, so fall back to the service request id when there is no ambient trace.
        var correlationId = Activity.Current?.TraceId.ToString() ?? request.Id;
        using var deliveryScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ServiceRequestId"] = request.Id,
            ["PacketVersion"] = packetVersion,
        });

        request.PacketEmailDelivery.BeginRun();

        for (var attempt = 1; attempt <= PacketEmailDeliveryEmbedded.MaxAttempts; attempt++)
        {
            request.PacketEmailDelivery.MarkAttempt();
            try
            {
                await _notificationService.SendPacketEmailAsync(message, cancellationToken);
                request.PacketEmailDelivery.MarkDelivered(packetVersion, DateTime.UtcNow);
                _logger.LogInformation(
                    "Packet email delivered for SR {ServiceRequestId} v{PacketVersion} on attempt {Attempt}/{MaxAttempts} to {RecipientCount} recipient(s) with {AttachmentCount} attachment(s)",
                    request.Id, packetVersion, attempt, PacketEmailDeliveryEmbedded.MaxAttempts, recipients.Count, message.Attachments.Count);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var isLastAttempt = attempt == PacketEmailDeliveryEmbedded.MaxAttempts;
                _logger.LogWarning(
                    ex,
                    "Packet email attempt {Attempt}/{MaxAttempts} failed for SR {ServiceRequestId} v{PacketVersion}{Retrying}",
                    attempt, PacketEmailDeliveryEmbedded.MaxAttempts, request.Id, packetVersion,
                    isLastAttempt ? string.Empty : "; will retry after backoff");

                if (isLastAttempt)
                {
                    request.PacketEmailDelivery.MarkFailed(TrimError($"{ex.GetType().Name}: {ex.Message}"));
                    break;
                }

                await Task.Delay(BackoffFor(attempt), cancellationToken);
            }
        }

        if (request.PacketEmailDelivery.Status == "Failed" && !request.PacketEmailDelivery.AlertRaised)
        {
            _logger.LogCritical(
                PacketEmailDeliveryExhausted,
                "Packet email delivery exhausted after {AttemptCount} attempts for SR {ServiceRequestId} v{PacketVersion} in tenant {TenantId}",
                request.PacketEmailDelivery.AttemptCount, request.Id, packetVersion, request.TenantId);
            request.PacketEmailDelivery.MarkAlertRaised();
        }

        // Persist the delivery outcome so a repeat run sees it and does not re-send.
        request.MarkAsUpdated(SystemUserId);
        await _serviceRequestRepository.UpdateAsync(request, cancellationToken);

        // Every issue of a multi-issue packet went out in that one message (Spec A-17).
        if (request.PacketEmailDelivery.Status == "Delivered")
        {
            foreach (var member in members.Where(m => m.Id != request.Id))
            {
                member.PacketEmailDelivery.MarkDelivered(member.PacketGeneration.PacketVersion, DateTime.UtcNow);
                member.MarkAsUpdated(SystemUserId);
                await _serviceRequestRepository.UpdateAsync(member, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Exponential backoff before the retry that follows <paramref name="attempt"/>:
    /// <c>RetryBaseDelay × 2^(attempt-1)</c> (attempt 1 → ×1, attempt 2 → ×2).
    /// </summary>
    private TimeSpan BackoffFor(int attempt) =>
        _packetEmailOptions.RetryBaseDelay * (1L << (attempt - 1));

    /// <summary>Caps an error string at <see cref="MaxErrorLength"/> for storage on the request.</summary>
    private static string TrimError(string error) =>
        error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;

    /// <summary>
    /// Turns the already-downloaded original photo bytes into email attachments — the same
    /// image attachments the packet renders as thumbnails (<c>Spec B-4</c>). A photo whose
    /// bytes could not be fetched is simply left off the email.
    /// </summary>
    private static IEnumerable<PacketEmailAttachment> BuildPhotoAttachments(
        ServiceRequest request,
        IReadOnlyDictionary<string, byte[]> photoImages,
        IReadOnlyDictionary<string, string> photoUrls)
    {
        var index = 0;
        foreach (var attachment in request.Attachments)
        {
            var isImage = attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            if (!isImage)
            {
                continue;
            }

            if (!photoUrls.TryGetValue(attachment.AttachmentId, out var url) ||
                !photoImages.TryGetValue(url, out var bytes))
            {
                continue;
            }

            index++;
            var fileName = string.IsNullOrWhiteSpace(attachment.FileName)
                ? $"photo-{index}"
                : attachment.FileName;

            yield return new PacketEmailAttachment
            {
                FileName = fileName,
                ContentType = attachment.ContentType!,
                Content = bytes,
            };
        }
    }
}
