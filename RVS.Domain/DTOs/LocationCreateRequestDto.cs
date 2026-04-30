using System.Collections.Generic;

namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for creating a new service location.
/// </summary>
public sealed record LocationCreateRequestDto
{
    public required string Name { get; init; }

    /// <summary>
    /// Optional URL-safe slug for the location's intake form path (e.g., <c>camping-world-salt-lake</c>).
    /// When omitted on create, the server auto-generates a slug from the dealership ("org") slug
    /// plus the location name and ensures uniqueness by appending a numeric suffix on collision.
    /// On update, leave null/empty to preserve the existing slug.
    /// </summary>
    public string? Slug { get; init; }
    public string? Phone { get; init; }
    public AddressDto? Address { get; init; }
    public IntakeConfigDto? IntakeConfig { get; init; }

    /// <summary>
    /// Capability codes (from the tenant's available capabilities) that are enabled
    /// for this location. Pass null to leave existing capabilities unchanged on update.
    /// </summary>
    public List<string>? EnabledCapabilities { get; init; }
}
