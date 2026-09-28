using RVS.Blazor.Intake.State;

namespace RVS.Blazor.Intake.Pages.Steps.Shared;

/// <summary>What the in-page camera takes: a photo, or a video with sound.</summary>
public enum CameraMode
{
    Photo,
    Video
}

/// <summary>How starting the in-page camera went.</summary>
public enum CameraStartResult
{
    Started,
    PermissionDenied,
    NoCamera,
    Unsupported,
    Failed
}

/// <summary>
/// A photo or video taken with the in-page camera. <paramref name="ThumbnailDataUrl"/> is a small
/// preview for a photo, <c>null</c> for a video.
/// </summary>
public sealed record CapturedMedia(byte[] Bytes, string ContentType, string? ThumbnailDataUrl);

/// <summary>A captured photo or video as a Step 7 attachment, or why it could not be one.</summary>
public readonly record struct CaptureAttachmentResult(AttachmentFileInfo? Attachment, string? Error);

/// <summary>
/// Rules for the in-page camera (issue #736). Photos and videos are taken inside the page rather
/// than in the phone's camera app: on a phone short of memory, Android kills the browser tab while
/// the camera app is in front, and the photo — and everything else attached — goes with it.
/// </summary>
public static class CameraCapture
{
    /// <summary>Longest edge of a photo, in pixels. Plenty for a VIN plate or a damaged part.</summary>
    public const int PhotoMaxEdgePx = 2560;

    /// <summary>Video bitrate asked of the recorder. Fixed, so the longest clip that fits the size limit is known.</summary>
    public const int VideoBitsPerSecond = 2_500_000;

    /// <summary>Audio bitrate asked of the recorder.</summary>
    public const int AudioBitsPerSecond = 128_000;

    /// <summary>
    /// The longest clip that fits in <paramref name="maxFileSizeMb"/> at the recording bitrates,
    /// with a tenth held back for the container and a recorder that overshoots. Rounded down to
    /// five seconds, and never under five.
    /// </summary>
    public static int MaxVideoSeconds(int maxFileSizeMb)
    {
        var bits = maxFileSizeMb * 1024.0 * 1024.0 * 8.0;
        var seconds = bits / (VideoBitsPerSecond + AudioBitsPerSecond) * 0.9;
        return Math.Max(5, (int)(seconds / 5) * 5);
    }

    /// <summary>
    /// The recorder's MIME type as the API accepts it: bare, without codec parameters. Chrome
    /// reports H.264 in WebM as <c>video/x-matroska</c>, which is the same container.
    /// </summary>
    public static string NormalizeContentType(string? recorded)
    {
        if (string.IsNullOrWhiteSpace(recorded))
        {
            return "video/webm";
        }

        var bare = recorded.Split(';', 2)[0].Trim().ToLowerInvariant();
        return bare == "video/x-matroska" ? "video/webm" : bare;
    }

    /// <summary>A file name for a capture, by kind and the time it was taken — the camera gives it none.</summary>
    public static string FileName(string contentType, DateTimeOffset takenAt)
    {
        var (kind, extension) = contentType switch
        {
            "video/mp4" => ("video", "mp4"),
            "video/webm" => ("video", "webm"),
            _ => ("photo", "jpg")
        };

        return $"{kind}-{takenAt:yyyyMMdd-HHmmss}.{extension}";
    }

    /// <summary>
    /// Makes <paramref name="media"/> a Step 7 attachment, under the same limits as a picked file.
    /// </summary>
    public static CaptureAttachmentResult ToAttachment(
        CapturedMedia media, int maxAttachments, int attachedCount, int maxFileSizeMb, DateTimeOffset takenAt)
    {
        ArgumentNullException.ThrowIfNull(media);

        if (AttachmentSlots.Remaining(maxAttachments, attachedCount) <= 0)
        {
            return new(null, $"Maximum {maxAttachments} files allowed.");
        }

        if (media.Bytes.Length == 0)
        {
            return new(null, "Nothing was captured. Please try again.");
        }

        if (media.Bytes.Length > maxFileSizeMb * 1024L * 1024L)
        {
            return media.ContentType.StartsWith("video/", StringComparison.Ordinal)
                ? new(null, $"That video is over the {maxFileSizeMb} MB limit. Please record a shorter clip.")
                : new(null, $"That photo is over the {maxFileSizeMb} MB limit.");
        }

        return new(new AttachmentFileInfo
        {
            FileName = FileName(media.ContentType, takenAt),
            ContentType = media.ContentType,
            SizeBytes = media.Bytes.Length,
            FileData = media.Bytes,
            ThumbnailDataUrl = media.ThumbnailDataUrl
        }, null);
    }

    /// <summary>What to tell the customer when the camera would not start; <c>null</c> when it did.</summary>
    public static string? StartFailureMessage(CameraStartResult result) => result switch
    {
        CameraStartResult.Started => null,
        CameraStartResult.PermissionDenied =>
            "Camera access is blocked. Allow the camera for this site in your browser settings, or use your phone's camera app instead.",
        CameraStartResult.NoCamera => "No camera was found on this device.",
        CameraStartResult.Unsupported => "This browser can't use the camera inside the page.",
        _ => "The camera couldn't start."
    };
}
