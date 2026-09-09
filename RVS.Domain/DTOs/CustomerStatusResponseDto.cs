namespace RVS.Domain.DTOs;

/// <summary>
/// Customer-facing status view (<c>Spec X-1</c>). Deliberately minimal: one entry per
/// linked service request, each showing only the unit, submission date, current status,
/// and the servicing location's phone number. No customer identity, no issue text, and
/// no conversation, messaging, or file exchange is exposed here.
/// </summary>
public sealed record CustomerStatusResponseDto
{
    /// <summary>One entry per service request linked to the status token.</summary>
    public List<CustomerStatusItemResponseDto> ServiceRequests { get; init; } = [];
}

/// <summary>
/// A single service request as shown on the customer status page (<c>Spec X-1</c>).
/// These four fields are the entire contract — nothing more may be added without a
/// spec change.
/// </summary>
public sealed record CustomerStatusItemResponseDto
{
    /// <summary>The unit under service — "year make model", or <c>null</c> when unknown.</summary>
    public string? Unit { get; init; }

    /// <summary>When the customer submitted the request (UTC).</summary>
    public DateTime SubmittedAtUtc { get; init; }

    /// <summary>Current status, e.g. <c>New</c>, <c>InProgress</c>, <c>Completed</c>.</summary>
    public string Status { get; init; } = default!;

    /// <summary>Phone number of the servicing location, or <c>null</c> when not on file.</summary>
    public string? LocationPhone { get; init; }
}
