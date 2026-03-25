using Microsoft.AspNetCore.Components;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Displays an attachment thumbnail with a type icon, file name, and formatted file size.
/// Supports image, video, and generic file type indicators.
/// </summary>
public partial class AttachmentThumbnail : ComponentBase
{
    /// <summary>
    /// The attachment metadata to display.
    /// </summary>
    [Parameter, EditorRequired]
    public AttachmentDto? Attachment { get; set; }

    /// <summary>
    /// Whether the attachment content type indicates an image.
    /// </summary>
    protected bool IsImage =>
        Attachment?.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Whether the attachment content type indicates a video.
    /// </summary>
    protected bool IsVideo =>
        Attachment?.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Formats a byte count into a human-readable file size string.
    /// </summary>
    protected static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };
}
