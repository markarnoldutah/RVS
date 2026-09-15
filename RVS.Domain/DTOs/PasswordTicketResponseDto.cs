namespace RVS.Domain.DTOs;

/// <summary>
/// A newly issued Auth0 set-password link for an existing user (Spec P-3, issue #563).
/// Shown once; never stored or logged.
/// </summary>
public sealed record PasswordTicketResponseDto
{
    public string UserId { get; init; } = string.Empty;
    public string PasswordTicketUrl { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}
