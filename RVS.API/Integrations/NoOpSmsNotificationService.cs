using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// No-op SMS notification service for development and testing.
/// Logs the notification details but performs no external calls.
/// </summary>
public sealed class NoOpSmsNotificationService : ISmsNotificationService
{
    private readonly ILogger<NoOpSmsNotificationService> _logger;

    public NoOpSmsNotificationService(ILogger<NoOpSmsNotificationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendSmsAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOpSmsNotificationService: Would send SMS to {Recipient}: {Message}", toPhoneNumber, message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendMagicLinkSmsAsync(string toPhoneNumber, string magicLinkUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOpSmsNotificationService: Would send magic link SMS to {Recipient}: {Url}", toPhoneNumber, magicLinkUrl);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendServiceRequestConfirmationSmsAsync(
        string toPhoneNumber, string serviceRequestId, string dealershipName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "NoOpSmsNotificationService: Would send SR confirmation SMS to {Recipient} for SR {ServiceRequestId} at {Dealership}",
            toPhoneNumber, serviceRequestId, dealershipName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendStatusChangeSmsAsync(
        string toPhoneNumber, string serviceRequestId, string newStatus,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "NoOpSmsNotificationService: Would send status change SMS to {Recipient} for SR {ServiceRequestId}, status={Status}",
            toPhoneNumber, serviceRequestId, newStatus);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendDealerMessageSmsAsync(
        string toPhoneNumber, string serviceRequestId, string dealershipName,
        string messageText, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "NoOpSmsNotificationService: Would send dealer message SMS to {Recipient} for SR {ServiceRequestId}: {Message}",
            toPhoneNumber, serviceRequestId, messageText);
        return Task.CompletedTask;
    }
}
