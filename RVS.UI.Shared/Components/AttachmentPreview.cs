namespace RVS.UI.Shared.Components;

/// <summary>
/// How an attachment tile previews its file (issue #699).
/// </summary>
public enum AttachmentPreviewKind
{
    /// <summary>A raster the browser can draw — shown as an <c>&lt;img&gt;</c> thumbnail.</summary>
    Image,

    /// <summary>A video — shown as its first frame, played in the inline player (issue #583).</summary>
    Video,

    /// <summary>The packet PDF — shown as an icon.</summary>
    Pdf,

    /// <summary>Anything else, including HEIC/HEIF — shown as an icon.</summary>
    File
}

/// <summary>
/// Pure presentation helper for attachment tiles in the manager SR detail (issue #699).
/// </summary>
public static class AttachmentPreview
{
    /// <summary>
    /// Media fragment that makes a <c>&lt;video preload="metadata"&gt;</c> paint a frame
    /// instead of an empty box — most browsers show nothing at <c>t=0</c>.
    /// </summary>
    private const string FirstFrameFragment = "#t=0.1";

    /// <summary>
    /// Classifies a content type. HEIC/HEIF is <see cref="AttachmentPreviewKind.File"/>:
    /// uploads are normally transcoded to JPEG on confirm (issue #508), but one that was
    /// not would be a broken image in every browser off Apple.
    /// </summary>
    /// <param name="contentType">The attachment's stored MIME type.</param>
    public static AttachmentPreviewKind Classify(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return AttachmentPreviewKind.File;

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return AttachmentPreviewKind.Video;

        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            return AttachmentPreviewKind.Pdf;

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("heic", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("heif", StringComparison.OrdinalIgnoreCase))
            return AttachmentPreviewKind.Image;

        return AttachmentPreviewKind.File;
    }

    /// <summary>
    /// The <c>src</c> for a video thumbnail: the read URL with a first-frame media fragment.
    /// The fragment never reaches the server, so the SAS signature is unaffected.
    /// </summary>
    /// <param name="readUrl">The attachment's read SAS URL.</param>
    public static string VideoThumbnailSrc(string readUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(readUrl);

        var hash = readUrl.IndexOf('#');
        var baseUrl = hash >= 0 ? readUrl[..hash] : readUrl;
        return baseUrl + FirstFrameFragment;
    }
}
