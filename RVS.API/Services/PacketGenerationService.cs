using RVS.API.Packets;
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

    /// <summary>Event id for the exhausted-retries alert (<c>Spec B-1</c>).</summary>
    private static readonly EventId PacketGenerationExhausted = new(434_001, nameof(PacketGenerationExhausted));

    private const string SystemUserId = "system";
    private const int MaxErrorLength = 500;

    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IPacketPhotoUrlResolver _photoUrlResolver;
    private readonly IBlobStorageService _blobStorage;
    private readonly IPacketGenerationQueue _queue;
    private readonly IUserContextAccessor _userContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PacketGenerationService> _logger;

    /// <summary>Creates the packet generation orchestrator with its repositories, blob storage, queue, and notification transport.</summary>
    public PacketGenerationService(
        IServiceRequestRepository serviceRequestRepository,
        ILocationRepository locationRepository,
        IPacketPhotoUrlResolver photoUrlResolver,
        IBlobStorageService blobStorage,
        IPacketGenerationQueue queue,
        IUserContextAccessor userContext,
        INotificationService notificationService,
        ILogger<PacketGenerationService> logger)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _locationRepository = locationRepository;
        _photoUrlResolver = photoUrlResolver;
        _blobStorage = blobStorage;
        _queue = queue;
        _userContext = userContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PacketGenerationOutcome> GenerateAsync(string tenantId, string serviceRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        var request = await _serviceRequestRepository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        // Record the attempt before doing the work so a mid-render crash still shows it was tried.
        request.PacketGeneration.MarkGenerating();
        request.MarkAsUpdated(SystemUserId);
        await _serviceRequestRepository.UpdateAsync(request, cancellationToken);

        try
        {
            var location = await _locationRepository.GetByIdAsync(tenantId, request.LocationId, cancellationToken);

            var photoUrls = await _photoUrlResolver.ResolveAsync(request, cancellationToken);

            var pasteBlockCap = location?.PacketConfig.PasteBlockCharacterCap
                ?? PacketConfigEmbedded.DefaultPasteBlockCharacterCap;

            var context = new PacketCompositionContext
            {
                LocationName = location?.Name,
                LocationPhone = location?.Phone,
                SubmittedAtUtc = request.CreatedAtUtc,
                StatusLinkUrl = null,   // minted by #427
                PasteBlock = PasteBlockGenerator.Generate(
                    request.IssueCategory,
                    request.IssueDescription,
                    statusLinkUrl: null,   // supplied by #427 once the status token is minted
                    characterCap: pasteBlockCap),
                PhotoUrls = photoUrls,
            };

            var packet = PacketComposer.Compose(request, context);

            // Render the HTML now too: it is the primary artifact and shares failure modes with
            // the PDF, so a broken packet is caught here rather than at email time (#437).
            var html = PacketHtmlRenderer.Render(packet);
            _logger.LogDebug("Packet generation: composed HTML packet ({Length} chars) for SR {ServiceRequestId}", html.Length, request.Id);

            var photoImages = await DownloadPhotoBytesAsync(request, photoUrls, cancellationToken);

            var pdf = PacketPdfRenderer.Render(packet, photoImages);

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

            // Deliver the packet by email (Spec B-4, #437). A delivery failure is logged and
            // swallowed here: the packet is generated and stored, and re-delivery is #438's job.
            await DeliverPacketEmailAsync(request, location, packet, html, pdf, photoImages, photoUrls, cancellationToken);

            return PacketGenerationOutcome.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = $"{ex.GetType().Name}: {ex.Message}";
            if (error.Length > MaxErrorLength)
            {
                error = error[..MaxErrorLength];
            }

            request.PacketGeneration.MarkFailed(error);

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

    /// <summary>
    /// Downloads the image bytes for each resolved photo so <see cref="PacketPdfRenderer"/> can
    /// embed them (<c>Spec B-3</c>). A single photo that cannot be fetched is logged and skipped —
    /// it renders as a labelled placeholder rather than failing the whole packet.
    /// </summary>
    private async Task<Dictionary<string, byte[]>> DownloadPhotoBytesAsync(
        ServiceRequest request, IReadOnlyDictionary<string, string> photoUrls, CancellationToken cancellationToken)
    {
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var attachment in request.Attachments)
        {
            if (!photoUrls.TryGetValue(attachment.AttachmentId, out var url) || string.IsNullOrWhiteSpace(attachment.BlobUri))
            {
                continue;
            }

            try
            {
                images[url] = await _blobStorage.DownloadAsync(AttachmentsContainer, attachment.BlobUri, cancellationToken);
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
    /// Sends the finished packet to the location's configured recipients (<c>Spec B-4</c>,
    /// issue #437). No-ops when the location has no packet config, delivery is disabled, or no
    /// recipient is set. Attachments follow the location's <c>attachPdf</c> / <c>includePhotos</c>
    /// flags. A send failure is logged and swallowed — generation has already succeeded and the
    /// PDF is stored; idempotent re-delivery with backoff is issue #438.
    /// </summary>
    private async Task DeliverPacketEmailAsync(
        ServiceRequest request,
        Location? location,
        ServicePacket packet,
        string html,
        byte[] pdf,
        IReadOnlyDictionary<string, byte[]> photoImages,
        IReadOnlyDictionary<string, string> photoUrls,
        CancellationToken cancellationToken)
    {
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
            attachments.AddRange(BuildPhotoAttachments(request, photoImages, photoUrls));
        }

        var message = PacketEmailComposer.Compose(
            packet, html, request.CustomerSnapshot.LastName, recipients, attachments);

        try
        {
            await _notificationService.SendPacketEmailAsync(message, cancellationToken);
            _logger.LogInformation(
                "Packet email dispatched for SR {ServiceRequestId} v{PacketVersion} to {RecipientCount} recipient(s) with {AttachmentCount} attachment(s)",
                request.Id, request.PacketGeneration.PacketVersion, recipients.Count, attachments.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Packet email dispatch failed for SR {ServiceRequestId} v{PacketVersion}; packet generation is unaffected",
                request.Id, request.PacketGeneration.PacketVersion);
        }
    }

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
