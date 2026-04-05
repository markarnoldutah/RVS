using System.Text.Json;
using System.Text.Json.Serialization;
using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Azure OpenAI GPT-4o Vision–powered VIN extraction service.
/// Sends the image to the chat completions endpoint and parses the structured JSON response.
/// Returns <c>null</c> on any network error, timeout, or unparseable response — never throws.
/// </summary>
public sealed class AzureOpenAiVinExtractionService : IVinExtractionService
{
    private const string ProviderName = nameof(AzureOpenAiVinExtractionService);

    private const string SystemPrompt =
        "You are a VIN extraction assistant. Extract the 17-character Vehicle Identification Number (VIN) from the image. " +
        "VINs contain only alphanumeric characters and never include the letters I, O, or Q. " +
        "Return ONLY a JSON object: {\"vin\": \"<the VIN>\", \"confidence\": <0.0-1.0>}. " +
        "If no VIN is visible, return {\"vin\": null, \"confidence\": 0.0}.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureOpenAiVinExtractionService> _logger;

    public AzureOpenAiVinExtractionService(
        HttpClient httpClient,
        ILogger<AzureOpenAiVinExtractionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VinExtractionResult?> ExtractVinFromImageAsync(byte[] imageData, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        try
        {
            var base64Image = Convert.ToBase64String(imageData);
            var dataUrl = $"data:{contentType};base64,{base64Image}";

            var request = new ChatCompletionRequest
            {
                Messages =
                [
                    new ChatMessage
                    {
                        Role = "user",
                        Content =
                        [
                            new ImageContentPart
                            {
                                Type = "image_url",
                                ImageUrl = new ImageUrl { Url = dataUrl }
                            },
                            new TextContentPart
                            {
                                Type = "text",
                                Text = SystemPrompt
                            }
                        ]
                    }
                ],
                MaxTokens = 200,
                ResponseFormat = new ResponseFormat { Type = "json_object" }
            };

            var response = await _httpClient.PostAsJsonAsync("chat/completions?api-version=2024-02-01", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Azure OpenAI VIN extraction returned empty content");
                return null;
            }

            var extracted = JsonSerializer.Deserialize<ExtractedVinPayload>(content, JsonOptions);
            if (extracted is null || string.IsNullOrWhiteSpace(extracted.Vin))
            {
                _logger.LogInformation("Azure OpenAI VIN extraction found no VIN in image");
                return null;
            }

            var normalized = extracted.Vin.Trim().ToUpperInvariant();
            var formatResult = VinValidator.ValidateFormat(normalized);
            if (!formatResult.IsValid)
            {
                _logger.LogWarning("Azure OpenAI returned an invalid VIN format: {Vin}", normalized);
                return null;
            }

            return new VinExtractionResult(normalized, extracted.Confidence, ProviderName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Azure OpenAI VIN extraction failed; returning null for manual fallback");
            return null;
        }
    }

    // ── Private request/response types ───────────────────────────────────

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("messages")]
        public required IReadOnlyList<ChatMessage> Messages { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }

        [JsonPropertyName("response_format")]
        public ResponseFormat? ResponseFormat { get; init; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required IReadOnlyList<object> Content { get; init; }
    }

    private sealed class ImageContentPart
    {
        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("image_url")]
        public required ImageUrl ImageUrl { get; init; }
    }

    private sealed class TextContentPart
    {
        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("text")]
        public required string Text { get; init; }
    }

    private sealed class ImageUrl
    {
        [JsonPropertyName("url")]
        public required string Url { get; init; }
    }

    private sealed class ResponseFormat
    {
        [JsonPropertyName("type")]
        public required string Type { get; init; }
    }

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

    private sealed class ExtractedVinPayload
    {
        [JsonPropertyName("vin")]
        public string? Vin { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }
    }
}
