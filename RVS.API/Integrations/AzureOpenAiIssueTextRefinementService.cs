using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Azure OpenAI–powered issue text refinement and category suggestion service.
/// Sends transcripts and descriptions to the chat completions endpoint and parses the structured JSON response.
/// Returns <c>null</c> on any network error, timeout, or unparseable response — never throws.
/// </summary>
public sealed class AzureOpenAiIssueTextRefinementService : IIssueTextRefinementService
{
    private const string ProviderName = nameof(AzureOpenAiIssueTextRefinementService);
    private const string ApiVersion = "2024-10-21";

    private const string RefineSystemPrompt =
        "You are an expert at cleaning up speech-to-text transcripts for RV service intake. " +
        "Remove filler words (um, uh, so, like, well, okay), fix capitalization and punctuation, " +
        "and produce a clear, concise customer issue description. " +
        "Return ONLY a JSON object: {\"cleaned_description\": \"<cleaned text>\", \"confidence\": <0.0-1.0>}.";

    private static readonly string[] ValidCategories =
        ["Electrical", "Plumbing", "HVAC", "Appliance", "Structural", "Slide-Out", "Awning"];

    private static readonly string SuggestSystemPrompt =
        "You are an RV service intake assistant. Given an issue description, suggest the most appropriate category " +
        $"from this list: {string.Join(", ", ValidCategories)}. " +
        "If no category fits well, use null for the category. " +
        "Return ONLY a JSON object: {\"category\": \"<category or null>\", \"confidence\": <0.0-1.0>}.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureOpenAiIssueTextRefinementService> _logger;

    public AzureOpenAiIssueTextRefinementService(
        HttpClient httpClient,
        ILogger<AzureOpenAiIssueTextRefinementService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IssueTextRefinementResult?> RefineTranscriptAsync(string rawTranscript, string? issueCategory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawTranscript);

        var userMessage = issueCategory is not null
            ? $"Category: {issueCategory}\n\nTranscript: {rawTranscript}"
            : rawTranscript;

        try
        {
            var requestBody = BuildTextRequestBody(RefineSystemPrompt, userMessage);
            _logger.LogDebug("Sending transcript refinement request ({Length} chars)", rawTranscript.Length);

            var response = await _httpClient.PostAsJsonAsync(
                $"chat/completions?api-version={ApiVersion}",
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Azure OpenAI transcript refinement returned HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode,
                    errorBody);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Azure OpenAI transcript refinement returned empty content");
                return null;
            }

            var payload = JsonSerializer.Deserialize<RefinementPayload>(content, JsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.CleanedDescription))
            {
                _logger.LogWarning("Azure OpenAI transcript refinement returned null or empty cleaned description");
                return null;
            }

            return new IssueTextRefinementResult(payload.CleanedDescription.Trim(), payload.Confidence, ProviderName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure OpenAI transcript refinement network error (Status: {StatusCode})", ex.StatusCode);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI transcript refinement timed out or was cancelled");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI transcript refinement returned unparseable response");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IssueCategorySuggestionResult?> SuggestCategoryAsync(string issueDescription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueDescription);

        try
        {
            var requestBody = BuildTextRequestBody(SuggestSystemPrompt, issueDescription);
            _logger.LogDebug("Sending category suggestion request ({Length} chars)", issueDescription.Length);

            var response = await _httpClient.PostAsJsonAsync(
                $"chat/completions?api-version={ApiVersion}",
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Azure OpenAI category suggestion returned HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode,
                    errorBody);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Azure OpenAI category suggestion returned empty content");
                return null;
            }

            var payload = JsonSerializer.Deserialize<CategoryPayload>(content, JsonOptions);
            if (payload is null)
            {
                _logger.LogWarning("Azure OpenAI category suggestion returned null payload");
                return null;
            }

            // Validate the suggested category against the known list and normalize casing.
            var category = payload.Category is not null
                ? ValidCategories.FirstOrDefault(c => c.Equals(payload.Category, StringComparison.OrdinalIgnoreCase))
                : null;
            if (payload.Category is not null && category is null)
            {
                _logger.LogWarning("Azure OpenAI suggested an unknown category: {Category}", payload.Category);
            }

            return new IssueCategorySuggestionResult(category, payload.Confidence, ProviderName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure OpenAI category suggestion network error (Status: {StatusCode})", ex.StatusCode);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI category suggestion timed out or was cancelled");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI category suggestion returned unparseable response");
            return null;
        }
    }

    private static JsonObject BuildTextRequestBody(string systemPrompt, string userMessage)
    {
        return new JsonObject
        {
            ["messages"] = new JsonArray(
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = systemPrompt
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = userMessage
                }
            ),
            ["max_tokens"] = 500,
            ["response_format"] = new JsonObject { ["type"] = "json_object" }
        };
    }

    // ── Private response types ───────────────────────────────────────────

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public IReadOnlyList<Choice>? Choices { get; init; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public AssistantMessage? Message { get; init; }
    }

    private sealed class AssistantMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed class RefinementPayload
    {
        [JsonPropertyName("cleaned_description")]
        public string? CleanedDescription { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }
    }

    private sealed class CategoryPayload
    {
        [JsonPropertyName("category")]
        public string? Category { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }
    }
}
