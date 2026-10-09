using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="ServiceSchedule"/> — the one booked start on a service request
/// (<c>Spec C-12</c>, issue #844): a date, an optional time and an IANA zone, stored as a UTC
/// instant and always shown in the stored zone with its abbreviation.
/// </summary>
public class ServiceScheduleTests
{
    private const string Denver = "America/Denver";
    private const string Phoenix = "America/Phoenix";

    /// <summary>Thursday, October 15 2026 — daylight time in Denver.</summary>
    private static readonly DateOnly Oct15 = new(2026, 10, 15);

    /// <summary>US clocks spring forward at 2:00 AM on Sunday, March 8 2026.</summary>
    private static readonly DateOnly SpringForward = new(2026, 3, 8);

    /// <summary>US clocks fall back at 2:00 AM on Sunday, November 1 2026.</summary>
    private static readonly DateOnly FallBack = new(2026, 11, 1);

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    // ── Validate ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WhenEverythingNull_ShouldSucceed()
    {
        ServiceSchedule.Validate(null, null, null).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenDateAndZone_ShouldSucceed()
    {
        ServiceSchedule.Validate(Oct15, null, Denver).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenDateTimeAndZone_ShouldSucceed()
    {
        ServiceSchedule.Validate(Oct15, new TimeOnly(9, 0), Denver).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenDateWithoutZone_ShouldFail(string? zone)
    {
        var result = ServiceSchedule.Validate(Oct15, new TimeOnly(9, 0), zone);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("time zone");
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("America/Denver;DROP")]
    [InlineData("<script>")]
    public void Validate_WhenZoneUnknown_ShouldFail(string zone)
    {
        ServiceSchedule.Validate(Oct15, null, zone).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenTimeWithoutDate_ShouldFail()
    {
        var result = ServiceSchedule.Validate(null, new TimeOnly(9, 0), Denver);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("date");
    }

    [Fact]
    public void Validate_WhenZoneWithoutDate_ShouldSucceed()
    {
        // Clearing the schedule from a form that still remembers the zone is a clear, not an error.
        ServiceSchedule.Validate(null, null, Denver).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTimeFallsInSpringForwardGap_ShouldFail()
    {
        var result = ServiceSchedule.Validate(SpringForward, new TimeOnly(2, 30), Denver);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Validate_WhenYearOutOfRange_ShouldFail(int year)
    {
        ServiceSchedule.Validate(new DateOnly(year, 6, 1), null, Denver).IsValid.Should().BeFalse();
    }

    // ── Resolve ─────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_WhenNoDate_ShouldReturnNull()
    {
        ServiceSchedule.Resolve(null, null, Denver).Should().BeNull();
    }

    [Fact]
    public void Resolve_WhenNineAmDenverInOctober_ShouldStoreFifteenHundredUtc()
    {
        var start = ServiceSchedule.Resolve(Oct15, new TimeOnly(9, 0), Denver);

        start.Should().Be(new ScheduledStart(Utc(2026, 10, 15, 15), Denver, TimeIsSet: true));
        start!.StartUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Resolve_WhenPhoenix_ShouldUseStandardOffsetAllYear()
    {
        ServiceSchedule.Resolve(Oct15, new TimeOnly(9, 0), Phoenix)!.StartUtc
            .Should().Be(Utc(2026, 10, 15, 16));
    }

    [Fact]
    public void Resolve_WhenDateOnly_ShouldStoreLocalMidnightAndTimeNotSet()
    {
        var start = ServiceSchedule.Resolve(Oct15, null, Denver);

        start.Should().Be(new ScheduledStart(Utc(2026, 10, 15, 6), Denver, TimeIsSet: false));
    }

    [Fact]
    public void Resolve_ShouldTrimZone()
    {
        ServiceSchedule.Resolve(Oct15, null, "  America/Denver  ")!.TimeZoneId.Should().Be(Denver);
    }

    [Fact]
    public void Resolve_WhenInvalid_ShouldThrowArgumentException()
    {
        var act = () => ServiceSchedule.Resolve(Oct15, new TimeOnly(9, 0), null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Resolve_WhenBeforeSpringForward_ShouldUseStandardOffset()
    {
        ServiceSchedule.Resolve(SpringForward, new TimeOnly(1, 30), Denver)!.StartUtc
            .Should().Be(Utc(2026, 3, 8, 8, 30));
    }

    [Fact]
    public void Resolve_WhenAfterSpringForward_ShouldUseDaylightOffset()
    {
        ServiceSchedule.Resolve(SpringForward, new TimeOnly(3, 0), Denver)!.StartUtc
            .Should().Be(Utc(2026, 3, 8, 9));
    }

    [Fact]
    public void Resolve_WhenRepeatedFallBackHour_ShouldTakeTheFirstOccurrence()
    {
        // 1:30 AM happens twice on Nov 1; the earlier (still daylight, MDT) one is meant.
        ServiceSchedule.Resolve(FallBack, new TimeOnly(1, 30), Denver)!.StartUtc
            .Should().Be(Utc(2026, 11, 1, 7, 30));
    }

    [Fact]
    public void Resolve_WhenAfterFallBack_ShouldUseStandardOffset()
    {
        ServiceSchedule.Resolve(FallBack, new TimeOnly(9, 0), Denver)!.StartUtc
            .Should().Be(Utc(2026, 11, 1, 16));
    }

    [Fact]
    public void Resolve_WhenDateOnlyOnDstDays_ShouldStoreThatDaysMidnight()
    {
        ServiceSchedule.Resolve(SpringForward, null, Denver)!.StartUtc.Should().Be(Utc(2026, 3, 8, 7));
        ServiceSchedule.Resolve(FallBack, null, Denver)!.StartUtc.Should().Be(Utc(2026, 11, 1, 6));
    }

    // ── Format ──────────────────────────────────────────────────────────

    [Fact]
    public void Format_WhenUnset_ShouldReturnNull()
    {
        ServiceSchedule.Format(null, null, false).Should().BeNull();
    }

    [Fact]
    public void Format_WhenNineAmDenverInOctober_ShouldShowMdt()
    {
        ServiceSchedule.Format(Utc(2026, 10, 15, 15), Denver, true)
            .Should().Be("Thu Oct 15 · 9:00 AM MDT");
    }

    [Fact]
    public void Format_WhenDateOnly_ShouldShowNoTimeAndNoZone()
    {
        ServiceSchedule.Format(Utc(2026, 10, 15, 6), Denver, false).Should().Be("Thu Oct 15");
    }

    [Fact]
    public void Format_ShouldNotDependOnTheHostZone()
    {
        // The stored zone is the only zone consulted; an instant late in the UTC day still reads
        // as the Denver date, whatever machine renders it.
        ServiceSchedule.Format(Utc(2026, 10, 16, 2, 30), Denver, true)
            .Should().Be("Thu Oct 15 · 8:30 PM MDT");
    }

    [Fact]
    public void Format_WhenStandardTime_ShouldShowMst()
    {
        ServiceSchedule.Format(Utc(2026, 1, 15, 16), Denver, true).Should().Be("Thu Jan 15 · 9:00 AM MST");
    }

    [Fact]
    public void Format_OnSpringForwardDay_ShouldSwitchAbbreviationAtTheChange()
    {
        ServiceSchedule.Format(Utc(2026, 3, 8, 8, 30), Denver, true).Should().Be("Sun Mar 8 · 1:30 AM MST");
        ServiceSchedule.Format(Utc(2026, 3, 8, 9), Denver, true).Should().Be("Sun Mar 8 · 3:00 AM MDT");
    }

    [Fact]
    public void Format_OnFallBackDay_ShouldTellTheRepeatedHourApart()
    {
        ServiceSchedule.Format(Utc(2026, 11, 1, 7, 30), Denver, true).Should().Be("Sun Nov 1 · 1:30 AM MDT");
        ServiceSchedule.Format(Utc(2026, 11, 1, 8, 30), Denver, true).Should().Be("Sun Nov 1 · 1:30 AM MST");
    }

    [Fact]
    public void Format_WhenPhoenix_ShouldShowMstInSummer()
    {
        ServiceSchedule.Format(Utc(2026, 7, 1, 16), Phoenix, true).Should().Be("Wed Jul 1 · 9:00 AM MST");
    }

    [Fact]
    public void Format_WhenUtc_ShouldShowUtc()
    {
        ServiceSchedule.Format(Utc(2026, 10, 15, 15), "UTC", true).Should().Be("Thu Oct 15 · 3:00 PM UTC");
    }

    [Fact]
    public void Format_WhenZoneNotCurated_ShouldShowNumericOffset()
    {
        ServiceSchedule.Format(Utc(2026, 10, 15, 8), "Europe/London", true)
            .Should().Be("Thu Oct 15 · 9:00 AM UTC+01:00");
    }

    [Fact]
    public void Format_WhenZoneUnresolvable_ShouldFallBackToUtc()
    {
        ServiceSchedule.Format(Utc(2026, 10, 15, 15), "Mars/Olympus_Mons", true)
            .Should().Be("Thu Oct 15 · 3:00 PM UTC");
    }

    [Fact]
    public void Format_RoundTripsResolve()
    {
        var start = ServiceSchedule.Resolve(Oct15, new TimeOnly(9, 0), Denver)!;

        ServiceSchedule.Format(start.StartUtc, start.TimeZoneId, start.TimeIsSet)
            .Should().Be("Thu Oct 15 · 9:00 AM MDT");
    }

    // ── ToLocal ─────────────────────────────────────────────────────────

    [Fact]
    public void ToLocal_WhenUnset_ShouldReturnNulls()
    {
        ServiceSchedule.ToLocal(null, null, false).Should().Be((null as DateOnly?, null as TimeOnly?));
    }

    [Fact]
    public void ToLocal_WhenTimeSet_ShouldReturnStoredZoneWallClock()
    {
        ServiceSchedule.ToLocal(Utc(2026, 10, 15, 15), Denver, true)
            .Should().Be((Oct15, new TimeOnly(9, 0)));
    }

    [Fact]
    public void ToLocal_WhenDateOnly_ShouldReturnNoTime()
    {
        ServiceSchedule.ToLocal(Utc(2026, 10, 15, 6), Denver, false)
            .Should().Be((Oct15, null as TimeOnly?));
    }
}
