using RVS.Domain.DTOs;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Client-side contract for the intake AI helper endpoints (VIN decode/extract, speech-to-text,
/// issue text refinement, category + insight suggestion).
///
/// Shared step components (steps 3 and 5) depend on this interface so the same UI can be driven by:
/// <list type="bullet">
///   <item>The anonymous Intake WASM, routed through <c>/api/intake/{slug}/*</c> (rate-limited).</item>
///   <item>The authenticated Manager WASM, routed through <c>/api/dealerships/{dealershipId}/intake/*</c>
///         with an Auth0 bearer token (no rate-limit hit, audit trail intact).</item>
/// </list>
/// Implementations encapsulate the scope (slug or dealership id) — the interface has no scope
/// parameters to leak the hosting context into the step components.
/// </summary>
public interface IIntakeAiClient
{
    /// <summary>
    /// Decodes a VIN using the configured VIN decoder service (NHTSA vPIC in production).
    /// Returns <c>null</c> when the VIN cannot be decoded (HTTP 404 from the API).
    /// </summary>
    Task<VinDecodeResponseDto?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a VIN from a photo using the server-side AI vision endpoint.
    /// Returns the AI envelope with VIN and confidence score, or <c>null</c> on network failure.
    /// </summary>
    Task<AiOperationResponseDto<VinExtractionResultDto>?> ExtractVinFromImageAsync(
        VinExtractionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes audio of an issue description using the server-side speech-to-text endpoint.
    /// Returns the AI envelope with transcript and confidence score, or <c>null</c> on network failure.
    /// </summary>
    Task<AiOperationResponseDto<IssueTranscriptionResultDto>?> TranscribeIssueAsync(
        IssueTranscriptionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refines a raw transcript into a clean issue description using the server-side AI endpoint.
    /// Returns the AI envelope with the cleaned description, or <c>null</c> on network failure.
    /// </summary>
    Task<AiOperationResponseDto<IssueTextRefinementResultDto>?> RefineIssueTextAsync(
        IssueTextRefinementRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests an issue category from the provided description using the server-side AI endpoint.
    /// Returns the AI envelope with the suggested category, or <c>null</c> on network failure.
    /// </summary>
    Task<AiOperationResponseDto<IssueCategorySuggestionResultDto>?> SuggestIssueCategoryAsync(
        IssueCategorySuggestionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Infers urgency and RV usage from the provided issue description using the server-side AI endpoint.
    /// Returns the AI envelope with the inferred values, or <c>null</c> on network failure.
    /// </summary>
    Task<AiOperationResponseDto<IssueInsightsSuggestionResultDto>?> SuggestIssueInsightsAsync(
        IssueInsightsSuggestionRequestDto request, CancellationToken cancellationToken = default);
}
