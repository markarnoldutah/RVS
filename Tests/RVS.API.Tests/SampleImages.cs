using ImageMagick;

namespace RVS.API.Tests;

/// <summary>
/// Small real image fixtures shared across tests. The HEIC sample is a 96 × 64 gradient
/// encoded as a single-image ISO-BMFF <c>heic</c> file — the shape an iPhone produces and
/// the format QuestPDF's decoder cannot read (issue <c>#492</c> item 8 / <c>#508</c>). The
/// JPEG and PNG generators produce real, decodable rasters at an arbitrary size for the
/// <c>#562</c> downscale-every-image path.
/// </summary>
internal static class SampleImages
{
    /// <summary>A valid 96 × 64 HEIC image (~850 bytes).</summary>
    public static byte[] Heic96x64() => Convert.FromBase64String(Heic96x64Base64);

    /// <summary>
    /// A real baseline JPEG of <paramref name="width"/> × <paramref name="height"/> pixels
    /// filled with a diagonal gradient (so it carries genuine high-frequency detail and a
    /// downscale actually reduces its byte size). <paramref name="quality"/> sets the encode
    /// quality — pass a low value to get a payload smaller than an 82-quality re-encode.
    /// </summary>
    public static byte[] Jpeg(uint width, uint height, uint quality = 90) =>
        Render(width, height, MagickFormat.Jpeg, quality);

    /// <summary>
    /// A real PNG of <paramref name="width"/> × <paramref name="height"/> pixels — the shape a
    /// screenshot or a diagram takes, used to prove the normaliser keeps PNG as PNG.
    /// </summary>
    public static byte[] Png(uint width, uint height) =>
        Render(width, height, MagickFormat.Png, quality: null);

    private static byte[] Render(uint width, uint height, MagickFormat format, uint? quality)
    {
        var settings = new MagickReadSettings { Width = width, Height = height };
        using var image = new MagickImage("gradient:navy-white", settings);
        image.Format = format;
        if (quality is { } q)
        {
            image.Quality = q;
        }

        return image.ToByteArray();
    }

    private const string Heic96x64Base64 =
        "AAAAJGZ0eXBoZWljAAAAAG1pZjFNaVBybWlhZk1pSEJoZWljAAABgG1ldGEAAAAAAAAAIWhkbHIA" +
        "AAAAAAAAAHBpY3QAAAAAAAAAAAAAAAAAAAAAJGRpbmYAAAAcZHJlZgAAAAAAAAABAAAADHVybCAA" +
        "AAABAAAADnBpdG0AAAAAAAEAAAAjaWluZgAAAAAAAQAAABVpbmZlAgAAAAABAABodmMxAAAAAOBp" +
        "cHJwAAAAwGlwY28AAAATY29scm5jbHgAAgACAAaAAAAAeGh2Y0MBAWAAAACwAAAAAAAe8AD8/fj4" +
        "AAAPA6AAAQAXQAEMAf//AWAAAAMAsAAAAwAAAwAeLAmhAAEAIkIBAQFgAAADALAAAAMAAAMAHqAw" +
        "gQWcuSRKSXE3AgIGpAKiAAEAEUQBwGESTATpEREkSRJEkSpAAAAAFGlzcGUAAAAAAAAAYAAAAEAA" +
        "AAAJaXJvdAAAAAAQcGl4aQAAAAADCAgIAAAAGGlwbWEAAAAAAAAAAQABBYGCA4QFAAAAHmlsb2MA" +
        "AAAARAAAAQABAAAAAQAAAbQAAAGeAAAAAW1kYXQAAAAAAAABrgAAAZomAa3ATSkreGb8VCSvnwJY" +
        "rc/7BfI6QNlrSfu16UcA2R71/lyJ4vq3ZHN/R3HU3fj9p/eDrDuR3bPAC9PYfhBzVu8ADUIaEU5" +
        "w/XwH2wG1NLhaQQBkNqhsGAdX4rWgUByvuCiDewVMVQEInbxnapKWqi9aNsPHQUePnMatAI9P95v" +
        "dtMKkkPOcB4lBlqEtVOlqZM/DZ3fAG5SDHXnG+ndsAR00SW53rsebLnBWgJwiTIBaQMEYq3xK4YJ" +
        "vlqvBpEOPxffr1Dn/T/X/jpSYGsESNjqmgrU+5lB+Is2KURZZidhtIeMV006F7mE3r7dtTW4Rmh" +
        "RDuYNFT8ex9tjh274fpcYcEgWBSVbzi4s6TsenpcayrBOBfM/raJYKWiMYqSC9yQlvIlOppVnEa" +
        "IWCvTHgYAt5Ukk44z1U8JqBKCXqwLtwxD/Nj/FwXH4a+tuM0HLL97OXCE6UBwghF57oVd0cT13K" +
        "6uxN8ywBAoSjn+618PQODNyRNmE4OtI1Xz523PPMa1UtA/q0JA/ijDDudy7ywQ0y3tOXliXl4A==";
}
