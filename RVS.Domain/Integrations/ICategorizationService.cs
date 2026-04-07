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
    /// Generates contextual diagnostic follow-up questions based on the issue category,
    /// description, and optional asset information. Returns structured questions with
    /// selectable options, help text, and an optional smart suggestion.
    /// </summary>
    /// <param name="issueCategory">Previously resolved issue category (required).</param>
    /// <param name="issueDescription">Customer-provided issue description for additional context.</param>
    /// <param name="manufacturer">RV manufacturer name for model-specific questions.</param>
    /// <param name="model">RV model name for model-specific questions.</param>
    /// <param name="year">RV model year for age-relevant questions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured diagnostic questions with options, help text, and optional smart suggestion.</returns>
    Task<DiagnosticQuestionsResult> SuggestDiagnosticQuestionsAsync(
        string issueCategory,
        string? issueDescription = null,
        string? manufacturer = null,
        string? model = null,
        int? year = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a diagnostic question generation operation, containing structured questions
/// with selectable options, free-text allowance, help text, and an optional smart suggestion.
/// </summary>
/// <param name="Questions">Ordered list of diagnostic questions with options and metadata.</param>
/// <param name="SmartSuggestion">Optional AI-generated suggestion or tip for the customer.</param>
/// <param name="Provider">Identifier for the service implementation that fulfilled the request.</param>
public sealed record DiagnosticQuestionsResult(
    IReadOnlyList<DiagnosticQuestionItem> Questions,
    string? SmartSuggestion,
    string Provider);

/// <summary>
/// A single diagnostic question with selectable options, help text, and free-text allowance.
/// </summary>
/// <param name="QuestionText">The question to display to the customer.</param>
/// <param name="Options">Predefined answer options the customer can select.</param>
/// <param name="AllowFreeText">Whether the customer can provide a free-text response.</param>
/// <param name="HelpText">Optional explanatory text displayed below the question.</param>
public sealed record DiagnosticQuestionItem(
    string QuestionText,
    IReadOnlyList<string> Options,
    bool AllowFreeText,
    string? HelpText);
