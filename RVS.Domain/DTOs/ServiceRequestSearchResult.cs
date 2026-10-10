using RVS.Domain.Entities;

namespace RVS.Domain.DTOs;

/// <summary>
/// The service requests a search matched (issue #849): the whole set for the board scope, or up
/// to <see cref="Validation.ServiceRequestSearch.MaxListResults"/> for the list scope.
/// </summary>
public sealed record ServiceRequestSearchResult
{
    /// <summary>The matching requests, newest first.</summary>
    public IReadOnlyList<ServiceRequest> Items { get; init; } = [];

    /// <summary>Whether more requests matched than the list scope returns.</summary>
    public bool IsTruncated { get; init; }
}
