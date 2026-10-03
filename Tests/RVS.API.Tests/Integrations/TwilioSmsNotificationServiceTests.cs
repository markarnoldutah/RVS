using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Tests for <see cref="TwilioSmsNotificationService"/>. Requests go to a recording handler, so
/// the assertions are about what would actually reach Twilio: whether a request is made, and
/// the form fields it carries.
/// </summary>
public class TwilioSmsNotificationServiceTests
{
    private const string TenantId = "ten_test";
    private const string LocationId = "loc_slc";
    private const string FromNumber = "+18885550100";
    private const string AccountSid = "ACaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string MessagingServiceSid = "MGbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string MessageSid = "SMcccccccccccccccccccccccccccccccc";

    private readonly RecordingHandler _twilio = new();
    private readonly Mock<ISmsSenderNumberResolver> _resolverMock = new();
    private readonly Mock<ITenantSmsRateLimiter> _rateLimiterMock = new();

    public TwilioSmsNotificationServiceTests()
    {
        _resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FromNumber);
        _rateLimiterMock.Setup(l => l.TryAcquire(It.IsAny<string>())).Returns(true);
        _twilio.Respond(HttpStatusCode.Created, $$"""{"sid":"{{MessageSid}}","status":"accepted"}""");
    }

    private TwilioSmsNotificationService CreateService(bool enabled = true, string? webhookBaseUrl = "https://api-staging.rvserviceflow.com")
    {
        var httpClient = new HttpClient(_twilio) { BaseAddress = new Uri("https://api.twilio.com/") };
        return new TwilioSmsNotificationService(
            httpClient,
            _resolverMock.Object,
            _rateLimiterMock.Object,
            Microsoft.Extensions.Options.Options.Create(new SmsOptions { Enabled = enabled, FromPhoneNumber = FromNumber }),
            Microsoft.Extensions.Options.Options.Create(new TwilioOptions
            {
                AccountSid = AccountSid,
                ApiKeySid = "SKdddddddddddddddddddddddddddddddd",
                ApiKeySecret = "secret",
                MessagingServiceSid = MessagingServiceSid,
                WebhookBaseUrl = webhookBaseUrl!,
            }),
            Mock.Of<ILogger<TwilioSmsNotificationService>>());
    }

    // ── Guard clauses ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, LocationId, "+18015551234", "Hi")]
    [InlineData(TenantId, " ", "+18015551234", "Hi")]
    [InlineData(TenantId, LocationId, "", "Hi")]
    [InlineData(TenantId, LocationId, "+18015551234", null)]
    public async Task SendSmsAsync_WhenAnArgumentIsBlank_ShouldThrowArgumentException(
        string? tenantId, string? locationId, string? phone, string? message)
    {
        var act = () => CreateService().SendSmsAsync(tenantId!, locationId!, phone!, message!);

        await act.Should().ThrowAsync<ArgumentException>();
        _twilio.Requests.Should().BeEmpty();
    }

    // ── The request Twilio receives ──────────────────────────────────────

    [Fact]
    public async Task SendSmsAsync_ShouldPostToTheAccountsMessagesResource()
    {
        await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        var request = _twilio.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Path.Should().Be($"/2010-04-01/Accounts/{AccountSid}/Messages.json");
    }

    [Fact]
    public async Task SendSmsAsync_ShouldSendFromTheResolvedNumberThroughTheMessagingService()
    {
        // The Messaging Service is what applies Advanced Opt-Out (STOP/START/HELP) to the send.
        await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi there");

        var form = _twilio.Requests[0].Form;
        form["From"].Should().Be(FromNumber);
        form["MessagingServiceSid"].Should().Be(MessagingServiceSid);
        form["To"].Should().Be("+18015551234");
        form["Body"].Should().Be("Hi there");
    }

    [Fact]
    public async Task SendSmsAsync_ShouldAskForAStatusCallbackOnTheApiHost()
    {
        // Status callbacks move an invite to Delivered or Failed (Spec A-14, issue #665).
        await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        _twilio.Requests[0].Form["StatusCallback"].Should()
            .Be("https://api-staging.rvserviceflow.com/api/events/twilio-sms/status");
    }

    [Fact]
    public async Task SendSmsAsync_WhenNoWebhookBaseUrl_ShouldStillSendWithoutAStatusCallback()
    {
        await CreateService(webhookBaseUrl: "").SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        _twilio.Requests[0].Form.ContainsKey("StatusCallback").Should().BeFalse();
    }

    [Fact]
    public async Task SendSmsAsync_ShouldNormaliseTheRecipientToE164BeforeItReachesTwilio()
    {
        await CreateService().SendSmsAsync(TenantId, LocationId, "(801) 555-1234", "Hi");

        _twilio.Requests[0].Form["To"].Should().Be("+18015551234");
    }

    // ── Gates: nothing reaches Twilio ────────────────────────────────────

    [Fact]
    public async Task SendSmsAsync_WhenTheRecipientCannotBeNormalised_ShouldNotCallTwilio()
    {
        var id = await CreateService().SendSmsAsync(TenantId, LocationId, "555-1234", "Hi");

        id.Should().BeNull();
        _twilio.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendSmsAsync_WhenSmsIsDisabled_ShouldNotCallTwilioOrConsumeTheLimit()
    {
        var id = await CreateService(enabled: false).SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        id.Should().BeNull();
        _twilio.Requests.Should().BeEmpty();
        _rateLimiterMock.Verify(l => l.TryAcquire(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendSmsAsync_WhenNoSenderNumberResolves_ShouldNotCallTwilio()
    {
        _resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var id = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        id.Should().BeNull();
        _twilio.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendSmsAsync_WhenTheTenantIsOverItsHourlyLimit_ShouldNotCallTwilio()
    {
        _rateLimiterMock.Setup(l => l.TryAcquire(TenantId)).Returns(false);

        var id = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        id.Should().BeNull();
        _twilio.Requests.Should().BeEmpty();
    }

    // ── Outcomes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SendSmsAsync_WhenTwilioAcceptsTheMessage_ShouldReturnItsSid()
    {
        var id = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        id.Should().Be(MessageSid);
    }

    [Fact]
    public async Task SendSmsAsync_WhenTheRecipientHasOptedOut_ShouldReturnNullAndNotThrow()
    {
        // Twilio refuses a send to a number that texted STOP (error 21610) synchronously.
        _twilio.Respond(HttpStatusCode.BadRequest,
            """{"code":21610,"message":"Attempt to send to unsubscribed recipient","status":400}""");

        var id = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        id.Should().BeNull();
    }

    [Fact]
    public async Task SendSmsAsync_WhenTwilioErrors_ShouldReturnNullAndNotThrow()
    {
        _twilio.Respond(HttpStatusCode.InternalServerError, "oops");

        var id = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        id.Should().BeNull();
    }

    [Fact]
    public async Task SendSmsAsync_WhenTheTransportThrows_ShouldReturnNullAndNotThrow()
    {
        _twilio.Throw(new HttpRequestException("connection reset"));

        var id = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Hi");

        id.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_ShouldReflectSmsEnabledOption(bool enabled)
    {
        CreateService(enabled).IsEnabled.Should().Be(enabled);
    }

    /// <summary>Answers every request with one canned response and keeps the form fields sent.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _respond = () => new HttpResponseMessage(HttpStatusCode.Created);

        public List<RecordedRequest> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body) => _respond = () =>
            new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        public void Throw(Exception exception) => _respond = () => throw exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var form = QueryHelpers.ParseQuery(body).ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.AbsolutePath, form));
            return _respond();
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, Dictionary<string, string> Form);
}
