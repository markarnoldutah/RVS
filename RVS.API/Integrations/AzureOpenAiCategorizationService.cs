using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Azure OpenAI–powered categorization service.
/// Uses the chat completions API for contextual diagnostic question generation.
/// Falls back to <see cref="RuleBasedCategorizationService"/> on timeout or error.
///
/// Question generation has its own client (issue #783): pointed at a reasoning-model deployment
/// (gpt-5) it sends the reasoning request shape, otherwise it is the text client and the gpt-4o
/// request is unchanged. Category suggestion always stays on the text client.
/// </summary>
public sealed class AzureOpenAiCategorizationService : ICategorizationService
{
    private const string ProviderName = nameof(AzureOpenAiCategorizationService);
    private const string ApiVersion = "2024-10-21";

    // Reasoning-model shape for question generation (gpt-5, #783), as in
    // AzureOpenAiPreliminaryAssessmentService. Reasoning tokens count against
    // max_completion_tokens and Azure reserves it against TPM up front, so it is sized for
    // "low" effort plus the ~400-token reply, and no higher.
    private const string ReasoningApiVersion = "2025-04-01-preview";
    private const int MaxTokens = 800;
    private const int MaxCompletionTokens = 3000;
    private static readonly HashSet<string> AllowedReasoningEfforts = new(StringComparer.OrdinalIgnoreCase) { "minimal", "low" };

    private const string DiagnosticSystemPrompt =
        "You are a senior RV service advisor with 15+ years of hands-on experience in the recreational vehicle " +
        "industry. You have deep expertise in all major RV brands (Thor Motor Coach, Winnebago, Forest River, " +
        "Airstream, Keystone, Grand Design, Coachmen, Fleetwood, Tiffin, Newmar, and others), all RV types " +
        "(Class A/B/C motorhomes, fifth wheels, travel trailers, toy haulers, and park models), and the full " +
        "range of RV systems: chassis and drivetrain, slide-outs, awnings, LP/propane, fresh/grey/black water, " +
        "electrical (12V DC, 120V AC, inverters, converters, solar), HVAC (rooftop ACs, heat pumps, furnaces), " +
        "generators, and all interior appliances. You understand both dealer service operations and RV owner " +
        "needs and usage patterns. Generate 2–4 diagnostic follow-up questions for a customer who is submitting " +
        "an RV service request. Each question should help a technician understand the issue before the RV arrives.\n\n" +
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
        "- Questions should be specific to the issue category, vehicle details, and customer's description.\n" +
        "- Draw on RV industry knowledge to ask questions a skilled technician would want answered.\n" +
        "- Include a smart suggestion only when you have a genuinely helpful RV-specific tip " +
        "(e.g. 'Check your coach battery disconnect switch before your visit').\n" +
        "- Keep questions concise and customer-friendly — avoid internal service jargon.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly HttpClient _questionsHttpClient;
    private readonly RuleBasedCategorizationService _fallback;
    private readonly bool _useReasoningModelRequest;
    private readonly string _reasoningEffort;
    private readonly ILogger<AzureOpenAiCategorizationService> _logger;

    /// <summary>
    /// Creates the service over the gpt-4o text client and the question-generation client, which
    /// is the same client when no questions deployment is configured.
    /// </summary>
    public AzureOpenAiCategorizationService(
        HttpClient httpClient,
        HttpClient questionsHttpClient,
        RuleBasedCategorizationService fallback,
        IOptions<AzureOpenAiQuestionsOptions> options,
        ILogger<AzureOpenAiCategorizationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _questionsHttpClient = questionsHttpClient;
        _fallback = fallback;
        _useReasoningModelRequest = options.Value.UseReasoningModelRequest;
        _reasoningEffort = options.Value.ReasoningEffort is { } effort && AllowedReasoningEfforts.Contains(effort)
            ? effort.ToLowerInvariant()
            : AzureOpenAiQuestionsOptions.DefaultReasoningEffort;
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

            var result = (await response.Content.ReadAsStringAsync(cancellationToken))?.Trim();

            // A-5: only a value inside the controlled vocabulary is trusted. Anything else
            // (free text, an invented label, empty) drops through to the keyword fallback.
            if (IssueCategoryVocabulary.IsValid(result))
            {
                return IssueCategoryVocabulary.Normalize(result);
            }

            if (!string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning(
                    "Azure OpenAI categorization returned out-of-vocabulary value '{Value}'; falling back to rule-based engine",
                    result);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
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
            var apiVersion = _useReasoningModelRequest ? ReasoningApiVersion : ApiVersion;

            _logger.LogDebug("Sending diagnostic question generation request for category {Category}",
                new string(issueCategory.Where(c => !char.IsControl(c)).ToArray()));

            var response = await _questionsHttpClient.PostAsJsonAsync(
                $"chat/completions?api-version={apiVersion}",
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

    // gpt-5 rejects "max_tokens" with a 400 and gpt-4o rejects "reasoning_effort"; either mismatch
    // would answer every intake from the question bank.
    private JsonObject BuildChatRequestBody(string systemPrompt, string userMessage)
    {
        var body = new JsonObject
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
            ["response_format"] = new JsonObject { ["type"] = "json_object" }
        };

        if (_useReasoningModelRequest)
        {
            body["max_completion_tokens"] = MaxCompletionTokens;
            body["reasoning_effort"] = _reasoningEffort;
        }
        else
        {
            body["max_tokens"] = MaxTokens;
        }

        return body;
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
