namespace RVS.Domain.DTOs;

/// <summary>
/// Platform-admin request to add an Auth0 user to a tenant (Spec P-2, issue #563). Adding an
/// email that already belongs to this tenant updates the user and issues a fresh set-password link.
/// </summary>
public sealed record TenantUserCreateRequestDto
{
    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    /// <summary><c>dealer:owner</c>, <c>dealer:manager</c>, <c>dealer:advisor</c> or <c>dealer:readonly</c>.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Locations the user can access. Required for location-scoped roles; ignored for <c>dealer:owner</c>.</summary>
    public List<string> LocationIds { get; init; } = [];
}
