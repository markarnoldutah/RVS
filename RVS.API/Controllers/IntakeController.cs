using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RVS.API.Integrations;
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
    private readonly IVinExtractionService _vinExtractionService;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly IIssueTextRefinementService _issueTextRefinementService;
    private readonly AiOptions _aiOptions;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeController"/>.
    /// </summary>
    public IntakeController(
        IIntakeOrchestrationService intakeService,
        ICategorizationService categorizationService,
        IAttachmentService attachmentService,
        IVinDecoderService vinDecoderService,
        IVinExtractionService vinExtractionService,
        ISpeechToTextService speechToTextService,
        IIssueTextRefinementService issueTextRefinementService,
        IOptions<AiOptions> aiOptions)
    {
        _intakeService = intakeService;
        _categorizationService = categorizationService;
        _attachmentService = attachmentService;
        _vinDecoderService = vinDecoderService;
        _vinExtractionService = vinExtractionService;
        _speechToTextService = speechToTextService;
        _issueTextRefinementService = issueTextRefinementService;
        _aiOptions = aiOptions.Value;
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
    /// Accepts optional context (description, asset info) for more targeted questions.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="request">Request containing the issue category and optional context.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("diagnostic-questions")]
    public async Task<ActionResult<DiagnosticQuestionsResponseDto>> GetDiagnosticQuestions(
        string locationSlug, [FromBody] DiagnosticQuestionsRequest request, CancellationToken ct = default)
    {
        var result = await _categorizationService.SuggestDiagnosticQuestionsAsync(
            request.IssueCategory,
            request.IssueDescription,
            request.Manufacturer,
            request.Model,
            request.Year,
            ct);

        var dto = new DiagnosticQuestionsResponseDto
        {
            Questions = result.Questions.Select(q => new DiagnosticQuestionDto
            {
                QuestionText = q.QuestionText,
                Options = q.Options.ToList(),
                AllowFreeText = q.AllowFreeText,
                HelpText = q.HelpText
            }).ToList(),
            SmartSuggestion = result.SmartSuggestion
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
    /// Extracts a VIN from a photo using Azure OpenAI GPT-4o Vision.
    /// Always returns HTTP 200 with an <see cref="AiOperationResponseDto{T}"/> envelope.
    /// A zero confidence score indicates no VIN was found or extraction failed.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="request">Base64-encoded image and MIME content type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>AI operation envelope with extracted VIN and confidence score.</returns>
    [HttpPost("ai/extract-vin")]
    public async Task<ActionResult<AiOperationResponseDto<VinExtractionResultDto>>> ExtractVin(
        string locationSlug, [FromBody] VinExtractionRequestDto request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return BadRequest(new { message = "ImageBase64 is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ContentType) || !request.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "ContentType must be a valid image MIME type (e.g. image/jpeg)." });
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(request.ImageBase64);
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "ImageBase64 is not valid base64-encoded data." });
        }

        if (imageBytes.Length > _aiOptions.MaxImageBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new { message = $"Image exceeds the maximum allowed size of {_aiOptions.MaxImageBytes / (1024 * 1024)} MB." });
        }

        var result = await _vinExtractionService.ExtractVinFromImageAsync(imageBytes, request.ContentType, ct);

        if (result is null)
        {
            return Ok(new AiOperationResponseDto<VinExtractionResultDto>
            {
                Success = true,
                Result = null,
                Confidence = 0.0,
                Warnings = ["Could not extract VIN from image."],
                Provider = "VinExtractionService",
                CorrelationId = correlationId
            });
        }

        return Ok(new AiOperationResponseDto<VinExtractionResultDto>
        {
            Success = true,
            Result = new VinExtractionResultDto { Vin = result.Vin },
            Confidence = result.Confidence,
            Warnings = [],
            Provider = result.Provider,
            CorrelationId = correlationId
        });
    }

    /// <summary>
    /// Transcribes audio of an issue description into text using a speech-to-text engine.
    /// Optionally returns a cleaned description suitable for the issue description field.
    /// Always returns HTTP 200 with an <see cref="AiOperationResponseDto{T}"/> envelope.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="request">Base64-encoded audio and MIME content type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>AI operation envelope with transcript and confidence score.</returns>
    [HttpPost("ai/transcribe-issue")]
    public async Task<ActionResult<AiOperationResponseDto<IssueTranscriptionResultDto>>> TranscribeIssue(
        string locationSlug, [FromBody] IssueTranscriptionRequestDto request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(request.AudioBase64))
        {
            return BadRequest(new { message = "AudioBase64 is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ContentType) || !request.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "ContentType must be a valid audio MIME type (e.g. audio/webm)." });
        }

        byte[] audioBytes;
        try
        {
            audioBytes = Convert.FromBase64String(request.AudioBase64);
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "AudioBase64 is not valid base64-encoded data." });
        }

        if (audioBytes.Length > _aiOptions.MaxAudioBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new { message = $"Audio exceeds the maximum allowed size of {_aiOptions.MaxAudioBytes / (1024 * 1024)} MB." });
        }

        var locale = request.Locale ?? "en-US";
        var result = await _speechToTextService.TranscribeAudioAsync(audioBytes, request.ContentType, locale, ct);

        if (result is null)
        {
            return Ok(new AiOperationResponseDto<IssueTranscriptionResultDto>
            {
                Success = true,
                Result = null,
                Confidence = 0.0,
                Warnings = ["Could not transcribe audio."],
                Provider = "SpeechToTextService",
                CorrelationId = correlationId
            });
        }

        return Ok(new AiOperationResponseDto<IssueTranscriptionResultDto>
        {
            Success = true,
            Result = new IssueTranscriptionResultDto
            {
                RawTranscript = result.RawTranscript,
                CleanedDescription = result.CleanedDescription
            },
            Confidence = result.Confidence,
            Warnings = [],
            Provider = result.Provider,
            CorrelationId = correlationId
        });
    }

    /// <summary>
    /// Refines a raw speech-to-text transcript into a clean, customer-editable issue description.
    /// Always returns HTTP 200 with an <see cref="AiOperationResponseDto{T}"/> envelope.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="request">Raw transcript and optional category context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>AI operation envelope with the cleaned description.</returns>
    [HttpPost("ai/refine-issue-text")]
    public async Task<ActionResult<AiOperationResponseDto<IssueTextRefinementResultDto>>> RefineIssueText(
        string locationSlug, [FromBody] IssueTextRefinementRequestDto request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(request.RawTranscript))
        {
            return BadRequest(new { message = "RawTranscript is required." });
        }

        if (request.RawTranscript.Length > 4000)
        {
            return BadRequest(new { message = "RawTranscript must not exceed 4,000 characters." });
        }

        var result = await _issueTextRefinementService.RefineTranscriptAsync(request.RawTranscript, request.IssueCategory, ct);

        if (result is null)
        {
            return Ok(new AiOperationResponseDto<IssueTextRefinementResultDto>
            {
                Success = true,
                Result = null,
                Confidence = 0.0,
                Warnings = ["Could not refine transcript."],
                Provider = "IssueTextRefinementService",
                CorrelationId = correlationId
            });
        }

        return Ok(new AiOperationResponseDto<IssueTextRefinementResultDto>
        {
            Success = true,
            Result = new IssueTextRefinementResultDto { CleanedDescription = result.CleanedDescription },
            Confidence = result.Confidence,
            Warnings = [],
            Provider = result.Provider,
            CorrelationId = correlationId
        });
    }

    /// <summary>
    /// Suggests an issue category based on the provided issue description text.
    /// Always returns HTTP 200 with an <see cref="AiOperationResponseDto{T}"/> envelope.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="request">Free-text issue description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>AI operation envelope with the suggested category.</returns>
    [HttpPost("ai/suggest-category")]
    public async Task<ActionResult<AiOperationResponseDto<IssueCategorySuggestionResultDto>>> SuggestCategory(
        string locationSlug, [FromBody] IssueCategorySuggestionRequestDto request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(request.IssueDescription))
        {
            return BadRequest(new { message = "IssueDescription is required." });
        }

        if (request.IssueDescription.Length > 2000)
        {
            return BadRequest(new { message = "IssueDescription must not exceed 2,000 characters." });
        }

        var result = await _issueTextRefinementService.SuggestCategoryAsync(request.IssueDescription, ct);

        if (result is null)
        {
            return Ok(new AiOperationResponseDto<IssueCategorySuggestionResultDto>
            {
                Success = true,
                Result = null,
                Confidence = 0.0,
                Warnings = ["Could not suggest a category."],
                Provider = "IssueTextRefinementService",
                CorrelationId = correlationId
            });
        }

        return Ok(new AiOperationResponseDto<IssueCategorySuggestionResultDto>
        {
            Success = true,
            Result = new IssueCategorySuggestionResultDto { IssueCategory = result.IssueCategory },
            Confidence = result.Confidence,
            Warnings = [],
            Provider = result.Provider,
            CorrelationId = correlationId
        });
    }

    /// <summary>
    /// Infers urgency and RV usage from the given issue description.
    /// Always returns HTTP 200 with an <see cref="AiOperationResponseDto{T}"/> envelope.
    /// Either or both result fields may be <c>null</c> when inference is not confident.
    /// </summary>
    /// <param name="locationSlug">Location slug (route segment).</param>
    /// <param name="request">Free-text issue description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>AI envelope with inferred urgency and RV usage.</returns>
    [HttpPost("ai/suggest-insights")]
    public async Task<ActionResult<AiOperationResponseDto<IssueInsightsSuggestionResultDto>>> SuggestInsights(
        string locationSlug, [FromBody] IssueInsightsSuggestionRequestDto request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(request.IssueDescription))
        {
            return BadRequest(new { message = "IssueDescription is required." });
        }

        if (request.IssueDescription.Length > 2000)
        {
            return BadRequest(new { message = "IssueDescription must not exceed 2,000 characters." });
        }

        var result = await _issueTextRefinementService.SuggestInsightsAsync(request.IssueDescription, ct);

        if (result is null)
        {
            return Ok(new AiOperationResponseDto<IssueInsightsSuggestionResultDto>
            {
                Success = true,
                Result = null,
                Confidence = 0.0,
                Warnings = ["Could not infer urgency or RV usage."],
                Provider = "IssueTextRefinementService",
                CorrelationId = correlationId
            });
        }

        return Ok(new AiOperationResponseDto<IssueInsightsSuggestionResultDto>
        {
            Success = true,
            Result = new IssueInsightsSuggestionResultDto
            {
                Urgency = result.Urgency,
                RvUsage = result.RvUsage
            },
            Confidence = result.Confidence,
            Warnings = [],
            Provider = result.Provider,
            CorrelationId = correlationId
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
    /// The issue category to generate diagnostic questions for (required).
    /// </summary>
    public required string IssueCategory { get; init; }

    /// <summary>
    /// Optional customer-provided issue description for additional context.
    /// </summary>
    public string? IssueDescription { get; init; }

    /// <summary>
    /// Optional RV manufacturer name for model-specific questions.
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// Optional RV model name for model-specific questions.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Optional RV model year for age-relevant questions.
    /// </summary>
    public int? Year { get; init; }
}
