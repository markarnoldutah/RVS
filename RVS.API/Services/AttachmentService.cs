using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing file attachments on service requests.
/// Handles SAS URL generation for direct client-to-blob upload, confirmation, and deletion.
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    private readonly IServiceRequestRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IUserContextAccessor _userContext;
    private readonly IImageTranscoder _imageTranscoder;
    private readonly ILogger<AttachmentService> _logger;

    private static readonly TimeSpan UploadSasDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReadSasDuration = TimeSpan.FromHours(1);

    private const string ContainerName = "rvs-attachments";

    /// <summary>
    /// Allowed MIME types for attachment validation.
    /// </summary>
    internal static readonly HashSet<string> AllowedMimeTypes =
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/heic",
        "image/heif",
        "video/mp4",
        "video/quicktime",
        "video/webm",
        "audio/mp4",
        "audio/x-m4a",
        "audio/wav",
        "audio/x-wav",
        "application/pdf"
    ];

    /// <summary>
    /// Initializes a new instance of <see cref="AttachmentService"/>.
    /// </summary>
    public AttachmentService(
        IServiceRequestRepository repository,
        IBlobStorageService blobStorage,
        IUserContextAccessor userContext,
        IImageTranscoder imageTranscoder,
        ILogger<AttachmentService> logger)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _userContext = userContext;
        _imageTranscoder = imageTranscoder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AttachmentUploadSasResponseDto> GenerateUploadSasAsync(
        string tenantId,
        string serviceRequestId,
        string fileName,
        string contentType,
        int maxAttachments = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var sr = await _repository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        if (sr.Attachments.Count >= maxAttachments)
        {
            throw new ArgumentException($"Maximum of {maxAttachments} attachments per service request exceeded.");
        }

        if (!AllowedMimeTypes.Contains(contentType))
        {
            throw new ArgumentException($"Content type '{contentType}' is not an allowed attachment type.");
        }

        var blobName = $"{tenantId}/{serviceRequestId}/{Guid.NewGuid()}_{fileName}";
        var sasUrl = await _blobStorage.GenerateUploadSasUrlAsync(ContainerName, blobName, cancellationToken);

        return new AttachmentUploadSasResponseDto
        {
            SasUrl = sasUrl,
            BlobName = blobName,
            ExpiresAtUtc = DateTime.UtcNow.Add(UploadSasDuration)
        };
    }

    /// <inheritdoc />
    public async Task<AttachmentSasDto> GenerateReadSasAsync(
        string tenantId,
        string serviceRequestId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);

        var sr = await _repository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        var attachment = sr.Attachments.Find(a => a.AttachmentId == attachmentId)
            ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' not found on service request '{serviceRequestId}'.");

        var sasUrl = await _blobStorage.GenerateReadSasUrlAsync(ContainerName, attachment.BlobUri, cancellationToken);

        return new AttachmentSasDto
        {
            SasUrl = sasUrl,
            ExpiresAtUtc = DateTime.UtcNow.Add(ReadSasDuration)
        };
    }

    /// <inheritdoc />
    public async Task<AttachmentDto> ConfirmAttachmentAsync(
        string tenantId,
        string serviceRequestId,
        AttachmentConfirmRequestDto request,
        int maxAttachments = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentNullException.ThrowIfNull(request);

        var sr = await _repository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        if (sr.Attachments.Count >= maxAttachments)
        {
            throw new ArgumentException($"Maximum of {maxAttachments} attachments per service request exceeded.");
        }

        if (!AllowedMimeTypes.Contains(request.ContentType))
        {
            throw new ArgumentException($"Content type '{request.ContentType}' is not an allowed attachment type.");
        }

        var blobExists = await _blobStorage.BlobExistsAsync(ContainerName, request.BlobName, cancellationToken);
        if (!blobExists)
        {
            throw new ArgumentException($"Blob '{request.BlobName}' has not been uploaded. Complete the direct upload before confirming.");
        }

        var blobName = request.BlobName;
        var fileName = request.FileName;
        var contentType = request.ContentType;
        var sizeBytes = request.SizeBytes;

        // Normalise every image upload once here so the stored blob — and every downstream
        // consumer (the PDF embed, the HTML packet's <img>, the emailed attachment) — is a
        // small, universally-renderable raster. HEIC/HEIF renders on Apple clients only and is
        // always converted to JPEG (issue #508); a full-resolution JPEG or PNG is downscaled
        // past MaxEdgePixels and re-encoded so it no longer rides into the packet email at full
        // size, forcing #521's size fitter to drop photos (issue #562). Any failure — undecodable payload, oversized
        // source, download error, or a re-encode that would not shrink an already-web-safe
        // image — keeps the original upload and logs.
        if (_imageTranscoder.CanNormalize(contentType))
        {
            (blobName, fileName, contentType, sizeBytes) = await NormalizeImageAsync(
                serviceRequestId, blobName, fileName, contentType, sizeBytes, cancellationToken);
        }

        var attachment = new ServiceRequestAttachmentEmbedded
        {
            AttachmentId = Guid.NewGuid().ToString(),
            BlobUri = blobName,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes
        };

        sr.Attachments.Add(attachment);
        sr.MarkAsUpdated(_userContext.UserId);

        await _repository.UpdateAsync(sr, cancellationToken);

        return attachment.ToDto();
    }

    /// <inheritdoc />
    public async Task DeleteAttachmentAsync(
        string tenantId,
        string serviceRequestId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);

        var sr = await _repository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        var attachment = sr.Attachments.Find(a => a.AttachmentId == attachmentId)
            ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' not found on service request '{serviceRequestId}'.");

        await _blobStorage.DeleteAsync(ContainerName, attachment.BlobUri, cancellationToken);

        sr.Attachments.Remove(attachment);
        sr.MarkAsUpdated(_userContext.UserId);

        await _repository.UpdateAsync(sr, cancellationToken);
    }

    /// <summary>
    /// Downloads the just-uploaded image blob, normalises it (EXIF orientation baked in,
    /// downscaled past <c>MaxEdgePixels</c>, metadata stripped, re-encoded — a PNG stays PNG,
    /// every other raster becomes JPEG), stores the result under a blob name matching its
    /// output format, and best-effort deletes the original when the blob name changed. Returns
    /// the blob name, file name, content type, and size to record on the attachment. Any
    /// failure — download error, an undecodable or oversized payload, or a re-encode that would
    /// not shrink an already-web-safe image — logs and returns the original values unchanged,
    /// so normalisation never blocks an upload from being confirmed. The original evidence copy
    /// is not retained: no repository tracks it, every packet consumer reads the normalised
    /// blob, and <c>Spec A-6</c> / <c>X-6</c> do not require retention (consistent with the
    /// HEIC path from <c>#508</c>).
    /// </summary>
    private async Task<(string BlobName, string FileName, string ContentType, long SizeBytes)> NormalizeImageAsync(
        string serviceRequestId,
        string blobName,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        byte[] original;
        try
        {
            original = await _blobStorage.DownloadAsync(ContainerName, blobName, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Image normalise: could not download blob {BlobName} for SR {ServiceRequestId}; keeping the original upload",
                blobName, serviceRequestId);
            return (blobName, fileName, contentType, sizeBytes);
        }

        var result = _imageTranscoder.Normalize(original, contentType, cancellationToken);
        if (result is null)
        {
            _logger.LogInformation(
                "Image normalise: no change for blob {BlobName} on SR {ServiceRequestId}; keeping the original upload",
                blobName, serviceRequestId);
            return (blobName, fileName, contentType, sizeBytes);
        }

        var extension = string.Equals(result.ContentType, "image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        var normalizedBlobName = Path.ChangeExtension(blobName, extension);
        var replacesOriginal = !string.Equals(normalizedBlobName, blobName, StringComparison.Ordinal);

        using (var stream = new MemoryStream(result.Bytes, writable: false))
        {
            await _blobStorage.UploadAsync(ContainerName, normalizedBlobName, stream, result.ContentType, cancellationToken);
        }

        if (replacesOriginal)
        {
            try
            {
                await _blobStorage.DeleteAsync(ContainerName, blobName, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Image normalise: output stored as {NormalizedBlobName} but the original {BlobName} could not be deleted",
                    normalizedBlobName, blobName);
            }
        }

        var normalizedFileName = Path.ChangeExtension(fileName, extension);

        _logger.LogInformation(
            "Image normalise: SR {ServiceRequestId} upload {OriginalName} -> {NormalizedName} ({ContentType}, {Width}x{Height}, {Bytes} bytes, was {OldBytes})",
            serviceRequestId, fileName, normalizedFileName, result.ContentType, result.Width, result.Height, result.Bytes.Length, sizeBytes);

        return (normalizedBlobName, normalizedFileName, result.ContentType, result.Bytes.Length);
    }
}
