using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Communication.Sms;
using Azure.Core.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Drives a real <see cref="SmsClient"/> over a recording HTTP transport, so the assertions are
/// about what would actually reach ACS: whether a request is made, and its from/to numbers.
/// </summary>
public class AcsSmsNotificationServiceTests
{
    private const string TenantId = "ten_test";
    private const string LocationId = "loc_slc";
    private const string FromNumber = "+18662319618";

    private readonly RecordingHandler _acs = new();
    private readonly Mock<ISmsSenderNumberResolver> _resolverMock = new();
    private readonly Mock<ITenantSmsRateLimiter> _rateLimiterMock = new();

    public AcsSmsNotificationServiceTests()
    {
        _resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FromNumber);
        _rateLimiterMock.Setup(l => l.TryAcquire(It.IsAny<string>())).Returns(true);
    }

    // ── Guard clauses ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendSmsAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => CreateService().SendSmsAsync(tenantId!, LocationId, "+18015551234", "Test message");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendSmsAsync_WhenLocationIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? locationId)
    {
        var act = () => CreateService().SendSmsAsync(TenantId, locationId!, "+18015551234", "Test message");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendSmsAsync_WhenToPhoneNumberIsNullOrWhiteSpace_ShouldThrowArgumentException(string? phone)
    {
        var act = () => CreateService().SendSmsAsync(TenantId, LocationId, phone!, "Test message");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendSmsAsync_WhenMessageIsNullOrWhiteSpace_ShouldThrowArgumentException(string? message)
    {
        var act = () => CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", message!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Sending ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SendSmsAsync_WhenEnabled_ShouldSendFromTheResolvedNumber()
    {
        await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        _acs.Bodies.Should().ContainSingle();
        FromOf(_acs.Bodies[0]).Should().Be(FromNumber);
        _resolverMock.Verify(r => r.ResolveAsync(TenantId, LocationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendSmsAsync_ShouldNormaliseTheRecipientToE164BeforeItReachesAcs()
    {
        await CreateService().SendSmsAsync(TenantId, LocationId, "(801) 555-1234", "Test message");

        _acs.Bodies.Should().ContainSingle();
        ToOf(_acs.Bodies[0]).Should().Be("+18015551234");
    }

    [Fact]
    public async Task SendSmsAsync_WhenTheRecipientCannotBeNormalised_ShouldNotCallAcs()
    {
        await CreateService().SendSmsAsync(TenantId, LocationId, "555-1234", "Test message");

        _acs.Bodies.Should().BeEmpty();
        _rateLimiterMock.Verify(l => l.TryAcquire(It.IsAny<string>()), Times.Never,
            "an unsendable number must not spend the tenant's allowance");
    }

    [Fact]
    public async Task SendSmsAsync_WhenSmsIsDisabled_ShouldNotCallAcsOrConsumeTheLimit()
    {
        await CreateService(enabled: false).SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        _acs.Bodies.Should().BeEmpty();
        _rateLimiterMock.Verify(l => l.TryAcquire(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendSmsAsync_WhenNoSenderNumberResolves_ShouldNotCallAcs()
    {
        _resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        _acs.Bodies.Should().BeEmpty();
    }

    [Fact]
    public async Task SendSmsAsync_WhenTheTenantIsOverItsHourlyLimit_ShouldNotCallAcs()
    {
        _rateLimiterMock.Setup(l => l.TryAcquire(TenantId)).Returns(false);

        await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        _acs.Bodies.Should().BeEmpty();
    }

    [Fact]
    public async Task SendSmsAsync_WhenAcsFails_ShouldNotThrow()
    {
        _acs.Status = HttpStatusCode.InternalServerError;

        var act = () => CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        await act.Should().NotThrowAsync();
        _acs.Bodies.Should().NotBeEmpty();
    }

    // ── Message id (issue #663) ──────────────────────────────────────────

    [Fact]
    public async Task SendSmsAsync_WhenAcsAcceptsTheMessage_ShouldReturnItsMessageId()
    {
        // The invite stores it so an ACS delivery report can be matched back (Spec A-14).
        var messageId = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        messageId.Should().Be("Outgoing_test");
    }

    [Fact]
    public async Task SendSmsAsync_ShouldAskAcsForADeliveryReport()
    {
        await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        JsonDocument.Parse(_acs.Bodies.Single()).RootElement
            .GetProperty("smsSendOptions").GetProperty("enableDeliveryReport").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public async Task SendSmsAsync_WhenAcsFails_ShouldReturnNull()
    {
        _acs.Status = HttpStatusCode.InternalServerError;

        var messageId = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        messageId.Should().BeNull();
    }

    [Fact]
    public async Task SendSmsAsync_WhenNotSent_ShouldReturnNull()
    {
        _rateLimiterMock.Setup(l => l.TryAcquire(It.IsAny<string>())).Returns(false);

        var messageId = await CreateService().SendSmsAsync(TenantId, LocationId, "+18015551234", "Test message");

        messageId.Should().BeNull();
    }

    // ── IsEnabled (issue #662) ───────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_ShouldReflectSmsEnabledOption(bool enabled)
    {
        CreateService(enabled).IsEnabled.Should().Be(enabled);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private AcsSmsNotificationService CreateService(bool enabled = true)
    {
        var clientOptions = new SmsClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(_acs)),
        };
        clientOptions.Retry.MaxRetries = 0;

        var smsClient = new SmsClient(
            "endpoint=https://dummy.communication.azure.com;accesskey=dGVzdA==", clientOptions);

        return new AcsSmsNotificationService(
            smsClient,
            _resolverMock.Object,
            _rateLimiterMock.Object,
            Microsoft.Extensions.Options.Options.Create(new SmsOptions { Enabled = enabled, FromPhoneNumber = FromNumber }),
            Mock.Of<ILogger<AcsSmsNotificationService>>());
    }

    private static string? FromOf(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("from").GetString();

    private static string? ToOf(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("smsRecipients")[0].GetProperty("to").GetString();

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        public HttpStatusCode Status { get; set; } = HttpStatusCode.Accepted;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);

            if (Status != HttpStatusCode.Accepted)
            {
                return new HttpResponseMessage(Status)
                {
                    Content = new StringContent("""{"error":{"code":"InternalError","message":"boom"}}""", Encoding.UTF8, "application/json"),
                };
            }

            var to = ToOf(body);
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    $$"""{"value":[{"to":"{{to}}","messageId":"Outgoing_test","httpStatusCode":202,"successful":true}]}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
