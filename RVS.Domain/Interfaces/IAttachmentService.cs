using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing file attachments on service requests.
/// Handles SAS URL generation, MIME validation, and attachment lifecycle.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Generates a time-limited SAS URL for uploading an attachment (15-minute expiry).
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="serviceRequestId">Service request to attach the file to.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SAS URL and expiry time.</returns>
    Task<AttachmentSasDto> GenerateUploadSasAsync(string tenantId, string serviceRequestId, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a time-limited SAS URL for reading an existing attachment (1-hour expiry).
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="serviceRequestId">Service request that owns the attachment.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SAS URL and expiry time.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the service request or attachment is not found.</exception>
    Task<AttachmentSasDto> GenerateReadSasAsync(string tenantId, string serviceRequestId, string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an attachment on a service request after validating MIME type via magic bytes,
    /// file size constraints, and max attachment count.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="serviceRequestId">Service request to attach the file to.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="declaredContentType">Content type declared by the client.</param>
    /// <param name="fileStream">Stream containing file content.</param>
    /// <param name="maxAttachments">Maximum number of attachments allowed per service request.</param>
    /// <param name="maxFileSizeMb">Maximum file size in megabytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated service request with the new attachment.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails (MIME mismatch, too many attachments, file too large).</exception>
    Task<ServiceRequest> CreateAttachmentAsync(string tenantId, string serviceRequestId, string fileName, string declaredContentType, Stream fileStream, int maxAttachments = 10, int maxFileSizeMb = 25, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the actual MIME type detected from file magic bytes matches the declared content type.
    /// </summary>
    /// <param name="fileStream">Stream containing file content. Position is reset after reading.</param>
    /// <param name="declaredContentType">Content type declared by the client.</param>
    /// <exception cref="ArgumentException">Thrown when the detected MIME type does not match the declared type.</exception>
    Task ValidateMimeTypeAsync(Stream fileStream, string declaredContentType);
}
