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

    /// <summary>
    /// The customer's preferred contact method — one of <c>Phone</c>, <c>Text</c>, or
    /// <c>Email</c> (see <see cref="Validation.PreferredContactMethod"/>). Required in the
    /// intake wizard; <c>null</c> on detail responses for requests created before it was
    /// captured.
    /// </summary>
    public string? PreferredContact { get; init; }
}
