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
        "You are an expert RV service intake specialist with deep knowledge of the recreational vehicle " +
        "industry, including all major brands (Thor Motor Coach, Winnebago, Forest River, Airstream, Keystone, " +
        "Grand Design, Coachmen, Fleetwood, Tiffin, Newmar), all RV types (Class A/B/C motorhomes, fifth wheels, " +
        "travel trailers, toy haulers, and park models), and RV-specific terminology such as slide-outs, awnings, " +
        "inverters, converters, LP/propane systems, black/grey/fresh water tanks, shore power, PDC, and more. " +
        "You excel at cleaning up speech-to-text transcripts of RV owner issue descriptions. " +
        "Remove filler words (um, uh, so, like, well, okay), fix capitalization and punctuation, " +
        "and produce a clear, concise customer issue description while preserving all RV-specific terms and details. " +
        "Return ONLY a JSON object: {\"cleaned_description\": \"<cleaned text>\", \"confidence\": <0.0-1.0>}.";

    private static readonly string[] ValidCategories =
        ["Electrical", "Plumbing", "HVAC", "Appliance", "Structural", "Slide-Out", "Awning"];

    private static readonly string[] ValidUrgencies =
        ["Low", "Medium", "High", "Critical"];

    private static readonly string[] ValidRvUsages =
        ["Full-Time", "Part-Time", "Seasonal", "Occasional"];

    private static readonly string SuggestSystemPrompt =
        "You are a senior RV service advisor with comprehensive knowledge of the recreational vehicle industry " +
        "and all major brands (Thor Motor Coach, Winnebago, Forest River, Airstream, Keystone, Grand Design, " +
        "Coachmen, Fleetwood, Tiffin, Newmar). You specialize in diagnosing and categorizing RV issues across " +
        "all vehicle types and systems, including chassis, slide-outs, awnings, LP/propane, water systems, " +
        "electrical, HVAC, generators, and appliances. Given an RV owner's issue description, suggest the most " +
        "appropriate service category " +
        $"from this list: {string.Join(", ", ValidCategories)}. " +
        "If no category fits well, use null for the category. " +
        "Return ONLY a JSON object: {\"category\": \"<category or null>\", \"confidence\": <0.0-1.0>}.";

    private static readonly string InsightsSystemPrompt =
        "You are an RV service intake assistant. Analyze the customer's issue description to infer two signals:\n" +
        "1. urgency — one of [Low, Medium, High, Critical] or null if not determinable:\n" +
        "   - Critical: safety risk, not drivable, gas/propane leak, fire risk, brake failure\n" +
        "   - High: major system failure (no heat in winter, no running water), unit unusable\n" +
        "   - Medium: partial failure with workaround available, planned trip approaching\n" +
        "   - Low: minor cosmetic, convenience, or non-urgent maintenance issue\n" +
        "2. rv_usage — one of [Full-Time, Part-Time, Seasonal, Occasional] or null if not determinable:\n" +
        "   - Full-Time: customer lives in the RV permanently\n" +
        "   - Part-Time: uses regularly on weekends or several times per month\n" +
        "   - Seasonal: uses primarily in summer, winter, or one specific season\n" +
        "   - Occasional: uses a few times per year or infrequently\n" +
        "Return ONLY a JSON object: {\"urgency\": \"<value or null>\", \"rv_usage\": \"<value or null>\", \"confidence\": <0.0-1.0>}.";

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

    /// <inheritdoc />
    public async Task<IssueInsightsSuggestionResult?> SuggestInsightsAsync(string issueDescription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueDescription);

        try
        {
            var requestBody = BuildTextRequestBody(InsightsSystemPrompt, issueDescription);
            _logger.LogDebug("Sending insights suggestion request ({Length} chars)", issueDescription.Length);

            var response = await _httpClient.PostAsJsonAsync(
                $"chat/completions?api-version={ApiVersion}",
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Azure OpenAI insights suggestion returned HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode,
                    errorBody);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Azure OpenAI insights suggestion returned empty content");
                return null;
            }

            var payload = JsonSerializer.Deserialize<InsightsPayload>(content, JsonOptions);
            if (payload is null)
            {
                _logger.LogWarning("Azure OpenAI insights suggestion returned null payload");
                return null;
            }

            var urgency = payload.Urgency is not null
                ? ValidUrgencies.FirstOrDefault(u => u.Equals(payload.Urgency, StringComparison.OrdinalIgnoreCase))
                : null;

            var rvUsage = payload.RvUsage is not null
                ? ValidRvUsages.FirstOrDefault(u => u.Equals(payload.RvUsage, StringComparison.OrdinalIgnoreCase))
                : null;

            if (payload.Urgency is not null && urgency is null)
                _logger.LogWarning("Azure OpenAI suggested an unknown urgency: {Urgency}", payload.Urgency);

            if (payload.RvUsage is not null && rvUsage is null)
                _logger.LogWarning("Azure OpenAI suggested an unknown RV usage: {RvUsage}", payload.RvUsage);

            return new IssueInsightsSuggestionResult(urgency, rvUsage, payload.Confidence, ProviderName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure OpenAI insights suggestion network error (Status: {StatusCode})", ex.StatusCode);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI insights suggestion timed out or was cancelled");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI insights suggestion returned unparseable response");
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

    private sealed class InsightsPayload
    {
        [JsonPropertyName("urgency")]
        public string? Urgency { get; init; }

        [JsonPropertyName("rv_usage")]
        public string? RvUsage { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }
    }
}
