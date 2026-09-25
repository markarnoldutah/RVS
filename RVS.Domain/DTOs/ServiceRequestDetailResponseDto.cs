namespace RVS.Domain.DTOs;

/// <summary>
/// Full detail response for a single service request, including all fields and attachments.
/// </summary>
public sealed record ServiceRequestDetailResponseDto
{
    public string Id { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string LocationId { get; init; } = default!;
    public string? CustomerProfileId { get; init; }
    public CustomerInfoDto Customer { get; init; } = default!;
    public AssetInfoDto Asset { get; init; } = default!;
    public string IssueCategory { get; init; } = default!;
    public string IssueDescription { get; init; } = default!;

    /// <summary>
    /// The customer's words before AI curation (issue #601). <c>null</c> when no curation ran,
    /// or for requests captured before this was recorded.
    /// </summary>
    public string? IssueDescriptionVerbatim { get; init; }

    public string? TechnicianSummary { get; init; }
    public string? Urgency { get; init; }
    public string? RvUsage { get; init; }
    public string? HasExtendedWarranty { get; init; }
    public string? ApproxPurchaseDate { get; init; }
    public string? Priority { get; init; }
    public string? AssignedTechnicianId { get; init; }
    public DateTime? ScheduledDateUtc { get; init; }
    public List<string> RequiredSkills { get; init; } = [];
    public int BoardSequence { get; init; }
    public List<DiagnosticResponseDto> DiagnosticResponses { get; init; } = [];
    public List<AttachmentDto> Attachments { get; init; } = [];
    public AiEnrichmentMetadataDto? AiEnrichment { get; init; }

    /// <summary>
    /// Distribution channel the customer arrived through (<c>Spec A-13</c>) — <c>qr</c>,
    /// <c>textrepl</c>, <c>quickreply</c>, <c>mgrapp</c>, <c>print</c>, or an ad-hoc tag. Null on requests
    /// created before channel tagging existed.
    /// </summary>
    public string? IntakeSource { get; init; }

    /// <summary>
    /// The A-14 advisor invite this request redeemed (<c>Spec A-14</c>); <c>null</c> unless
    /// <see cref="IntakeSource"/> is <c>advisor</c> and the invite was still valid on submission.
    /// </summary>
    public string? IntakeInviteId { get; init; }

    /// <summary>The advisor whose invite produced this request; set together with <see cref="IntakeInviteId"/>.</summary>
    public string? AdvisorUserId { get; init; }

    /// <summary>
    /// The current manager-authored customer status note (<c>Spec C-9</c>), or <c>null</c> when
    /// none is set. Shown to the customer on the status page; editable only from the manager app.
    /// </summary>
    public CustomerStatusNoteDto? CustomerStatusNote { get; init; }

    /// <summary>
    /// How the request was closed without work (<c>Spec C-4</c>), or <c>null</c> when it was not.
    /// Present only while <see cref="Status"/> is <c>Cancelled</c>. Manager-only; never shown to
    /// the customer.
    /// </summary>
    public ServiceRequestDispositionDto? Disposition { get; init; }

    /// <summary>
    /// Service-packet generation state (<c>Spec B-1</c>) for the manager detail view
    /// (<c>Spec C-2</c>, issue #443). Always present; <c>Pending</c> before the first attempt.
    /// </summary>
    public PacketGenerationDto PacketGeneration { get; init; } = new();

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

/// <summary>
/// A request's disposition (<c>Spec C-4</c>) as returned to the manager app.
/// </summary>
public sealed record ServiceRequestDispositionDto
{
    /// <summary>The stored reason code, e.g. <c>WrongLocation</c>.</summary>
    public string ReasonCode { get; init; } = default!;

    /// <summary>The human label for <see cref="ReasonCode"/>, e.g. <c>Wrong location</c>.</summary>
    public string ReasonLabel { get; init; } = default!;

    /// <summary>UTC time the request was dispositioned.</summary>
    public DateTime DisposedAtUtc { get; init; }
}

/// <summary>
/// The manager-authored customer status note (<c>Spec C-9</c>) as returned to the manager app.
/// </summary>
public sealed record CustomerStatusNoteDto
{
    /// <summary>The note text shown to the customer.</summary>
    public string Text { get; init; } = default!;

    /// <summary>UTC time the note was last set or edited.</summary>
    public DateTime UpdatedAtUtc { get; init; }
}

/// <summary>
/// A request's service-packet generation state (<c>Spec B-1</c>) as returned to the manager app
/// (<c>Spec C-2</c>, issue #443). Carries the state only — the failure text and the PDF's blob
/// path are internal and stay on the entity.
/// </summary>
public sealed record PacketGenerationDto
{
    /// <summary><c>Pending</c>, <c>Generating</c>, <c>Succeeded</c>, or <c>Failed</c>.</summary>
    public string Status { get; init; } = "Pending";

    /// <summary>Attempts made in the current run; reset to <c>0</c> by a regeneration.</summary>
    public int AttemptCount { get; init; }

    /// <summary>Attempts allowed before generation gives up and alerts.</summary>
    public int MaxAttempts { get; init; }

    /// <summary>
    /// True when <see cref="Status"/> is <c>Failed</c> and every attempt has been used — the
    /// worker will not retry, so only a manual regeneration will produce a packet.
    /// </summary>
    public bool RetriesExhausted { get; init; }

    /// <summary>UTC time the most recent attempt started. Null before the first attempt.</summary>
    public DateTime? LastAttemptAtUtc { get; init; }

    /// <summary>UTC time of the most recent successful generation. Null until the first success.</summary>
    public DateTime? GeneratedAtUtc { get; init; }

    /// <summary>Version of the last good packet; <c>0</c> until the first success.</summary>
    public int PacketVersion { get; init; }
}
