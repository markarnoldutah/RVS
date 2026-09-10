using System.Globalization;
using RVS.Domain.Integrations;

namespace RVS.Domain.Packets;

/// <summary>
/// Assembles the packet delivery email (<c>Spec B-4</c>, issue #437) from a composed
/// <see cref="ServicePacket"/> and its rendered HTML.
///
/// A pure transform: it reads only its arguments and returns a new
/// <see cref="PacketEmailMessage"/>. It performs no I/O, renders no HTML, downloads no
/// attachment bytes, and knows nothing about the transport. The packet generation
/// orchestrator resolves the recipient list and attachment bytes and hands them in.
///
/// <para><b>Subject.</b> <c>[RVS] {category} — {year} {make} {model} — {customer last name}</c>,
/// degrading field by field: an unclassified request reads <c>Uncategorized</c>; a unit with
/// no year/make/model reads <c>Unknown vehicle</c>; a missing last name reads
/// <c>Unknown</c>.</para>
///
/// <para><b>Plain-text body.</b> The DMS paste block (<c>Spec B-5</c>) so a text-only client
/// still gets category, the verbatim description, and the status link. The packet's own
/// <see cref="ServicePacket.PasteBlock"/> is used when present; otherwise one is generated
/// here from the same fields.</para>
/// </summary>
public static class PacketEmailComposer
{
    /// <summary>Category placeholder when the request was never classified (<c>Spec A-5</c>).</summary>
    private const string UncategorizedLabel = "Uncategorized";

    /// <summary>Unit-header placeholder when year, make, and model are all absent.</summary>
    private const string UnknownVehicleLabel = "Unknown vehicle";

    /// <summary>Last-name placeholder for requests created before the name was captured.</summary>
    private const string UnknownLastNameLabel = "Unknown";

    /// <summary>The em-dash separating the three subject segments, matching <c>Spec B-4</c>.</summary>
    private const string SubjectSeparator = " — ";

    /// <summary>
    /// Builds the packet email for one request.
    /// </summary>
    /// <param name="packet">The composed packet — subject fields and the plain-text fallback come from here.</param>
    /// <param name="htmlBody">The rendered packet HTML, used verbatim as the inline HTML body.</param>
    /// <param name="customerLastName">The customer's last name for the subject; blank becomes <c>Unknown</c>.</param>
    /// <param name="recipients">The location's configured recipient addresses; blanks are dropped and at least one must remain.</param>
    /// <param name="attachments">The PDF and/or original photos per the location's configuration. <c>null</c> is treated as none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="packet"/> or <paramref name="recipients"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="htmlBody"/> is blank, or <paramref name="recipients"/> has no non-blank entry.</exception>
    public static PacketEmailMessage Compose(
        ServicePacket packet,
        string htmlBody,
        string? customerLastName,
        IReadOnlyList<string> recipients,
        IReadOnlyList<PacketEmailAttachment>? attachments = null)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentNullException.ThrowIfNull(recipients);

        var cleanRecipients = recipients
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToList();

        if (cleanRecipients.Count == 0)
        {
            throw new ArgumentException("At least one non-blank recipient is required.", nameof(recipients));
        }

        return new PacketEmailMessage
        {
            Subject = BuildSubject(packet, customerLastName),
            HtmlBody = htmlBody,
            PlainTextBody = BuildPlainTextBody(packet),
            Recipients = cleanRecipients,
            Attachments = attachments is null ? [] : [.. attachments],
        };
    }

    private static string BuildSubject(ServicePacket packet, string? customerLastName)
    {
        var category = string.IsNullOrWhiteSpace(packet.IssueCategory)
            ? UncategorizedLabel
            : packet.IssueCategory.Trim();

        var vehicle = BuildVehicle(packet.Unit);

        var lastName = string.IsNullOrWhiteSpace(customerLastName)
            ? UnknownLastNameLabel
            : customerLastName.Trim();

        return $"[RVS] {category}{SubjectSeparator}{vehicle}{SubjectSeparator}{lastName}";
    }

    private static string BuildVehicle(PacketUnitHeader unit)
    {
        var parts = new List<string>(3);

        if (unit.Year is { } year)
        {
            parts.Add(year.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(unit.Make))
        {
            parts.Add(unit.Make.Trim());
        }

        if (!string.IsNullOrWhiteSpace(unit.Model))
        {
            parts.Add(unit.Model.Trim());
        }

        return parts.Count == 0 ? UnknownVehicleLabel : string.Join(' ', parts);
    }

    /// <summary>
    /// The plain-text body this composer would send for <paramref name="packet"/> — the packet's
    /// own paste block, or one generated from the same fields when it has none.
    ///
    /// Public so a caller can measure the body before composing: the email size budget
    /// (<see cref="PacketEmailSizeFitter"/>, issue #521) has to charge the real bodies against
    /// the ACS request ceiling, and re-deriving them here keeps that measurement and the
    /// composed message from drifting apart.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="packet"/> is null.</exception>
    public static string BuildPlainTextBody(ServicePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return string.IsNullOrWhiteSpace(packet.PasteBlock)
            ? PasteBlockGenerator.Generate(packet.IssueCategory, packet.IssueDescription, packet.StatusLink?.Url)
            : packet.PasteBlock;
    }
}
