using MudBlazor;
using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// The browser's half of the service-request search (issue #849). The API applies every filter and
/// returns the whole matching set; the list sorts and pages it here rather than on the server.
/// </summary>
public static class ServiceRequestListView
{
    /// <summary>Shown above the list when the API returned only the newest <see cref="ServiceRequestSearch.MaxListResults"/> rows.</summary>
    public static string TruncatedNotice { get; } =
        $"Showing the newest {ServiceRequestSearch.MaxListResults}. Narrow your filters to see older requests.";

    /// <summary>The board's search (<c>Spec C-10</c>): open work plus the Done window, at one location.</summary>
    /// <param name="locationId">The selected location, or <c>null</c> for all.</param>
    public static ServiceRequestSearchRequestDto BoardRequest(string? locationId) => new()
    {
        Scope = ServiceRequestSearch.BoardScope,
        LocationId = locationId,
    };

    private static readonly IReadOnlyDictionary<string, int> PriorityRank =
        new Dictionary<string, int>(StringComparer.Ordinal) { ["Critical"] = 0, ["High"] = 1, ["Medium"] = 2, ["Low"] = 3 };

    /// <summary>
    /// The Priority column's sort key: most urgent first when ascending, unset or unknown last.
    /// </summary>
    public static int PrioritySortKey(ServiceRequestSummaryResponseDto row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.Priority is { } priority && PriorityRank.TryGetValue(priority, out var rank) ? rank : PriorityRank.Count;
    }

    /// <summary>
    /// The Scheduled column's sort key (<c>Spec C-1</c> / C-12): the booked instant, with
    /// unscheduled requests last in both directions. The table reverses the key when sorting
    /// descending, so an unset start maps to the end that the reversal moves last.
    /// </summary>
    public static DateTime ScheduledSortKey(ServiceRequestSummaryResponseDto row, SortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.ScheduledStartUtc
            ?? (direction == SortDirection.Descending ? DateTime.MinValue : DateTime.MaxValue);
    }
}
