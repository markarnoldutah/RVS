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
    public Task SendPacketEmailAsync(PacketEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogDebug(
            "NoOpNotificationService: Would send packet email '{Subject}' to {RecipientCount} recipient(s) with {AttachmentCount} attachment(s)",
            message.Subject, message.Recipients.Count, message.Attachments.Count);
        return Task.CompletedTask;
    }
}
