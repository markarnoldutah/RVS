using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing file attachments on service requests.
/// Handles SAS URL generation for direct client-to-blob upload, confirmation, and deletion.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Generates a time-limited SAS URL for uploading an attachment directly to blob storage (15-minute expiry).
    /// Validates the content type is allowed and that the attachment cap has not been reached before
    /// issuing the SAS, so clients cannot start a wasted upload. The cap is the service request's
    /// location <c>MaxAttachments</c>, never more than five (<c>Spec A-6</c>); the service reads it
    /// itself, so no caller can skip it.
    /// Returns the SAS URL, blob name (needed for confirm), and expiry time.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="serviceRequestId">Service request to attach the file to.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentType">MIME content type declared by the client (e.g. "image/jpeg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SAS URL, blob name, and expiry time.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    /// <exception cref="ArgumentException">Thrown when the content type is not allowed or max attachments exceeded.</exception>
    Task<AttachmentUploadSasResponseDto> GenerateUploadSasAsync(string tenantId, string serviceRequestId, string fileName, string contentType, CancellationToken cancellationToken = default);

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
    /// Confirms a direct-upload attachment after the client has uploaded the blob via SAS URL.
    /// Re-checks the location's attachment cap (<c>Spec A-6</c>), validates that the blob exists,
    /// validates the content type, appends the attachment to the service request, and saves.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="serviceRequestId">Service request to attach the file to.</param>
    /// <param name="request">Confirmation details including blob name, file name, content type, and size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The confirmed attachment DTO.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails (unsupported content type, too many attachments, blob not found).</exception>
    Task<AttachmentDto> ConfirmAttachmentAsync(string tenantId, string serviceRequestId, AttachmentConfirmRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an attachment from a service request and removes the backing blob.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="serviceRequestId">Service request that owns the attachment.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the service request or attachment is not found.</exception>
    Task DeleteAttachmentAsync(string tenantId, string serviceRequestId, string attachmentId, CancellationToken cancellationToken = default);
}
