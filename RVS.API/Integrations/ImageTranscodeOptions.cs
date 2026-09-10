namespace RVS.API.Integrations;

/// <summary>
/// Tuning for <see cref="MagickImageTranscoder"/> (issues <c>#508</c>, <c>#562</c>). Bound from
/// the <c>ImageTranscode</c> section of <c>appsettings.json</c>; every value has a working
/// default, so a deployment that sets nothing still normalises every image upload.
/// </summary>
public sealed class ImageTranscodeOptions
{
    /// <summary>
    /// Largest source payload the normaliser will attempt to decode, in bytes. Anything
    /// bigger is rejected before decode (the original is kept) so a decompression bomb
    /// cannot exhaust memory. Defaults to 25 MB — the <c>Spec A-6</c> per-file ceiling.
    /// </summary>
    public long MaxSourceBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>
    /// Longest edge, in pixels, of the re-encoded image. Larger images are downscaled
    /// proportionally. Defaults to <b>1600</b> (issue <c>#562</c>): a packet photo is viewed on
    /// a phone or printed inside a 210 × 279 mm greyscale page, and 1600 px is more than
    /// either can show. It cuts a native 12 MP phone photo's pixel count about sixfold, which
    /// is what the emailed attachment set carries. Measured on real photos against the default
    /// 9.5 MB ACS budget: 2048 px attached six photos with ~0.5 MB to spare and five of ten;
    /// 1600 px attaches six at ~6.4 MB and eight of ten. Raise it per environment if a location
    /// needs more zoom-in detail (a serial plate, a wiring run) at the cost of attachments.
    /// </summary>
    public int MaxEdgePixels { get; set; } = 1600;

    /// <summary>JPEG quality (1–100) for the re-encode. Defaults to 82 — visually lossless for photos at packet sizes.</summary>
    public int JpegQuality { get; set; } = 82;
}
