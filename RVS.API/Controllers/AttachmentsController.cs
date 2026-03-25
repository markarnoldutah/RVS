using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Manages file attachments on service requests (SAS upload, SAS read, confirm, delete).
/// Clients upload directly to blob storage using SAS URLs — no binary proxying through the API.
/// </summary>
[ApiController]
[Route("api/dealerships/{dealershipId}/service-requests/{srId}/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _service;
    private readonly ClaimsService _claimsService;

    /// <summary>
    /// Initializes a new instance of <see cref="AttachmentsController"/>.
    /// </summary>
    public AttachmentsController(IAttachmentService service, ClaimsService claimsService)
    {
        _service = service;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Generates a time-limited SAS URL for direct client-to-blob upload (15-minute expiry).
    /// Validates the content type and max-attachment cap before issuing the SAS URL.
    /// Returns the SAS URL and blob name needed for the subsequent confirm step.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="fileName">Original file name for the attachment.</param>
    /// <param name="contentType">MIME content type declared by the client (e.g. "image/jpeg").</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("upload-url")]
    [Authorize(Policy = "CanUploadAttachments")]
    public async Task<ActionResult<AttachmentUploadSasResponseDto>> GetUploadSas(
        string dealershipId, string srId, [FromQuery] string fileName, [FromQuery] string contentType, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var result = await _service.GenerateUploadSasAsync(tenantId, srId, fileName, contentType, cancellationToken: ct);

        return Ok(result);
    }

    /// <summary>
    /// Confirms a direct-upload attachment after the client has uploaded the blob via SAS URL.
    /// Validates the blob exists, records the attachment on the service request.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="request">Confirmation details (blob name, file name, content type, size).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("confirm")]
    [Authorize(Policy = "CanUploadAttachments")]
    public async Task<ActionResult<AttachmentDto>> ConfirmUpload(
        string dealershipId, string srId, [FromBody] AttachmentConfirmRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var attachment = await _service.ConfirmAttachmentAsync(tenantId, srId, request, cancellationToken: ct);

        return CreatedAtAction(nameof(GetReadSas), new { dealershipId, srId, attachmentId = attachment.AttachmentId }, attachment);
    }

    /// <summary>
    /// Generates a time-limited SAS URL for reading an existing attachment.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{attachmentId}/sas")]
    [Authorize(Policy = "CanReadAttachments")]
    public async Task<ActionResult<AttachmentSasDto>> GetReadSas(
        string dealershipId, string srId, string attachmentId, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var sas = await _service.GenerateReadSasAsync(tenantId, srId, attachmentId, ct);

        return Ok(sas);
    }

    /// <summary>
    /// Deletes an attachment from a service request.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{attachmentId}")]
    [Authorize(Policy = "CanDeleteAttachments")]
    public async Task<IActionResult> Delete(
        string dealershipId, string srId, string attachmentId, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        await _service.DeleteAttachmentAsync(tenantId, srId, attachmentId, ct);

        return NoContent();
    }
}
