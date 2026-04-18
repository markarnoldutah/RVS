namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for creating a new service location.
/// When <see cref="Slug"/> is omitted or empty the API will auto-generate
/// a slug from the dealership name and location name.
/// </summary>
public sealed record LocationCreateRequestDto
{
    public required string Name { get; init; }
    public string? Slug { get; init; }
    public string? Phone { get; init; }
    public AddressDto? Address { get; init; }
    public IntakeConfigDto? IntakeConfig { get; init; }
}
