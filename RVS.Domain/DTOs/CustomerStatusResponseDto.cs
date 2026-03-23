namespace RVS.Domain.DTOs;

/// <summary>
/// Customer-facing status view showing service request summaries across dealerships.
/// </summary>
public sealed record CustomerStatusResponseDto
{
    public string FirstName { get; init; } = default!;
    public List<ServiceRequestSummaryResponseDto> ServiceRequests { get; init; } = [];
}
