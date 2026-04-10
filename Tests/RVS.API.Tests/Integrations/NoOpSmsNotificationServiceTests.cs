using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class NoOpSmsNotificationServiceTests
{
    private readonly NoOpSmsNotificationService _sut = new(Mock.Of<ILogger<NoOpSmsNotificationService>>());

    [Fact]
    public async Task SendSmsAsync_ShouldCompleteWithoutThrowing()
    {
        var act = () => _sut.SendSmsAsync("+18015551234", "Test message");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendMagicLinkSmsAsync_ShouldCompleteWithoutThrowing()
    {
        var act = () => _sut.SendMagicLinkSmsAsync("+18015551234", "https://app.rvserviceflow.com/status/abc123");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationSmsAsync_ShouldCompleteWithoutThrowing()
    {
        var act = () => _sut.SendServiceRequestConfirmationSmsAsync("+18015551234", "sr_001", "Blue Compass RV");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendStatusChangeSmsAsync_ShouldCompleteWithoutThrowing()
    {
        var act = () => _sut.SendStatusChangeSmsAsync("+18015551234", "sr_001", "InProgress");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendDealerMessageSmsAsync_ShouldCompleteWithoutThrowing()
    {
        var act = () => _sut.SendDealerMessageSmsAsync("+18015551234", "sr_001", "Blue Compass RV", "Your part arrived.");
        await act.Should().NotThrowAsync();
    }
}
