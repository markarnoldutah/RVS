namespace RVS.Domain.DTOs;

/// <summary>
/// The channel-tagged intake links for one location (<c>Spec A-13</c>, issue #599) — every URL
/// a dealer might hand out, each already routed through <c>go.rvintake.com</c> so the channel
/// is observed before the customer reaches the form.
/// </summary>
public sealed record LocationIntakeLinksResponseDto
{
    /// <summary>The location these links belong to.</summary>
    public required string LocationId { get; init; }

    /// <summary>The location's intake slug — the last path segment of every link here.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// For printed material: business cards, invoices, counter signage. Carries no query string,
    /// because printed URLs cannot — which is why an absent <c>src</c> is recorded as print.
    /// </summary>
    public required string PrintUrl { get; init; }

    /// <summary>Encoded in the QR sticker and the NFC tag.</summary>
    public required string QrUrl { get; init; }

    /// <summary>For the Text Replacement / keyboard-shortcut snippet an advisor sends from Recents.</summary>
    public required string TextReplacementUrl { get; init; }

    /// <summary>For the canned "Respond with Text" / Quick Response message sent from an incoming call.</summary>
    public required string QuickReplyUrl { get; init; }
}
