namespace RVS.Domain.DTOs;

/// <summary>
/// Platform-admin request to add a location to an existing tenant (Spec P-5, issue #563).
/// </summary>
public sealed record TenantLocationCreateRequestDto
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional slug. When omitted it is generated from the dealership and location names.</summary>
    public string? Slug { get; init; }

    public string? Phone { get; init; }

    /// <summary>1–10 packet-email recipients.</summary>
    public List<string> Recipients { get; init; } = [];
}
