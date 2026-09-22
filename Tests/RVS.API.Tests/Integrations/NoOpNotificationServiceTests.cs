using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.Domain.Integrations;

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
    public async Task SendPacketEmailAsync_ShouldCompleteWithoutThrowing()
    {
        var message = new PacketEmailMessage
        {
            Subject = "[RVS] Slide System — 2021 Jayco Eagle — Doe",
            HtmlBody = "<p>Packet</p>",
            PlainTextBody = "CATEGORY: SLIDE SYSTEM",
            Recipients = ["service@dealer.example"],
        };

        var act = () => _sut.SendPacketEmailAsync(message);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendPacketEmailAsync_WhenMessageIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.SendPacketEmailAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void IsEnabled_ShouldBeFalse()
    {
        // The advisor invite dialog reads this: a no-op must not offer to email anyone.
        _sut.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SendTransactionalEmailAsync_ShouldSendNothingAndReturnNull()
    {
        var result = await _sut.SendTransactionalEmailAsync("user@example.com", "Subject", "<p>Body</p>", "Body");

        result.Should().BeNull();
    }
}
