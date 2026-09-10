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

        // HEIC/HEIF renders on Apple clients only: the HTML packet shows a blank <img>, the
        // PDF decoder cannot read it, and emailed .heic files will not preview in most desktop
        // mail clients. Transcode once here so the stored blob — and every downstream consumer —
        // is a universally-renderable JPEG (issue #508, finishes #492 item 8). A transcode
        // failure keeps the original upload; the packet then shows its labelled placeholder.
        if (_imageTranscoder.CanTranscode(contentType))
        {
            (blobName, fileName, contentType, sizeBytes) = await TranscodeToJpegAsync(
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
    /// Downloads the just-uploaded HEIC/HEIF blob, transcodes it to JPEG, stores the JPEG under
    /// a sibling <c>.jpg</c> blob name, and best-effort deletes the original. Returns the blob
    /// name, file name, content type, and size to record on the attachment. Any failure —
    /// download error, or an undecodable payload — logs a warning and returns the original
    /// values unchanged, so a bad transcode never blocks the upload from being confirmed.
    /// </summary>
    private async Task<(string BlobName, string FileName, string ContentType, long SizeBytes)> TranscodeToJpegAsync(
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
                "HEIC transcode: could not download blob {BlobName} for SR {ServiceRequestId}; keeping the original upload",
                blobName, serviceRequestId);
            return (blobName, fileName, contentType, sizeBytes);
        }

        var result = _imageTranscoder.TranscodeToJpeg(original, cancellationToken);
        if (result is null)
        {
            _logger.LogWarning(
                "HEIC transcode failed for blob {BlobName} on SR {ServiceRequestId}; keeping the original upload (packet will show a placeholder)",
                blobName, serviceRequestId);
            return (blobName, fileName, contentType, sizeBytes);
        }

        var jpegBlobName = Path.ChangeExtension(blobName, ".jpg");
        if (string.Equals(jpegBlobName, blobName, StringComparison.Ordinal))
        {
            jpegBlobName = $"{blobName}.jpg";
        }

        using (var jpegStream = new MemoryStream(result.JpegBytes, writable: false))
        {
            await _blobStorage.UploadAsync(ContainerName, jpegBlobName, jpegStream, "image/jpeg", cancellationToken);
        }

        try
        {
            await _blobStorage.DeleteAsync(ContainerName, blobName, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "HEIC transcode: JPEG stored as {JpegBlobName} but the original {BlobName} could not be deleted",
                jpegBlobName, blobName);
        }

        var jpegFileName = Path.ChangeExtension(fileName, ".jpg");

        _logger.LogInformation(
            "HEIC transcode: SR {ServiceRequestId} upload {OriginalName} -> {JpegName} ({Width}x{Height}, {Bytes} bytes)",
            serviceRequestId, fileName, jpegFileName, result.Width, result.Height, result.JpegBytes.Length);

        return (jpegBlobName, jpegFileName, "image/jpeg", result.JpegBytes.Length);
    }
}
