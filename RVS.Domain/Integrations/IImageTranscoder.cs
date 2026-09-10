namespace RVS.Domain.Integrations;

/// <summary>
/// Transcodes Apple HEIC/HEIF image bytes to baseline JPEG so every downstream packet
/// consumer gets a universally-renderable raster: the HTML packet's <c>&lt;img&gt;</c>, the
/// PDF renderer's SkiaSharp decoder (which cannot read HEIC), and the emailed photo
/// attachment (which many desktop mail clients cannot preview). Finishes <c>#492</c> item 8
/// — see issue <c>#508</c>.
///
/// Non-HEIC images are left untouched: <see cref="CanTranscode"/> returns <c>false</c> for
/// them and the caller stores the original bytes.
/// </summary>
public interface IImageTranscoder
{
    /// <summary>
    /// <c>true</c> when <paramref name="contentType"/> is a format this transcoder rewrites
    /// to JPEG (<c>image/heic</c>, <c>image/heif</c>; case-insensitive). Every other value —
    /// including <c>null</c>, already-web-safe rasters, and non-images — returns <c>false</c>
    /// so the caller keeps the original.
    /// </summary>
    bool CanTranscode(string? contentType);

    /// <summary>
    /// Decodes <paramref name="source"/> and re-encodes it as a baseline JPEG with EXIF
    /// orientation baked in and metadata stripped. Images larger than the configured
    /// maximum edge are downscaled proportionally.
    /// </summary>
    /// <param name="source">The original image bytes (expected to be HEIC/HEIF).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The JPEG bytes and their pixel dimensions on success; <c>null</c> when the bytes
    /// could not be decoded, are empty, or exceed the configured size cap. On <c>null</c>
    /// the caller keeps the original and logs — the packet then shows the labelled
    /// placeholder, exactly as before this fix.
    /// </returns>
    ImageTranscodeResult? TranscodeToJpeg(byte[] source, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a successful <see cref="IImageTranscoder.TranscodeToJpeg"/> call.</summary>
/// <param name="JpegBytes">The re-encoded JPEG payload.</param>
/// <param name="Width">Pixel width of the encoded image.</param>
/// <param name="Height">Pixel height of the encoded image.</param>
public sealed record ImageTranscodeResult(byte[] JpegBytes, int Width, int Height);
