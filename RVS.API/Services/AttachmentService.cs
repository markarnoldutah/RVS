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
        IUserContextAccessor userContext)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _userContext = userContext;
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

        var attachment = new ServiceRequestAttachmentEmbedded
        {
            AttachmentId = Guid.NewGuid().ToString(),
            BlobUri = request.BlobName,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes
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
}
