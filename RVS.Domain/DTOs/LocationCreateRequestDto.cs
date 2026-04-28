using System.Collections.Generic;

namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for creating a new service location.
/// </summary>
public sealed record LocationCreateRequestDto
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Phone { get; init; }
    public AddressDto? Address { get; init; }
    public IntakeConfigDto? IntakeConfig { get; init; }

    /// <summary>
    /// Capability codes (from the tenant's available capabilities) that are enabled
    /// for this location. Pass null to leave existing capabilities unchanged on update.
    /// </summary>
    public List<string>? EnabledCapabilities { get; init; }
}
