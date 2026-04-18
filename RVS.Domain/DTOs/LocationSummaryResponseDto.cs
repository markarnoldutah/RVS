namespace RVS.Domain.DTOs;

/// <summary>
/// Summary response for a location, containing enough data for table display
/// without requiring a separate detail call per location.
/// </summary>
public sealed record LocationSummaryResponseDto
{
    public string LocationId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string? Phone { get; init; }
    public AddressDto? Address { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
