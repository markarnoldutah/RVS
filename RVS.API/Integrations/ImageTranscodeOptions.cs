namespace RVS.API.Integrations;

/// <summary>
/// Tuning for <see cref="MagickImageTranscoder"/> (issue <c>#508</c>). Bound from the
/// <c>ImageTranscode</c> section of <c>appsettings.json</c>; every value has a working
/// default, so a deployment that sets nothing still transcodes HEIC/HEIF uploads.
/// </summary>
public sealed class ImageTranscodeOptions
{
    /// <summary>
    /// Largest source payload the transcoder will attempt to decode, in bytes. Anything
    /// bigger is rejected before decode (the original is kept) so a decompression bomb
    /// cannot exhaust memory. Defaults to 25 MB — comfortably above a full-resolution
    /// iPhone HEIC (~1–3 MB).
    /// </summary>
    public long MaxSourceBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>
    /// Longest edge, in pixels, of the encoded JPEG. Larger images are downscaled
    /// proportionally — this also keeps the packet PDF small. Defaults to 4096, which
    /// preserves native iPhone resolution (4032 × 3024).
    /// </summary>
    public int MaxEdgePixels { get; set; } = 4096;

    /// <summary>JPEG quality (1–100) for the re-encode. Defaults to 82 — visually lossless for photos at packet sizes.</summary>
    public int JpegQuality { get; set; } = 82;
}
