namespace RVS.Domain.DTOs;

/// <summary>
/// One advisor intake invite as the manager app's send dialog lists it (<c>Spec A-14</c>,
/// issue #663): who it went to, and where it got to.
/// </summary>
public record IntakeInviteSummaryResponseDto
{
    /// <summary>Invite identifier. Not the token: the token is never returned for a texted invite.</summary>
    public required string Id { get; init; }

    /// <summary>The location whose intake form the invite opens.</summary>
    public required string LocationId { get; init; }

    /// <summary>The caller's first name.</summary>
    public required string FirstName { get; init; }

    /// <summary>The caller's phone in E.164, or <c>null</c> for a self-entry invite without one.</summary>
    public string? Phone { get; init; }

    /// <summary><c>true</c> for a <i>Fill it in myself</i> invite.</summary>
    public bool IsSelfEntry { get; init; }

    /// <summary>When the invite was created.</summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>When the text was handed to ACS, or <c>null</c> if it never was.</summary>
    public DateTime? SentAtUtc { get; init; }

    /// <summary>When the link stops working.</summary>
    public DateTime ExpiresAtUtc { get; init; }

    /// <summary>When the resulting intake was submitted, or <c>null</c>.</summary>
    public DateTime? RedeemedAtUtc { get; init; }

    /// <summary>
    /// <c>pending</c>, <c>queued</c>, <c>delivered</c>, <c>failed</c> or <c>notSent</c>
    /// (see <c>IntakeInviteDeliveryStatus</c>).
    /// </summary>
    public required string DeliveryStatus { get; init; }
}
