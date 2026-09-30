using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Spec A-19 (issue #478): a disabled tenant's intake keeps capturing for 60 days from
/// <c>DisabledAtUtc</c>, then expires.
/// </summary>
public class TenantAccessGateEmbeddedTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IntakeCaptureDays_ShouldBeSixty()
    {
        TenantAccessGateEmbedded.IntakeCaptureDays.Should().Be(60);
    }

    [Fact]
    public void IsIntakeExpired_WhenLoginsEnabled_ShouldBeFalse()
    {
        var gate = new TenantAccessGateEmbedded { LoginsEnabled = true, DisabledAtUtc = Now.AddDays(-365) };

        gate.IsIntakeExpired(Now).Should().BeFalse();
    }

    [Fact]
    public void IsIntakeExpired_WhenDisabledWithinTheCaptureWindow_ShouldBeFalse()
    {
        var gate = new TenantAccessGateEmbedded { LoginsEnabled = false, DisabledAtUtc = Now.AddDays(-59) };

        gate.IsIntakeExpired(Now).Should().BeFalse();
    }

    [Fact]
    public void IsIntakeExpired_OneTickBeforeSixtyDays_ShouldBeFalse()
    {
        var gate = new TenantAccessGateEmbedded { LoginsEnabled = false, DisabledAtUtc = Now.AddDays(-60).AddTicks(1) };

        gate.IsIntakeExpired(Now).Should().BeFalse();
    }

    [Fact]
    public void IsIntakeExpired_AtExactlySixtyDays_ShouldBeTrue()
    {
        var gate = new TenantAccessGateEmbedded { LoginsEnabled = false, DisabledAtUtc = Now.AddDays(-60) };

        gate.IsIntakeExpired(Now).Should().BeTrue();
    }

    [Fact]
    public void IsIntakeExpired_WhenDisabledLongerThanTheCaptureWindow_ShouldBeTrue()
    {
        var gate = new TenantAccessGateEmbedded { LoginsEnabled = false, DisabledAtUtc = Now.AddDays(-200) };

        gate.IsIntakeExpired(Now).Should().BeTrue();
    }

    [Fact]
    public void IsIntakeExpired_WhenDisabledWithNoTimestamp_ShouldBeFalse()
    {
        // No clock to run: keep capturing rather than guess when it started.
        var gate = new TenantAccessGateEmbedded { LoginsEnabled = false, DisabledAtUtc = null };

        gate.IsIntakeExpired(Now).Should().BeFalse();
    }
}
