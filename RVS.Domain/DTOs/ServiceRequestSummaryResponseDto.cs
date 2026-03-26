namespace RVS.Domain.DTOs;

/// <summary>
/// Lightweight summary of a service request for list and search views.
/// </summary>
public sealed record ServiceRequestSummaryResponseDto
{
    public string Id { get; init; } = default!;
    public string LocationId { get; init; } = default!;
    public string? LocationName { get; init; }
    public string Status { get; init; } = default!;
    public string CustomerFullName { get; init; } = default!;
    public string? AssetDisplay { get; init; }
    public string IssueCategory { get; init; } = default!;
    public string? TechnicianSummary { get; init; }
    public int AttachmentCount { get; init; }
    public string? AssignedTechnicianId { get; init; }
    public string? Priority { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
