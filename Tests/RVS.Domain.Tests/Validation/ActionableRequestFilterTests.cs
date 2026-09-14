using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="ActionableRequestFilter"/> — the rule the manager board lands on by
/// default so "open app → change status" is one glance (issue #498, scope item 5).
///
/// Contract under test: every open request is actionable; a closed one (Completed or Cancelled)
/// stays visible only on the local calendar day it was last changed, so the manager sees what
/// they closed today and nothing older.
/// </summary>
public class ActionableRequestFilterTests
{
    // 2026-09-12 09:00 in Mountain Daylight Time (UTC-6) = 15:00 UTC.
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 9, 0, 0, TimeSpan.FromHours(-6));

    [Theory]
    [InlineData("New")]
    [InlineData("InProgress")]
    [InlineData("WaitingOnParts")]
    [InlineData("WaitingOnCustomer")]
    public void IsActionable_WhenOpen_ShouldBeTrueRegardlessOfAge(string status)
    {
        var longAgo = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        ActionableRequestFilter.IsActionable(status, longAgo, longAgo, Now).Should().BeTrue();
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void IsActionable_WhenClosedEarlierToday_ShouldBeTrue(string status)
    {
        // 06:30 UTC on the 12th is 00:30 local on the 12th.
        var updated = new DateTime(2026, 9, 12, 6, 30, 0, DateTimeKind.Utc);

        ActionableRequestFilter.IsActionable(status, updated.AddDays(-3), updated, Now).Should().BeTrue();
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void IsActionable_WhenClosedOnAnEarlierLocalDay_ShouldBeFalse(string status)
    {
        // 05:30 UTC on the 12th is 23:30 local on the 11th — yesterday for this manager.
        var updated = new DateTime(2026, 9, 12, 5, 30, 0, DateTimeKind.Utc);

        ActionableRequestFilter.IsActionable(status, updated.AddDays(-3), updated, Now).Should().BeFalse();
    }

    [Fact]
    public void IsActionable_WhenClosedAndNeverUpdated_ShouldFallBackToCreatedDate()
    {
        var createdToday = new DateTime(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc);
        var createdYesterday = createdToday.AddDays(-1);

        ActionableRequestFilter.IsActionable("Completed", createdToday, null, Now).Should().BeTrue();
        ActionableRequestFilter.IsActionable("Completed", createdYesterday, null, Now).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsActionable_WhenStatusIsBlank_ShouldThrowArgumentException(string? status)
    {
        var act = () => ActionableRequestFilter.IsActionable(status!, DateTime.UtcNow, null, Now);

        act.Should().Throw<ArgumentException>();
    }
}
