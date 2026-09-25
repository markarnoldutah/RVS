namespace RVS.Domain.DTOs;

/// <summary>
/// A short-lived read link to a request's latest generated service-packet PDF
/// (<c>Spec C-2</c>, issue #443). Issued on demand; the blob path itself is never returned.
/// </summary>
public sealed record PacketPdfLinkDto
{
    /// <summary>Pre-signed read URL for the PDF.</summary>
    public string SasUrl { get; init; } = default!;

    /// <summary>UTC time the link stops working.</summary>
    public DateTime ExpiresAtUtc { get; init; }

    /// <summary>The packet version the link points at.</summary>
    public int PacketVersion { get; init; }
}
