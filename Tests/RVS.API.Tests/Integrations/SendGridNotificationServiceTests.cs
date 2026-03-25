using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class SendGridNotificationServiceTests
{
    private readonly Mock<ILogger<SendGridNotificationService>> _loggerMock = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendEmailAsync_WhenToEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? toEmail)
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.Accepted));
        var act = () => sut.SendEmailAsync(toEmail!, "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendEmailAsync_WhenSubjectIsNullOrWhiteSpace_ShouldThrowArgumentException(string? subject)
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.Accepted));
        var act = () => sut.SendEmailAsync("user@example.com", subject!, "<p>Body</p>");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendEmailAsync_WhenHtmlBodyIsNullOrWhiteSpace_ShouldThrowArgumentException(string? htmlBody)
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.Accepted));
        var act = () => sut.SendEmailAsync("user@example.com", "Subject", htmlBody!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendEmailAsync_ShouldCompleteImmediately()
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.Accepted));

        var act = () => sut.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenToEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? toEmail)
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.Accepted));
        var act = () => sut.SendServiceRequestConfirmationAsync(toEmail!, "sr_001");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.Accepted));
        var act = () => sut.SendServiceRequestConfirmationAsync("user@example.com", srId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_ShouldCompleteImmediately()
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.Accepted));

        var act = () => sut.SendServiceRequestConfirmationAsync("user@example.com", "sr_001");

        await act.Should().NotThrowAsync();
    }

    private SendGridNotificationService CreateService(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.sendgrid.com/")
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendGrid:FromEmail"] = "test@rvserviceflow.com"
            })
            .Build();

        return new SendGridNotificationService(httpClient, _loggerMock.Object, config);
    }
}
