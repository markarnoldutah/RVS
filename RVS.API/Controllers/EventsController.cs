using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// The inbound Event Grid webhook for Azure Communication Services SMS events (issue #665):
/// carrier keywords texted to the sending number, and delivery reports for texts RVS sent.
///
/// Anonymous by necessity — Event Grid delivers with no user — so the subscription's endpoint
/// URL carries a shared secret, checked on every request before the body is looked at. It also
/// answers Event Grid's subscription validation handshake, which is how a new subscription is
/// allowed to start delivering here at all.
///
/// The endpoint always answers <c>200</c> once the secret checks out, including for events it
/// ignores. A non-2xx tells Event Grid to retry, and there is nothing to gain from retrying a
/// message that is not a keyword. A genuine failure still throws, and the retry is wanted then.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    /// <summary>ACS raises this when someone texts the sending number.</summary>
    private const string SmsReceivedEventType = "Microsoft.Communication.SMSReceived";

    /// <summary>ACS raises this for the delivery outcome of a text RVS sent.</summary>
    private const string SmsDeliveryReportEventType = "Microsoft.Communication.SMSDeliveryReportReceived";

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private readonly IInboundSmsEventService _inboundSmsEvents;
    private readonly EventGridInboundOptions _options;
    private readonly ILogger<EventsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EventsController"/>.
    /// </summary>
    public EventsController(
        IInboundSmsEventService inboundSmsEvents,
        IOptions<EventGridInboundOptions> options,
        ILogger<EventsController> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _inboundSmsEvents = inboundSmsEvents;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Receives ACS SMS events from Event Grid.
    /// </summary>
    /// <param name="key">The shared secret from the subscription's endpoint URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// POST /api/events/acs-sms?key=...
    /// </example>
    [HttpPost("acs-sms")]
    public async Task<IActionResult> AcsSms([FromQuery] string? key, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            // No secret configured means the webhook was never provisioned for this environment.
            _logger.LogError("Inbound Event Grid request refused: EventGrid:Inbound:Key is not configured");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!IsAuthorized(key))
        {
            _logger.LogWarning("Inbound Event Grid request refused: bad or missing key");
            return Unauthorized();
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            return Ok();
        }

        var events = EventGridEvent.ParseMany(BinaryData.FromString(body));

        foreach (var gridEvent in events)
        {
            // The handshake: Event Grid will not deliver anything until this code is echoed.
            if (gridEvent.TryGetSystemEventData(out var systemEvent)
                && systemEvent is SubscriptionValidationEventData validation)
            {
                _logger.LogInformation("Event Grid subscription validation handshake for topic {Topic}", gridEvent.Topic);
                return Ok(new SubscriptionValidationResponse { ValidationResponse = validation.ValidationCode });
            }

            await DispatchAsync(gridEvent, ct);
        }

        return Ok();
    }

    /// <summary>
    /// Routes one event to the handler. Anything that is not an SMS event we subscribed to is
    /// ignored rather than treated as an error.
    ///
    /// The event data is parsed here rather than through the SDK's typed system events: ACS
    /// emits the SMS payload in PascalCase (<c>MessageId</c>, <c>From</c>), while the SDK's
    /// models bind camelCase, so those models come back empty. Case-insensitive parsing reads
    /// either shape, and keeps working if ACS changes its mind.
    /// </summary>
    private async Task DispatchAsync(EventGridEvent gridEvent, CancellationToken ct)
    {
        switch (gridEvent.EventType)
        {
            case SmsReceivedEventType:
            {
                var data = Parse<AcsSmsReceivedData>(gridEvent);
                if (data?.From is null)
                {
                    _logger.LogWarning("Inbound SMS event {EventId} carried no sender; ignored", gridEvent.Id);
                    return;
                }

                await _inboundSmsEvents.HandleInboundMessageAsync(
                    data.From,
                    // The event id is the fallback: a redelivery reuses it, which is what the
                    // deduplicator needs, and ACS has always supplied the message id in practice.
                    string.IsNullOrWhiteSpace(data.MessageId) ? gridEvent.Id : data.MessageId,
                    data.Message,
                    data.ReceivedTimestamp?.UtcDateTime ?? gridEvent.EventTime.UtcDateTime,
                    ct);
                return;
            }

            case SmsDeliveryReportEventType:
            {
                var data = Parse<AcsSmsDeliveryReportData>(gridEvent);
                if (string.IsNullOrWhiteSpace(data?.MessageId))
                {
                    _logger.LogWarning("Delivery report {EventId} carried no message id; ignored", gridEvent.Id);
                    return;
                }

                await _inboundSmsEvents.HandleDeliveryReportAsync(
                    data.MessageId,
                    data.DeliveryStatus,
                    data.ReceivedTimestamp?.UtcDateTime ?? gridEvent.EventTime.UtcDateTime,
                    ct);
                return;
            }

            default:
                _logger.LogDebug("Ignoring Event Grid event of type {EventType}", gridEvent.EventType);
                return;
        }
    }

    /// <summary>Reads an event's data, tolerating either casing. Returns null on malformed JSON.</summary>
    private T? Parse<T>(EventGridEvent gridEvent) where T : class
    {
        try
        {
            return gridEvent.Data.ToObjectFromJson<T>(CaseInsensitive);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Event Grid event {EventId} carried unreadable data; ignored", gridEvent.Id);
            return null;
        }
    }

    /// <summary>
    /// Compares the supplied key with the configured one in fixed time, so a wrong key cannot be
    /// discovered a character at a time.
    /// </summary>
    private bool IsAuthorized(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes(_options.Key!));
    }
}

/// <summary>
/// The body Event Grid expects back from the subscription validation handshake.
/// </summary>
public sealed record SubscriptionValidationResponse
{
    /// <summary>The validation code echoed back verbatim.</summary>
    public required string ValidationResponse { get; init; }
}

/// <summary>
/// The <c>Microsoft.Communication.SMSReceived</c> payload, in the fields RVS acts on.
/// </summary>
/// <param name="MessageId">The inbound message's ACS id, used to spot a redelivered event.</param>
/// <param name="From">The customer's number, in E.164.</param>
/// <param name="Message">The text as sent. Only an exact keyword is ever acted on.</param>
/// <param name="ReceivedTimestamp">When the customer sent it.</param>
internal sealed record AcsSmsReceivedData(string? MessageId, string? From, string? Message, DateTimeOffset? ReceivedTimestamp);

/// <summary>
/// The <c>Microsoft.Communication.SMSDeliveryReportReceived</c> payload, in the fields RVS acts on.
/// </summary>
/// <param name="MessageId">The ACS message id recorded when the text was sent.</param>
/// <param name="DeliveryStatus">ACS's status, e.g. <c>Delivered</c> or <c>Failed</c>.</param>
/// <param name="ReceivedTimestamp">When the report was raised.</param>
internal sealed record AcsSmsDeliveryReportData(string? MessageId, string? DeliveryStatus, DateTimeOffset? ReceivedTimestamp);
