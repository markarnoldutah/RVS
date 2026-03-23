using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class BatchAndAnalyticsDtoTests
{
    [Fact]
    public void BatchOutcomeRequestDto_SetsRequiredFields()
    {
        var dto = new BatchOutcomeRequestDto
        {
            ServiceRequestIds = ["sr-1", "sr-2", "sr-3"]
        };

        dto.ServiceRequestIds.Should().HaveCount(3);
        dto.FailureMode.Should().BeNull();
        dto.RepairAction.Should().BeNull();
        dto.PartsUsed.Should().BeNull();
        dto.LaborHours.Should().BeNull();
    }

    [Fact]
    public void BatchOutcomeResponseDto_DefaultCollectionsAreEmpty()
    {
        var dto = new BatchOutcomeResponseDto();

        dto.Succeeded.Should().BeEmpty();
        dto.Failed.Should().BeEmpty();
    }

    [Fact]
    public void BatchOutcomeFailureDto_SetsRequiredFields()
    {
        var dto = new BatchOutcomeFailureDto
        {
            ServiceRequestId = "sr-1",
            Reason = "Not found"
        };

        dto.ServiceRequestId.Should().Be("sr-1");
        dto.Reason.Should().Be("Not found");
    }

    [Fact]
    public void ServiceRequestAnalyticsResponseDto_DefaultCollectionsAreEmpty()
    {
        var dto = new ServiceRequestAnalyticsResponseDto();

        dto.TotalRequests.Should().Be(0);
        dto.RequestsByStatus.Should().BeEmpty();
        dto.RequestsByCategory.Should().BeEmpty();
        dto.RequestsByLocation.Should().BeEmpty();
        dto.TopFailureModes.Should().BeEmpty();
        dto.TopRepairActions.Should().BeEmpty();
        dto.TopPartsUsed.Should().BeEmpty();
        dto.AverageRepairTimeHours.Should().BeNull();
        dto.AverageDaysToComplete.Should().BeNull();
    }

    [Fact]
    public void AnalyticsRankItem_PositionalRecordConstruction()
    {
        var item = new AnalyticsRankItem("Hydraulic Pump", 42);

        item.Name.Should().Be("Hydraulic Pump");
        item.Count.Should().Be(42);
    }

    [Fact]
    public void AnalyticsRankItem_RecordEquality()
    {
        var item1 = new AnalyticsRankItem("Motor", 10);
        var item2 = new AnalyticsRankItem("Motor", 10);

        item1.Should().Be(item2);
    }
}
