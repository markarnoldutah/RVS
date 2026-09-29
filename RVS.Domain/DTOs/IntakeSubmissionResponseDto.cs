namespace RVS.Domain.DTOs;

/// <summary>
/// Response returned after a successful intake submission, containing the created service request
/// and the magic-link token for checking request status.
/// </summary>
public sealed record IntakeSubmissionResponseDto
{
    /// <summary>Full detail of the created service request — the first, when the submission reported several problems.</summary>
    public ServiceRequestDetailResponseDto ServiceRequest { get; init; } = default!;

    /// <summary>
    /// The id of every service request the submission created, one per problem in the order the
    /// customer reported them (<c>Spec A-17</c>). The first is <see cref="ServiceRequest"/>'s. The
    /// intake app uploads each problem's attachments to the request at the same position.
    /// </summary>
    public List<string> ServiceRequestIds { get; init; } = [];

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
