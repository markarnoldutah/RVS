using ImageMagick;

namespace RVS.API.Packets;

/// <summary>
/// Crops a packet photo to the shape of its PDF grid cell, centred, so every photo fills an
/// identical cell whatever shape it was taken in. A portrait phone shot fitted whole into a
/// fixed-height cell came out a narrow strip beside a wide one (issue #736). The trim is for the
/// page only: the full photo is still attached to the packet email and opens from the manager app.
/// </summary>
public static class PacketPhotoCrop
{
    private const uint JpegQuality = 85;

    /// <summary>
    /// Returns <paramref name="bytes"/> cropped to <paramref name="aspect"/> (width ÷ height)
    /// about the centre and no wider than <paramref name="maxWidthPx"/>; a PNG stays a PNG,
    /// anything else becomes a JPEG. <c>null</c> when the bytes cannot be decoded.
    /// </summary>
    public static byte[]? ToAspect(byte[] bytes, double aspect, uint maxWidthPx)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aspect);

        try
        {
            using var image = new MagickImage(bytes);
            var keepAsPng = image.Format == MagickFormat.Png;

            // Upload normalising already baked orientation in; an image that skipped it must
            // still be cropped the way it is seen.
            image.AutoOrient();

            var (width, height) = (image.Width, image.Height);
            var (cropWidth, cropHeight) = (double)width / height > aspect
                ? ((uint)Math.Round(height * aspect), height)
                : (width, (uint)Math.Round(width / aspect));

            image.Crop(new MagickGeometry(
                (int)((width - cropWidth) / 2), (int)((height - cropHeight) / 2), cropWidth, cropHeight));
            image.ResetPage();

            if (image.Width > maxWidthPx)
            {
                image.Resize(maxWidthPx, 0);
            }

            image.Strip();
            if (keepAsPng)
            {
                image.Format = MagickFormat.Png;
            }
            else
            {
                image.Format = MagickFormat.Jpeg;
                image.Quality = JpegQuality;
            }

            return image.ToByteArray();
        }
        catch (MagickException)
        {
            return null;
        }
    }
}
