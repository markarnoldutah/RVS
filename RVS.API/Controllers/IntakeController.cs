using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Customer-facing intake endpoints. All routes are anonymous — no authentication required.
/// </summary>
[ApiController]
[Route("api/intake/{locationSlug}")]
[AllowAnonymous]
[EnableRateLimiting("IntakeEndpoint")]
public class IntakeController : ControllerBase
{
    private readonly IIntakeOrchestrationService _intakeService;
    private readonly ICategorizationService _categorizationService;
    private readonly IAttachmentService _attachmentService;
    private readonly IVinDecoderService _vinDecoderService;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeController"/>.
    /// </summary>
    public IntakeController(
        IIntakeOrchestrationService intakeService,
        ICategorizationService categorizationService,
        IAttachmentService attachmentService,
        IVinDecoderService vinDecoderService)
    {
        _intakeService = intakeService;
        _categorizationService = categorizationService;
        _attachmentService = attachmentService;
        _vinDecoderService = vinDecoderService;
    }

    /// <summary>
    /// Returns the intake form configuration for the specified location slug.
    /// Includes dealership name, accepted file types, issue categories, and optional customer prefill.
    /// </summary>
    /// <param name="locationSlug">Location slug for resolving the intake context.</param>
    /// <param name="token">Optional magic-link token to prefill customer data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// GET /api/intake/camping-world-slc/config?token=abc123
    /// </example>
    [HttpGet("config")]
    public async Task<ActionResult<IntakeConfigResponseDto>> GetConfig(
        string locationSlug, [FromQuery] string? token = null, CancellationToken ct = default)
    {
        var config = await _intakeService.GetIntakeConfigAsync(locationSlug, token, ct);

        return Ok(config);
    }

    /// <summary>
    /// Returns AI-generated diagnostic follow-up questions based on the selected issue category.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="request">Request containing the issue category.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("diagnostic-questions")]
    public async Task<ActionResult<DiagnosticQuestionsResponseDto>> GetDiagnosticQuestions(
        string locationSlug, [FromBody] DiagnosticQuestionsRequest request, CancellationToken ct = default)
    {
        var questions = await _categorizationService.SuggestDiagnosticQuestionsAsync(request.IssueCategory, ct);

        var dto = new DiagnosticQuestionsResponseDto
        {
            Questions = questions.Select(q => new DiagnosticQuestionDto
            {
                QuestionText = q,
                AllowFreeText = true
            }).ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// Decodes a VIN using the configured third-party VIN decoder service (NHTSA vPIC in production, mock in development).
    /// Returns manufacturer, model, and year information for the given VIN.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="vin">17-character Vehicle Identification Number to decode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// GET /api/intake/camping-world-slc/decode-vin/1RGDE4428R1000001
    /// </example>
    [HttpGet("decode-vin/{vin}")]
    public async Task<ActionResult<VinDecodeResponseDto>> DecodeVin(
        string locationSlug, string vin, CancellationToken ct = default)
    {
        var result = await _vinDecoderService.DecodeVinAsync(vin, ct);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(new VinDecodeResponseDto
        {
            Vin = result.Vin,
            Manufacturer = result.Manufacturer,
            Model = result.Model,
            Year = result.Year
        });
    }

    /// <summary>
    /// Submits a new service request through the customer intake flow.
    /// Orchestrates customer account creation, profile resolution, and SR creation.
    /// </summary>
    /// <param name="locationSlug">Location slug for resolving tenant and location.</param>
    /// <param name="request">Service request creation data from the intake form.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("service-requests")]
    public async Task<ActionResult<ServiceRequestDetailResponseDto>> SubmitServiceRequest(
        string locationSlug, [FromBody] ServiceRequestCreateRequestDto request, CancellationToken ct = default)
    {
        var serviceRequest = await _intakeService.ExecuteAsync(locationSlug, request, ct);

        return CreatedAtAction(nameof(GetConfig), new { locationSlug }, serviceRequest.ToDetailDto());
    }

    /// <summary>
    /// Generates a time-limited SAS URL for direct client-to-blob upload of an attachment (15-minute expiry).
    /// Validates the content type and max-attachment cap before issuing the SAS URL.
    /// Returns the SAS URL and blob name needed for the subsequent confirm step.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="fileName">Original file name for the attachment.</param>
    /// <param name="contentType">MIME content type declared by the client (e.g. "image/jpeg").</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("service-requests/{srId}/attachments/upload-url")]
    public async Task<ActionResult<AttachmentUploadSasResponseDto>> GetUploadSas(
        string locationSlug, string srId, [FromQuery] string fileName, [FromQuery] string contentType, CancellationToken ct = default)
    {
        var tenantId = await _intakeService.ResolveSlugToTenantIdAsync(locationSlug, ct);

        var result = await _attachmentService.GenerateUploadSasAsync(tenantId, srId, fileName, contentType, cancellationToken: ct);

        return Ok(result);
    }

    /// <summary>
    /// Confirms a direct-upload attachment after the client has uploaded the blob via SAS URL.
    /// Validates the blob exists, records the attachment on the service request.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="request">Confirmation details (blob name, file name, content type, size).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("service-requests/{srId}/attachments/confirm")]
    public async Task<ActionResult<AttachmentDto>> ConfirmUpload(
        string locationSlug, string srId, [FromBody] AttachmentConfirmRequestDto request, CancellationToken ct = default)
    {
        var tenantId = await _intakeService.ResolveSlugToTenantIdAsync(locationSlug, ct);

        var attachment = await _attachmentService.ConfirmAttachmentAsync(tenantId, srId, request, cancellationToken: ct);

        return CreatedAtAction(nameof(GetConfig), new { locationSlug }, attachment);
    }
}

/// <summary>
/// Request body for the diagnostic questions endpoint.
/// </summary>
public sealed record DiagnosticQuestionsRequest
{
    /// <summary>
    /// The issue category to generate diagnostic questions for.
    /// </summary>
    public required string IssueCategory { get; init; }
}
