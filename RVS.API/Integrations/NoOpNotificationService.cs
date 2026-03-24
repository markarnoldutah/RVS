using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// No-op notification service for development and testing.
/// Logs the notification details but performs no external calls.
/// </summary>
public sealed class NoOpNotificationService : INotificationService
{
    private readonly ILogger<NoOpNotificationService> _logger;

    public NoOpNotificationService(ILogger<NoOpNotificationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOpNotificationService: Would send email to {Recipient} with subject '{Subject}'", toEmail, subject);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendServiceRequestConfirmationAsync(string toEmail, string serviceRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOpNotificationService: Would send SR confirmation to {Recipient} for SR {ServiceRequestId}", toEmail, serviceRequestId);
        return Task.CompletedTask;
    }
}
