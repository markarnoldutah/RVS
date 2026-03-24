namespace RVS.Domain.DTOs;

/// <summary>
/// Vehicle/asset information associated with a service request.
/// </summary>
public sealed record AssetInfoDto
{
    public required string AssetId { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public int? Year { get; init; }
}
