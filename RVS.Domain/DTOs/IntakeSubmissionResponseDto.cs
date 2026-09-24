namespace RVS.Domain.DTOs;

/// <summary>
/// Response returned after a successful intake submission, containing the created service request
/// and the magic-link token for checking request status.
/// </summary>
public sealed record IntakeSubmissionResponseDto
{
    /// <summary>Full detail of the created service request.</summary>
    public ServiceRequestDetailResponseDto ServiceRequest { get; init; } = default!;

    /// <summary>
    /// Magic-link token that can be used to check the status of the service request.
    /// The token is generated or reused during the intake orchestration and has a 90-day expiry.
    /// </summary>
    public string? MagicLinkToken { get; init; }

    /// <summary>
    /// When <see cref="MagicLinkToken"/> stops working (UTC), or <c>null</c> when it does not expire.
    /// A reused token keeps its original expiry, so the intake app is told rather than left to
    /// assume a fresh TTL when it remembers the link on the device (issue #716).
    /// </summary>
    public DateTime? MagicLinkExpiresAtUtc { get; init; }
}
