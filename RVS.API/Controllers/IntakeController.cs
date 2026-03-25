using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
public class IntakeController : ControllerBase
{
    private readonly IIntakeOrchestrationService _intakeService;
    private readonly ICategorizationService _categorizationService;
    private readonly IAttachmentService _attachmentService;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeController"/>.
    /// </summary>
    public IntakeController(
        IIntakeOrchestrationService intakeService,
        ICategorizationService categorizationService,
        IAttachmentService attachmentService)
    {
        _intakeService = intakeService;
        _categorizationService = categorizationService;
        _attachmentService = attachmentService;
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
    /// Uploads a file attachment to an existing service request created during intake.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="file">The file to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("service-requests/{srId}/attachments")]
    public async Task<ActionResult<AttachmentDto>> UploadAttachment(
        string locationSlug, string srId, IFormFile file, CancellationToken ct = default)
    {
        using var stream = file.OpenReadStream();

        var sr = await _attachmentService.CreateAttachmentAsync(
            string.Empty, srId, file.FileName, file.ContentType, stream, cancellationToken: ct);

        var attachment = sr.Attachments[^1].ToDto();

        return Ok(attachment);
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
