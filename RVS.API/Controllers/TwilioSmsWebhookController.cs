using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Twilio's messaging webhooks (issue #665): texts sent to the environment's toll-free number,
/// and status callbacks for texts RVS sent. Both are set on the environment's Messaging Service.
///
/// Anonymous by necessity — Twilio calls with no user — so every request must carry a valid
/// <c>X-Twilio-Signature</c>, checked before any field is acted on. The signature covers the exact
/// URL Twilio called, which is rebuilt from <see cref="TwilioOptions.WebhookBaseUrl"/> rather than
/// from the request: App Service terminates TLS, so the app sees a different scheme and host.
///
/// A keyword handler, not a conversation. Twilio's Advanced Opt-Out answers STOP, START and HELP
/// itself, so the inbound endpoint always replies with empty TwiML.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/events/twilio-sms")]
public sealed class TwilioSmsWebhookController : ControllerBase
{
    /// <summary>Empty TwiML: acknowledge, and send nothing back.</summary>
    private const string EmptyTwiml = "<Response/>";

    private readonly IInboundSmsEventService _inboundSmsEvents;
    private readonly TwilioOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TwilioSmsWebhookController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TwilioSmsWebhookController"/>.
    /// </summary>
    public TwilioSmsWebhookController(
        IInboundSmsEventService inboundSmsEvents,
        IOptions<TwilioOptions> options,
        TimeProvider timeProvider,
        ILogger<TwilioSmsWebhookController> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _inboundSmsEvents = inboundSmsEvents;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Receives a text sent to the toll-free number. Only STOP / START / UNSTOP change anything;
    /// everything else, HELP included, is acknowledged and dropped.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// POST /api/events/twilio-sms/inbound
    /// </example>
    [HttpPost("inbound")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Inbound(CancellationToken ct = default)
    {
        var (refusal, form) = await AuthenticateAsync(ct);
        if (refusal is not null)
        {
            return refusal;
        }

        var from = form!["From"].ToString();
        var messageSid = form["MessageSid"].ToString();
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(messageSid))
        {
            _logger.LogWarning("Inbound Twilio message carried no sender or Message SID; ignored");
            return Content(EmptyTwiml, "text/xml");
        }

        // With Advanced Opt-Out on, Twilio names the keyword it enforced. Prefer that over the raw
        // body: it covers synonyms RVS's vocabulary may not, and it is what the carrier now blocks.
        var optOutType = form["OptOutType"].ToString();
        var keywordText = string.IsNullOrWhiteSpace(optOutType) ? form["Body"].ToString() : optOutType;

        // Twilio's inbound webhook carries no timestamp, so receipt time orders the keywords.
        await _inboundSmsEvents.HandleInboundMessageAsync(
            from, messageSid, keywordText, _timeProvider.GetUtcNow().UtcDateTime, ct);

        return Content(EmptyTwiml, "text/xml");
    }

    /// <summary>
    /// Receives a status callback for a text RVS sent. Only a terminal status changes an invite.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// POST /api/events/twilio-sms/status
    /// </example>
    [HttpPost("status")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Status(CancellationToken ct = default)
    {
        var (refusal, form) = await AuthenticateAsync(ct);
        if (refusal is not null)
        {
            return refusal;
        }

        var messageSid = form!["MessageSid"].ToString();
        if (string.IsNullOrWhiteSpace(messageSid))
        {
            _logger.LogWarning("Twilio status callback carried no Message SID; ignored");
            return Ok();
        }

        await _inboundSmsEvents.HandleDeliveryReportAsync(
            messageSid, form["MessageStatus"].ToString(), _timeProvider.GetUtcNow().UtcDateTime, ct);

        return Ok();
    }

    /// <summary>
    /// Reads the form and checks the signature. Returns a refusal to send back, or the form.
    /// </summary>
    private async Task<(IActionResult? Refusal, IFormCollection? Form)> AuthenticateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.AuthToken) || string.IsNullOrWhiteSpace(_options.WebhookBaseUrl))
        {
            // Without both, no signature can be checked: the webhook was never provisioned here.
            _logger.LogError("Twilio webhook refused: Twilio:AuthToken or Twilio:WebhookBaseUrl is not configured");
            return (StatusCode(StatusCodes.Status503ServiceUnavailable), null);
        }

        var form = await Request.ReadFormAsync(ct);
        var url = _options.WebhookBaseUrl.TrimEnd('/') + Request.Path + Request.QueryString;
        var parameters = form.SelectMany(field => field.Value.Select(value => new KeyValuePair<string, string>(field.Key, value ?? string.Empty)));

        if (!TwilioRequestSignature.IsValid(_options.AuthToken, url, parameters, Request.Headers[TwilioRequestSignature.HeaderName]))
        {
            _logger.LogWarning("Twilio webhook refused: bad or missing {Header}", TwilioRequestSignature.HeaderName);
            return (StatusCode(StatusCodes.Status403Forbidden), null);
        }

        return (null, form);
    }
}
