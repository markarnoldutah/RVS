using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using RVS.API.Controllers;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Interfaces;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace RVS.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="TwilioSmsWebhookController"/> — Twilio's inbound-message and
/// status-callback webhooks (issue #665): the signature check, then dispatch to
/// <see cref="IInboundSmsEventService"/>.
/// </summary>
public class TwilioSmsWebhookControllerTests
{
    private const string AuthToken = "twilio-auth-token";
    private const string BaseUrl = "https://api-staging.rvserviceflow.com";
    private const string Phone = "+18015551234";
    private const string MessageSid = "SMcccccccccccccccccccccccccccccccc";

    private static readonly DateTimeOffset Now = new(2026, 10, 2, 16, 0, 0, TimeSpan.Zero);

    private readonly Mock<IInboundSmsEventService> _service = new();

    private TwilioSmsWebhookController CreateController(
        string path,
        Dictionary<string, string> form,
        string? signature = null,
        string? authToken = AuthToken,
        string? baseUrl = BaseUrl)
    {
        var time = new Mock<TimeProvider>();
        time.Setup(t => t.GetUtcNow()).Returns(Now);

        var controller = new TwilioSmsWebhookController(
            _service.Object,
            MsOptions.Create(new TwilioOptions { AuthToken = authToken!, WebhookBaseUrl = baseUrl! }),
            time.Object,
            Mock.Of<ILogger<TwilioSmsWebhookController>>());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = path;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(form.ToDictionary(kv => kv.Key, kv => new StringValues(kv.Value)));
        httpContext.Request.Headers[TwilioRequestSignature.HeaderName] =
            signature ?? TwilioRequestSignature.Compute(AuthToken, BaseUrl + path, form);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static Dictionary<string, string> Inbound(string body, string? optOutType = null)
    {
        var form = new Dictionary<string, string>
        {
            ["MessageSid"] = MessageSid,
            ["AccountSid"] = "ACaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ["From"] = Phone,
            ["To"] = "+18885550100",
            ["Body"] = body,
        };
        if (optOutType is not null)
        {
            form["OptOutType"] = optOutType;
        }
        return form;
    }

    private static Dictionary<string, string> Status(string status) => new()
    {
        ["MessageSid"] = MessageSid,
        ["MessageStatus"] = status,
        ["To"] = Phone,
    };

    private const string InboundPath = "/api/events/twilio-sms/inbound";
    private const string StatusPath = "/api/events/twilio-sms/status";

    // ---- Signature and configuration ----------------------------------------------------------

    [Fact]
    public async Task Inbound_WhenTheSignatureIsWrong_ShouldReturn403AndDispatchNothing()
    {
        var controller = CreateController(InboundPath, Inbound("STOP"), signature: "forged=");

        var result = await controller.Inbound(CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Inbound_WhenSignedForTheInternalUrl_ShouldReturn403()
    {
        // Behind App Service the app sees http and an internal host; Twilio signed the public URL.
        var form = Inbound("STOP");
        var controller = CreateController(InboundPath, form,
            signature: TwilioRequestSignature.Compute(AuthToken, "http://localhost" + InboundPath, form));

        var result = await controller.Inbound(CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Theory]
    [InlineData(null, BaseUrl)]
    [InlineData("", BaseUrl)]
    [InlineData(AuthToken, "")]
    public async Task Inbound_WhenNotConfigured_ShouldReturn503(string? authToken, string? baseUrl)
    {
        // Without both, no signature can be checked, so nothing is accepted.
        var controller = CreateController(InboundPath, Inbound("STOP"), authToken: authToken, baseUrl: baseUrl);

        var result = await controller.Inbound(CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Status_WhenTheSignatureIsWrong_ShouldReturn403()
    {
        var controller = CreateController(StatusPath, Status("delivered"), signature: "forged=");

        var result = await controller.Status(CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _service.VerifyNoOtherCalls();
    }

    // ---- Inbound messages ---------------------------------------------------------------------

    [Fact]
    public async Task Inbound_WhenSigned_ShouldPassTheMessageToTheServiceAndReturnEmptyTwiml()
    {
        var controller = CreateController(InboundPath, Inbound("STOP"));

        var result = await controller.Inbound(CancellationToken.None);

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().Be("text/xml");
        content.Content.Should().Be("<Response/>", "Advanced Opt-Out replies; RVS adds nothing");
        _service.Verify(s => s.HandleInboundMessageAsync(Phone, MessageSid, "STOP", Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("STOP", "unsubscribe")]
    [InlineData("START", "yes")]
    [InlineData("HELP", "info")]
    public async Task Inbound_WhenTwilioClassifiedAKeyword_ShouldPassItsClassificationNotTheBody(string optOutType, string body)
    {
        // OptOutType is what Twilio actually enforced; it recognises synonyms RVS's vocabulary may not.
        var controller = CreateController(InboundPath, Inbound(body, optOutType));

        await controller.Inbound(CancellationToken.None);

        _service.Verify(s => s.HandleInboundMessageAsync(Phone, MessageSid, optOutType, Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Inbound_WhenFromOrSidIsMissing_ShouldAcknowledgeAndDispatchNothing()
    {
        var form = Inbound("STOP");
        form.Remove("From");
        var controller = CreateController(InboundPath, form);

        var result = await controller.Inbound(CancellationToken.None);

        result.Should().BeOfType<ContentResult>();
        _service.VerifyNoOtherCalls();
    }

    // ---- Status callbacks ---------------------------------------------------------------------

    [Fact]
    public async Task Status_WhenSigned_ShouldPassTheStatusToTheService()
    {
        var controller = CreateController(StatusPath, Status("delivered"));

        var result = await controller.Status(CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        _service.Verify(s => s.HandleDeliveryReportAsync(MessageSid, "delivered", Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Status_WhenSidIsMissing_ShouldAcknowledgeAndDispatchNothing()
    {
        var form = Status("delivered");
        form.Remove("MessageSid");
        var controller = CreateController(StatusPath, form);

        var result = await controller.Status(CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        _service.VerifyNoOtherCalls();
    }
}
