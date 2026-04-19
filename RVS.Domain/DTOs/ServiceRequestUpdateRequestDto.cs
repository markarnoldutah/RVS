namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for updating an existing service request.
/// Carries the updatable fields plus <see cref="UpdatedAtUtc"/> for optimistic concurrency.
/// </summary>
public sealed record ServiceRequestUpdateRequestDto
{
    public required string Status { get; init; }
    public required string IssueDescription { get; init; }
    public string? IssueCategory { get; init; }
    public string? TechnicianSummary { get; init; }
    public required string Priority { get; init; }
    public string? Urgency { get; init; }
    public string? RvUsage { get; init; }
    public string? HasExtendedWarranty { get; init; }
    public string? ApproxPurchaseDate { get; init; }
    public string? AssignedTechnicianId { get; init; }
    public string? AssignedBayId { get; init; }
    public DateTime? ScheduledDateUtc { get; init; }
    public List<string> RequiredSkills { get; init; } = [];
    public ServiceEventDto? ServiceEvent { get; init; }

    /// <summary>
    /// Board display order within a status column. Lower values appear first.
    /// </summary>
    public int? BoardSequence { get; init; }

    /// <summary>
    /// Last-known <c>UpdatedAtUtc</c> value for optimistic concurrency validation.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; init; }
}
