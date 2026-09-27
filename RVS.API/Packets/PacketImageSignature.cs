namespace RVS.API.Packets;

/// <summary>
/// Recognises the two image formats a packet logo may be (<c>Spec A-16</c>, issue <c>#470</c>)
/// by their leading bytes. PNG and JPEG only: they are what both QuestPDF and every mail client
/// render, where SVG, WebP and HEIC each fail on one side or the other. The bytes decide, not a
/// declared content type, which plenty of hosts get wrong.
/// </summary>
internal static class PacketImageSignature
{
    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];

    /// <summary><c>true</c> when <paramref name="bytes"/> starts with a PNG or JPEG signature.</summary>
    public static bool IsPngOrJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(Png) || bytes.StartsWith(Jpeg);
}
