using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="PacketGenerationEmbedded"/> state machine that tracks
/// service-packet generation on a <see cref="ServiceRequest"/> (<c>Spec B-1</c>, issue #434).
/// </summary>
public class PacketGenerationEmbeddedTests
{
    [Fact]
    public void New_ShouldStartPendingWithNoAttempts()
    {
        var pg = new PacketGenerationEmbedded();

        pg.Status.Should().Be("Pending");
        pg.AttemptCount.Should().Be(0);
        pg.PacketVersion.Should().Be(0);
        pg.LastAttemptAtUtc.Should().BeNull();
        pg.LastError.Should().BeNull();
        pg.GeneratedAtUtc.Should().BeNull();
        pg.PdfBlobPath.Should().BeNull();
        pg.AlertRaised.Should().BeFalse();
    }

    [Fact]
    public void NewServiceRequest_ShouldHavePendingPacketGeneration()
    {
        var sr = new ServiceRequest();

        sr.PacketGeneration.Should().NotBeNull();
        sr.PacketGeneration.Status.Should().Be("Pending");
    }

    [Fact]
    public void MaxAttempts_ShouldBeThree()
    {
        PacketGenerationEmbedded.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public void MarkGenerating_ShouldIncrementAttemptAndStampTime()
    {
        var pg = new PacketGenerationEmbedded();

        var before = DateTime.UtcNow;
        pg.MarkGenerating();

        pg.Status.Should().Be("Generating");
        pg.AttemptCount.Should().Be(1);
        pg.LastAttemptAtUtc.Should().NotBeNull();
        pg.LastAttemptAtUtc!.Value.Should().BeOnOrAfter(before);

        pg.MarkGenerating();
        pg.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void MarkSucceeded_ShouldBumpVersionSetPathAndClearError()
    {
        var pg = new PacketGenerationEmbedded();
        pg.MarkGenerating();
        pg.MarkFailed("Boom: earlier failure");

        var generatedAt = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
        pg.MarkSucceeded("packets/ten_x/sr_1/v1.pdf", generatedAt);

        pg.Status.Should().Be("Succeeded");
        pg.PacketVersion.Should().Be(1);
        pg.PdfBlobPath.Should().Be("packets/ten_x/sr_1/v1.pdf");
        pg.GeneratedAtUtc.Should().Be(generatedAt);
        pg.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkSucceeded_CalledTwice_ShouldIncrementVersionEachTime()
    {
        var pg = new PacketGenerationEmbedded();

        pg.MarkSucceeded("packets/a/v1.pdf", DateTime.UtcNow);
        pg.MarkSucceeded("packets/a/v2.pdf", DateTime.UtcNow);

        pg.PacketVersion.Should().Be(2);
        pg.PdfBlobPath.Should().Be("packets/a/v2.pdf");
    }

    [Fact]
    public void MarkFailed_ShouldSetStatusAndError()
    {
        var pg = new PacketGenerationEmbedded();
        pg.MarkGenerating();

        pg.MarkFailed("InvalidOperationException: blob upload rejected");

        pg.Status.Should().Be("Failed");
        pg.LastError.Should().Be("InvalidOperationException: blob upload rejected");
        pg.AttemptCount.Should().Be(1);
    }

    [Fact]
    public void MarkAlertRaised_ShouldSetFlag()
    {
        var pg = new PacketGenerationEmbedded();

        pg.MarkAlertRaised();

        pg.AlertRaised.Should().BeTrue();
    }

    [Fact]
    public void ResetForRegeneration_ShouldClearAttemptsErrorAndAlertButKeepVersion()
    {
        var pg = new PacketGenerationEmbedded();
        pg.MarkGenerating();
        pg.MarkSucceeded("packets/a/v1.pdf", DateTime.UtcNow);
        pg.MarkGenerating();
        pg.MarkGenerating();
        pg.MarkGenerating();
        pg.MarkFailed("boom");
        pg.MarkAlertRaised();

        pg.ResetForRegeneration();

        pg.Status.Should().Be("Pending");
        pg.AttemptCount.Should().Be(0);
        pg.LastError.Should().BeNull();
        pg.AlertRaised.Should().BeFalse();
        pg.PacketVersion.Should().Be(1, "prior successful versions are retained");
        pg.PdfBlobPath.Should().Be("packets/a/v1.pdf");
    }
}
