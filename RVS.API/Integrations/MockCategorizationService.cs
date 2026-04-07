using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Development mock that returns a fixed category and 3 stock diagnostic questions.
/// </summary>
public sealed class MockCategorizationService : ICategorizationService
{
    private const string ProviderName = nameof(MockCategorizationService);

    private readonly ILogger<MockCategorizationService> _logger;

    public MockCategorizationService(ILogger<MockCategorizationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> CategorizeAsync(string issueDescription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueDescription);

        _logger.LogDebug("MockCategorizationService returning 'General' category");
        return Task.FromResult("General");
    }

    /// <inheritdoc />
    public Task<DiagnosticQuestionsResult> SuggestDiagnosticQuestionsAsync(
        string issueCategory,
        string? issueDescription = null,
        string? manufacturer = null,
        string? model = null,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCategory);

        _logger.LogDebug("MockCategorizationService returning stock diagnostic questions for category {Category}", issueCategory);

        IReadOnlyList<DiagnosticQuestionItem> questions =
        [
            new DiagnosticQuestionItem(
                "Can you describe the issue in more detail?",
                ["Noise or vibration", "Visible damage", "Performance issue", "Other"],
                true,
                "Include when the issue started and any symptoms you've noticed."),
            new DiagnosticQuestionItem(
                "When did you first notice the problem?",
                ["Today", "This week", "This month", "Over a month ago"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Is the issue intermittent or constant?",
                ["Intermittent", "Constant", "Getting worse", "Only under certain conditions"],
                true,
                null)
        ];

        var smartSuggestion = !string.IsNullOrWhiteSpace(issueDescription)
            ? "Take a photo or video of the issue before your visit — it helps our technicians prepare."
            : null;

        var result = new DiagnosticQuestionsResult(questions, smartSuggestion, ProviderName);
        return Task.FromResult(result);
    }
}
