namespace RVS.Domain.DTOs;

/// <summary>
/// Platform-admin edit of an existing tenant user (Spec P-10, issue #647). Replaces the name, the
/// role and the locations together; the email cannot be changed — delete and re-add instead.
/// </summary>
public sealed record TenantUserUpdateRequestDto
{
    public string DisplayName { get; init; } = string.Empty;

    /// <summary><c>dealer:owner</c>, <c>dealer:manager</c>, <c>dealer:advisor</c> or <c>dealer:readonly</c>. Replaces the user's other dealer roles.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Locations the user can access. Required for location-scoped roles; cleared for <c>dealer:owner</c>.</summary>
    public List<string> LocationIds { get; init; } = [];
}
