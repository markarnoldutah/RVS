using System.Text.Json;
using System.Text.Json.Nodes;
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
    private const string ApiVersion = "2024-10-21";

    private const string SystemPrompt =
        "You are a VIN extraction assistant. Extract the 17-character Vehicle Identification Number (VIN) from the image. " +
        "VINs contain only alphanumeric characters and never include the letters I, O, or Q. " +
        "Return ONLY a JSON object: {\"vin\": \"<the VIN>\", \"confidence\": <0.0-1.0>}. " +
        "If no VIN is visible, return {\"vin\": null, \"confidence\": 0.0}.";

    private const string UserPrompt = "Extract the VIN from this image.";

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
            var requestBody = BuildRequestBody(dataUrl);

            _logger.LogDebug("Sending VIN extraction request ({ImageBytes} bytes, {ContentType})", imageData.Length, contentType);

            var response = await _httpClient.PostAsJsonAsync(
                $"chat/completions?api-version={ApiVersion}",
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Azure OpenAI VIN extraction returned HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode,
                    errorBody);
                return null;
            }

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
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure OpenAI VIN extraction network error (Status: {StatusCode})", ex.StatusCode);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI VIN extraction timed out or was cancelled");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI VIN extraction returned unparseable response");
            return null;
        }
    }

    /// <summary>
    /// Builds the Azure OpenAI chat completion request JSON with a system message and
    /// a multimodal user message containing text and an image with high-detail processing.
    /// </summary>
    private static JsonObject BuildRequestBody(string dataUrl)
    {
        return new JsonObject
        {
            ["messages"] = new JsonArray(
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = SystemPrompt
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray(
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = UserPrompt
                        },
                        new JsonObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JsonObject
                            {
                                ["url"] = dataUrl,
                                ["detail"] = "high"
                            }
                        }
                    )
                }
            ),
            ["max_tokens"] = 200,
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

    private sealed class ExtractedVinPayload
    {
        [JsonPropertyName("vin")]
        public string? Vin { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }
    }
}
