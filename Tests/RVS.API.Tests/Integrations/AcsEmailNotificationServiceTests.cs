using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

public class AcsEmailNotificationServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendEmailAsync_WhenToEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? toEmail)
    {
        var sut = CreateService();
        var act = () => sut.SendEmailAsync(toEmail!, "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendEmailAsync_WhenSubjectIsNullOrWhiteSpace_ShouldThrowArgumentException(string? subject)
    {
        var sut = CreateService();
        var act = () => sut.SendEmailAsync("user@example.com", subject!, "<p>Body</p>");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendEmailAsync_WhenHtmlBodyIsNullOrWhiteSpace_ShouldThrowArgumentException(string? htmlBody)
    {
        var sut = CreateService();
        var act = () => sut.SendEmailAsync("user@example.com", "Subject", htmlBody!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendEmailAsync_WithValidInputs_ShouldCompleteImmediately()
    {
        var sut = CreateService();

        var act = () => sut.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenToEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? toEmail)
    {
        var sut = CreateService();
        var act = () => sut.SendServiceRequestConfirmationAsync(toEmail!, "sr_001");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendServiceRequestConfirmationAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateService();
        var act = () => sut.SendServiceRequestConfirmationAsync("user@example.com", srId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendServiceRequestConfirmationAsync_WithValidInputs_ShouldCompleteImmediately()
    {
        var sut = CreateService();

        var act = () => sut.SendServiceRequestConfirmationAsync("user@example.com", "sr_001");

        await act.Should().NotThrowAsync();
    }

    // ── SendPacketEmailAsync (Spec B-4, issue #437) ───────────────────────

    [Fact]
    public async Task SendPacketEmailAsync_WhenMessageIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateService();

        var act = () => sut.SendPacketEmailAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendPacketEmailAsync_WhenSubjectIsNullOrWhiteSpace_ShouldThrowArgumentException(string? subject)
    {
        var sut = CreateService();

        var act = () => sut.SendPacketEmailAsync(BuildMessage() with { Subject = subject! });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendPacketEmailAsync_WhenHtmlBodyIsNullOrWhiteSpace_ShouldThrowArgumentException(string? html)
    {
        var sut = CreateService();

        var act = () => sut.SendPacketEmailAsync(BuildMessage() with { HtmlBody = html! });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendPacketEmailAsync_WhenPlainTextBodyIsNullOrWhiteSpace_ShouldThrowArgumentException(string? text)
    {
        var sut = CreateService();

        var act = () => sut.SendPacketEmailAsync(BuildMessage() with { PlainTextBody = text! });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendPacketEmailAsync_WhenThereAreNoRecipients_ShouldThrowArgumentException()
    {
        var sut = CreateService();

        var act = () => sut.SendPacketEmailAsync(BuildMessage() with { Recipients = [] });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static PacketEmailMessage BuildMessage() => new()
    {
        Subject = "[RVS] Slide System — 2021 Jayco Eagle — Doe",
        HtmlBody = "<p>Packet</p>",
        PlainTextBody = "CATEGORY: SLIDE SYSTEM",
        Recipients = ["service@dealer.example"],
    };

    /// <summary>
    /// Creates an AcsEmailNotificationService with a mock EmailClient that throws on Send
    /// to verify fire-and-forget semantics (errors are logged, not propagated).
    /// The guard clause tests run before the EmailClient is invoked, so they still work.
    /// </summary>
    private static AcsEmailNotificationService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureCommunicationServices:Email:FromAddress"] = "test@notifications.rvserviceflow.com",
                ["AzureCommunicationServices:Email:SenderDisplayName"] = "Test RVS"
            })
            .Build();

        // EmailClient is sealed and cannot be mocked with Moq.
        // We construct it with a dummy endpoint — the fire-and-forget method will fail
        // and log the error, but guard clause tests validate before that call.
        var emailClient = new Azure.Communication.Email.EmailClient("endpoint=https://dummy.communication.azure.com;accesskey=dGVzdA==");

        return new AcsEmailNotificationService(
            emailClient,
            Mock.Of<ILogger<AcsEmailNotificationService>>(),
            config);
    }
}
