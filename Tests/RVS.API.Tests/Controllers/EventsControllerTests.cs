using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Controllers;
using RVS.API.Options;
using RVS.Domain.Interfaces;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace RVS.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="EventsController"/> — the inbound Event Grid webhook for ACS SMS
/// events (issue #665): the validation handshake, the shared-secret check, and dispatch.
/// </summary>
public class EventsControllerTests
{
    private const string Key = "a-key-long-enough-to-pass-validation";
    private const string Phone = "+18015551234";
    private const string MessageId = "Outgoing_abc";

    private readonly Mock<IInboundSmsEventService> _service = new();

    private EventsController CreateController(string? configuredKey = Key, string body = "[]")
    {
        var controller = new EventsController(
            _service.Object,
            MsOptions.Create(new EventGridInboundOptions { Key = configuredKey }),
            Mock.Of<ILogger<EventsController>>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Request = { Body = new MemoryStream(Encoding.UTF8.GetBytes(body)) } },
        };

        return controller;
    }

    private static string ValidationEvent(string code) => $$"""
        [{
          "id": "1", "topic": "/subscriptions/x", "subject": "",
          "eventType": "Microsoft.EventGrid.SubscriptionValidationEvent",
          "eventTime": "2026-09-19T16:00:00Z", "dataVersion": "1.0",
          "data": { "validationCode": "{{code}}", "validationUrl": "https://example.com" }
        }]
        """;

    private static string SmsReceivedEvent(string message) => $$"""
        [{
          "id": "2", "topic": "/subscriptions/x", "subject": "/phonenumber/{{Phone}}",
          "eventType": "Microsoft.Communication.SMSReceived",
          "eventTime": "2026-09-19T16:00:00Z", "dataVersion": "1.0",
          "data": {
            "MessageId": "{{MessageId}}", "From": "{{Phone}}", "To": "+18662319618",
            "Message": "{{message}}", "ReceivedTimestamp": "2026-09-19T15:59:00Z"
          }
        }]
        """;

    private static string DeliveryReportEvent(string status) => $$"""
        [{
          "id": "3", "topic": "/subscriptions/x", "subject": "/phonenumber/+18662319618",
          "eventType": "Microsoft.Communication.SMSDeliveryReportReceived",
          "eventTime": "2026-09-19T16:00:00Z", "dataVersion": "1.0",
          "data": {
            "MessageId": "{{MessageId}}", "From": "+18662319618", "To": "{{Phone}}",
            "DeliveryStatus": "{{status}}", "DeliveryStatusDetails": "",
            "ReceivedTimestamp": "2026-09-19T15:59:30Z"
          }
        }]
        """;

    [Fact]
    public async Task AcsSms_WhenValidationHandshake_ShouldEchoTheValidationCode()
    {
        var controller = CreateController(body: ValidationEvent("code-123"));

        var result = await controller.AcsSms(Key);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<SubscriptionValidationResponse>()
            .Which.ValidationResponse.Should().Be("code-123");
    }

    [Fact]
    public async Task AcsSms_WhenKeyIsWrong_ShouldReturn401AndReadNothing()
    {
        var controller = CreateController(body: SmsReceivedEvent("STOP"));

        var result = await controller.AcsSms("not-the-key");

        result.Should().BeOfType<UnauthorizedResult>();
        _service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AcsSms_WhenKeyIsMissing_ShouldReturn401(string? key)
    {
        var controller = CreateController(body: SmsReceivedEvent("STOP"));

        var result = await controller.AcsSms(key);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AcsSms_WhenNoKeyIsConfigured_ShouldRefuseEveryRequest()
    {
        // An unauthenticated webhook that writes opt-outs is worse than one that is switched off.
        var controller = CreateController(configuredKey: null, body: SmsReceivedEvent("STOP"));

        var result = await controller.AcsSms(Key);

        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcsSms_WhenSmsReceived_ShouldHandTheMessageToTheService()
    {
        var controller = CreateController(body: SmsReceivedEvent("STOP"));

        var result = await controller.AcsSms(Key);

        result.Should().BeOfType<OkResult>();
        _service.Verify(
            s => s.HandleInboundMessageAsync(
                Phone,
                MessageId,
                "STOP",
                new DateTime(2026, 9, 19, 15, 59, 0, DateTimeKind.Utc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcsSms_WhenDeliveryReport_ShouldHandItToTheService()
    {
        var controller = CreateController(body: DeliveryReportEvent("Delivered"));

        var result = await controller.AcsSms(Key);

        result.Should().BeOfType<OkResult>();
        _service.Verify(
            s => s.HandleDeliveryReportAsync(
                MessageId,
                "Delivered",
                new DateTime(2026, 9, 19, 15, 59, 30, DateTimeKind.Utc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcsSms_WhenEventTypeIsNotOurs_ShouldIgnoreItAndStillReturn200()
    {
        // A non-2xx would make Event Grid retry something we will never act on.
        const string body = """
            [{
              "id": "4", "topic": "/subscriptions/x", "subject": "",
              "eventType": "Microsoft.Communication.ChatMessageReceived",
              "eventTime": "2026-09-19T16:00:00Z", "dataVersion": "1.0", "data": {}
            }]
            """;
        var controller = CreateController(body: body);

        var result = await controller.AcsSms(Key);

        result.Should().BeOfType<OkResult>();
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcsSms_WhenBodyIsEmpty_ShouldReturn200()
    {
        var controller = CreateController(body: "");

        var result = await controller.AcsSms(Key);

        result.Should().BeOfType<OkResult>();
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcsSms_WhenBatchCarriesSeveralEvents_ShouldHandleEachOne()
    {
        // Event Grid batches by design.
        var body = "[" + string.Join(",",
            SmsReceivedEvent("STOP").Trim('[', ']', '\n', ' '),
            DeliveryReportEvent("Failed").Trim('[', ']', '\n', ' ')) + "]";
        var controller = CreateController(body: body);

        var result = await controller.AcsSms(Key);

        result.Should().BeOfType<OkResult>();
        _service.Verify(s => s.HandleInboundMessageAsync(Phone, It.IsAny<string>(), "STOP", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _service.Verify(s => s.HandleDeliveryReportAsync(MessageId, "Failed", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcsSms_WhenAcsSendsCamelCaseData_ShouldStillDispatch()
    {
        // ACS documents PascalCase for SMS payloads; the SDK's own models bind camelCase.
        // Whichever arrives, the keyword has to land.
        const string body = """
            [{
              "id": "5", "topic": "/subscriptions/x", "subject": "/phonenumber/+18015551234",
              "eventType": "Microsoft.Communication.SMSReceived",
              "eventTime": "2026-09-19T16:00:00Z", "dataVersion": "1.0",
              "data": {
                "messageId": "m", "from": "+18015551234", "to": "+18662319618",
                "message": "STOP", "receivedTimestamp": "2026-09-19T15:59:00Z"
              }
            }]
            """;
        var controller = CreateController(body: body);

        var result = await controller.AcsSms(Key);

        result.Should().BeOfType<OkResult>();
        _service.Verify(
            s => s.HandleInboundMessageAsync(Phone, It.IsAny<string>(), "STOP", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcsSms_WhenSmsEventHasNoSender_ShouldIgnoreItAndStillReturn200()
    {
        const string body = """
            [{
              "id": "6", "topic": "/subscriptions/x", "subject": "",
              "eventType": "Microsoft.Communication.SMSReceived",
              "eventTime": "2026-09-19T16:00:00Z", "dataVersion": "1.0",
              "data": { "Message": "STOP" }
            }]
            """;
        var controller = CreateController(body: body);

        var result = await controller.AcsSms(Key);

        result.Should().BeOfType<OkResult>();
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcsSms_WhenTimestampIsMissing_ShouldFallBackToTheEventTime()
    {
        const string body = """
            [{
              "id": "7", "topic": "/subscriptions/x", "subject": "",
              "eventType": "Microsoft.Communication.SMSReceived",
              "eventTime": "2026-09-19T16:00:00Z", "dataVersion": "1.0",
              "data": { "From": "+18015551234", "Message": "STOP" }
            }]
            """;
        var controller = CreateController(body: body);

        await controller.AcsSms(Key);

        _service.Verify(
            s => s.HandleInboundMessageAsync(
                Phone, It.IsAny<string>(), "STOP", new DateTime(2026, 9, 19, 16, 0, 0, DateTimeKind.Utc), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcsSms_WhenSmsEventHasNoMessageId_ShouldFallBackToTheEventId()
    {
        // The deduplicator keys on this, so it cannot be empty or a redelivery would reply twice.
        const string body = """
            [{
              "id": "event-8", "topic": "/subscriptions/x", "subject": "",
              "eventType": "Microsoft.Communication.SMSReceived",
              "eventTime": "2026-09-19T16:00:00Z", "dataVersion": "1.0",
              "data": { "From": "+18015551234", "Message": "HELP" }
            }]
            """;
        var controller = CreateController(body: body);

        await controller.AcsSms(Key);

        _service.Verify(
            s => s.HandleInboundMessageAsync(Phone, "event-8", "HELP", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
