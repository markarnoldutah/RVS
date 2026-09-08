using RVS.Domain.Entities;

namespace RVS.Domain.DTOs;

/// <summary>
/// Per-location service-packet configuration (<c>Spec B-6</c> / <c>C-6</c>).
///
/// Every field carries the same default as <see cref="PacketConfigEmbedded"/>, so a caller
/// that wants the standard behaviour only has to send the recipient address.
/// </summary>
public sealed record PacketConfigDto
{
    /// <summary>Whether the packet email is delivered for this location.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Email addresses that receive the packet. 0–10 entries.</summary>
    public List<string> Recipients { get; init; } = [];

    /// <summary>Attach the rendered PDF to the packet email.</summary>
    public bool AttachPdf { get; init; } = true;

    /// <summary>Attach the original photos to the packet email.</summary>
    public bool IncludePhotos { get; init; } = true;

    /// <summary>Character cap for the plain-text paste block.</summary>
    public int PasteBlockCharacterCap { get; init; } = PacketConfigEmbedded.DefaultPasteBlockCharacterCap;

    /// <summary>Time-to-live, in days, for the customer status link minted into the packet.</summary>
    public int StatusLinkTtlDays { get; init; } = PacketConfigEmbedded.DefaultStatusLinkTtlDays;

    /// <summary>Optional absolute URL to a location-specific logo rendered on the packet.</summary>
    public string? LogoUrl { get; init; }
}
