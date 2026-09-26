namespace RVS.Blazor.Intake.Pages.Steps.Shared;

/// <summary>
/// Pure presentation helpers for the Step 7 attachment list (issue #758): how many more files the
/// customer can add, said in the drop zone, and the thumbnail shown in place of a bare file name.
/// </summary>
public static class AttachmentSlots
{
    /// <summary>How many more files fit under <paramref name="maxAttachments"/>; never negative.</summary>
    public static int Remaining(int maxAttachments, int attachedCount) =>
        Math.Max(0, maxAttachments - attachedCount);

    /// <summary>
    /// The drop zone's prompt. Once something is attached it counts what is left, so it is
    /// obvious the zone still takes more; when full it says how to make room.
    /// </summary>
    public static string Prompt(int maxAttachments, int attachedCount)
    {
        var remaining = Remaining(maxAttachments, attachedCount);

        return remaining switch
        {
            0 => $"Maximum of {maxAttachments} reached. Remove one to add another.",
            1 => "Add 1 more photo or video",
            _ when attachedCount <= 0 => $"Add up to {remaining} photos or videos",
            _ => $"Add up to {remaining} more photos or videos"
        };
    }

    /// <summary>
    /// The error shown when a selection held more files than there was room for (issue #766).
    /// <paramref name="attachedCount"/> is the count <em>after</em> the files that fit were added,
    /// so a selection that filled the list says so rather than quoting the room it had before.
    /// </summary>
    public static string OverflowMessage(int maxAttachments, int attachedCount)
    {
        var remaining = Remaining(maxAttachments, attachedCount);

        return remaining switch
        {
            0 => "No more files can be added.",
            1 => "Only 1 more file can be added.",
            _ => $"Only {remaining} more files can be added."
        };
    }

    /// <summary>
    /// A <c>data:</c> URL for a thumbnail's bytes. Only for the small resized image the browser
    /// produces, never the original upload — a multi-megabyte data URL in the DOM would be
    /// re-diffed on every render.
    /// </summary>
    /// <param name="contentType">The thumbnail's MIME type, e.g. <c>image/jpeg</c>.</param>
    /// <param name="bytes">The thumbnail's bytes.</param>
    public static string ThumbnailDataUrl(string contentType, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(bytes);

        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }
}
