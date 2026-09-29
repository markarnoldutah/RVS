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
    /// Event id for a packet email that could not carry its PDF within the ACS size budget
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

    /// <summary>
    /// How long after a multi-problem submission is created its first request will hold the
    /// combined packet email for its siblings' packets (<c>Spec A-17</c>, <c>B-4</c>, issue #806).
    /// Each sibling may itself wait out <see cref="AttachmentUploadWindow"/> before generating, so
    /// this allows for that and a couple of minutes of generation. Once it closes the email goes
    /// with whatever has generated; a sibling that generates later is emailed on its own.
    /// </summary>
    internal static readonly TimeSpan SiblingWaitWindow = AttachmentUploadWindow + TimeSpan.FromMinutes(2);

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

        // Intake's photos land after the 201 that created this request (issue #516). Hold off
        // while fewer than promised have arrived — before MarkGenerating, so polling never eats
        // one of the three attempts — and render regardless once the window closes.
        if (IsWaitingForAttachments(request))
        {
            _logger.LogInformation(
                "Packet generation deferred for SR {ServiceRequestId}: {ArrivedCount} of {ExpectedCount} attachment(s) uploaded, still inside the {WindowSeconds}s upload window",
                request.Id, request.Attachments.Count, request.PacketGeneration.ExpectedAttachmentCount,
                AttachmentUploadWindow.TotalSeconds);

            return PacketGenerationOutcome.WaitingForAttachments;
        }

        // A multi-problem submission sends one packet email (Spec A-17, B-4, issue #806), and its
        // first request sends it. Hold off, like the attachment wait above, while a sibling's
        // packet is still on its way, and send with whatever has generated once the window closes.
        IReadOnlyList<ServiceRequest> siblings = [];
        if (OwnsCombinedDelivery(request))
        {
            var submission = await _serviceRequestRepository.GetBySubmissionIdAsync(tenantId, request.SubmissionId!, cancellationToken);
            siblings = [.. submission.Where(sr => sr.Id != request.Id)];

            var unsettled = siblings.Count(sr => !sr.PacketGeneration.IsSettled);
            if (unsettled > 0 && DateTime.UtcNow - request.CreatedAtUtc < SiblingWaitWindow)
            {
                _logger.LogInformation(
                    "Packet generation deferred for SR {ServiceRequestId}: {UnsettledCount} of {SiblingCount} sibling packet(s) still generating, inside the {WindowSeconds}s wait window",
                    request.Id, unsettled, siblings.Count, SiblingWaitWindow.TotalSeconds);

                return PacketGenerationOutcome.WaitingForSiblings;
            }
        }

        // Record the attempt before doing the work so a mid-render crash still shows it was tried.
        request.PacketGeneration.MarkGenerating();
        request.MarkAsUpdated(SystemUserId);
        await _serviceRequestRepository.UpdateAsync(request, cancellationToken);

        try
        {
            var location = await _locationRepository.GetByIdAsync(tenantId, request.LocationId, cancellationToken);

            // The dealer's logo (Spec A-16, issue #470). The PDF needs its bytes; the HTML only
            // names its URL, and does so only when the fetch succeeded, so the two renderings show
            // the logo or both leave it out. A logo that will not load never fails the packet.
            var logoUrl = location?.Branding is { HasLogo: true } branding ? branding.LogoUrl : null;
            var logoImage = logoUrl is null ? null : await _logoFetcher.FetchAsync(logoUrl, cancellationToken);
            if (logoImage is null)
            {
                logoUrl = null;
            }

            var own = await ComposeAsync(request, location, logoUrl, assess: true, cancellationToken);
            var packet = own.Packet;

            // Render the HTML now too: it is the primary artifact and shares failure modes with
            // the PDF, so a broken packet is caught here rather than at email time (#437).
            var html = PacketHtmlRenderer.Render(packet);
            _logger.LogDebug("Packet generation: composed HTML packet ({Length} chars) for SR {ServiceRequestId}", html.Length, request.Id);

            var pdf = PacketPdfRenderer.Render(packet, own.PhotoImages, logoImage);

            var nextVersion = request.PacketGeneration.PacketVersion + 1;
            var pdfBlobPath = $"packets/{tenantId}/{request.Id}/v{nextVersion}.pdf";
            using (var pdfStream = new MemoryStream(pdf, writable: false))
            {
                await _blobStorage.UploadAsync(AttachmentsContainer, pdfBlobPath, pdfStream, "application/pdf", cancellationToken);
            }

            request.PacketGeneration.MarkSucceeded(pdfBlobPath, DateTime.UtcNow);
            request.MarkAsUpdated(SystemUserId);
            await _serviceRequestRepository.UpdateAsync(request, cancellationToken);

            _logger.LogInformation(
                "Packet generation succeeded for SR {ServiceRequestId} (version {PacketVersion}, attempt {AttemptCount})",
                request.Id, request.PacketGeneration.PacketVersion, request.PacketGeneration.AttemptCount);

            // Deliver the packet by email (Spec B-4, #437 send, #438 idempotency + retry). A
            // delivery failure never fails generation: the packet is generated and stored.
            // A submission's first request carries every settled sibling's packet (Spec A-17).
            List<ComposedPacket> parts = [own, .. await ComposeSiblingsAsync(siblings, location, logoUrl, cancellationToken)];
            await DeliverPacketEmailAsync(request, location, parts, html, pdf, logoImage, cancellationToken);

            return PacketGenerationOutcome.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            request.PacketGeneration.MarkFailed(TrimError($"{ex.GetType().Name}: {ex.Message}"));

            var exhausted = request.PacketGeneration.AttemptCount >= PacketGenerationEmbedded.MaxAttempts;
            if (exhausted && !request.PacketGeneration.AlertRaised)
            {
                _logger.LogCritical(
                    PacketGenerationExhausted, ex,
                    "Packet generation exhausted after {AttemptCount} attempts for SR {ServiceRequestId} in tenant {TenantId}",
                    request.PacketGeneration.AttemptCount, request.Id, tenantId);
                request.PacketGeneration.MarkAlertRaised();
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Packet generation attempt {AttemptCount} failed for SR {ServiceRequestId} in tenant {TenantId}",
                    request.PacketGeneration.AttemptCount, request.Id, tenantId);
            }

            request.MarkAsUpdated(SystemUserId);
            await _serviceRequestRepository.UpdateAsync(request, cancellationToken);

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

        request.PacketGeneration.ResetForRegeneration();
        request.MarkAsUpdated(_userContext.UserId);
        await _serviceRequestRepository.UpdateAsync(request, cancellationToken);

        if (!_queue.TryEnqueue(new PacketGenerationJob(tenantId, request.Id, "regeneration")))
        {
            _logger.LogWarning(
                "Packet regeneration for SR {ServiceRequestId} could not be enqueued; it stays Pending and will need another regenerate request",
                request.Id);
        }
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
    /// Resolves and downloads <paramref name="request"/>'s photos and composes its packet.
    /// <paramref name="assess"/> runs the preliminary assessment when the request has none yet —
    /// true only for the request being generated; a sibling composed for the combined email
    /// renders the assessment its own generation stored, or none.
    /// </summary>
    private async Task<ComposedPacket> ComposeAsync(
        ServiceRequest request, Location? location, string? logoUrl, bool assess, CancellationToken cancellationToken)
    {
        var photoUrls = await _photoUrlResolver.ResolveAsync(request, cancellationToken);

        // One download feeds both the assessment, which reads the photos (issue #772), and
        // the PDF, which embeds them. It used to happen after the assessment, for the PDF only.
        var downloadedPhotos = await DownloadPhotosAsync(request, photoUrls, cancellationToken);

        if (assess)
        {
            await EnsurePreliminaryAssessmentAsync(request, downloadedPhotos, cancellationToken);
        }

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
    /// Composes the packet of every sibling whose own generation succeeded, in submission order,
    /// for the combined email (<c>Spec A-17</c>). A sibling that cannot be composed is logged and
    /// left out: the email still goes, and that request keeps its own stored packet.
    /// </summary>
    private async Task<List<ComposedPacket>> ComposeSiblingsAsync(
        IReadOnlyList<ServiceRequest> siblings, Location? location, string? logoUrl, CancellationToken cancellationToken)
    {
        var parts = new List<ComposedPacket>();
        foreach (var sibling in siblings.Where(sr => sr.PacketGeneration.Status == "Succeeded").OrderBy(sr => sr.SubmissionPosition))
        {
            try
            {
                parts.Add(await ComposeAsync(sibling, location, logoUrl, assess: false, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Combined packet email: could not compose sibling SR {ServiceRequestId}; sending without it",
                    sibling.Id);
            }
        }

        return parts;
    }

    /// <summary>
    /// Whether <paramref name="request"/> sends its submission's combined email: it is the first
    /// request of a multi-problem submission and nothing has been emailed for it yet. A later
    /// regeneration emails its own packet, like any other request.
    /// </summary>
    private static bool OwnsCombinedDelivery(ServiceRequest request) =>
        request.IsSubmissionLead && request.PacketEmailDelivery.DeliveredPacketVersion == 0;

    /// <summary>
    /// Whether <paramref name="request"/>'s packet should wait to go out in its submission's
    /// combined email instead of its own (<c>Spec A-17</c>): it is a later sibling, and the first
    /// request has neither sent that email nor given up generating. Once the lead has sent, a
    /// sibling that missed the email, or is regenerated, sends its own; the lead's send marks
    /// the siblings it carried as delivered, so idempotency keeps those from sending twice.
    /// </summary>
    private async Task<bool> IsLeftToTheLeadAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsInMultiIssueSubmission || request.IsSubmissionLead)
        {
            return false;
        }

        var lead = await _serviceRequestRepository.GetByIdAsync(request.TenantId, request.SubmissionId!, cancellationToken);
        if (lead is null)
        {
            return false;
        }

        var leadGaveUp = lead.PacketGeneration is { Status: "Failed", IsSettled: true }
            || lead.PacketEmailDelivery.Status == "Failed";

        return lead.PacketEmailDelivery.DeliveredPacketVersion == 0 && !leadGaveUp;
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
    /// </summary>
    private async Task DeliverPacketEmailAsync(
        ServiceRequest request,
        Location? location,
        IReadOnlyList<ComposedPacket> parts,
        string html,
        byte[] pdf,
        byte[]? logoImage,
        CancellationToken cancellationToken)
    {
        var packet = parts[0].Packet;

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

        // A later sibling's first packet travels in the lead's combined email (Spec A-17).
        if (await IsLeftToTheLeadAsync(request, cancellationToken))
        {
            _logger.LogInformation(
                "Packet email for SR {ServiceRequestId} left to SR {LeadServiceRequestId}, which sends its submission's combined email",
                request.Id, request.SubmissionId);
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

        // Several problems go out as one email: every packet in the HTML body and one merged
        // PDF (Spec A-17, B-4). The request's own stored PDF is not the one attached then.
        var combined = parts.Count > 1;
        IReadOnlyList<ServicePacket> packets = [.. parts.Select(p => p.Packet)];
        if (combined)
        {
            html = PacketHtmlRenderer.RenderCombined(packets);
        }

        var attachments = new List<PacketEmailAttachment>();
        if (config.AttachPdf)
        {
            attachments.Add(new PacketEmailAttachment
            {
                FileName = combined
                    ? $"service-packets-{packet.Origin.ReferenceCode}.pdf"
                    : $"service-packet-{packet.Origin.ReferenceCode}.pdf",
                ContentType = "application/pdf",
                Content = combined ? RenderCombinedPdf(parts, logoImage) : pdf,
            });
        }

        if (config.IncludePhotos)
        {
            foreach (var part in parts)
            {
                attachments.AddRange(BuildPhotoAttachments(part.Request, part.PhotoImages, part.PhotoUrls));
            }
        }

        // Trim the attachment set to what ACS will accept (Spec B-4, #521). Spec A-6 allows ten
        // 25 MB uploads, so a photo-heavy submission's original photos can exceed the 10 MB
        // request ceiling on their own; before this it failed every attempt and left the shop a
        // request with no packet. The PDF is not the problem (QuestPDF resamples embedded images,
        // keeping it at roughly 1.5–3 MB) and outranks the photos.
        var plainTextBody = PacketEmailComposer.BuildCombinedPlainTextBody(packets);
        var fit = PacketEmailSizeFitter.Fit(attachments, html, plainTextBody, _packetEmailOptions.MaxRequestBytes);

        if (fit.AnythingDropped)
        {
            _logger.LogWarning(
                "Packet email for SR {ServiceRequestId} v{PacketVersion} exceeded the {BudgetBytes}-byte ACS budget: attaching {KeptCount} of {CandidateCount} file(s) at ~{EstimatedBytes} bytes, dropping {DroppedFiles}",
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
            html = combined
                ? PacketHtmlRenderer.RenderCombined(packets, photosDropped: true)
                : PacketHtmlRenderer.Render(packet, packet.ManagerLinks?.RequestUrl);
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
                "Packet email for SR {ServiceRequestId} v{PacketVersion} in tenant {TenantId} went out with no PDF: it did not fit the {BudgetBytes}-byte ACS budget on its own",
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

        // The siblings carried by a combined email are delivered too: record it on each, so a
        // sibling's own later run sees its packet version already sent (Spec A-17).
        if (combined && request.PacketEmailDelivery.Status == "Delivered")
        {
            foreach (var sibling in parts.Skip(1).Select(p => p.Request))
            {
                sibling.PacketEmailDelivery.MarkDelivered(sibling.PacketGeneration.PacketVersion, DateTime.UtcNow);
                sibling.MarkAsUpdated(SystemUserId);
                await _serviceRequestRepository.UpdateAsync(sibling, cancellationToken);
            }
        }
    }

    /// <summary>
    /// The merged PDF for a combined email: every part's packet, one per sheet, with each part's
    /// downloaded photos (<c>Spec A-17</c>). Read URLs are unique, so the photo maps merge cleanly.
    /// </summary>
    private static byte[] RenderCombinedPdf(IReadOnlyList<ComposedPacket> parts, byte[]? logoImage)
    {
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (url, bytes) in parts.SelectMany(p => p.PhotoImages))
        {
            images[url] = bytes;
        }

        return PacketPdfRenderer.RenderCombined([.. parts.Select(p => p.Packet)], images, logoImage);
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
