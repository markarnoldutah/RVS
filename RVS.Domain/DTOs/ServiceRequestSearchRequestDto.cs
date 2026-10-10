namespace RVS.Domain.DTOs;

/// <summary>
/// Search/filter criteria for querying service requests. Every filter runs on the server, so a
/// result is the whole matching set; the manager app only sorts and pages it (issue #849).
/// </summary>
public sealed record ServiceRequestSearchRequestDto
{
    /// <summary>
    /// <c>List</c> (the default when blank) or <c>Board</c>; see
    /// <see cref="Validation.ServiceRequestSearch"/>. Any other value is rejected with a 400.
    /// </summary>
    public string? Scope { get; init; }

    public string? Keyword { get; init; }

    /// <summary>
    /// A C-3 status, or <see cref="Validation.ServiceRequestSearch.OpenStatusFilter"/> for every
    /// request that is not Completed or Cancelled.
    /// </summary>
    public string? Status { get; init; }
    public string? LocationId { get; init; }

    /// <summary>An <see cref="Validation.IssueCategoryVocabulary"/> code, matched exactly; any other value is a 400.</summary>
    public string? IssueCategory { get; init; }

    /// <summary>A priority, matched exactly.</summary>
    public string? Priority { get; init; }

    /// <summary>Part of an assigned technician's id, matched ignoring case.</summary>
    public string? AssignedTechnicianId { get; init; }

    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }

    /// <summary>
    /// Job-type filter (<c>Spec C-1</c> / C-11): a job-type code, or <c>JobTypes.NotTriagedFilter</c>
    /// for requests with none.
    /// </summary>
    public string? JobType { get; init; }
}
