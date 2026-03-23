namespace RVS.Domain.DTOs;

/// <summary>
/// Full detail response for a dealership, including intake configuration.
/// </summary>
public sealed record DealershipDetailDto
{
    public string Id { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string? LogoUrl { get; init; }
    public string? ServiceEmail { get; init; }
    public string? Phone { get; init; }
    public IntakeConfigDto? IntakeConfig { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
