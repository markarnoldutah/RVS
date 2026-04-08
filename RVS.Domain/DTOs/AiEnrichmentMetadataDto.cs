namespace RVS.Domain.DTOs;

/// <summary>
/// Manager-facing AI provenance contract. Exposes which AI capabilities were used
/// during intake, their providers, and confidence scores so that service advisors
/// can distinguish AI-generated fields from human-entered data.
/// </summary>
/// <example>
/// <code>
/// var metadata = new AiEnrichmentMetadataDto
/// {
///     CategorySuggestionProvider = "AzureOpenAiIssueTextRefinementService",
///     CategorySuggestionConfidence = 0.91,
///     DiagnosticQuestionsProvider = "AzureOpenAiCategorizationService",
///     VinExtractionProvider = "AzureOpenAiVinExtractionService",
///     VinExtractionConfidence = 0.95,
///     EnrichedAtUtc = DateTime.UtcNow
/// };
/// </code>
/// </example>
public sealed record AiEnrichmentMetadataDto
{
    /// <summary>
    /// Provider that produced the issue category suggestion, or <c>null</c> if manually selected.
    /// </summary>
    public string? CategorySuggestionProvider { get; init; }

    /// <summary>
    /// Confidence score of the issue category suggestion (<c>0.0</c> to <c>1.0</c>).
    /// </summary>
    public double? CategorySuggestionConfidence { get; init; }

    /// <summary>
    /// Provider that generated diagnostic questions.
    /// </summary>
    public string? DiagnosticQuestionsProvider { get; init; }

    /// <summary>
    /// Provider that transcribed the issue audio, or <c>null</c> if no audio was submitted.
    /// </summary>
    public string? TranscriptionProvider { get; init; }

    /// <summary>
    /// Confidence score of the audio transcription (<c>0.0</c> to <c>1.0</c>).
    /// </summary>
    public double? TranscriptionConfidence { get; init; }

    /// <summary>
    /// Provider that extracted the VIN from a photo, or <c>null</c> if VIN was entered manually.
    /// </summary>
    public string? VinExtractionProvider { get; init; }

    /// <summary>
    /// Confidence score of VIN extraction (<c>0.0</c> to <c>1.0</c>).
    /// </summary>
    public double? VinExtractionConfidence { get; init; }

    /// <summary>
    /// Provider that inferred urgency and RV usage, or <c>null</c> if not inferred.
    /// </summary>
    public string? InsightsSuggestionProvider { get; init; }

    /// <summary>
    /// Confidence score of the urgency/RV-usage inference (<c>0.0</c> to <c>1.0</c>).
    /// </summary>
    public double? InsightsSuggestionConfidence { get; init; }

    /// <summary>
    /// UTC timestamp when AI enrichment metadata was last computed.
    /// </summary>
    public DateTime? EnrichedAtUtc { get; init; }
}
