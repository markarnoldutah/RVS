using ImageMagick;
using Microsoft.Extensions.Options;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// <see cref="IImageTranscoder"/> backed by Magick.NET (ImageMagick, Apache-2.0) with its
/// bundled HEIF delegate (libheif, LGPL-3.0, dynamically linked in the native binary). The
/// <c>Magick.NET-Q8-AnyCPU</c> package ships native builds for win/linux/osx x64 + arm64,
/// so the same assembly runs on the Linux App Service and on a developer machine with no
/// extra install (issue <c>#508</c>).
///
/// Decoding is pure CPU work and synchronous — mirroring <c>PacketPdfRenderer</c>. A
/// full-resolution 12 MP phone photo normalises in roughly 0.7–0.9 s on a developer machine
/// (measured for <c>#562</c>), which is acceptable on the low-volume attachment-confirm path —
/// each upload is confirmed by its own request.
///
/// Issue <c>#562</c> widened this from HEIC/HEIF only to every raster the intake accepts,
/// and lowered <see cref="ImageTranscodeOptions.MaxEdgePixels"/> so the downscale actually
/// fires for the common full-resolution JPEG.
/// </summary>
public sealed class MagickImageTranscoder : IImageTranscoder
{
    private const string JpegContentType = "image/jpeg";
    private const string PngContentType = "image/png";

    private static readonly HashSet<string> HeicContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/heic", "image/heif" };

    private static readonly HashSet<string> NormalisableContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/heic", "image/heif", JpegContentType, PngContentType, "image/webp",
        };

    private readonly ImageTranscodeOptions _options;
    private readonly ILogger<MagickImageTranscoder> _logger;

    /// <summary>Creates the normaliser with its tuning options and logger.</summary>
    public MagickImageTranscoder(IOptions<ImageTranscodeOptions> options, ILogger<MagickImageTranscoder> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanNormalize(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && NormalisableContentTypes.Contains(contentType.Trim());

    /// <inheritdoc />
    public ImageTranscodeResult? Normalize(byte[] source, string? contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Length == 0)
        {
            return null;
        }

        if (source.Length > _options.MaxSourceBytes)
        {
            _logger.LogWarning(
                "Image normalise skipped: source is {SourceBytes} bytes, over the {MaxSourceBytes}-byte cap",
                source.Length, _options.MaxSourceBytes);
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var declared = contentType?.Trim();
        var isHeic = declared is not null && HeicContentTypes.Contains(declared);
        var keepAsPng = string.Equals(declared, PngContentType, StringComparison.OrdinalIgnoreCase);

        try
        {
            using var image = new MagickImage(source);

            // Bake EXIF orientation into the pixels — an image with no orientation tag renders
            // upright everywhere.
            image.AutoOrient();

            var maxEdge = _options.MaxEdgePixels;
            if (maxEdge > 0 && (image.Width > maxEdge || image.Height > maxEdge))
            {
                image.Resize(new MagickGeometry((uint)maxEdge, (uint)maxEdge) { Greater = true });
            }

            string outputContentType;
            if (keepAsPng)
            {
                // A PNG is usually a screenshot, a diagram, or line art — JPEG's block
                // artifacts show badly on hard edges, so it stays PNG and takes the smaller
                // saving from the downscale alone.
                image.Format = MagickFormat.Png;
                outputContentType = PngContentType;
            }
            else
            {
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)Math.Clamp(_options.JpegQuality, 1, 100);
                outputContentType = JpegContentType;
            }

            image.Strip();

            var bytes = image.ToByteArray();

            // Never hand back a bigger file than we were given for a raster that already renders
            // everywhere: a small, already-optimised JPEG/PNG/WebP must not grow just because it
            // passed through here. HEIC/HEIF is exempt — its original does not render off Apple
            // devices, so the larger JPEG is still the right answer (issue #508).
            if (!isHeic && bytes.Length >= source.Length)
            {
                _logger.LogInformation(
                    "Image normalise made no saving ({NewBytes} bytes vs {SourceBytes} original); keeping the original",
                    bytes.Length, source.Length);
                return null;
            }

            return new ImageTranscodeResult(bytes, outputContentType, (int)image.Width, (int)image.Height);
        }
        catch (MagickException ex)
        {
            _logger.LogWarning(
                ex,
                "Image normalise failed to decode/encode a {SourceBytes}-byte payload; caller keeps the original",
                source.Length);
            return null;
        }
    }
}
