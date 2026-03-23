namespace RVS.Domain.DTOs;

/// <summary>
/// Paged search result containing service request summaries.
/// </summary>
public sealed record ServiceRequestSearchResultResponseDto
{
    public PagedResult<ServiceRequestSummaryResponseDto> Results { get; init; } = new();
}
