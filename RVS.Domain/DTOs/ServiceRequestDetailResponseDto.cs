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
    public string? TechnicianSummary { get; init; }
    public string? Urgency { get; init; }
    public string? RvUsage { get; init; }
    public string? HasExtendedWarranty { get; init; }
    public string? ApproxPurchaseDate { get; init; }
    public string? Priority { get; init; }
    public string? AssignedTechnicianId { get; init; }
    public string? AssignedBayId { get; init; }
    public DateTime? ScheduledDateUtc { get; init; }
    public List<string> RequiredSkills { get; init; } = [];
    public int BoardSequence { get; init; }
    public List<DiagnosticResponseDto> DiagnosticResponses { get; init; } = [];
    public List<AttachmentDto> Attachments { get; init; } = [];
    public AiEnrichmentMetadataDto? AiEnrichment { get; init; }

    /// <summary>
    /// The current manager-authored customer status note (<c>Spec C-9</c>), or <c>null</c> when
    /// none is set. Shown to the customer on the status page; editable only from the manager app.
    /// </summary>
    public CustomerStatusNoteDto? CustomerStatusNote { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
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
