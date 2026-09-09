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
    /// Raw per-customer status token for checking request status (Spec X-5). A fresh token is
    /// issued on every submission; only its SHA-256 hash is persisted, and it expires after 30 days
    /// (sliding renewal on use).
    /// </summary>
    public string? MagicLinkToken { get; init; }
}
