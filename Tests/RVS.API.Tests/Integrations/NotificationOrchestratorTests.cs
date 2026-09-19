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
    private const string TenantId = "ten_test";
    private const string LocationId = "loc_slc";

    private readonly Mock<INotificationService> _emailMock = new();
    private readonly Mock<ISmsNotificationService> _smsMock = new();
    private readonly Mock<ILogger<NotificationOrchestrator>> _loggerMock = new();
    private readonly NotificationOrchestrator _sut;

    public NotificationOrchestratorTests()
    {
        _sut = new NotificationOrchestrator(
            _emailMock.Object,
            _smsMock.Object,
            _loggerMock.Object);
    }

    // ── SendServiceRequestConfirmationAsync: routing (Spec A-2, issue #662) ──
    // PreferredContact chooses the channel; SmsOptOut/EmailOptOut are a hard veto. Exactly one
    // confirmation is sent, or none. Content is built inline and sent via the generic
    // SendEmailAsync/SendSmsAsync — there's no dedicated confirmation method on the channel interfaces.

    private const string Email = "user@example.com";
    private const string Phone = "+18015551234";

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_TextPreferredAndSmsAvailable_ShouldSendSmsOnly()
    {
        SmsEnabled();

        await SendAsync("Text", smsOptOut: false, emailOptOut: false, Email, Phone);

        _smsMock.Verify(
            s => s.SendSmsAsync(TenantId, LocationId, Phone, It.Is<string>(m => m.Contains(StatusUrl) && m.Contains(DealerPhone)), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoEmail();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_TextPreferredButSmsDisabled_ShouldFallBackToEmailAndLogWarning()
    {
        SmsDisabled();

        await SendAsync("Text", smsOptOut: false, emailOptOut: false, Email, Phone);

        VerifyEmailSentTo(Email);
        VerifyNoSms();
        VerifyWarningLogged();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_TextPreferredButNoPhone_ShouldFallBackToEmailAndLogWarning()
    {
        SmsEnabled();

        await SendAsync("Text", smsOptOut: false, emailOptOut: false, Email, null);

        VerifyEmailSentTo(Email);
        VerifyNoSms();
        VerifyWarningLogged();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_TextPreferredButSmsOptedOut_ShouldFallBackToEmail()
    {
        // The validator rejects this pairing at intake; the orchestrator still honours the veto.
        SmsEnabled();

        await SendAsync("Text", smsOptOut: true, emailOptOut: false, Email, Phone);

        VerifyEmailSentTo(Email);
        VerifyNoSms();
    }

    [Theory]
    [InlineData("Email")]
    [InlineData("Phone")]
    [InlineData(null)]
    public async Task SendServiceRequestConfirmationAsync_EmailPhoneOrNoPreference_ShouldSendEmailOnly(string? preferredContact)
    {
        SmsEnabled();

        await SendAsync(preferredContact, smsOptOut: false, emailOptOut: false, Email, Phone);

        _emailMock.Verify(
            e => e.SendEmailAsync(
                Email,
                It.Is<string>(s => s.Contains("Blue Compass RV")),
                It.Is<string>(b => b.Contains(StatusUrl) && b.Contains(DealerPhone)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoSms();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_PreferenceMatchedCaseInsensitively()
    {
        SmsEnabled();

        await SendAsync("text", smsOptOut: false, emailOptOut: false, Email, Phone);

        _smsMock.Verify(
            s => s.SendSmsAsync(TenantId, LocationId, Phone, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoEmail();
    }

    [Theory]
    [InlineData("Phone")]
    [InlineData("Email")]
    [InlineData(null)]
    public async Task SendServiceRequestConfirmationAsync_EmailOptedOutAndSmsPermitted_ShouldSendSms(string? preferredContact)
    {
        SmsEnabled();

        await SendAsync(preferredContact, smsOptOut: false, emailOptOut: true, Email, Phone);

        _smsMock.Verify(
            s => s.SendSmsAsync(TenantId, LocationId, Phone, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoEmail();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_NoEmailAddressAndSmsPermitted_ShouldSendSms()
    {
        SmsEnabled();

        await SendAsync("Email", smsOptOut: false, emailOptOut: false, null, Phone);

        _smsMock.Verify(
            s => s.SendSmsAsync(TenantId, LocationId, Phone, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoEmail();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_EmailOptedOutAndSmsDisabled_ShouldSendNothingAndLogWarning()
    {
        SmsDisabled();

        await SendAsync("Phone", smsOptOut: false, emailOptOut: true, Email, Phone);

        VerifyNoEmail();
        VerifyNoSms();
        VerifyWarningLogged();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_BothOptedOut_ShouldSendNothingAndLogWarning()
    {
        SmsEnabled();

        await SendAsync("Phone", smsOptOut: true, emailOptOut: true, Email, Phone);

        VerifyNoEmail();
        VerifyNoSms();
        VerifyWarningLogged();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_EmailOptedOutAndNoPhone_ShouldSendNothingAndLogWarning()
    {
        SmsEnabled();

        await SendAsync("Phone", smsOptOut: false, emailOptOut: true, Email, null);

        VerifyNoEmail();
        VerifyNoSms();
        VerifyWarningLogged();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_WhenBothContactsMissing_ShouldNotThrowAndSendNothing()
    {
        SmsEnabled();

        await SendAsync("Text", smsOptOut: false, emailOptOut: false, null, null);

        VerifyNoEmail();
        VerifyNoSms();
        VerifyWarningLogged();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_WhenDealerPhoneIsNull_ShouldOmitPhoneFromContent()
    {
        SmsEnabled();

        await _sut.SendServiceRequestConfirmationAsync(
            TenantId, LocationId, "Email", false, false, Email, Phone, "sr_001", "Blue Compass RV", StatusUrl, null);
        await _sut.SendServiceRequestConfirmationAsync(
            TenantId, LocationId, "Text", false, false, Email, Phone, "sr_001", "Blue Compass RV", StatusUrl, null);

        _emailMock.Verify(
            e => e.SendEmailAsync(
                Email,
                It.IsAny<string>(),
                It.Is<string>(b => b.Contains(StatusUrl) && !b.Contains(DealerPhone)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _smsMock.Verify(
            s => s.SendSmsAsync(TenantId, LocationId, Phone, It.Is<string>(m => m.Contains(StatusUrl) && !m.Contains(DealerPhone)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync(
            tenantId!, LocationId, "Email", false, false, "user@example.com", null, "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenLocationIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? locationId)
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync(
            TenantId, locationId!, "Email", false, false, "user@example.com", null, "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync(
            TenantId, LocationId, "Email", false, false, "user@example.com", null, srId!, "Blue Compass RV", StatusUrl, DealerPhone);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenDealershipNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? dealer)
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync(
            TenantId, LocationId, "Email", false, false, "user@example.com", null, "sr_001", dealer!, StatusUrl, DealerPhone);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenStatusUrlIsNullOrWhiteSpace_ShouldThrowArgumentException(string? statusUrl)
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync(
            TenantId, LocationId, "Email", false, false, "user@example.com", null, "sr_001", "Blue Compass RV", statusUrl!, DealerPhone);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SmsEnabled() => _smsMock.SetupGet(s => s.IsEnabled).Returns(true);

    private void SmsDisabled() => _smsMock.SetupGet(s => s.IsEnabled).Returns(false);

    private Task SendAsync(string? preferredContact, bool smsOptOut, bool emailOptOut, string? toEmail, string? toPhone) =>
        _sut.SendServiceRequestConfirmationAsync(
            TenantId, LocationId, preferredContact, smsOptOut, emailOptOut, toEmail, toPhone,
            "sr_001", "Blue Compass RV", StatusUrl, DealerPhone);

    private void VerifyEmailSentTo(string toEmail) =>
        _emailMock.Verify(
            e => e.SendEmailAsync(toEmail, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyNoEmail() =>
        _emailMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private void VerifyNoSms() =>
        _smsMock.Verify(
            s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private void VerifyWarningLogged() =>
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
}
