namespace RVS.Domain.Integrations;

/// <summary>
/// Categorizes service request issue descriptions using AI (Azure OpenAI) with
/// a deterministic fallback for environments without an AI endpoint configured.
/// </summary>
public interface ICategorizationService
{
    /// <summary>
    /// Categorizes a free-text issue description into a standard issue category.
    /// Falls back to a default category when the AI endpoint is unavailable.
    /// </summary>
    /// <param name="issueDescription">Free-text description provided by the customer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved issue category string.</returns>
    Task<string> CategorizeAsync(string issueDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests follow-up diagnostic questions based on an issue category.
    /// </summary>
    /// <param name="issueCategory">Previously resolved issue category.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An ordered list of suggested diagnostic questions.</returns>
    Task<IReadOnlyList<string>> SuggestDiagnosticQuestionsAsync(string issueCategory, CancellationToken cancellationToken = default);
}
