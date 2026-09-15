namespace RVS.Domain.DTOs;

/// <summary>
/// Outcome of adding a user to a tenant (Spec P-2, issue #563).
/// </summary>
public sealed record TenantUserProvisioningResponseDto
{
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;

    /// <summary><c>created</c>, or <c>already existed</c> when the email was already a user in this tenant.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Auth0 set-password link. Shown once; never stored or logged.</summary>
    public string PasswordTicketUrl { get; init; } = string.Empty;

    public DateTime PasswordTicketExpiresAtUtc { get; init; }
}
