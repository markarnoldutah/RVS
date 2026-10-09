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

    /// <summary>
    /// The <c>Spec C-4</c> disposition reason code when the request was closed without work;
    /// <c>null</c> for every other request, including a plain <c>Cancelled</c>.
    /// </summary>
    public string? DispositionReasonCode { get; init; }
    public string CustomerFullName { get; init; } = default!;
    public string? AssetDisplay { get; init; }
    public string IssueCategory { get; init; } = default!;
    public string? TechnicianSummary { get; init; }
    public int AttachmentCount { get; init; }
    public string? AssignedTechnicianId { get; init; }
    public string? Priority { get; init; }
    public int BoardSequence { get; init; }

    /// <summary>How the operator will do the work (<c>Spec C-11</c>); null until triaged.</summary>
    public string? JobType { get; init; }

    /// <summary>The booked start (<c>Spec C-12</c>) as a UTC instant, for sorting; null when unscheduled.</summary>
    public DateTime? ScheduledStartUtc { get; init; }

    /// <summary>"Thu Oct 15 · 9:00 AM MDT", always in the stored zone; null when unscheduled.</summary>
    public string? ScheduledDisplay { get; init; }

    /// <summary>
    /// The multi-issue intake submission this request belongs to (<c>Spec A-17</c>): its first
    /// request's id. <c>null</c> for a request reported on its own.
    /// </summary>
    public string? SubmissionId { get; init; }

    /// <summary>This request's 1-based position in its submission — the "2" of "Issue 2 of 3". <c>0</c> when not grouped.</summary>
    public int SubmissionPosition { get; init; }

    /// <summary>How many issues its submission reported — the "3" of "Issue 2 of 3". <c>0</c> when not grouped.</summary>
    public int SubmissionCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
