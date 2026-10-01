using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class AnalyticsDtoTests
{
    [Fact]
    public void ServiceRequestAnalyticsResponseDto_DefaultCollectionsAreEmpty()
    {
        var dto = new ServiceRequestAnalyticsResponseDto();

        dto.TotalRequests.Should().Be(0);
        dto.RequestsByStatus.Should().BeEmpty();
        dto.RequestsByCategory.Should().BeEmpty();
        dto.RequestsByLocation.Should().BeEmpty();
        dto.AverageDaysToComplete.Should().BeNull();
    }
}
