using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Development mock that returns a fixed category and 3 stock diagnostic questions.
/// </summary>
public sealed class MockCategorizationService : ICategorizationService
{
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
    public Task<IReadOnlyList<string>> SuggestDiagnosticQuestionsAsync(string issueCategory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCategory);

        _logger.LogDebug("MockCategorizationService returning stock diagnostic questions for category {Category}", issueCategory);

        IReadOnlyList<string> questions =
        [
            "Can you describe the issue in more detail?",
            "When did you first notice the problem?",
            "Is the issue intermittent or constant?"
        ];

        return Task.FromResult(questions);
    }
}
