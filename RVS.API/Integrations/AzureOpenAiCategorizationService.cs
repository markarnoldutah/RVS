using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Azure OpenAI–powered categorization service.
/// Falls back to <see cref="RuleBasedCategorizationService"/> on timeout or error.
/// </summary>
public sealed class AzureOpenAiCategorizationService : ICategorizationService
{
    private readonly HttpClient _httpClient;
    private readonly RuleBasedCategorizationService _fallback;
    private readonly ILogger<AzureOpenAiCategorizationService> _logger;

    public AzureOpenAiCategorizationService(
        HttpClient httpClient,
        RuleBasedCategorizationService fallback,
        ILogger<AzureOpenAiCategorizationService> logger)
    {
        _httpClient = httpClient;
        _fallback = fallback;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> CategorizeAsync(string issueDescription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueDescription);

        try
        {
            var payload = new { prompt = issueDescription };
            var response = await _httpClient.PostAsJsonAsync("categorize", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result.Trim();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Azure OpenAI categorization failed; falling back to rule-based engine");
        }

        return await _fallback.CategorizeAsync(issueDescription, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> SuggestDiagnosticQuestionsAsync(string issueCategory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCategory);

        try
        {
            var payload = new { category = issueCategory };
            var response = await _httpClient.PostAsJsonAsync("diagnostic-questions", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var questions = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken);
            if (questions is { Count: > 0 })
            {
                return questions.AsReadOnly();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Azure OpenAI diagnostic question generation failed; falling back to rule-based engine");
        }

        return await _fallback.SuggestDiagnosticQuestionsAsync(issueCategory, cancellationToken);
    }
}
