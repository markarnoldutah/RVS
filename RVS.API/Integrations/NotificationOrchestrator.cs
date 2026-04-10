using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Routes transactional notifications to the appropriate channel (email or SMS)
/// based on the customer's notification preference. Injects both
/// <see cref="INotificationService"/> (email) and <see cref="ISmsNotificationService"/> (SMS).
/// </summary>
public sealed class NotificationOrchestrator : INotificationOrchestrator
{
    private readonly INotificationService _emailService;
    private readonly ISmsNotificationService _smsService;
    private readonly ILogger<NotificationOrchestrator> _logger;

    public NotificationOrchestrator(
        INotificationService emailService,
        ISmsNotificationService smsService,
        ILogger<NotificationOrchestrator> logger)
    {
        _emailService = emailService;
        _smsService = smsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendServiceRequestConfirmationAsync(
        string notificationPreference,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string dealershipName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipName);

        var channel = NormalizePreference(notificationPreference);

        if (channel == "sms" && !string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            _logger.LogInformation("Routing SR confirmation to SMS for SR {ServiceRequestId}", serviceRequestId);
            await _smsService.SendServiceRequestConfirmationSmsAsync(
                toPhoneNumber, serviceRequestId, dealershipName, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Routing SR confirmation to email for SR {ServiceRequestId}", serviceRequestId);
            await _emailService.SendServiceRequestConfirmationAsync(
                toEmail, serviceRequestId, cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Cannot send SR confirmation for SR {ServiceRequestId}: no valid contact for preference '{Preference}'",
                serviceRequestId, notificationPreference);
        }
    }

    /// <inheritdoc />
    public async Task SendStatusChangeAsync(
        string notificationPreference,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string newStatus,
        string dealershipName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipName);

        var channel = NormalizePreference(notificationPreference);

        if (channel == "sms" && !string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            _logger.LogInformation("Routing status change to SMS for SR {ServiceRequestId}", serviceRequestId);
            await _smsService.SendStatusChangeSmsAsync(
                toPhoneNumber, serviceRequestId, newStatus, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Routing status change to email for SR {ServiceRequestId}", serviceRequestId);
            var subject = $"Service Request {serviceRequestId} — Status Update: {newStatus}";
            var htmlBody = $"<p>Your service request <strong>{serviceRequestId}</strong> at {dealershipName} has been updated to: <strong>{newStatus}</strong>.</p>";
            await _emailService.SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Cannot send status change for SR {ServiceRequestId}: no valid contact for preference '{Preference}'",
                serviceRequestId, notificationPreference);
        }
    }

    /// <inheritdoc />
    public async Task SendMagicLinkAsync(
        string notificationPreference,
        string? toEmail,
        string? toPhoneNumber,
        string magicLinkUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(magicLinkUrl);

        var channel = NormalizePreference(notificationPreference);

        if (channel == "sms" && !string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            _logger.LogInformation("Routing magic link to SMS");
            await _smsService.SendMagicLinkSmsAsync(toPhoneNumber, magicLinkUrl, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Routing magic link to email");
            var subject = "Your RV Service Flow Status Page Link";
            var htmlBody = $"<p>View your service request status: <a href=\"{magicLinkUrl}\">{magicLinkUrl}</a></p>";
            await _emailService.SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Cannot send magic link: no valid contact for preference '{Preference}'", notificationPreference);
        }
    }

    /// <summary>
    /// Normalizes the notification preference to lowercase. Defaults to "email" if invalid.
    /// </summary>
    internal static string NormalizePreference(string? preference)
    {
        var normalized = preference?.Trim().ToLowerInvariant();
        return normalized is "sms" or "email" ? normalized : "email";
    }
}
