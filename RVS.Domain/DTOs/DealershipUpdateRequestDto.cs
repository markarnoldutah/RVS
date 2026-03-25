namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for updating an existing dealership.
/// </summary>
public sealed record DealershipUpdateRequestDto
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? LogoUrl { get; init; }
    public string? ServiceEmail { get; init; }
    public string? Phone { get; init; }
    public IntakeConfigDto? IntakeConfig { get; init; }
}
