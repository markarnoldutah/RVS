using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Sends transactional SMS through Twilio Programmable Messaging. A typed <see cref="HttpClient"/>
/// whose base address and API-key credentials are set at registration
/// (<see cref="SmsServiceCollectionExtensions"/>). Never throws for a failed send: the caller
/// learns the outcome only from whether a Message SID comes back.
///
/// Every send passes four gates before Twilio is called (issue #661): SMS is enabled, the
/// recipient normalises to E.164, the location resolves a sending number, and the tenant has
/// hourly allowance left. Registration already swaps in the no-op service when SMS is off; the
/// <see cref="SmsOptions.Enabled"/> check here is the backstop.
///
/// Sends go through the environment's Messaging Service, which applies Advanced Opt-Out: Twilio
/// refuses a send to a number that texted STOP, and answers STOP, START and HELP itself.
/// </summary>
public sealed class TwilioSmsNotificationService : ISmsNotificationService
{
    /// <summary>Where Twilio posts status updates for a sent message.</summary>
    public const string StatusCallbackPath = "/api/events/twilio-sms/status";

    private readonly HttpClient _httpClient;
    private readonly ISmsSenderNumberResolver _senderNumberResolver;
    private readonly ITenantSmsRateLimiter _rateLimiter;
    private readonly SmsOptions _options;
    private readonly TwilioOptions _twilio;
    private readonly ILogger<TwilioSmsNotificationService> _logger;

    public TwilioSmsNotificationService(
        HttpClient httpClient,
        ISmsSenderNumberResolver senderNumberResolver,
        ITenantSmsRateLimiter rateLimiter,
        IOptions<SmsOptions> options,
        IOptions<TwilioOptions> twilioOptions,
        ILogger<TwilioSmsNotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(twilioOptions);

        _httpClient = httpClient;
        _senderNumberResolver = senderNumberResolver;
        _rateLimiter = rateLimiter;
        _options = options.Value;
        _twilio = twilioOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public async Task<string?> SendSmsAsync(
        string tenantId, string locationId, string toPhoneNumber, string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!_options.Enabled)
        {
            _logger.LogDebug("SMS is disabled; not sending for tenant {TenantId}", tenantId);
            return null;
        }

        if (!PhoneNumberNormalizer.TryNormalize(toPhoneNumber, out var to))
        {
            _logger.LogWarning(
                "Not sending SMS for tenant {TenantId}: recipient is not a valid US/CA number", tenantId);
            return null;
        }

        try
        {
            var from = await _senderNumberResolver.ResolveAsync(tenantId, locationId, cancellationToken);
            if (from is null)
            {
                _logger.LogWarning(
                    "Not sending SMS for tenant {TenantId}: no sending number for location {LocationId}",
                    tenantId, locationId);
                return null;
            }

            if (!_rateLimiter.TryAcquire(tenantId))
            {
                _logger.LogWarning(
                    "Not sending SMS for tenant {TenantId}: over the limit of {Limit} messages per hour",
                    tenantId, _options.MaxMessagesPerTenantPerHour);
                return null;
            }

            using var response = await _httpClient.PostAsync(
                $"2010-04-01/Accounts/{Uri.EscapeDataString(_twilio.AccountSid)}/Messages.json",
                new FormUrlEncodedContent(BuildForm(from, to, message)),
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var sent = await response.Content.ReadFromJsonAsync<TwilioMessage>(cancellationToken);
                _logger.LogInformation("Twilio SMS sent to {Recipient}, MessageSid: {MessageSid}", to, sent?.Sid);
                return sent?.Sid;
            }

            // Twilio's error body names the code, e.g. 21610 for a recipient who texted STOP.
            var error = await ReadErrorAsync(response, cancellationToken);
            _logger.LogWarning(
                "Twilio SMS send failed to {Recipient}: {ErrorCode} {ErrorMessage} (HttpStatus: {HttpStatus})",
                to, error?.Code, error?.Message, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to send SMS via Twilio to {Recipient}", to);
        }

        return null;
    }

    private Dictionary<string, string> BuildForm(string from, string to, string message)
    {
        var form = new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = from,
            ["Body"] = message,
        };

        if (!string.IsNullOrWhiteSpace(_twilio.MessagingServiceSid))
        {
            form["MessagingServiceSid"] = _twilio.MessagingServiceSid;
        }

        // Without a public base URL there is nowhere to call back to; the send still goes, and
        // the invite simply stays Queued.
        if (!string.IsNullOrWhiteSpace(_twilio.WebhookBaseUrl))
        {
            form["StatusCallback"] = _twilio.WebhookBaseUrl.TrimEnd('/') + StatusCallbackPath;
        }

        return form;
    }

    private static async Task<TwilioError?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<TwilioError>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed record TwilioMessage([property: JsonPropertyName("sid")] string? Sid);

    private sealed record TwilioError(
        [property: JsonPropertyName("code")] int? Code,
        [property: JsonPropertyName("message")] string? Message);
}
