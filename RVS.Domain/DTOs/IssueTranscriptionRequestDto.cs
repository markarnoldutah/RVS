namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for the speech-to-text transcription endpoint.
/// Sent by the client as a JSON body to <c>POST api/intake/{locationSlug}/ai/transcribe-issue</c>.
/// </summary>
public sealed record IssueTranscriptionRequestDto
{
    /// <summary>Base64-encoded audio payload (without the data URL prefix).</summary>
    public required string AudioBase64 { get; init; }

    /// <summary>
    /// MIME content type of the audio.
    /// Must start with <c>audio/</c>; allowed values are
    /// <c>audio/webm</c>, <c>audio/wav</c>, and <c>audio/mp4</c>.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// BCP-47 locale hint for speech recognition (e.g. <c>"en-US"</c>).
    /// Defaults to <c>"en-US"</c> when omitted or <c>null</c>.
    /// </summary>
    public string? Locale { get; init; }
}
