using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class AcsSmsNotificationServiceTests
{
    // ── SendSmsAsync Guard Clauses ───────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendSmsAsync_WhenToPhoneNumberIsNullOrWhiteSpace_ShouldThrowArgumentException(string? phone)
    {
        var sut = CreateService();
        var act = () => sut.SendSmsAsync(phone!, "Test message");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendSmsAsync_WhenMessageIsNullOrWhiteSpace_ShouldThrowArgumentException(string? message)
    {
        var sut = CreateService();
        var act = () => sut.SendSmsAsync("+18015551234", message!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendSmsAsync_WithValidInputs_ShouldNotThrow()
    {
        var sut = CreateService();
        var act = () => sut.SendSmsAsync("+18015551234", "Test message");
        await act.Should().NotThrowAsync();
    }

    // ── SendMagicLinkSmsAsync Guard Clauses ──────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendMagicLinkSmsAsync_WhenPhoneIsNullOrWhiteSpace_ShouldThrowArgumentException(string? phone)
    {
        var sut = CreateService();
        var act = () => sut.SendMagicLinkSmsAsync(phone!, "https://app.rvserviceflow.com/status/abc123");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendMagicLinkSmsAsync_WhenUrlIsNullOrWhiteSpace_ShouldThrowArgumentException(string? url)
    {
        var sut = CreateService();
        var act = () => sut.SendMagicLinkSmsAsync("+18015551234", url!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendMagicLinkSmsAsync_WithValidInputs_ShouldNotThrow()
    {
        var sut = CreateService();
        var act = () => sut.SendMagicLinkSmsAsync("+18015551234", "https://app.rvserviceflow.com/status/abc123");
        await act.Should().NotThrowAsync();
    }

    // ── SendServiceRequestConfirmationSmsAsync Guard Clauses ─────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationSmsAsync_WhenPhoneIsNullOrWhiteSpace_ShouldThrowArgumentException(string? phone)
    {
        var sut = CreateService();
        var act = () => sut.SendServiceRequestConfirmationSmsAsync(phone!, "sr_001", "Blue Compass RV");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationSmsAsync_WhenSrIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateService();
        var act = () => sut.SendServiceRequestConfirmationSmsAsync("+18015551234", srId!, "Blue Compass RV");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationSmsAsync_WhenDealershipNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? dealer)
    {
        var sut = CreateService();
        var act = () => sut.SendServiceRequestConfirmationSmsAsync("+18015551234", "sr_001", dealer!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationSmsAsync_WithValidInputs_ShouldNotThrow()
    {
        var sut = CreateService();
        var act = () => sut.SendServiceRequestConfirmationSmsAsync("+18015551234", "sr_001", "Blue Compass RV");
        await act.Should().NotThrowAsync();
    }

    // ── SendStatusChangeSmsAsync Guard Clauses ───────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendStatusChangeSmsAsync_WhenPhoneIsNullOrWhiteSpace_ShouldThrowArgumentException(string? phone)
    {
        var sut = CreateService();
        var act = () => sut.SendStatusChangeSmsAsync(phone!, "sr_001", "InProgress");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendStatusChangeSmsAsync_WhenSrIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateService();
        var act = () => sut.SendStatusChangeSmsAsync("+18015551234", srId!, "InProgress");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendStatusChangeSmsAsync_WhenStatusIsNullOrWhiteSpace_ShouldThrowArgumentException(string? status)
    {
        var sut = CreateService();
        var act = () => sut.SendStatusChangeSmsAsync("+18015551234", "sr_001", status!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendStatusChangeSmsAsync_WithValidInputs_ShouldNotThrow()
    {
        var sut = CreateService();
        var act = () => sut.SendStatusChangeSmsAsync("+18015551234", "sr_001", "InProgress");
        await act.Should().NotThrowAsync();
    }

    // ── SendDealerMessageSmsAsync Guard Clauses ──────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendDealerMessageSmsAsync_WhenPhoneIsNullOrWhiteSpace_ShouldThrowArgumentException(string? phone)
    {
        var sut = CreateService();
        var act = () => sut.SendDealerMessageSmsAsync(phone!, "sr_001", "Blue Compass RV", "Your part arrived.");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendDealerMessageSmsAsync_WhenMessageTextIsNullOrWhiteSpace_ShouldThrowArgumentException(string? msg)
    {
        var sut = CreateService();
        var act = () => sut.SendDealerMessageSmsAsync("+18015551234", "sr_001", "Blue Compass RV", msg!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendDealerMessageSmsAsync_WithValidInputs_ShouldNotThrow()
    {
        var sut = CreateService();
        var act = () => sut.SendDealerMessageSmsAsync("+18015551234", "sr_001", "Blue Compass RV", "Your part arrived.");
        await act.Should().NotThrowAsync();
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenFromPhoneNumberIsMissing_ShouldThrowInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var smsClient = new Azure.Communication.Sms.SmsClient("endpoint=https://dummy.communication.azure.com;accesskey=dGVzdA==");

        var act = () => new AcsSmsNotificationService(
            smsClient,
            Mock.Of<ILogger<AcsSmsNotificationService>>(),
            config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*FromPhoneNumber*");
    }

    // ── Helper ───────────────────────────────────────────────────────────

    private static AcsSmsNotificationService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureCommunicationServices:Sms:FromPhoneNumber"] = "+18005551234"
            })
            .Build();

        var smsClient = new Azure.Communication.Sms.SmsClient("endpoint=https://dummy.communication.azure.com;accesskey=dGVzdA==");

        return new AcsSmsNotificationService(
            smsClient,
            Mock.Of<ILogger<AcsSmsNotificationService>>(),
            config);
    }
}
