namespace RVS.API.Integrations;

/// <summary>
/// Configuration options for AI integration payload limits and allowed media types.
/// Bound from the <c>Ai</c> section of <c>appsettings.json</c>.
/// </summary>
/// <example>
/// <code>
/// // appsettings.json
/// "Ai": {
///   "MaxImageBytes": 5242880,
///   "MaxAudioBytes": 10485760,
///   "AllowedImageTypes": ["image/jpeg", "image/png", "image/webp"],
///   "AllowedAudioTypes": ["audio/webm", "audio/wav", "audio/mp4"]
/// }
/// </code>
/// </example>
public sealed class AiOptions
{
    /// <summary>
    /// Maximum allowed image payload size in bytes.
    /// Requests exceeding this limit are rejected with HTTP 413.
    /// Defaults to 5 MB (5 242 880 bytes).
    /// </summary>
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum allowed audio payload size in bytes.
    /// Requests exceeding this limit are rejected with HTTP 413.
    /// Defaults to 10 MB (10 485 760 bytes).
    /// </summary>
    public long MaxAudioBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// MIME types accepted for image AI operations (e.g. VIN extraction from photo).
    /// Content types not in this list are rejected with HTTP 400.
    /// </summary>
    public string[] AllowedImageTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    /// <summary>
    /// MIME types accepted for audio AI operations (e.g. speech-to-text transcription).
    /// Content types not in this list are rejected with HTTP 400.
    /// </summary>
    public string[] AllowedAudioTypes { get; set; } =
    [
        "audio/webm",
        "audio/wav",
        "audio/mp4"
    ];
}
