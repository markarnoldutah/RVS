using System.Text.Json;
using System.Text.Json.Serialization;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Azure Cognitive Services Speech-to-Text service using the Speech REST API.
/// Sends raw audio bytes and returns a transcript with confidence score.
/// Returns <c>null</c> on any network error, timeout, no-speech result, or unparseable response — never throws.
/// </summary>
public sealed class AzureSpeechToTextService : ISpeechToTextService
{
    private const string ProviderName = nameof(AzureSpeechToTextService);

    // Format=detailed returns NBest array with per-result confidence scores.
    private const string QueryTemplate = "?language={0}&format=detailed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureSpeechToTextService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AzureSpeechToTextService"/>.
    /// </summary>
    /// <param name="httpClient">
    /// Pre-configured <see cref="HttpClient"/> whose <see cref="HttpClient.BaseAddress"/> points to the
    /// region-specific Speech endpoint and whose default headers include <c>Ocp-Apim-Subscription-Key</c>.
    /// </param>
    /// <param name="logger">Logger instance.</param>
    public AzureSpeechToTextService(HttpClient httpClient, ILogger<AzureSpeechToTextService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SpeechToTextResult?> TranscribeAudioAsync(
        byte[] audioData,
        string contentType,
        string locale,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioData);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        try
        {
            var relativeUrl = string.Format(QueryTemplate, Uri.EscapeDataString(locale));

            // Sanitize user-supplied strings before logging to prevent log-injection attacks.
            var safeContentType = Sanitize(contentType);
            var safeLocale = Sanitize(locale);

            _logger.LogDebug(
                "Sending audio to Azure Speech ({Bytes} bytes, {ContentType}, {Locale})",
                audioData.Length, safeContentType, safeLocale);

            using var content = new ByteArrayContent(audioData);
            content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);

            var response = await _httpClient.PostAsync(relativeUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Azure Speech transcription returned HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode, errorBody);
                return null;
            }

            var speechResponse = await response.Content
                .ReadFromJsonAsync<SpeechRecognitionResponse>(JsonOptions, cancellationToken);

            if (speechResponse is null)
            {
                _logger.LogWarning("Azure Speech transcription returned null response body");
                return null;
            }

            if (speechResponse.RecognitionStatus != "Success")
            {
                _logger.LogInformation(
                    "Azure Speech transcription status was {Status} — no speech detected or audio unrecognisable",
                    speechResponse.RecognitionStatus);
                return null;
            }

            var displayText = speechResponse.DisplayText;
            if (string.IsNullOrWhiteSpace(displayText))
            {
                _logger.LogWarning("Azure Speech transcription returned empty DisplayText");
                return null;
            }

            // Pick the confidence from the top NBest candidate, defaulting to 0.0 when absent.
            var confidence = speechResponse.NBest?.FirstOrDefault()?.Confidence ?? 0.0;

            return new SpeechToTextResult(displayText, CleanedDescription: null, confidence, ProviderName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure Speech transcription network error (Status: {StatusCode})", ex.StatusCode);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Azure Speech transcription timed out or was cancelled");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Azure Speech transcription returned unparseable response");
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Strips newline characters from user-supplied strings to prevent log-injection attacks.
    /// </summary>
    private static string Sanitize(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
             .Replace("\n", string.Empty, StringComparison.Ordinal);

    // ── Private response types ────────────────────────────────────────────

    private sealed class SpeechRecognitionResponse
    {
        [JsonPropertyName("RecognitionStatus")]
        public string? RecognitionStatus { get; init; }

        [JsonPropertyName("DisplayText")]
        public string? DisplayText { get; init; }

        [JsonPropertyName("NBest")]
        public IReadOnlyList<NBestEntry>? NBest { get; init; }
    }

    private sealed class NBestEntry
    {
        [JsonPropertyName("Confidence")]
        public double Confidence { get; init; }
    }
}
