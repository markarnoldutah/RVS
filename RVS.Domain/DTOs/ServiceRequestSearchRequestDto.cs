namespace RVS.Domain.DTOs;

/// <summary>
/// Search/filter criteria for querying service requests.
/// </summary>
public sealed record ServiceRequestSearchRequestDto
{
    public string? Keyword { get; init; }
    public string? Status { get; init; }
    public string? IssueCategory { get; init; }
    public string? LocationId { get; init; }
    public string? AssignedTechnicianId { get; init; }
    public string? AssetId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? Priority { get; init; }

    /// <summary>
    /// Job-type filter (<c>Spec C-1</c> / C-11): a job-type code, or <c>JobTypes.NotTriagedFilter</c>
    /// for requests with none.
    /// </summary>
    public string? JobType { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
