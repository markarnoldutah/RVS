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
}
