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
/// full-resolution iPhone HEIC transcodes in well under a second, which is acceptable on the
/// low-volume attachment-confirm path.
/// </summary>
public sealed class MagickImageTranscoder : IImageTranscoder
{
    private static readonly HashSet<string> HeicContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/heic", "image/heif" };

    private readonly ImageTranscodeOptions _options;
    private readonly ILogger<MagickImageTranscoder> _logger;

    /// <summary>Creates the transcoder with its tuning options and logger.</summary>
    public MagickImageTranscoder(IOptions<ImageTranscodeOptions> options, ILogger<MagickImageTranscoder> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanTranscode(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && HeicContentTypes.Contains(contentType.Trim());

    /// <inheritdoc />
    public ImageTranscodeResult? TranscodeToJpeg(byte[] source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Length == 0)
        {
            return null;
        }

        if (source.Length > _options.MaxSourceBytes)
        {
            _logger.LogWarning(
                "Image transcode skipped: source is {SourceBytes} bytes, over the {MaxSourceBytes}-byte cap",
                source.Length, _options.MaxSourceBytes);
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var image = new MagickImage(source);

            // Bake EXIF orientation into the pixels — a JPEG with no orientation tag renders
            // upright everywhere.
            image.AutoOrient();

            var maxEdge = _options.MaxEdgePixels;
            if (maxEdge > 0 && (image.Width > maxEdge || image.Height > maxEdge))
            {
                image.Resize(new MagickGeometry((uint)maxEdge, (uint)maxEdge) { Greater = true });
            }

            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)Math.Clamp(_options.JpegQuality, 1, 100);
            image.Strip();

            var bytes = image.ToByteArray();
            return new ImageTranscodeResult(bytes, (int)image.Width, (int)image.Height);
        }
        catch (MagickException ex)
        {
            _logger.LogWarning(
                ex,
                "Image transcode failed to decode/encode a {SourceBytes}-byte payload; caller keeps the original",
                source.Length);
            return null;
        }
    }
}
