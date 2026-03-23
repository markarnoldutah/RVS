namespace RVS.Domain.DTOs;

/// <summary>
/// Mailing/physical address information for a location.
/// </summary>
public sealed record AddressDto
{
    public string? Address1 { get; init; }
    public string? Address2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
}
