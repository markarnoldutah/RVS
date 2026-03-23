namespace RVS.Domain.DTOs;

/// <summary>
/// Customer contact information used in service request creation and detail views.
/// </summary>
public sealed record CustomerInfoDto
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
}
