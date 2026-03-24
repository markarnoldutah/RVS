namespace RVS.Domain.DTOs;

/// <summary>
/// Full detail response for a customer profile, including asset ownership and request history.
/// </summary>
public sealed record CustomerProfileDetailResponseDto
{
    public string Id { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string? Phone { get; init; }
    public string GlobalCustomerAcctId { get; init; } = default!;
    public int TotalRequestCount { get; init; }
    public List<string> ServiceRequestIds { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
