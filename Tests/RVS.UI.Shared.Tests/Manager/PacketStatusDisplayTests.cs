using FluentAssertions;
using MudBlazor;
using RVS.Blazor.Manager.Shared;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// The detail dialog's packet section (<c>Spec C-2</c>, issue #443): what each
/// <c>packetGeneration.status</c> reads as, when the exhausted-retries failure state shows,
/// when Regenerate is offered, and the state the dialog shows once a regeneration is accepted.
/// </summary>
public class PacketStatusDisplayTests
{
    private static PacketGenerationDto Packet(string status, int attempts = 0, bool exhausted = false, int version = 0) => new()
    {
        Status = status,
        AttemptCount = attempts,
        MaxAttempts = 3,
        RetriesExhausted = exhausted,
        PacketVersion = version
    };

    // ---- GetLabel ---------------------------------------------------------------

    [Theory]
    [InlineData("Pending", "Queued")]
    [InlineData("Generating", "Generating")]
    [InlineData("Succeeded", "Ready")]
    public void GetLabel_ForNonFailedStatuses_ShouldReadPlainly(string status, string expected)
    {
        PacketStatusDisplay.GetLabel(Packet(status)).Should().Be(expected);
    }

    [Fact]
    public void GetLabel_WhenFailedWithRetriesLeft_ShouldSayRetryingWithAttempts()
    {
        PacketStatusDisplay.GetLabel(Packet("Failed", attempts: 1)).Should().Be("Retrying (attempt 1 of 3)");
    }

    [Fact]
    public void GetLabel_WhenFailedAndExhausted_ShouldSayFailed()
    {
        PacketStatusDisplay.GetLabel(Packet("Failed", attempts: 3, exhausted: true)).Should().Be("Failed");
    }

    [Fact]
    public void GetLabel_WhenStatusUnknown_ShouldEchoIt()
    {
        PacketStatusDisplay.GetLabel(Packet("Mystery")).Should().Be("Mystery");
    }

    // ---- GetSeverity ------------------------------------------------------------

    [Theory]
    [InlineData("Pending", false, Severity.Info)]
    [InlineData("Generating", false, Severity.Info)]
    [InlineData("Succeeded", false, Severity.Success)]
    [InlineData("Failed", false, Severity.Warning)]
    [InlineData("Failed", true, Severity.Error)]
    public void GetSeverity_ShouldMatchState(string status, bool exhausted, Severity expected)
    {
        PacketStatusDisplay.GetSeverity(Packet(status, exhausted: exhausted)).Should().Be(expected);
    }

    // ---- IsFailedExhausted ------------------------------------------------------

    [Fact]
    public void IsFailedExhausted_OnlyWhenFailedAndRetriesExhausted()
    {
        PacketStatusDisplay.IsFailedExhausted(Packet("Failed", 3, exhausted: true)).Should().BeTrue();
        PacketStatusDisplay.IsFailedExhausted(Packet("Failed", 1)).Should().BeFalse();
        PacketStatusDisplay.IsFailedExhausted(Packet("Succeeded", 1)).Should().BeFalse();
    }

    // ---- CanRegenerate ----------------------------------------------------------

    [Theory]
    [InlineData("Failed", true)]
    [InlineData("Succeeded", true)]
    [InlineData("Pending", false)]
    [InlineData("Generating", false)]
    public void CanRegenerate_ShouldBeOfferedUnlessARunIsInFlight(string status, bool expected)
    {
        PacketStatusDisplay.CanRegenerate(Packet(status)).Should().Be(expected);
    }

    [Fact]
    public void CanRegenerate_WhenFailedAndExhausted_ShouldBeOffered()
    {
        PacketStatusDisplay.CanRegenerate(Packet("Failed", 3, exhausted: true)).Should().BeTrue();
    }

    // ---- HasPdf -----------------------------------------------------------------

    [Theory]
    [InlineData("Pending", 0, false)]
    [InlineData("Succeeded", 1, true)]
    [InlineData("Failed", 2, true)]     // a failed regeneration keeps the last good PDF
    [InlineData("Generating", 1, true)]
    [InlineData("Failed", 0, false)]
    public void HasPdf_ShouldBeTrueOnceAnyVersionHasBeenGenerated(string status, int version, bool expected)
    {
        PacketStatusDisplay.HasPdf(Packet(status, version: version)).Should().Be(expected);
    }

    // ---- AsPendingRegeneration --------------------------------------------------

    [Fact]
    public void AsPendingRegeneration_ShouldResetToPendingAndKeepTheLastGoodVersion()
    {
        var generatedAt = new DateTime(2026, 9, 20, 14, 30, 0, DateTimeKind.Utc);
        var failed = Packet("Failed", attempts: 3, exhausted: true, version: 2) with { GeneratedAtUtc = generatedAt };

        var pending = PacketStatusDisplay.AsPendingRegeneration(failed);

        pending.Status.Should().Be("Pending");
        pending.AttemptCount.Should().Be(0);
        pending.RetriesExhausted.Should().BeFalse();
        pending.PacketVersion.Should().Be(2);
        pending.GeneratedAtUtc.Should().Be(generatedAt);
    }

    [Fact]
    public void AsPendingRegeneration_WhenNull_ShouldThrow()
    {
        var act = () => PacketStatusDisplay.AsPendingRegeneration(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
