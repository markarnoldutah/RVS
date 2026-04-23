using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RVS.API.Integrations;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Authenticated, dealer-scoped counterpart of the anonymous <see cref="IntakeController"/>.
/// Exposes the VIN-decode / VIN-extract / speech-to-text / category / insights helpers to
/// authenticated dealer staff (e.g. a manager creating a walk-in service request), so those
/// flows avoid the anonymous rate limiter and produce a clean audit trail.
///
/// The underlying AI services are stateless — the <c>dealershipId</c> route segment is present
/// for URL/logging consistency with the rest of the manager surface, and tenant access is
/// enforced via the JWT <c>tenantId</c> claim resolved by <see cref="ClaimsService"/>.
/// </summary>
[ApiController]
[Route("api/dealerships/{dealershipId}/intake")]
[Authorize]
public class DealerIntakeController : ControllerBase
{
    private readonly IVinDecoderService _vinDecoderService;
    private readonly IVinExtractionService _vinExtractionService;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly IIssueTextRefinementService _issueTextRefinementService;
    private readonly AiOptions _aiOptions;
    private readonly ClaimsService _claimsService;
    private readonly ILogger<DealerIntakeController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DealerIntakeController"/>.
    /// </summary>
    public DealerIntakeController(
        IVinDecoderService vinDecoderService,
        IVinExtractionService vinExtractionService,
        ISpeechToTextService speechToTextService,
        IIssueTextRefinementService issueTextRefinementService,
        IOptions<AiOptions> aiOptions,
        ClaimsService claimsService,
        ILogger<DealerIntakeController> logger)
    {
        _vinDecoderService = vinDecoderService;
        _vinExtractionService = vinExtractionService;
        _speechToTextService = speechToTextService;
        _issueTextRefinementService = issueTextRefinementService;
        _aiOptions = aiOptions.Value;
        _claimsService = claimsService;
        _logger = logger;
    }

    /// <summary>
    /// Decodes a VIN via the configured VIN decoder service (NHTSA in prod).
    /// </summary>
    [HttpGet("decode-vin/{vin}")]
    public async Task<ActionResult<VinDecodeResponseDto>> DecodeVin(
        string dealershipId, string vin, CancellationToken ct = default)
    {
        _ = _claimsService.GetTenantIdOrThrow();

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
    /// </summary>
    [HttpPost("extract-vin")]
    public async Task<ActionResult<AiOperationResponseDto<VinExtractionResultDto>>> ExtractVin(
        string dealershipId, [FromBody] VinExtractionRequestDto request, CancellationToken ct = default)
    {
        _ = _claimsService.GetTenantIdOrThrow();

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

        var sw = Stopwatch.StartNew();
        var result = await _vinExtractionService.ExtractVinFromImageAsync(imageBytes, request.ContentType, ct);
        var elapsedMs = sw.ElapsedMilliseconds;

        if (result is null)
        {
            _logger.LogInformation(
                "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
                "VinExtraction", "VinExtractionService", 0.0, elapsedMs, false, false);

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

        _logger.LogInformation(
            "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
            "VinExtraction", result.Provider, result.Confidence, elapsedMs, false, true);

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
    /// Transcribes audio of an issue description into text using speech-to-text.
    /// </summary>
    [HttpPost("transcribe-issue")]
    public async Task<ActionResult<AiOperationResponseDto<IssueTranscriptionResultDto>>> TranscribeIssue(
        string dealershipId, [FromBody] IssueTranscriptionRequestDto request, CancellationToken ct = default)
    {
        _ = _claimsService.GetTenantIdOrThrow();

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
        var sw = Stopwatch.StartNew();
        var result = await _speechToTextService.TranscribeAudioAsync(audioBytes, request.ContentType, locale, ct);
        var elapsedMs = sw.ElapsedMilliseconds;

        if (result is null)
        {
            _logger.LogInformation(
                "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
                "TranscribeIssue", "SpeechToTextService", 0.0, elapsedMs, false, false);

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

        _logger.LogInformation(
            "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
            "TranscribeIssue", result.Provider, result.Confidence, elapsedMs, false, true);

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
    /// Refines a raw speech-to-text transcript into a clean, editable issue description.
    /// </summary>
    [HttpPost("refine-issue-text")]
    public async Task<ActionResult<AiOperationResponseDto<IssueTextRefinementResultDto>>> RefineIssueText(
        string dealershipId, [FromBody] IssueTextRefinementRequestDto request, CancellationToken ct = default)
    {
        _ = _claimsService.GetTenantIdOrThrow();

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

        var sw = Stopwatch.StartNew();
        var result = await _issueTextRefinementService.RefineTranscriptAsync(request.RawTranscript, request.IssueCategory, ct);
        var elapsedMs = sw.ElapsedMilliseconds;

        if (result is null)
        {
            _logger.LogInformation(
                "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
                "RefineIssueText", "IssueTextRefinementService", 0.0, elapsedMs, false, false);

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

        _logger.LogInformation(
            "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
            "RefineIssueText", result.Provider, result.Confidence, elapsedMs, false, true);

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
    /// Suggests an issue category from the provided description.
    /// </summary>
    [HttpPost("suggest-category")]
    public async Task<ActionResult<AiOperationResponseDto<IssueCategorySuggestionResultDto>>> SuggestCategory(
        string dealershipId, [FromBody] IssueCategorySuggestionRequestDto request, CancellationToken ct = default)
    {
        _ = _claimsService.GetTenantIdOrThrow();

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

        var sw = Stopwatch.StartNew();
        var result = await _issueTextRefinementService.SuggestCategoryAsync(request.IssueDescription, ct);
        var elapsedMs = sw.ElapsedMilliseconds;

        if (result is null)
        {
            _logger.LogInformation(
                "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
                "SuggestCategory", "IssueTextRefinementService", 0.0, elapsedMs, false, false);

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

        _logger.LogInformation(
            "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
            "SuggestCategory", result.Provider, result.Confidence, elapsedMs, false, true);

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
    /// Infers urgency and RV usage from the provided issue description.
    /// </summary>
    [HttpPost("suggest-insights")]
    public async Task<ActionResult<AiOperationResponseDto<IssueInsightsSuggestionResultDto>>> SuggestInsights(
        string dealershipId, [FromBody] IssueInsightsSuggestionRequestDto request, CancellationToken ct = default)
    {
        _ = _claimsService.GetTenantIdOrThrow();

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

        var sw = Stopwatch.StartNew();
        var result = await _issueTextRefinementService.SuggestInsightsAsync(request.IssueDescription, ct);
        var elapsedMs = sw.ElapsedMilliseconds;

        if (result is null)
        {
            _logger.LogInformation(
                "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
                "SuggestInsights", "IssueTextRefinementService", 0.0, elapsedMs, false, false);

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

        _logger.LogInformation(
            "AI telemetry: {Capability} completed — Provider={Provider}, Confidence={Confidence}, LatencyMs={LatencyMs}, Fallback={Fallback}, Success={Success}",
            "SuggestInsights", result.Provider, result.Confidence, elapsedMs, false, true);

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
}
