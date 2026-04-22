using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Azure OpenAI Whisper speech-to-text service using the REST API.
/// Sends raw audio as multipart/form-data and returns the transcribed text.
/// Returns <c>null</c> on any network error, timeout, empty response, or unparseable result — never throws.
/// </summary>
public sealed class AzureWhisperSpeechToTextService : ISpeechToTextService
{
    private const string ProviderName = nameof(AzureWhisperSpeechToTextService);
    private const string TranscriptionPath = "audio/transcriptions?api-version=2024-02-01";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureWhisperSpeechToTextService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AzureWhisperSpeechToTextService"/>.
    /// </summary>
    /// <param name="httpClient">
    /// Pre-configured <see cref="HttpClient"/> whose <see cref="HttpClient.BaseAddress"/> points to
    /// <c>{endpoint}/openai/deployments/{whisperDeployment}/</c> and whose default headers include <c>api-key</c>.
    /// </param>
    /// <param name="logger">Logger instance.</param>
    public AzureWhisperSpeechToTextService(HttpClient httpClient, ILogger<AzureWhisperSpeechToTextService> logger)
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
            var safeContentType = Sanitize(contentType);
            var safeLocale = Sanitize(locale);

            _logger.LogDebug(
                "Sending audio to Azure OpenAI Whisper ({Bytes} bytes, {ContentType}, {Locale})",
                audioData.Length, safeContentType, safeLocale);

            using var form = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(audioData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            form.Add(fileContent, "file", ResolveFileName(contentType));

            form.Add(new StringContent(MapLocaleToLanguage(locale)), "language");
            form.Add(new StringContent("json"), "response_format");

            var response = await _httpClient.PostAsync(TranscriptionPath, form, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Whisper transcription returned HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode, errorBody);
                return null;
            }

            var whisperResponse = await response.Content
                .ReadFromJsonAsync<WhisperTranscriptionResponse>(JsonOptions, cancellationToken);

            if (whisperResponse is null)
            {
                _logger.LogWarning("Whisper transcription returned null response body");
                return null;
            }

            var transcript = whisperResponse.Text?.Trim();

            if (string.IsNullOrWhiteSpace(transcript))
            {
                _logger.LogWarning("Whisper transcription returned empty or whitespace-only text");
                return null;
            }

            return new SpeechToTextResult(transcript, CleanedDescription: null, Confidence: 0.0, ProviderName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Whisper transcription network error (Status: {StatusCode})", ex.StatusCode);
            return null;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Whisper transcription timed out or was cancelled");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Whisper transcription returned unparseable response");
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Maps a MIME content type to a filename hint for the multipart upload.
    /// Whisper uses the file extension to infer the audio format.
    /// </summary>
    private static string ResolveFileName(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "audio/wav" or "audio/x-wav" => "audio.wav",
            "audio/webm" => "audio.webm",
            "audio/mp4" or "audio/m4a" => "audio.m4a",
            "audio/mpeg" or "audio/mp3" => "audio.mp3",
            "audio/ogg" => "audio.ogg",
            "audio/flac" => "audio.flac",
            _ => "audio.wav" // safe default — Whisper is format-flexible
        };

    /// <summary>
    /// Extracts the ISO 639-1 language code from a BCP-47 locale tag.
    /// For example, <c>"en-US"</c> → <c>"en"</c>, <c>"es-MX"</c> → <c>"es"</c>.
    /// </summary>
    private static string MapLocaleToLanguage(string locale)
    {
        var hyphenIndex = locale.IndexOf('-', StringComparison.Ordinal);
        return hyphenIndex > 0 ? locale[..hyphenIndex].ToLowerInvariant() : locale.ToLowerInvariant();
    }

    /// <summary>
    /// Strips newline characters from user-supplied strings to prevent log-injection attacks.
    /// </summary>
    private static string Sanitize(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
             .Replace("\n", string.Empty, StringComparison.Ordinal);

    // ── Private response types ────────────────────────────────────────────

    private sealed class WhisperTranscriptionResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
