using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Tests for <see cref="SendGridEmailNotificationService"/> — the SendGrid v3 <c>mail/send</c>
/// transport behind <see cref="INotificationService"/>. Requests are captured by a recording
/// handler, so each test asserts on the JSON SendGrid would actually receive.
/// </summary>
public class SendGridEmailNotificationServiceTests
{
    private const string FromAddress = "DoNotReply@mail-staging.rvintake.com";

    private readonly RecordingHandler _handler = new();

    private SendGridEmailNotificationService CreateService(string? fromAddress = FromAddress, string? displayName = "RV Intake [Test]")
    {
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://api.sendgrid.com/") };
        var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            FromAddress = fromAddress!,
            SenderDisplayName = displayName!,
        });
        return new SendGridEmailNotificationService(
            httpClient, options, Mock.Of<ILogger<SendGridEmailNotificationService>>());
    }

    private static PacketEmailMessage Packet(params string[] recipients) => new()
    {
        Subject = "Service request SR-1001",
        HtmlBody = "<table><tr><td>Packet</td></tr></table>",
        PlainTextBody = "Packet",
        Recipients = recipients,
        Attachments =
        [
            new PacketEmailAttachment
            {
                FileName = "packet.pdf",
                ContentType = "application/pdf",
                Content = new byte[] { 0x25, 0x50, 0x44, 0x46 },
            },
        ],
    };

    // ---- Construction -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenFromAddressIsMissing_ShouldThrowInvalidOperationException(string? fromAddress)
    {
        // No default sender: the domain differs per environment, and a wrong one is rejected by
        // SendGrid for an unauthenticated domain, surfacing only as a packet that never arrives.
        var act = () => CreateService(fromAddress);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Email:FromAddress*");
    }

    [Fact]
    public void IsEnabled_ShouldBeTrue()
    {
        CreateService().IsEnabled.Should().BeTrue();
    }

    // ---- Every send ---------------------------------------------------------------------------

    [Fact]
    public async Task SendTransactionalEmailAsync_ShouldPostToMailSendWithTheSenderAndDisplayName()
    {
        _handler.Respond(HttpStatusCode.Accepted, messageId: "msg-1");

        await CreateService().SendTransactionalEmailAsync("kim@example.com", "Your link", "<p>Hi</p>", "Hi");

        _handler.Requests.Should().ContainSingle();
        var request = _handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.Path.Should().Be("/v3/mail/send");
        request.Json.GetProperty("from").GetProperty("email").GetString().Should().Be(FromAddress);
        request.Json.GetProperty("from").GetProperty("name").GetString().Should().Be("RV Intake [Test]");
    }

    [Fact]
    public async Task SendTransactionalEmailAsync_ShouldTurnOffClickOpenAndSubscriptionTracking()
    {
        // Click tracking would rewrite the packet's photo SAS links and the status link through
        // sendgrid.net; the open pixel and the unsubscribe footer have no place on transactional mail.
        _handler.Respond(HttpStatusCode.Accepted, messageId: "msg-1");

        await CreateService().SendTransactionalEmailAsync("kim@example.com", "Your link", "<p>Hi</p>", "Hi");

        var tracking = _handler.Requests[0].Json.GetProperty("tracking_settings");
        tracking.GetProperty("click_tracking").GetProperty("enable").GetBoolean().Should().BeFalse();
        tracking.GetProperty("click_tracking").GetProperty("enable_text").GetBoolean().Should().BeFalse();
        tracking.GetProperty("open_tracking").GetProperty("enable").GetBoolean().Should().BeFalse();
        tracking.GetProperty("subscription_tracking").GetProperty("enable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SendTransactionalEmailAsync_ShouldSendPlainTextBeforeHtml()
    {
        // SendGrid rejects a request whose content array does not start with text/plain.
        _handler.Respond(HttpStatusCode.Accepted, messageId: "msg-1");

        await CreateService().SendTransactionalEmailAsync("kim@example.com", "Your link", "<p>Hi</p>", "Hi");

        var content = _handler.Requests[0].Json.GetProperty("content").EnumerateArray().ToList();
        content.Select(c => c.GetProperty("type").GetString()).Should().Equal("text/plain", "text/html");
        content[0].GetProperty("value").GetString().Should().Be("Hi");
        content[1].GetProperty("value").GetString().Should().Be("<p>Hi</p>");
    }

    // ---- SendTransactionalEmailAsync ----------------------------------------------------------

    [Fact]
    public async Task SendTransactionalEmailAsync_WhenAccepted_ShouldReturnTheSendGridMessageId()
    {
        _handler.Respond(HttpStatusCode.Accepted, messageId: "14c5d75ce93.dfd.64b469.filter0001.16648.5515E0B88.0");

        var id = await CreateService().SendTransactionalEmailAsync("kim@example.com", "Your link", "<p>Hi</p>", "Hi");

        id.Should().Be("14c5d75ce93.dfd.64b469.filter0001.16648.5515E0B88.0");
        var to = _handler.Requests[0].Json.GetProperty("personalizations")[0].GetProperty("to");
        to.EnumerateArray().Select(t => t.GetProperty("email").GetString()).Should().Equal("kim@example.com");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendTransactionalEmailAsync_WhenRejected_ShouldReturnNullAndNotThrow(HttpStatusCode status)
    {
        // The emailed invite records the outcome itself; a refusal is Failed, not an exception.
        _handler.Respond(status);

        var id = await CreateService().SendTransactionalEmailAsync("kim@example.com", "Your link", "<p>Hi</p>", "Hi");

        id.Should().BeNull();
    }

    [Fact]
    public async Task SendTransactionalEmailAsync_WhenTheTransportThrows_ShouldReturnNull()
    {
        _handler.Throw(new HttpRequestException("connection reset"));

        var id = await CreateService().SendTransactionalEmailAsync("kim@example.com", "Your link", "<p>Hi</p>", "Hi");

        id.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "s", "h", "t")]
    [InlineData("kim@example.com", " ", "h", "t")]
    [InlineData("kim@example.com", "s", "", "t")]
    [InlineData("kim@example.com", "s", "h", null)]
    public async Task SendTransactionalEmailAsync_WhenAnArgumentIsBlank_ShouldThrowArgumentException(
        string? to, string? subject, string? html, string? text)
    {
        var act = () => CreateService().SendTransactionalEmailAsync(to!, subject!, html!, text!);

        await act.Should().ThrowAsync<ArgumentException>();
        _handler.Requests.Should().BeEmpty();
    }

    // ---- SendEmailAsync -----------------------------------------------------------------------

    [Theory]
    [InlineData(null, "s", "h")]
    [InlineData("kim@example.com", "", "h")]
    [InlineData("kim@example.com", "s", "  ")]
    public async Task SendEmailAsync_WhenAnArgumentIsBlank_ShouldThrowArgumentException(
        string? to, string? subject, string? html)
    {
        var act = () => CreateService().SendEmailAsync(to!, subject!, html!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendEmailAsync_WhenTheSendFails_ShouldNotThrow()
    {
        // Fire-and-forget: the caller never learns about a failure, and must never see one.
        _handler.Respond(HttpStatusCode.InternalServerError);

        var act = () => CreateService().SendEmailAsync("kim@example.com", "Hello", "<p>Hi</p>");

        await act.Should().NotThrowAsync();
    }

    // ---- SendPacketEmailAsync -----------------------------------------------------------------

    [Fact]
    public async Task SendPacketEmailAsync_ShouldSendOneMessageToEveryRecipientWithAttachmentsInBase64()
    {
        _handler.Respond(HttpStatusCode.Accepted, messageId: "msg-1");

        await CreateService().SendPacketEmailAsync(Packet("service@acme.example", "parts@acme.example"));

        var json = _handler.Requests.Should().ContainSingle().Subject.Json;
        json.GetProperty("personalizations").GetArrayLength().Should().Be(1);
        json.GetProperty("personalizations")[0].GetProperty("to").EnumerateArray()
            .Select(t => t.GetProperty("email").GetString())
            .Should().Equal("service@acme.example", "parts@acme.example");
        json.GetProperty("subject").GetString().Should().Be("Service request SR-1001");

        var attachment = json.GetProperty("attachments").EnumerateArray().Should().ContainSingle().Subject;
        attachment.GetProperty("filename").GetString().Should().Be("packet.pdf");
        attachment.GetProperty("type").GetString().Should().Be("application/pdf");
        attachment.GetProperty("disposition").GetString().Should().Be("attachment");
        attachment.GetProperty("content").GetString().Should().Be(Convert.ToBase64String([0x25, 0x50, 0x44, 0x46]));
    }

    [Fact]
    public async Task SendPacketEmailAsync_WhenRecipientsRepeat_ShouldSendEachOnce()
    {
        // SendGrid rejects the whole request when one personalization names an address twice.
        _handler.Respond(HttpStatusCode.Accepted, messageId: "msg-1");

        await CreateService().SendPacketEmailAsync(Packet("service@acme.example", "Service@Acme.example", " "));

        _handler.Requests[0].Json.GetProperty("personalizations")[0].GetProperty("to").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SendPacketEmailAsync_WithNoAttachments_ShouldOmitTheAttachmentsArray()
    {
        // SendGrid rejects an empty attachments array.
        _handler.Respond(HttpStatusCode.Accepted, messageId: "msg-1");

        await CreateService().SendPacketEmailAsync(Packet("service@acme.example") with { Attachments = [] });

        _handler.Requests[0].Json.TryGetProperty("attachments", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendPacketEmailAsync_WhenRejected_ShouldThrowSoTheCallerRetries(HttpStatusCode status)
    {
        // PacketGenerationService owns retry with backoff; it only retries what throws.
        _handler.Respond(status);

        var act = () => CreateService().SendPacketEmailAsync(Packet("service@acme.example"));

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendPacketEmailAsync_WhenMessageIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => CreateService().SendPacketEmailAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendPacketEmailAsync_WhenThereAreNoRecipients_ShouldThrowArgumentException()
    {
        var act = () => CreateService().SendPacketEmailAsync(Packet(" ", ""));

        await act.Should().ThrowAsync<ArgumentException>();
        _handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "<p/>", "t")]
    [InlineData("s", " ", "t")]
    [InlineData("s", "<p/>", "")]
    public async Task SendPacketEmailAsync_WhenSubjectOrABodyIsBlank_ShouldThrowArgumentException(
        string subject, string html, string text)
    {
        var message = Packet("service@acme.example") with { Subject = subject, HtmlBody = html, PlainTextBody = text };

        var act = () => CreateService().SendPacketEmailAsync(message);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Answers every request with one canned response and keeps what was sent, with the body
    /// parsed as JSON so assertions read the payload rather than a string.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _respond = () => new HttpResponseMessage(HttpStatusCode.Accepted);

        public List<RecordedRequest> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string? messageId = null) => _respond = () =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.Accepted ? string.Empty : """{"errors":[{"message":"nope"}]}"""),
            };
            if (messageId is not null)
            {
                response.Headers.Add("X-Message-Id", messageId);
            }
            return response;
        };

        public void Throw(Exception exception) => _respond = () => throw exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.AbsolutePath, JsonDocument.Parse(body).RootElement.Clone()));
            return _respond();
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, JsonElement Json);
}
