using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="PacketEmailDeliveryEmbedded"/> state machine that tracks
/// idempotent, retried packet-email delivery on a <see cref="ServiceRequest"/>
/// (<c>Spec B-4</c>, issue #438).
/// </summary>
public class PacketEmailDeliveryEmbeddedTests
{
    [Fact]
    public void New_ShouldStartPendingWithNoAttemptsOrDeliveredVersion()
    {
        var d = new PacketEmailDeliveryEmbedded();

        d.Status.Should().Be("Pending");
        d.AttemptCount.Should().Be(0);
        d.DeliveredPacketVersion.Should().Be(0);
        d.LastAttemptAtUtc.Should().BeNull();
        d.DeliveredAtUtc.Should().BeNull();
        d.LastError.Should().BeNull();
        d.AlertRaised.Should().BeFalse();
    }

    [Fact]
    public void NewServiceRequest_ShouldHavePendingPacketEmailDelivery()
    {
        var sr = new ServiceRequest();

        sr.PacketEmailDelivery.Should().NotBeNull();
        sr.PacketEmailDelivery.Status.Should().Be("Pending");
    }

    [Fact]
    public void MaxAttempts_ShouldBeThree()
    {
        PacketEmailDeliveryEmbedded.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public void MarkAttempt_ShouldIncrementAttemptAndStampTime()
    {
        var d = new PacketEmailDeliveryEmbedded();

        var before = DateTime.UtcNow;
        d.MarkAttempt();

        d.AttemptCount.Should().Be(1);
        d.LastAttemptAtUtc.Should().NotBeNull();
        d.LastAttemptAtUtc!.Value.Should().BeOnOrAfter(before);

        d.MarkAttempt();
        d.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void MarkDelivered_ShouldSetStatusVersionTimeAndClearError()
    {
        var d = new PacketEmailDeliveryEmbedded();
        d.MarkAttempt();
        d.MarkFailed("SmtpException: transient 421");

        var deliveredAt = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
        d.MarkDelivered(4, deliveredAt);

        d.Status.Should().Be("Delivered");
        d.DeliveredPacketVersion.Should().Be(4);
        d.DeliveredAtUtc.Should().Be(deliveredAt);
        d.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_ShouldSetStatusAndError()
    {
        var d = new PacketEmailDeliveryEmbedded();
        d.MarkAttempt();

        d.MarkFailed("InvalidOperationException: ACS rejected the message");

        d.Status.Should().Be("Failed");
        d.LastError.Should().Be("InvalidOperationException: ACS rejected the message");
        d.AttemptCount.Should().Be(1);
    }

    [Fact]
    public void MarkAlertRaised_ShouldSetFlag()
    {
        var d = new PacketEmailDeliveryEmbedded();

        d.MarkAlertRaised();

        d.AlertRaised.Should().BeTrue();
    }

    [Fact]
    public void BeginRun_ShouldClearAttemptsErrorAndAlertButKeepDeliveredVersion()
    {
        var d = new PacketEmailDeliveryEmbedded();
        d.MarkAttempt();
        d.MarkDelivered(2, DateTime.UtcNow);
        d.MarkAttempt();
        d.MarkAttempt();
        d.MarkFailed("boom");
        d.MarkAlertRaised();

        d.BeginRun();

        d.Status.Should().Be("Pending");
        d.AttemptCount.Should().Be(0);
        d.LastError.Should().BeNull();
        d.AlertRaised.Should().BeFalse();
        d.DeliveredPacketVersion.Should().Be(2, "a prior successful delivery is retained");
        d.DeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void IsDeliveredFor_WhenDeliveredForThatVersion_ShouldBeTrue()
    {
        var d = new PacketEmailDeliveryEmbedded();
        d.MarkAttempt();
        d.MarkDelivered(3, DateTime.UtcNow);

        d.IsDeliveredFor(3).Should().BeTrue();
    }

    [Fact]
    public void IsDeliveredFor_WhenDeliveredForADifferentVersion_ShouldBeFalse()
    {
        var d = new PacketEmailDeliveryEmbedded();
        d.MarkAttempt();
        d.MarkDelivered(3, DateTime.UtcNow);

        d.IsDeliveredFor(4).Should().BeFalse();
    }

    [Fact]
    public void IsDeliveredFor_WhenNeverDelivered_ShouldBeFalse()
    {
        var d = new PacketEmailDeliveryEmbedded();

        d.IsDeliveredFor(0).Should().BeFalse();
        d.IsDeliveredFor(1).Should().BeFalse();
    }

    [Fact]
    public void IsDeliveredFor_WhenLastRunFailedAfterAnEarlierSuccess_ShouldBeFalseForThatVersion()
    {
        var d = new PacketEmailDeliveryEmbedded();
        d.MarkAttempt();
        d.MarkDelivered(1, DateTime.UtcNow);
        d.BeginRun();
        d.MarkAttempt();
        d.MarkFailed("later run failed");

        d.IsDeliveredFor(2).Should().BeFalse();
        d.IsDeliveredFor(1).Should().BeFalse("the status is no longer Delivered");
    }
}
