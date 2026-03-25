using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class NoOpNotificationServiceTests
{
    private readonly NoOpNotificationService _sut = new(Mock.Of<ILogger<NoOpNotificationService>>());

    [Fact]
    public async Task SendEmailAsync_ShouldCompleteWithoutThrowing()
    {
        var act = () => _sut.SendEmailAsync("user@example.com", "Test Subject", "<p>Body</p>");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_ShouldCompleteWithoutThrowing()
    {
        var act = () => _sut.SendServiceRequestConfirmationAsync("user@example.com", "sr_001");

        await act.Should().NotThrowAsync();
    }
}
