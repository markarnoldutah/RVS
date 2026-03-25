using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Manages file attachments on service requests (upload, read SAS, delete).
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
    /// Uploads a file attachment to a service request.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="file">The file to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "CanUploadAttachments")]
    public async Task<ActionResult<AttachmentDto>> Upload(
        string dealershipId, string srId, IFormFile file, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        using var stream = file.OpenReadStream();
        var sr = await _service.CreateAttachmentAsync(
            tenantId, srId, file.FileName, file.ContentType, stream, cancellationToken: ct);

        var attachment = sr.Attachments[^1].ToDto();

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
