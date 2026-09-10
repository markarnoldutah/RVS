using FluentAssertions;
using RVS.Domain.Packets;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="PacketEmailBudgetValidator"/> — the startup check on the configured
/// packet-email size budget (<c>Spec B-4</c>, issue #521).
///
/// Contract under test: a budget must be large enough that the packet PDF always fits, and must
/// not exceed ACS's request ceiling — above it the original #521 failure (every send rejected,
/// no packet delivered) quietly returns. Both bounds are inclusive.
/// </summary>
public class PacketEmailBudgetValidatorTests
{
    // ── Happy paths ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultBudget_ReturnsSuccess()
    {
        var result = PacketEmailBudgetValidator.Validate(PacketEmailSizeFitter.DefaultMaxRequestBytes);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_ExactlyTheMinimum_ReturnsSuccess()
    {
        var result = PacketEmailBudgetValidator.Validate(PacketEmailBudgetValidator.MinimumMaxRequestBytes);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ExactlyTheAcsCeiling_ReturnsSuccess()
    {
        var result = PacketEmailBudgetValidator.Validate(PacketEmailSizeFitter.AcsMaxRequestBytes);

        result.IsValid.Should().BeTrue();
    }

    // ── Too small ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1_000L)]
    public void Validate_FarBelowTheMinimum_ReturnsFailure(long budget)
    {
        var result = PacketEmailBudgetValidator.Validate(budget);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_OneByteBelowTheMinimum_ReturnsFailure()
    {
        var result = PacketEmailBudgetValidator.Validate(PacketEmailBudgetValidator.MinimumMaxRequestBytes - 1);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void MinimumMaxRequestBytes_ShouldLeaveRoomForAMeasuredPacketPdf()
    {
        // QuestPDF resamples embedded images to their placed size, so the packet PDF does not
        // grow with source resolution — but it does grow with photo count and detail. Measured
        // with real 12 MP phone photos (2026-09-10): 1.60 MB for six, 2.83 MB for ten, the most
        // Spec A-6 allows. Base64 inflates the ten-photo PDF to ~3.8 MB on the wire. The minimum
        // exists so the PDF always fits; it must clear that with room for both bodies.
        const long measuredTenPhotoPdfBytes = 2_830_000;
        var pdfOnTheWire = 4L * ((measuredTenPhotoPdfBytes + 2) / 3);

        PacketEmailBudgetValidator.MinimumMaxRequestBytes.Should().BeGreaterThan(pdfOnTheWire);
    }

    // ── Too large ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10_000_001L)]
    [InlineData(30_000_000L)]
    public void Validate_AboveTheAcsCeiling_ReturnsFailure(long budget)
    {
        var result = PacketEmailBudgetValidator.Validate(budget);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    // ── Operator-facing message ──────────────────────────────────────────────

    [Theory]
    [InlineData(1_000L)]
    [InlineData(30_000_000L)]
    public void Validate_Failure_NamesTheConfigurationKey(long budget)
    {
        // The app refuses to start on this message, so it has to tell an operator which
        // setting to fix.
        var result = PacketEmailBudgetValidator.Validate(budget);

        result.ErrorMessage.Should().Contain("PacketEmail:MaxRequestBytes");
    }
}
