namespace RVS.Domain.Integrations;

/// <summary>
/// A fully-composed service-packet email, ready for a transport to send (<c>Spec B-4</c>,
/// issue #437). Render-agnostic and transport-agnostic: it carries the finished subject,
/// the inline HTML body, a plain-text alternative for text-only clients, the resolved
/// recipient list, and the file attachments — nothing about ACS, MIME, or headers.
///
/// Built by <see cref="Packets.PacketEmailComposer"/> from a
/// <see cref="Packets.ServicePacket"/>; consumed by
/// <see cref="INotificationService.SendPacketEmailAsync"/>.
/// </summary>
public sealed record PacketEmailMessage
{
    /// <summary>
    /// Subject line, already formatted as
    /// <c>[RVS] {category} — {year} {make} {model} — {customer last name}</c>.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>The packet HTML — used verbatim as the email's inline HTML body (<c>Spec B-3</c>).</summary>
    public required string HtmlBody { get; init; }

    /// <summary>
    /// Plain-text body for clients that do not render HTML — the DMS paste block
    /// (<c>Spec B-5</c>), so a text-only reader still gets category, the verbatim
    /// description, and the status link.
    /// </summary>
    public required string PlainTextBody { get; init; }

    /// <summary>
    /// The addresses that receive the packet — the location's configured recipient list
    /// (<c>Spec B-4</c>, 1–10 entries). Never empty.
    /// </summary>
    public required IReadOnlyList<string> Recipients { get; init; }

    /// <summary>
    /// File attachments per the location's configuration: the rendered PDF and/or the
    /// original photos. Empty when the location opts out of both.
    /// </summary>
    public IReadOnlyList<PacketEmailAttachment> Attachments { get; init; } = [];
}

/// <summary>One file attached to a <see cref="PacketEmailMessage"/>.</summary>
public sealed record PacketEmailAttachment
{
    /// <summary>File name shown to the recipient, e.g. <c>service-packet-A1B2C3D4.pdf</c>.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type, e.g. <c>application/pdf</c> or <c>image/jpeg</c>.</summary>
    public required string ContentType { get; init; }

    /// <summary>Raw file bytes.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }
}
