namespace RVS.Domain.Packets;

/// <summary>
/// The packet content that does not live on a <see cref="Entities.ServiceRequest"/> and
/// must be resolved by the caller before composition: the request's location, the
/// submission timestamp, the minted status link, the generated DMS paste block, and the
/// per-request read URLs for photo attachments.
///
/// Supplying these as data keeps <see cref="PacketComposer.Compose"/> a pure transform —
/// it performs no repository lookups, no SAS generation, and no rendering. The packet
/// generation orchestrator populates this record.
/// </summary>
public sealed record PacketCompositionContext
{
    /// <summary>Display name of the location the request was submitted to.</summary>
    public string? LocationName { get; init; }

    /// <summary>Public phone number for that location.</summary>
    public string? LocationPhone { get; init; }

    /// <summary>When the request was submitted.</summary>
    public required DateTimeOffset SubmittedAtUtc { get; init; }

    /// <summary>Fully-formed customer status URL, or <c>null</c> if none has been minted yet.</summary>
    public string? StatusLinkUrl { get; init; }

    /// <summary>Pre-generated DMS paste block text, or <c>null</c> if not generated yet.</summary>
    public string? PasteBlock { get; init; }

    /// <summary>
    /// Time-limited read URLs for photo attachments, keyed by
    /// <see cref="Entities.ServiceRequestAttachmentEmbedded.AttachmentId"/>. An image
    /// attachment with no entry here is omitted from the packet rather than rendered broken.
    /// </summary>
    public IReadOnlyDictionary<string, string> PhotoUrls { get; init; } =
        new Dictionary<string, string>();
}
