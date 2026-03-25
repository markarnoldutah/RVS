using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing file attachments on service requests.
/// Handles SAS URL generation, MIME magic-byte validation, and attachment lifecycle.
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    private readonly IServiceRequestRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IUserContextAccessor _userContext;

    private static readonly TimeSpan UploadSasDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReadSasDuration = TimeSpan.FromHours(1);

    private const string ContainerName = "rvs-attachments";

    /// <summary>
    /// Allowed MIME types for validation reference.
    /// </summary>
    private static readonly HashSet<string> AllowedMimeTypes =
    [
        "image/jpeg",
        "image/png",
        "video/mp4",
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
        IUserContextAccessor userContext)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task<AttachmentSasDto> GenerateUploadSasAsync(
        string tenantId,
        string serviceRequestId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var blobName = $"{tenantId}/{serviceRequestId}/{Guid.NewGuid()}_{fileName}";
        var sasUrl = await _blobStorage.GenerateUploadSasUrlAsync(ContainerName, blobName, cancellationToken);

        return new AttachmentSasDto
        {
            SasUrl = sasUrl,
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
    public async Task<ServiceRequest> CreateAttachmentAsync(
        string tenantId,
        string serviceRequestId,
        string fileName,
        string declaredContentType,
        Stream fileStream,
        int maxAttachments = 10,
        int maxFileSizeMb = 25,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredContentType);
        ArgumentNullException.ThrowIfNull(fileStream);

        var sr = await _repository.GetByIdAsync(tenantId, serviceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{serviceRequestId}' not found.");

        if (sr.Attachments.Count >= maxAttachments)
        {
            throw new ArgumentException($"Maximum of {maxAttachments} attachments per service request exceeded.");
        }

        var maxSizeBytes = (long)maxFileSizeMb * 1024 * 1024;
        if (fileStream.CanSeek && fileStream.Length > maxSizeBytes)
        {
            throw new ArgumentException($"File size exceeds the maximum allowed size of {maxFileSizeMb} MB.");
        }

        await ValidateMimeTypeAsync(fileStream, declaredContentType);

        var attachmentId = Guid.NewGuid().ToString();
        var blobName = $"{tenantId}/{serviceRequestId}/{attachmentId}_{fileName}";
        var blobUri = await _blobStorage.UploadAsync(ContainerName, blobName, fileStream, declaredContentType, cancellationToken);

        var attachment = new ServiceRequestAttachmentEmbedded
        {
            AttachmentId = attachmentId,
            BlobUri = blobUri,
            FileName = fileName,
            ContentType = declaredContentType,
            SizeBytes = fileStream.CanSeek ? fileStream.Length : 0
        };

        sr.Attachments.Add(attachment);
        sr.MarkAsUpdated(_userContext.UserId);

        return await _repository.UpdateAsync(sr, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ValidateMimeTypeAsync(Stream fileStream, string declaredContentType)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredContentType);

        var header = new byte[8];
        var bytesRead = await fileStream.ReadAsync(header.AsMemory(0, 8));

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        var detectedType = DetectMimeType(header, bytesRead);

        if (detectedType is not null && !string.Equals(detectedType, declaredContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"MIME type mismatch: declared {declaredContentType}, detected {detectedType}.");
        }
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
    /// Detects MIME type from the first bytes of a file using magic byte signatures.
    /// </summary>
    internal static string? DetectMimeType(byte[] header, int bytesRead)
    {
        if (bytesRead < 3)
        {
            return null;
        }

        if (bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytesRead >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytesRead >= 4 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
        {
            return "audio/wav";
        }

        if (bytesRead >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
        {
            return "application/pdf";
        }

        if (bytesRead >= 8 && HasFtypSignature(header, bytesRead))
        {
            return "video/mp4";
        }

        return null;
    }

    /// <summary>
    /// Checks for the 'ftyp' box signature common to MP4/M4A containers.
    /// </summary>
    private static bool HasFtypSignature(byte[] header, int bytesRead)
    {
        return bytesRead >= 8
            && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70;
    }
}
