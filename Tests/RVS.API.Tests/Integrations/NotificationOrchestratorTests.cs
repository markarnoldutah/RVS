using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

public class NotificationOrchestratorTests
{
    private const string StatusUrl = "https://rvintake.com/status/abc123token";
    private const string DealerPhone = "(801) 555-1234";

    private readonly Mock<INotificationService> _emailMock = new();
    private readonly Mock<ISmsNotificationService> _smsMock = new();
    private readonly NotificationOrchestrator _sut;

    public NotificationOrchestratorTests()
    {
        _sut = new NotificationOrchestrator(
            _emailMock.Object,
            _smsMock.Object,
            Mock.Of<ILogger<NotificationOrchestrator>>());
    }

    // ── SendServiceRequestConfirmationAsync ──────────────────────────────
    // Confirmation content is built inline (same pattern as SendStatusChangeAsync /
    // SendMagicLinkAsync below) and sent via the generic SendEmailAsync/SendSmsAsync —
    // there's no dedicated confirmation method on the channel interfaces.

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_DefaultOptOuts_ShouldSendBothChannels()
    {
        await _sut.SendServiceRequestConfirmationAsync(
            false, false, "user@example.com", "+18015551234", "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        _emailMock.Verify(
            e => e.SendEmailAsync(
                "user@example.com",
                It.Is<string>(s => s.Contains("Blue Compass RV")),
                It.Is<string>(b => b.Contains(StatusUrl) && b.Contains(DealerPhone)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendSmsAsync("+18015551234", It.Is<string>(m => m.Contains(StatusUrl)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_SmsOptOut_ShouldSendEmailOnly()
    {
        await _sut.SendServiceRequestConfirmationAsync(
            true, false, "user@example.com", "+18015551234", "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        _emailMock.Verify(
            e => e.SendEmailAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_EmailOptOut_ShouldSendSmsOnly()
    {
        await _sut.SendServiceRequestConfirmationAsync(
            false, true, "user@example.com", "+18015551234", "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        _emailMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _smsMock.Verify(
            s => s.SendSmsAsync("+18015551234", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_BothOptedOut_ShouldNotCallAnyService()
    {
        await _sut.SendServiceRequestConfirmationAsync(
            true, true, "user@example.com", "+18015551234", "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        _emailMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _smsMock.Verify(
            s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_NoPhone_ShouldSkipSmsEvenIfNotOptedOut()
    {
        await _sut.SendServiceRequestConfirmationAsync(
            false, false, "user@example.com", null, "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        _emailMock.Verify(
            e => e.SendEmailAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_NoEmail_ShouldSkipEmailEvenIfNotOptedOut()
    {
        await _sut.SendServiceRequestConfirmationAsync(
            false, false, null, "+18015551234", "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        _emailMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _smsMock.Verify(
            s => s.SendSmsAsync("+18015551234", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_WhenDealerPhoneIsNull_ShouldOmitPhoneFromContent()
    {
        await _sut.SendServiceRequestConfirmationAsync(
            false, false, "user@example.com", "+18015551234", "sr_001", "Blue Compass RV", StatusUrl, null);

        _emailMock.Verify(
            e => e.SendEmailAsync(
                "user@example.com",
                It.IsAny<string>(),
                It.Is<string>(b => b.Contains(StatusUrl) && !b.Contains(DealerPhone)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendSmsAsync("+18015551234", It.Is<string>(m => m.Contains(StatusUrl) && !m.Contains(DealerPhone)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync(
            false, false, "user@example.com", null, srId!, "Blue Compass RV", StatusUrl, DealerPhone);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenDealershipNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? dealer)
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync(
            false, false, "user@example.com", null, "sr_001", dealer!, StatusUrl, DealerPhone);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenStatusUrlIsNullOrWhiteSpace_ShouldThrowArgumentException(string? statusUrl)
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync(
            false, false, "user@example.com", null, "sr_001", "Blue Compass RV", statusUrl!, DealerPhone);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── SendStatusChangeAsync ────────────────────────────────────────────

    [Fact]
    public async Task SendStatusChangeAsync_DefaultOptOuts_ShouldSendBothChannels()
    {
        await _sut.SendStatusChangeAsync(
            false, false, "user@example.com", "+18015551234", "sr_001", "InProgress", "Blue Compass RV");

        _emailMock.Verify(
            e => e.SendEmailAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendStatusChangeSmsAsync("+18015551234", "sr_001", "InProgress", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendStatusChangeAsync_SmsOptOut_ShouldSendEmailOnly()
    {
        await _sut.SendStatusChangeAsync(
            true, false, "user@example.com", "+18015551234", "sr_001", "InProgress", "Blue Compass RV");

        _emailMock.Verify(
            e => e.SendEmailAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendStatusChangeSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendStatusChangeAsync_EmailOptOut_ShouldSendSmsOnly()
    {
        await _sut.SendStatusChangeAsync(
            false, true, "user@example.com", "+18015551234", "sr_001", "InProgress", "Blue Compass RV");

        _emailMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _smsMock.Verify(
            s => s.SendStatusChangeSmsAsync("+18015551234", "sr_001", "InProgress", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendStatusChangeAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var act = () => _sut.SendStatusChangeAsync(
            false, false, "user@example.com", null, srId!, "InProgress", "Blue Compass RV");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── SendMagicLinkAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SendMagicLinkAsync_DefaultOptOuts_ShouldSendBothChannels()
    {
        await _sut.SendMagicLinkAsync(
            false, false, "user@example.com", "+18015551234", "https://app.rvserviceflow.com/status/abc");

        _emailMock.Verify(
            e => e.SendEmailAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendMagicLinkSmsAsync("+18015551234", "https://app.rvserviceflow.com/status/abc", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMagicLinkAsync_SmsOptOut_ShouldSendEmailOnly()
    {
        await _sut.SendMagicLinkAsync(
            true, false, "user@example.com", "+18015551234", "https://app.rvserviceflow.com/status/abc");

        _emailMock.Verify(
            e => e.SendEmailAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendMagicLinkSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendMagicLinkAsync_EmailOptOut_ShouldSendSmsOnly()
    {
        await _sut.SendMagicLinkAsync(
            false, true, "user@example.com", "+18015551234", "https://app.rvserviceflow.com/status/abc");

        _emailMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _smsMock.Verify(
            s => s.SendMagicLinkSmsAsync("+18015551234", "https://app.rvserviceflow.com/status/abc", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendMagicLinkAsync_WhenMagicLinkUrlIsNullOrWhiteSpace_ShouldThrowArgumentException(string? url)
    {
        var act = () => _sut.SendMagicLinkAsync(
            false, false, "user@example.com", null, url!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Edge case: both contacts missing ─────────────────────────────────

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_WhenBothContactsMissing_ShouldNotThrowAndNotCallAnyService()
    {
        await _sut.SendServiceRequestConfirmationAsync(
            false, false, null, null, "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        _emailMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _smsMock.Verify(
            s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
