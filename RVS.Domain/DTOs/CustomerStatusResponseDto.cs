namespace RVS.Domain.DTOs;

/// <summary>
/// Customer-facing status view (<c>Spec X-1</c>). One entry per linked service request. The
/// surface is display-only for the customer: there is no field here, and no endpoint anywhere,
/// that lets the customer send anything back — no reply, no inbound message, no file upload
/// (<c>Spec X-1</c>, one hard constraint). What it <i>shows</i> is free to grow as it becomes
/// useful; today that is the unit, issue category, submission date, current status, the
/// servicing location's name, logo and phone number, the scheduled date/time
/// (<c>Spec C-12</c>), and any manager-authored status note (<c>Spec C-9</c>).
/// No customer identity and no free-text problem description ever cross this boundary.
/// </summary>
public sealed record CustomerStatusResponseDto
{
    /// <summary>One entry per service request linked to the status token.</summary>
    public List<CustomerStatusItemResponseDto> ServiceRequests { get; init; } = [];

    /// <summary>
    /// When the status token used for this request stops working (UTC), or <c>null</c> when it does
    /// not expire. Lets the intake app remember a link opened from an email for exactly as long as
    /// it will resolve (issue #716).
    /// </summary>
    public DateTime? MagicLinkExpiresAtUtc { get; init; }
}

/// <summary>
/// A single service request as shown on the customer status page (<c>Spec X-1</c> / <c>C-9</c>).
/// Everything here is read-only for the customer.
/// </summary>
public sealed record CustomerStatusItemResponseDto
{
    /// <summary>The unit under service — "year make model", or <c>null</c> when unknown.</summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Display name of the issue category (e.g. <c>Plumbing &amp; Water</c>), or <c>null</c> when
    /// none was recorded. A controlled-vocabulary label, never the customer's free text (issue #741).
    /// </summary>
    public string? IssueCategory { get; init; }

    /// <summary>When the customer submitted the request (UTC).</summary>
    public DateTime SubmittedAtUtc { get; init; }

    /// <summary>Current status, e.g. <c>New</c>, <c>InProgress</c>, <c>Completed</c>.</summary>
    public string Status { get; init; } = default!;

    /// <summary>Phone number of the servicing location, or <c>null</c> when not on file.</summary>
    public string? LocationPhone { get; init; }

    /// <summary>
    /// Name of the servicing location, or <c>null</c> when unknown. Shown beside the logo, and
    /// in its place when there is none, so a customer with requests at several dealers can tell
    /// the cards apart (issue #793).
    /// </summary>
    public string? LocationName { get; init; }

    /// <summary>
    /// Absolute https URL of the servicing location's logo (<c>Spec A-16</c> branding), or
    /// <c>null</c> when none is set (issue #793).
    /// </summary>
    public string? LocationLogoUrl { get; init; }

    /// <summary>
    /// The current manager-authored status note (<c>Spec C-9</c>), or <c>null</c> when none is set.
    /// Plain text, shown verbatim as an advisory line. One-directional — the customer cannot reply.
    /// </summary>
    public string? StatusNote { get; init; }

    /// <summary>
    /// When the work is booked (<c>Spec C-12</c>), already formatted in the zone it was booked in —
    /// "Thu Oct 15 · 9:00 AM MDT", or "Thu Oct 15" with no time — or <c>null</c> when unscheduled.
    /// </summary>
    public string? ScheduledDisplay { get; init; }
}
