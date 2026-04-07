namespace RVS.Domain.Integrations;

/// <summary>
/// Refines raw speech-to-text transcripts into clean issue descriptions and suggests
/// issue categories using AI language capabilities.
/// </summary>
public interface IIssueTextRefinementService
{
    /// <summary>
    /// Cleans up a raw transcript into a customer-editable issue description.
    /// </summary>
    /// <param name="rawTranscript">Raw speech-to-text transcript to refine.</param>
    /// <param name="issueCategory">Optional issue category that provides context for cleanup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Refinement result with the cleaned description and confidence score, or <c>null</c>
    /// if refinement failed. Never throws — callers should fall back to the raw transcript on <c>null</c>.
    /// </returns>
    Task<IssueTextRefinementResult?> RefineTranscriptAsync(string rawTranscript, string? issueCategory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests an issue category for the given issue description.
    /// </summary>
    /// <param name="issueDescription">Free-text description provided by the customer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Category suggestion with confidence score, or <c>null</c> if no confident suggestion
    /// is available. Never throws — callers should fall back to manual selection on <c>null</c>.
    /// </returns>
    Task<IssueCategorySuggestionResult?> SuggestCategoryAsync(string issueDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Infers urgency and RV usage from the given issue description.
    /// </summary>
    /// <param name="issueDescription">Free-text description provided by the customer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Insights result with inferred urgency and RV usage, or <c>null</c> if no confident
    /// inference is available. Never throws — callers should leave the fields unset on <c>null</c>.
    /// </returns>
    Task<IssueInsightsSuggestionResult?> SuggestInsightsAsync(string issueDescription, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transcript refinement operation.
/// </summary>
/// <param name="CleanedDescription">Customer-editable final draft of the issue description.</param>
/// <param name="Confidence">Confidence score in the range 0.0–1.0.</param>
/// <param name="Provider">Identifier for the service implementation that fulfilled the request.</param>
public sealed record IssueTextRefinementResult(string CleanedDescription, double Confidence, string Provider);

/// <summary>
/// Result of an issue category suggestion operation.
/// </summary>
/// <param name="IssueCategory">Suggested category, or <c>null</c> when no confident suggestion is available.</param>
/// <param name="Confidence">Confidence score in the range 0.0–1.0.</param>
/// <param name="Provider">Identifier for the service implementation that fulfilled the request.</param>
public sealed record IssueCategorySuggestionResult(string? IssueCategory, double Confidence, string Provider);

/// <summary>
/// Result of an issue insights suggestion operation.
/// </summary>
/// <param name="Urgency">Inferred urgency (e.g. "Low", "High", "Critical"), or <c>null</c> when not determinable.</param>
/// <param name="RvUsage">Inferred RV usage pattern (e.g. "Full-Time", "Seasonal"), or <c>null</c> when not determinable.</param>
/// <param name="Confidence">Confidence score in the range 0.0–1.0.</param>
/// <param name="Provider">Identifier for the service implementation that fulfilled the request.</param>
public sealed record IssueInsightsSuggestionResult(string? Urgency, string? RvUsage, double Confidence, string Provider);
