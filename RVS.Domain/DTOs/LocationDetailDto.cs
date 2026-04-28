using System.Collections.Generic;

namespace RVS.Domain.DTOs;

/// <summary>
/// Full detail response for a location, including address and intake configuration.
/// </summary>
public sealed record LocationDetailDto
{
    public string Id { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string? Phone { get; init; }
    public AddressDto? Address { get; init; }
    public IntakeConfigDto? IntakeConfig { get; init; }

    /// <summary>
    /// Capability codes that are enabled for this location.
    /// </summary>
    public List<string> EnabledCapabilities { get; init; } = [];

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
