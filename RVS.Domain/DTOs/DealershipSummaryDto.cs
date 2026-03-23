namespace RVS.Domain.DTOs;

/// <summary>
/// Lightweight summary of a dealership for list views.
/// </summary>
public sealed record DealershipSummaryDto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string? Phone { get; init; }
}
