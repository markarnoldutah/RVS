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
    public string? Priority { get; init; }
    public string? AssignedTechnicianId { get; init; }
    public string? AssignedBayId { get; init; }
    public DateTime? ScheduledDateUtc { get; init; }
    public List<string> RequiredSkills { get; init; } = [];
    public List<DiagnosticResponseDto> DiagnosticResponses { get; init; } = [];
    public List<AttachmentDto> Attachments { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
