using FluentAssertions;
using Moq;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class AnalyticsServiceTests
{
    private readonly Mock<IServiceRequestRepository> _repoMock = new();
    private readonly AnalyticsService _sut;

    public AnalyticsServiceTests()
    {
        _sut = new AnalyticsService(_repoMock.Object);
    }

    // ── Guard Clauses ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetServiceRequestSummaryAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetServiceRequestSummaryAsync(tenantId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Empty Results ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_WhenNoRequests_ShouldReturnEmptyDto()
    {
        _repoMock.Setup(r => r.GetForAnalyticsAsync("ten_1", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceRequest>());

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.TotalRequests.Should().Be(0);
        result.RequestsByStatus.Should().BeEmpty();
        result.RequestsByCategory.Should().BeEmpty();
        result.RequestsByLocation.Should().BeEmpty();
        result.TopFailureModes.Should().BeEmpty();
        result.TopRepairActions.Should().BeEmpty();
        result.TopPartsUsed.Should().BeEmpty();
        result.AverageRepairTimeHours.Should().BeNull();
        result.AverageDaysToComplete.Should().BeNull();
    }

    // ── Filter Pass-Through ──────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldPassFiltersToRepository()
    {
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var locationId = "loc_slc";

        _repoMock.Setup(r => r.GetForAnalyticsAsync("ten_1", from, to, locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceRequest>());

        await _sut.GetServiceRequestSummaryAsync("ten_1", from, to, locationId);

        _repoMock.Verify(r => r.GetForAnalyticsAsync("ten_1", from, to, locationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── TotalRequests ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldReturnCorrectTotalRequests()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(status: "New"),
            BuildServiceRequest(status: "InProgress"),
            BuildServiceRequest(status: "Completed")
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.TotalRequests.Should().Be(3);
    }

    // ── RequestsByStatus ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldGroupByStatus()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(status: "New"),
            BuildServiceRequest(status: "New"),
            BuildServiceRequest(status: "InProgress"),
            BuildServiceRequest(status: "Completed"),
            BuildServiceRequest(status: "Completed"),
            BuildServiceRequest(status: "Completed")
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.RequestsByStatus.Should().ContainKey("New").WhoseValue.Should().Be(2);
        result.RequestsByStatus.Should().ContainKey("InProgress").WhoseValue.Should().Be(1);
        result.RequestsByStatus.Should().ContainKey("Completed").WhoseValue.Should().Be(3);
    }

    // ── RequestsByCategory ───────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldGroupByCategory()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(issueCategory: "Slide System"),
            BuildServiceRequest(issueCategory: "Slide System"),
            BuildServiceRequest(issueCategory: "Plumbing"),
            BuildServiceRequest(issueCategory: null)
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.RequestsByCategory.Should().HaveCount(2);
        result.RequestsByCategory.Should().ContainKey("Slide System").WhoseValue.Should().Be(2);
        result.RequestsByCategory.Should().ContainKey("Plumbing").WhoseValue.Should().Be(1);
    }

    // ── RequestsByLocation ───────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldGroupByLocation()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(locationId: "loc_slc"),
            BuildServiceRequest(locationId: "loc_slc"),
            BuildServiceRequest(locationId: "loc_den")
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.RequestsByLocation.Should().ContainKey("loc_slc").WhoseValue.Should().Be(2);
        result.RequestsByLocation.Should().ContainKey("loc_den").WhoseValue.Should().Be(1);
    }

    // ── TopFailureModes ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldReturnTopFailureModes()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(failureMode: "Motor Failure"),
            BuildServiceRequest(failureMode: "Motor Failure"),
            BuildServiceRequest(failureMode: "Seal Leak"),
            BuildServiceRequest(failureMode: null)
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.TopFailureModes.Should().HaveCount(2);
        result.TopFailureModes[0].Name.Should().Be("Motor Failure");
        result.TopFailureModes[0].Count.Should().Be(2);
        result.TopFailureModes[1].Name.Should().Be("Seal Leak");
        result.TopFailureModes[1].Count.Should().Be(1);
    }

    // ── TopRepairActions ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldReturnTopRepairActions()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(repairAction: "Replaced Motor"),
            BuildServiceRequest(repairAction: "Replaced Motor"),
            BuildServiceRequest(repairAction: "Sealed Joint")
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.TopRepairActions.Should().HaveCount(2);
        result.TopRepairActions[0].Name.Should().Be("Replaced Motor");
        result.TopRepairActions[0].Count.Should().Be(2);
    }

    // ── TopPartsUsed ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldReturnTopPartsUsed()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(partsUsed: ["Slide Motor", "Bolt Kit"]),
            BuildServiceRequest(partsUsed: ["Slide Motor"]),
            BuildServiceRequest(partsUsed: [])
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.TopPartsUsed.Should().HaveCount(2);
        result.TopPartsUsed[0].Name.Should().Be("Slide Motor");
        result.TopPartsUsed[0].Count.Should().Be(2);
        result.TopPartsUsed[1].Name.Should().Be("Bolt Kit");
        result.TopPartsUsed[1].Count.Should().Be(1);
    }

    // ── AverageRepairTimeHours ───────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldCalculateAverageRepairTimeHours()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(laborHours: 2.0m),
            BuildServiceRequest(laborHours: 4.0m),
            BuildServiceRequest(laborHours: null)
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.AverageRepairTimeHours.Should().Be(3.0m);
    }

    [Fact]
    public async Task GetServiceRequestSummaryAsync_WhenNoLaborHours_ShouldReturnNullAverageRepairTime()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(laborHours: null),
            BuildServiceRequest(laborHours: null)
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.AverageRepairTimeHours.Should().BeNull();
    }

    // ── AverageDaysToComplete ────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceRequestSummaryAsync_ShouldCalculateAverageDaysToComplete()
    {
        var baseDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(status: "Completed", createdAtUtc: baseDate, updatedAtUtc: baseDate.AddDays(2)),
            BuildServiceRequest(status: "Completed", createdAtUtc: baseDate, updatedAtUtc: baseDate.AddDays(4)),
            BuildServiceRequest(status: "New", createdAtUtc: baseDate, updatedAtUtc: baseDate.AddDays(10))
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.AverageDaysToComplete.Should().Be(3.0m);
    }

    [Fact]
    public async Task GetServiceRequestSummaryAsync_WhenNoCompletedRequests_ShouldReturnNullAverageDays()
    {
        var requests = new List<ServiceRequest>
        {
            BuildServiceRequest(status: "New"),
            BuildServiceRequest(status: "InProgress")
        };
        SetupRepository(requests);

        var result = await _sut.GetServiceRequestSummaryAsync("ten_1");

        result.AverageDaysToComplete.Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetupRepository(List<ServiceRequest> requests)
    {
        _repoMock.Setup(r => r.GetForAnalyticsAsync("ten_1", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);
    }

    private static ServiceRequest BuildServiceRequest(
        string status = "New",
        string? issueCategory = "General",
        string locationId = "loc_slc",
        string? failureMode = null,
        string? repairAction = null,
        List<string>? partsUsed = null,
        decimal? laborHours = null,
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null)
    {
        var sr = new ServiceRequest
        {
            TenantId = "ten_1",
            Status = status,
            LocationId = locationId,
            IssueCategory = issueCategory,
            CustomerSnapshot = new CustomerSnapshotEmbedded
            {
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com"
            }
        };

        if (failureMode is not null || repairAction is not null || partsUsed is not null || laborHours is not null)
        {
            sr.ServiceEvent = new ServiceEventEmbedded
            {
                FailureMode = failureMode,
                RepairAction = repairAction,
                PartsUsed = partsUsed ?? [],
                LaborHours = laborHours
            };
        }

        // Use reflection to set init-only CreatedAtUtc when provided
        if (createdAtUtc.HasValue)
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.CreatedAtUtc))!
                .SetValue(sr, createdAtUtc.Value);
        }

        if (updatedAtUtc.HasValue)
        {
            sr.MarkAsUpdated();
            typeof(EntityBase).GetProperty(nameof(EntityBase.UpdatedAtUtc))!
                .SetValue(sr, updatedAtUtc.Value);
        }

        return sr;
    }
}
