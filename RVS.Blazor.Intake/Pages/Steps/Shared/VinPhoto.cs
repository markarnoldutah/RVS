using Microsoft.AspNetCore.Components.Forms;

namespace RVS.Blazor.Intake.Pages.Steps.Shared;

/// <summary>A VIN photo read for extraction: the bytes to send and their MIME type.</summary>
public sealed record VinPhotoImage(byte[] Bytes, string ContentType)
{
    /// <summary>The bytes as base64, the form the extract-vin endpoint takes.</summary>
    public string Base64 => Convert.ToBase64String(Bytes);

    /// <summary>A <c>data:</c> URL for the Step 3 preview.</summary>
    public string DataUrl => $"data:{ContentType};base64,{Base64}";
}

/// <summary>
/// Reads the photo taken on Step 3 so its VIN can be extracted (issue #736).
/// <para>
/// A phone camera's full-size photo is commonly past the API's limit and costs memory the tab
/// can ill afford, so the browser shrinks it to a JPEG first — plenty for reading a VIN plate,
/// and it also applies the photo's orientation. When the browser cannot (a format it does not
/// decode), the original is sent if it fits.
/// </para>
/// </summary>
public static class VinPhoto
{
    /// <summary>Longest edge of the resized photo, in pixels.</summary>
    public const int MaxEdgePx = 2560;

    /// <summary>
    /// The extract-vin endpoint's limit — <c>AzureOpenAi:MaxImageBytes</c> in the API, 5 MB by
    /// default. It answers 413 above it.
    /// </summary>
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> ExtensionToMimeType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".heif"] = "image/heif"
    };

    /// <summary>Asks the browser for a JPEG no larger than <see cref="MaxEdgePx"/> on either edge.</summary>
    public static Task<IBrowserFile> ResizeInBrowser(IBrowserFile file) =>
        file.RequestImageFileAsync("image/jpeg", MaxEdgePx, MaxEdgePx).AsTask();

    /// <summary>
    /// Reads <paramref name="file"/>, resized by <paramref name="resize"/> where that works.
    /// Returns <c>null</c> when nothing sendable could be read. Must run while
    /// <paramref name="file"/> is still valid.
    /// </summary>
    public static async Task<VinPhotoImage?> ReadAsync(
        IBrowserFile file,
        Func<IBrowserFile, Task<IBrowserFile>> resize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(resize);

        try
        {
            var resized = await resize(file);
            var bytes = await ReadAllAsync(resized, cancellationToken);
            if (bytes is { Length: > 0 })
            {
                return new VinPhotoImage(bytes, "image/jpeg");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fall through to the original.
        }

        if (file.Size > MaxUploadBytes)
        {
            return null;
        }

        try
        {
            var bytes = await ReadAllAsync(file, cancellationToken);
            return bytes is { Length: > 0 } ? new VinPhotoImage(bytes, ContentTypeOf(file)) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the whole file. The browser stream returns a chunk per read, so one
    /// <c>ReadAsync</c> into a file-sized buffer leaves most of it zeros.
    /// </summary>
    private static async Task<byte[]?> ReadAllAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        if (file.Size > MaxUploadBytes)
        {
            return null;
        }

        await using var stream = file.OpenReadStream(MaxUploadBytes, cancellationToken);
        using var buffer = new MemoryStream((int)file.Size);
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <summary>Android camera captures can arrive with no content type; fall back to the extension, then JPEG.</summary>
    private static string ContentTypeOf(IBrowserFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            return file.ContentType;
        }

        var extension = Path.GetExtension(file.Name);
        return !string.IsNullOrEmpty(extension) && ExtensionToMimeType.TryGetValue(extension, out var inferred)
            ? inferred
            : "image/jpeg";
    }
}
