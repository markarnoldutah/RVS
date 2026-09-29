using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Azure OpenAI GPT-4o Vision–powered VIN extraction service. Falls back to the vehicle serial
/// number when the plate carries no VIN, as on a truck camper (issue #807).
/// Sends the image to the chat completions endpoint and parses the structured JSON response.
/// Returns <c>null</c> on any network error, timeout, or unparseable response — never throws.
/// </summary>
public sealed class AzureOpenAiVinExtractionService : IVinExtractionService
{
    private const string ProviderName = nameof(AzureOpenAiVinExtractionService);
    private const string ApiVersion = "2024-10-21";

    private const string SystemPrompt =
        "You are an RV industry specialist with expertise in vehicle identification for all types of recreational " +
        "vehicles, including motorhomes (Class A, B, and C), fifth wheels, travel trailers, toy haulers, and park " +
        "models from all major manufacturers (Thor Motor Coach, Winnebago, Forest River, Airstream, Keystone, " +
        "Grand Design, Coachmen, Fleetwood, Tiffin, Newmar, and others). You are skilled at reading VIN plates, " +
        "stickers, and identification documents found on RVs and their chassis. " +
        "Extract the 17-character Vehicle Identification Number (VIN) from the image. " +
        "VINs contain only alphanumeric characters and never include the letters I, O, or Q. " +
        "Some RVs have no VIN: a truck camper, for example, carries a manufacturer's plate with a shorter " +
        "\"Vehicle Serial No.\" or \"Serial Number\". If there is no VIN but there is a vehicle serial number, " +
        "extract the serial number instead, exactly as printed. Never return a model number, approval number, " +
        "date, weight or phone number as the VIN. " +
        "If the plate also shows the vehicle's manufacturer, model or year, report them too, exactly as printed: " +
        "the manufacturer's brand name (e.g. \"Lance\", not its street address), the model designation, and the " +
        "model year as four digits — or, when only a date of manufacture is shown (e.g. \"03/03\"), that year " +
        "(2003). Use null for anything not printed on the plate; never guess from the RV's appearance. " +
        "Return ONLY a JSON object: {\"vin\": \"<the VIN or serial number>\", \"manufacturer\": <string or null>, " +
        "\"model\": <string or null>, \"year\": <number or null>, \"confidence\": <0.0-1.0>}. " +
        "The confidence is for the VIN or serial number. " +
        "If neither is visible, return {\"vin\": null, \"manufacturer\": null, \"model\": null, \"year\": null, \"confidence\": 0.0}.";

    private const string UserPrompt = "Extract the VIN, or the vehicle serial number if there is no VIN, from this image.";

    // Step 4 prefill (issue #807): plate text the customer can override, so anything doubtful is dropped.
    private const int MaxDetailLength = 60;
    private const int MinYear = 1900;
    private static readonly string[] PlaceholderValues = ["N/A", "NA", "NONE", "UNKNOWN", "-", "NULL"];

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

            var normalized = VehicleIdentifierValidator.Normalize(extracted.Vin);
            var formatResult = VehicleIdentifierValidator.Validate(normalized);
            if (!formatResult.IsValid)
            {
                _logger.LogWarning("Azure OpenAI returned an invalid VIN or serial number: {Vin}", normalized);
                return null;
            }

            return new VinExtractionResult(
                normalized,
                extracted.Confidence,
                ProviderName,
                CleanDetail(extracted.Manufacturer),
                CleanDetail(extracted.Model),
                ParseYear(extracted.Year));
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
    /// A manufacturer or model read off the plate, or <c>null</c> when it is absent, a placeholder
    /// such as "N/A", too long to be a name, or carries markup or control characters.
    /// </summary>
    private static string? CleanDetail(JsonElement? element)
    {
        var raw = element?.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number => element.Value.GetRawText(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (cleaned.Length > MaxDetailLength
            || cleaned.Any(c => char.IsControl(c) || c is '<' or '>')
            || PlaceholderValues.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return cleaned;
    }

    /// <summary>
    /// A four-digit year between 1900 and two years ahead, as a number or a numeric string;
    /// otherwise <c>null</c>. Mirrors the Step 4 year rule.
    /// </summary>
    private static int? ParseYear(JsonElement? element)
    {
        int year;
        switch (element?.ValueKind)
        {
            case JsonValueKind.Number when element.Value.TryGetInt32(out year):
                break;
            case JsonValueKind.String when int.TryParse(element.Value.GetString(), out year):
                break;
            default:
                return null;
        }

        return year >= MinYear && year <= DateTime.UtcNow.Year + 2 ? year : null;
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

        // Raw elements: a model such as 1121 or a year can come back as a number or a string, and
        // one oddly-typed detail must not cost the customer the VIN.
        [JsonPropertyName("manufacturer")]
        public JsonElement? Manufacturer { get; init; }

        [JsonPropertyName("model")]
        public JsonElement? Model { get; init; }

        [JsonPropertyName("year")]
        public JsonElement? Year { get; init; }
    }
}
