using Azure.Communication.Sms;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Sends transactional SMS messages via Azure Communication Services.
/// Uses fire-and-forget semantics — errors are logged but never thrown to the caller.
/// </summary>
public sealed class AcsSmsNotificationService : ISmsNotificationService
{
    private readonly SmsClient _smsClient;
    private readonly ILogger<AcsSmsNotificationService> _logger;
    private readonly string _fromPhoneNumber;

    public AcsSmsNotificationService(
        SmsClient smsClient,
        ILogger<AcsSmsNotificationService> logger,
        IConfiguration configuration)
    {
        _smsClient = smsClient;
        _logger = logger;
        _fromPhoneNumber = configuration["AzureCommunicationServices:Sms:FromPhoneNumber"]
            ?? throw new InvalidOperationException("AzureCommunicationServices:Sms:FromPhoneNumber configuration is required.");
    }

    /// <inheritdoc />
    public async Task SendSmsAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        try
        {
            var response = await _smsClient.SendAsync(
                from: _fromPhoneNumber,
                to: toPhoneNumber,
                message: message,
                cancellationToken: cancellationToken);

            if (response.Value.Successful)
            {
                _logger.LogInformation(
                    "ACS SMS sent to {Recipient}, MessageId: {MessageId}",
                    toPhoneNumber, response.Value.MessageId);
            }
            else
            {
                _logger.LogWarning(
                    "ACS SMS send failed to {Recipient}: {ErrorMessage} (HttpStatus: {HttpStatus})",
                    toPhoneNumber, response.Value.ErrorMessage, response.Value.HttpStatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS via ACS to {Recipient}", toPhoneNumber);
        }
    }

    /// <inheritdoc />
    public async Task SendMagicLinkSmsAsync(string toPhoneNumber, string magicLinkUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(magicLinkUrl);

        var message = $"RV Service Flow: View your service request status: {magicLinkUrl} Reply STOP to opt out.";
        await SendSmsAsync(toPhoneNumber, message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendServiceRequestConfirmationSmsAsync(
        string toPhoneNumber, string serviceRequestId, string dealershipName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipName);

        var message = $"RV Service Flow: Your service request at {dealershipName} is confirmed (Ref: {serviceRequestId}). Reply STOP to opt out.";
        await SendSmsAsync(toPhoneNumber, message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendStatusChangeSmsAsync(
        string toPhoneNumber, string serviceRequestId, string newStatus,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);

        var message = $"RV Service Flow: Your service request {serviceRequestId} status changed to: {newStatus}. Reply STOP to opt out.";
        await SendSmsAsync(toPhoneNumber, message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendDealerMessageSmsAsync(
        string toPhoneNumber, string serviceRequestId, string dealershipName,
        string messageText, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipName);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageText);

        var message = $"{dealershipName} (Ref: {serviceRequestId}): {messageText} Reply STOP to opt out.";
        await SendSmsAsync(toPhoneNumber, message, cancellationToken);
    }
}
