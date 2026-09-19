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
        var act = () => _sut.SendSmsAsync("ten_test", "loc_slc", "+18015551234", "Test message");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void IsEnabled_ShouldBeFalse()
    {
        // The orchestrator routes a Text customer's confirmation to email when SMS is unavailable (issue #662).
        _sut.IsEnabled.Should().BeFalse();
    }
}
