namespace RVS.Domain.Integrations;

/// <summary>
/// Transcribes audio recordings of issue descriptions into text using a speech-to-text engine.
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// Transcribes the provided audio and optionally produces a cleaned description.
    /// </summary>
    /// <param name="audioData">Raw audio bytes.</param>
    /// <param name="contentType">MIME content type of the audio (e.g. <c>"audio/webm"</c>).</param>
    /// <param name="locale">BCP-47 locale hint for speech recognition (e.g. <c>"en-US"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Transcription result with raw transcript and confidence score, or <c>null</c> if
    /// transcription failed or no speech was detected.
    /// Never throws — callers should fall back to manual entry on <c>null</c>.
    /// </returns>
    Task<SpeechToTextResult?> TranscribeAudioAsync(byte[] audioData, string contentType, string locale, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a speech-to-text transcription operation.
/// </summary>
/// <param name="RawTranscript">Direct speech-to-text output from the recognition engine.</param>
/// <param name="CleanedDescription">AI-cleaned text suitable for the issue description, or <c>null</c> when not available.</param>
/// <param name="Confidence">Confidence score in the range 0.0–1.0.</param>
/// <param name="Provider">Identifier for the service implementation that fulfilled the request.</param>
public sealed record SpeechToTextResult(string RawTranscript, string? CleanedDescription, double Confidence, string Provider);
