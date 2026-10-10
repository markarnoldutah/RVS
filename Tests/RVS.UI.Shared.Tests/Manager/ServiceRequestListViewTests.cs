using FluentAssertions;
using MudBlazor;
using RVS.Blazor.Manager.Shared;
using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// The client half of the service-request list and board (issue #849). The server applies every
/// filter and returns the whole matching set; the browser only sorts and pages it, with
/// unscheduled requests last whichever way the Scheduled column is sorted.
/// </summary>
public class ServiceRequestListViewTests
{
    private static readonly DateTime Thursday = new(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Friday = new(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc);

    // ── ScheduledSortKey ─────────────────────────────────────────────────────

    [Fact]
    public void ScheduledSortKey_Ascending_ShouldPutSoonestFirstAndUnscheduledLast()
    {
        var rows = new[] { Row("none"), Row("fri", scheduled: Friday), Row("thu", scheduled: Thursday) };

        rows.OrderBy(r => ServiceRequestListView.ScheduledSortKey(r, SortDirection.Ascending))
            .Select(r => r.Id).Should().Equal("thu", "fri", "none");
    }

    [Fact]
    public void ScheduledSortKey_Descending_ShouldPutLatestFirstAndUnscheduledStillLast()
    {
        var rows = new[] { Row("none"), Row("thu", scheduled: Thursday), Row("fri", scheduled: Friday) };

        rows.OrderByDescending(r => ServiceRequestListView.ScheduledSortKey(r, SortDirection.Descending))
            .Select(r => r.Id).Should().Equal("fri", "thu", "none");
    }

    // ── PrioritySortKey ──────────────────────────────────────────────────────

    [Fact]
    public void PrioritySortKey_Ascending_ShouldOrderByUrgencyWithUnsetLast()
    {
        var rows = new[] { Row("none"), Row("low", priority: "Low"), Row("crit", priority: "Critical"), Row("med", priority: "Medium"), Row("high", priority: "High") };

        rows.OrderBy(ServiceRequestListView.PrioritySortKey)
            .Select(r => r.Id).Should().Equal("crit", "high", "med", "low", "none");
    }

    // ── Defaults and requests ────────────────────────────────────────────────

    [Fact]
    public void TruncatedNotice_ShouldNameTheCap()
    {
        ServiceRequestListView.TruncatedNotice.Should()
            .Be($"Showing the newest {ServiceRequestSearch.MaxListResults}. Narrow your filters to see older requests.");
    }

    [Fact]
    public void BoardRequest_ShouldAskForTheBoardScopeAtTheSelectedLocation()
    {
        var request = ServiceRequestListView.BoardRequest("loc_1");

        request.Scope.Should().Be(ServiceRequestSearch.BoardScope);
        request.LocationId.Should().Be("loc_1");
        request.Status.Should().BeNull();
        request.Keyword.Should().BeNull();
    }

    private static ServiceRequestSummaryResponseDto Row(
        string id,
        string? priority = null,
        DateTime? scheduled = null) => new()
    {
        Id = id,
        Priority = priority,
        ScheduledStartUtc = scheduled,
    };
}
