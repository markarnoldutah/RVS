using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Azure OpenAI–powered categorization service.
/// Uses the chat completions API for contextual diagnostic question generation.
/// Falls back to <see cref="RuleBasedCategorizationService"/> on timeout or error.
/// </summary>
public sealed class AzureOpenAiCategorizationService : ICategorizationService
{
    private const string ProviderName = nameof(AzureOpenAiCategorizationService);
    private const string ApiVersion = "2024-10-21";

    private const string DiagnosticSystemPrompt =
        "You are an expert RV service advisor. Generate 2–4 diagnostic follow-up questions for a customer " +
        "who is submitting an RV service request. Each question should help a technician understand the issue " +
        "before the RV arrives.\n\n" +
        "Return ONLY a JSON object with this exact structure:\n" +
        "{\n" +
        "  \"questions\": [\n" +
        "    {\n" +
        "      \"question_text\": \"<question>\",\n" +
        "      \"options\": [\"<option1>\", \"<option2>\", ...],\n" +
        "      \"allow_free_text\": true,\n" +
        "      \"help_text\": \"<optional explanation or null>\"\n" +
        "    }\n" +
        "  ],\n" +
        "  \"smart_suggestion\": \"<optional brief tip for the customer, or null>\"\n" +
        "}\n\n" +
        "Guidelines:\n" +
        "- Each question MUST have 2–6 predefined answer options.\n" +
        "- Always allow free text for additional details.\n" +
        "- Questions should be specific to the issue category and description.\n" +
        "- Include a smart suggestion only when you have a helpful tip (e.g. 'Check the breaker panel before your visit').\n" +
        "- Keep questions concise and customer-friendly (no jargon).";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            var response = await _httpClient.PostAsJsonAsync(
                $"chat/completions?api-version={ApiVersion}",
                payload,
                cancellationToken);
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
    public async Task<DiagnosticQuestionsResult> SuggestDiagnosticQuestionsAsync(
        string issueCategory,
        string? issueDescription = null,
        string? manufacturer = null,
        string? model = null,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCategory);

        try
        {
            var userMessage = BuildDiagnosticUserMessage(issueCategory, issueDescription, manufacturer, model, year);
            var requestBody = BuildChatRequestBody(DiagnosticSystemPrompt, userMessage);

            _logger.LogDebug("Sending diagnostic question generation request for category {Category}",
                new string(issueCategory.Where(c => !char.IsControl(c)).ToArray()));

            var response = await _httpClient.PostAsJsonAsync(
                $"chat/completions?api-version={ApiVersion}",
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Azure OpenAI diagnostic question generation returned HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode,
                    errorBody);
                return await _fallback.SuggestDiagnosticQuestionsAsync(issueCategory, issueDescription, manufacturer, model, year, cancellationToken);
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Azure OpenAI diagnostic question generation returned empty content");
                return await _fallback.SuggestDiagnosticQuestionsAsync(issueCategory, issueDescription, manufacturer, model, year, cancellationToken);
            }

            var payload = JsonSerializer.Deserialize<DiagnosticQuestionsPayload>(content, JsonOptions);
            if (payload?.Questions is not { Count: > 0 })
            {
                _logger.LogWarning("Azure OpenAI diagnostic question generation returned no questions");
                return await _fallback.SuggestDiagnosticQuestionsAsync(issueCategory, issueDescription, manufacturer, model, year, cancellationToken);
            }

            var questions = payload.Questions
                .Select(q => new DiagnosticQuestionItem(
                    q.QuestionText ?? "Follow-up question",
                    q.Options ?? [],
                    q.AllowFreeText,
                    q.HelpText))
                .ToList()
                .AsReadOnly();

            return new DiagnosticQuestionsResult(questions, payload.SmartSuggestion, ProviderName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Azure OpenAI diagnostic question generation failed; falling back to rule-based engine");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI diagnostic question generation returned unparseable response");
        }

        return await _fallback.SuggestDiagnosticQuestionsAsync(issueCategory, issueDescription, manufacturer, model, year, cancellationToken);
    }

    private static string BuildDiagnosticUserMessage(
        string issueCategory,
        string? issueDescription,
        string? manufacturer,
        string? model,
        int? year)
    {
        var parts = new List<string> { $"Issue Category: {issueCategory}" };

        if (!string.IsNullOrWhiteSpace(issueDescription))
            parts.Add($"Issue Description: {issueDescription}");

        if (!string.IsNullOrWhiteSpace(manufacturer) || !string.IsNullOrWhiteSpace(model) || year.HasValue)
        {
            var assetParts = new List<string>();
            if (year.HasValue) assetParts.Add($"{year}");
            if (!string.IsNullOrWhiteSpace(manufacturer)) assetParts.Add(manufacturer);
            if (!string.IsNullOrWhiteSpace(model)) assetParts.Add(model);
            parts.Add($"Vehicle: {string.Join(" ", assetParts)}");
        }

        return string.Join("\n", parts);
    }

    private static JsonObject BuildChatRequestBody(string systemPrompt, string userMessage)
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
            ["max_tokens"] = 800,
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

    private sealed class DiagnosticQuestionsPayload
    {
        [JsonPropertyName("questions")]
        public List<DiagnosticQuestionPayload>? Questions { get; init; }

        [JsonPropertyName("smart_suggestion")]
        public string? SmartSuggestion { get; init; }
    }

    private sealed class DiagnosticQuestionPayload
    {
        [JsonPropertyName("question_text")]
        public string? QuestionText { get; init; }

        [JsonPropertyName("options")]
        public List<string>? Options { get; init; }

        [JsonPropertyName("allow_free_text")]
        public bool AllowFreeText { get; init; } = true;

        [JsonPropertyName("help_text")]
        public string? HelpText { get; init; }
    }
}
