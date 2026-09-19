using Azure.Communication.Sms;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Sends transactional SMS messages via Azure Communication Services.
/// Uses fire-and-forget semantics — errors are logged but never thrown to the caller.
///
/// Every send passes four gates before ACS is called (issue #661): SMS is enabled, the recipient
/// normalises to E.164, the location resolves a sending number, and the tenant has hourly
/// allowance left. Registration already swaps in the no-op service when SMS is off; the
/// <see cref="SmsOptions.Enabled"/> check here is the backstop.
/// </summary>
public sealed class AcsSmsNotificationService : ISmsNotificationService
{
    private readonly SmsClient _smsClient;
    private readonly ISmsSenderNumberResolver _senderNumberResolver;
    private readonly ITenantSmsRateLimiter _rateLimiter;
    private readonly SmsOptions _options;
    private readonly ILogger<AcsSmsNotificationService> _logger;

    public AcsSmsNotificationService(
        SmsClient smsClient,
        ISmsSenderNumberResolver senderNumberResolver,
        ITenantSmsRateLimiter rateLimiter,
        IOptions<SmsOptions> options,
        ILogger<AcsSmsNotificationService> logger)
    {
        _smsClient = smsClient;
        _senderNumberResolver = senderNumberResolver;
        _rateLimiter = rateLimiter;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendSmsAsync(
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
            return;
        }

        if (!PhoneNumberNormalizer.TryNormalize(toPhoneNumber, out var to))
        {
            _logger.LogWarning(
                "Not sending SMS for tenant {TenantId}: recipient is not a valid US/CA number", tenantId);
            return;
        }

        try
        {
            var from = await _senderNumberResolver.ResolveAsync(tenantId, locationId, cancellationToken);
            if (from is null)
            {
                _logger.LogWarning(
                    "Not sending SMS for tenant {TenantId}: no sending number for location {LocationId}",
                    tenantId, locationId);
                return;
            }

            if (!_rateLimiter.TryAcquire(tenantId))
            {
                _logger.LogWarning(
                    "Not sending SMS for tenant {TenantId}: over the limit of {Limit} messages per hour",
                    tenantId, _options.MaxMessagesPerTenantPerHour);
                return;
            }

            var response = await _smsClient.SendAsync(
                from: from,
                to: to,
                message: message,
                cancellationToken: cancellationToken);

            if (response.Value.Successful)
            {
                _logger.LogInformation(
                    "ACS SMS sent to {Recipient}, MessageId: {MessageId}",
                    to, response.Value.MessageId);
            }
            else
            {
                _logger.LogWarning(
                    "ACS SMS send failed to {Recipient}: {ErrorMessage} (HttpStatus: {HttpStatus})",
                    to, response.Value.ErrorMessage, response.Value.HttpStatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS via ACS to {Recipient}", to);
        }
    }
}
