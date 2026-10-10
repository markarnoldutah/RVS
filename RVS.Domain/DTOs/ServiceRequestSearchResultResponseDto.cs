namespace RVS.Domain.DTOs;

/// <summary>
/// Every service request a search matched, as one page of summaries (issue #849).
/// </summary>
public sealed record ServiceRequestSearchResultResponseDto
{
    public PagedResult<ServiceRequestSummaryResponseDto> Results { get; init; } = new();

    /// <summary>
    /// Whether more requests matched than the list returns
    /// (<see cref="Validation.ServiceRequestSearch.MaxListResults"/>); the list tells the user to
    /// narrow the filters.
    /// </summary>
    public bool IsTruncated { get; init; }
}
