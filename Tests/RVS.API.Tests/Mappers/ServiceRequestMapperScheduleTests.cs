using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

/// <summary>
/// Scheduled date/time (<c>Spec C-12</c>, issue #844) through the service-request mapper: the
/// operator's local date, optional time and zone in on update; the stored instant, the local
/// values and the one display string out on the detail, summary and customer status DTOs.
/// </summary>
public class ServiceRequestMapperScheduleTests
{
    private const string Denver = "America/Denver";
    private static readonly DateTime NineAmMdtOct15 = new(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplyUpdate_WhenDateTimeAndZone_ShouldStoreUtcStartZoneAndTimeSet()
    {
        var entity = BuildServiceRequest();

        entity.ApplyUpdate(BuildUpdateRequest() with
        {
            ScheduledDate = new DateOnly(2026, 10, 15),
            ScheduledTime = new TimeOnly(9, 0),
            ScheduledTimeZone = Denver,
        }, "usr_1");

        entity.ScheduledStartUtc.Should().Be(NineAmMdtOct15);
        entity.ScheduledTimeZone.Should().Be(Denver);
        entity.ScheduledTimeIsSet.Should().BeTrue();
    }

    [Fact]
    public void ApplyUpdate_WhenDateOnly_ShouldStoreTimeNotSet()
    {
        var entity = BuildServiceRequest();

        entity.ApplyUpdate(BuildUpdateRequest() with
        {
            ScheduledDate = new DateOnly(2026, 10, 15),
            ScheduledTimeZone = Denver,
        }, "usr_1");

        entity.ScheduledStartUtc.Should().Be(new DateTime(2026, 10, 15, 6, 0, 0, DateTimeKind.Utc));
        entity.ScheduledTimeIsSet.Should().BeFalse();
    }

    [Fact]
    public void ApplyUpdate_WhenNoDate_ShouldClearTheSchedule()
    {
        var entity = BuildScheduledServiceRequest();

        entity.ApplyUpdate(BuildUpdateRequest() with { ScheduledTimeZone = Denver }, "usr_1");

        entity.ScheduledStartUtc.Should().BeNull();
        entity.ScheduledTimeZone.Should().BeNull();
        entity.ScheduledTimeIsSet.Should().BeFalse();
    }

    [Fact]
    public void ToDetailDto_WhenScheduled_ShouldCarryInstantLocalValuesAndDisplay()
    {
        var dto = BuildScheduledServiceRequest().ToDetailDto();

        dto.ScheduledStartUtc.Should().Be(NineAmMdtOct15);
        dto.ScheduledTimeZone.Should().Be(Denver);
        dto.ScheduledTimeIsSet.Should().BeTrue();
        dto.ScheduledDate.Should().Be(new DateOnly(2026, 10, 15));
        dto.ScheduledTime.Should().Be(new TimeOnly(9, 0));
        dto.ScheduledDisplay.Should().Be("Thu Oct 15 · 9:00 AM MDT");
    }

    [Fact]
    public void ToDetailDto_WhenDateOnly_ShouldCarryNoTime()
    {
        var entity = BuildScheduledServiceRequest();
        entity.ScheduledStartUtc = new DateTime(2026, 10, 15, 6, 0, 0, DateTimeKind.Utc);
        entity.ScheduledTimeIsSet = false;

        var dto = entity.ToDetailDto();

        dto.ScheduledTime.Should().BeNull();
        dto.ScheduledDisplay.Should().Be("Thu Oct 15");
    }

    [Fact]
    public void ToDetailDto_WhenUnscheduled_ShouldCarryNulls()
    {
        var dto = BuildServiceRequest().ToDetailDto();

        dto.ScheduledStartUtc.Should().BeNull();
        dto.ScheduledDate.Should().BeNull();
        dto.ScheduledTime.Should().BeNull();
        dto.ScheduledDisplay.Should().BeNull();
    }

    [Fact]
    public void ToSummaryDto_ShouldCarryInstantAndDisplay()
    {
        var dto = BuildScheduledServiceRequest().ToSummaryDto();

        dto.ScheduledStartUtc.Should().Be(NineAmMdtOct15);
        dto.ScheduledDisplay.Should().Be("Thu Oct 15 · 9:00 AM MDT");
    }

    [Fact]
    public void ToCustomerStatusItemDto_ShouldCarryDisplay()
    {
        BuildScheduledServiceRequest().ToCustomerStatusItemDto(location: null)
            .ScheduledDisplay.Should().Be("Thu Oct 15 · 9:00 AM MDT");
    }

    [Fact]
    public void ToCustomerStatusItemDto_WhenUnscheduled_ShouldCarryNull()
    {
        BuildServiceRequest().ToCustomerStatusItemDto(location: null).ScheduledDisplay.Should().BeNull();
    }

    private static ServiceRequest BuildScheduledServiceRequest()
    {
        var entity = BuildServiceRequest();
        entity.ScheduledStartUtc = NineAmMdtOct15;
        entity.ScheduledTimeZone = Denver;
        entity.ScheduledTimeIsSet = true;
        return entity;
    }

    private static ServiceRequest BuildServiceRequest() => new()
    {
        Id = "sr_1",
        TenantId = "ten_1",
        Status = "New",
        LocationId = "loc_1",
        CustomerProfileId = "cp_1",
        IssueDescription = "Water heater not working",
        Priority = "High",
        CustomerSnapshot = new CustomerSnapshotEmbedded { FirstName = "Ann", LastName = "Lee", Email = "ann@example.com" },
    };

    private static ServiceRequestUpdateRequestDto BuildUpdateRequest() => new()
    {
        Status = "New",
        IssueDescription = "Water heater not working",
        Priority = "High",
    };
}
