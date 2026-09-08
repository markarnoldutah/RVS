namespace RVS.Domain.Packets;

/// <summary>
/// The packet composition model — one immutable, render-agnostic representation of a
/// finished service packet, assembled by <see cref="PacketComposer"/> from a
/// <see cref="Entities.ServiceRequest"/>. Both the HTML and PDF renderers consume this
/// single model so their content and ordering cannot diverge (<c>Spec B-2</c>,
/// <c>B-3</c>).
///
/// Property order is the packet's section order per <c>Spec B-2</c>. This type carries
/// no behaviour, no formatting, and no knowledge of page size, fonts, or output format.
/// It is never persisted and is not an API DTO.
/// </summary>
public sealed record ServicePacket
{
    /// <summary>1. Unit header — year / make / model / VIN.</summary>
    public required PacketUnitHeader Unit { get; init; }

    /// <summary>2. Customer and preferred contact.</summary>
    public required PacketCustomer Customer { get; init; }

    /// <summary>3. Location, submission timestamp, short reference code.</summary>
    public required PacketOrigin Origin { get; init; }

    /// <summary>4. Issue category. <c>null</c> when unclassified (it is advisory, <c>Spec A-5</c>).</summary>
    public string? IssueCategory { get; init; }

    /// <summary>
    /// 5. AI summary, always labelled as AI-generated. <c>null</c> when no summary exists.
    /// Rendered above the customer's description so the concise recreation of the problem
    /// is read first (<c>Spec B-2</c> item 5).
    /// </summary>
    public PacketAiSummary? AiSummary { get; init; }

    /// <summary>6. The customer's description, verbatim — never trimmed or rewritten.</summary>
    public required string IssueDescription { get; init; }

    /// <summary>7. Diagnostic Q&amp;A. Empty when no diagnostic responses were captured.</summary>
    public required IReadOnlyList<PacketDiagnosticEntry> Diagnostics { get; init; }

    /// <summary>8. Photo thumbnails. Empty when there are no image attachments with a resolved URL.</summary>
    public required IReadOnlyList<PacketPhoto> Photos { get; init; }

    /// <summary>9. DMS paste block (<c>Spec B-5</c>). <c>null</c> until it is generated upstream.</summary>
    public string? PasteBlock { get; init; }

    /// <summary>10. Customer status link (<c>Spec X-1</c>). <c>null</c> until a token is minted upstream.</summary>
    public PacketStatusLink? StatusLink { get; init; }
}

/// <summary>
/// Unit header. Each field degrades independently; <see cref="HasVin"/> lets a renderer
/// drop the VIN line without losing year / make / model.
/// </summary>
public sealed record PacketUnitHeader
{
    public int? Year { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }
    public string? Vin { get; init; }

    /// <summary><c>true</c> only when a non-blank VIN is present.</summary>
    public bool HasVin => !string.IsNullOrWhiteSpace(Vin);
}

/// <summary>Customer identity and contact preference.</summary>
public sealed record PacketCustomer
{
    public required string FullName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }

    /// <summary>
    /// Preferred contact method (<c>Spec A-2</c>). Not yet captured on any entity, so this
    /// is always <c>null</c> today; the slot exists so a renderer needs no change once
    /// intake records it.
    /// </summary>
    public string? PreferredContact { get; init; }
}

/// <summary>Where and when the request originated, plus a short human-quotable code.</summary>
public sealed record PacketOrigin
{
    public string? LocationName { get; init; }
    public string? LocationPhone { get; init; }
    public required DateTimeOffset SubmittedAtUtc { get; init; }

    /// <summary>Short reference code derived from the service request id.</summary>
    public required string ReferenceCode { get; init; }
}

/// <summary>One diagnostic question and the customer's answer(s) to it.</summary>
public sealed record PacketDiagnosticEntry
{
    public required string Question { get; init; }

    /// <summary>Selected options followed by the free-text answer, when given.</summary>
    public required IReadOnlyList<string> Answers { get; init; }
}

/// <summary>The AI-generated summary. The label is not optional (<c>Spec B-2</c> item 7).</summary>
public sealed record PacketAiSummary
{
    public required string Text { get; init; }

    /// <summary>Always <c>true</c> — this block is presented as AI-generated.</summary>
    public bool IsAiGenerated => true;
}

/// <summary>One photo thumbnail, referenced by a time-limited read URL (never base64).</summary>
public sealed record PacketPhoto
{
    public required string Url { get; init; }
    public required string FileName { get; init; }
    public string? Caption { get; init; }
}

/// <summary>The anonymous customer status link.</summary>
public sealed record PacketStatusLink
{
    public required string Url { get; init; }
}
