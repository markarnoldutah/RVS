namespace RVS.Domain.Integrations;

/// <summary>
/// Normalises a raster image upload to a small, universally-renderable form before it is
/// recorded on a service request and flows into the three packet consumers that carry photo
/// bytes: the PDF embed (<c>#432</c>), the HTML packet's <c>&lt;img&gt;</c> (<c>#431</c>), and
/// the emailed photo attachment (<c>Spec B-4</c>).
///
/// It began as an Apple-only HEIC/HEIF → JPEG transcode (issue <c>#508</c>, finishing
/// <c>#492</c> item 8), because those formats only render on Apple clients. Issue <c>#562</c>
/// widened it to every raster the intake accepts: a full-resolution Android JPEG or a
/// point-and-shoot PNG rides into the packet email as a full-size attachment, which is what
/// <c>#521</c>'s size fitter finally has to throw photos overboard to fit. (The PDF is not the
/// problem — QuestPDF resamples each image to its placed size — so the payoff is in the
/// attachment set.) Normalising every image on upload removes the reason photos get dropped.
///
/// Formats outside <see cref="CanNormalize"/> — <c>image/gif</c>, video, audio, PDF — are left
/// byte-for-byte as uploaded: <see cref="CanNormalize"/> returns <c>false</c> and the caller
/// stores the original.
/// </summary>
public interface IImageTranscoder
{
    /// <summary>
    /// <c>true</c> when <paramref name="contentType"/> is a raster this normaliser rewrites:
    /// <c>image/heic</c>, <c>image/heif</c>, <c>image/jpeg</c>, <c>image/png</c>,
    /// <c>image/webp</c> (case-insensitive, surrounding whitespace ignored). Every other value
    /// — including <c>null</c>, <c>image/gif</c>, and non-images — returns <c>false</c> so the
    /// caller keeps the original.
    /// </summary>
    bool CanNormalize(string? contentType);

    /// <summary>
    /// Decodes <paramref name="source"/>, bakes EXIF orientation into the pixels, downscales
    /// the longest edge past the configured maximum, strips metadata, and re-encodes. A PNG is
    /// re-encoded as PNG so a screenshot, diagram, or line art keeps its sharp edges; every
    /// other accepted format is re-encoded as baseline JPEG at the configured quality. The
    /// re-encode is applied once, at upload, and a service-diagnostic photo tolerates the one
    /// generation of JPEG loss.
    /// </summary>
    /// <param name="source">The original upload bytes.</param>
    /// <param name="contentType">
    /// The upload's declared content type. Decides the output format (PNG stays PNG) and
    /// whether the never-grow guard applies — see the return value.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The re-encoded bytes, their content type, and their pixel dimensions on success.
    /// <c>null</c> means "keep the original, unchanged" and is returned when the payload is
    /// empty, over the configured size cap, or cannot be decoded — and also when the source is
    /// already a web-safe raster (JPEG / PNG / WebP) that the re-encode would not make smaller,
    /// so a small already-optimised image never grows just by passing through. HEIC/HEIF is
    /// exempt from that last rule: its original does not render off Apple devices, so the
    /// larger JPEG is still the right answer (behaviour unchanged from <c>#508</c>). On
    /// <c>null</c> the caller keeps the original and logs; <c>#521</c>'s size fitter stays as
    /// the backstop for anything that slips through.
    /// </returns>
    ImageTranscodeResult? Normalize(byte[] source, string? contentType, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a successful <see cref="IImageTranscoder.Normalize"/> call.</summary>
/// <param name="Bytes">The re-encoded image payload.</param>
/// <param name="ContentType">MIME type of <paramref name="Bytes"/> — <c>image/jpeg</c> or <c>image/png</c>.</param>
/// <param name="Width">Pixel width of the encoded image.</param>
/// <param name="Height">Pixel height of the encoded image.</param>
public sealed record ImageTranscodeResult(byte[] Bytes, string ContentType, int Width, int Height);
