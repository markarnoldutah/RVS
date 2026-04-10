using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Routes transactional notifications to email and/or SMS channels.
/// By default both channels are used; customers can opt out of either via
/// <c>smsOptOut</c> and <c>emailOptOut</c> flags.
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
        bool smsOptOut,
        bool emailOptOut,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string dealershipName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipName);

        var sent = false;

        if (!emailOptOut && !string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Sending SR confirmation via email for SR {ServiceRequestId}", serviceRequestId);
            await _emailService.SendServiceRequestConfirmationAsync(
                toEmail, serviceRequestId, cancellationToken);
            sent = true;
        }

        if (!smsOptOut && !string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            _logger.LogInformation("Sending SR confirmation via SMS for SR {ServiceRequestId}", serviceRequestId);
            await _smsService.SendServiceRequestConfirmationSmsAsync(
                toPhoneNumber, serviceRequestId, dealershipName, cancellationToken);
            sent = true;
        }

        if (!sent)
        {
            _logger.LogWarning(
                "Cannot send SR confirmation for SR {ServiceRequestId}: no available channel (smsOptOut={SmsOptOut}, emailOptOut={EmailOptOut})",
                serviceRequestId, smsOptOut, emailOptOut);
        }
    }

    /// <inheritdoc />
    public async Task SendStatusChangeAsync(
        bool smsOptOut,
        bool emailOptOut,
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

        var sent = false;

        if (!emailOptOut && !string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Sending status change via email for SR {ServiceRequestId}", serviceRequestId);
            var subject = $"Service Request {serviceRequestId} — Status Update: {newStatus}";
            var htmlBody = $"<p>Your service request <strong>{serviceRequestId}</strong> at {dealershipName} has been updated to: <strong>{newStatus}</strong>.</p>";
            await _emailService.SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
            sent = true;
        }

        if (!smsOptOut && !string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            _logger.LogInformation("Sending status change via SMS for SR {ServiceRequestId}", serviceRequestId);
            await _smsService.SendStatusChangeSmsAsync(
                toPhoneNumber, serviceRequestId, newStatus, cancellationToken);
            sent = true;
        }

        if (!sent)
        {
            _logger.LogWarning(
                "Cannot send status change for SR {ServiceRequestId}: no available channel (smsOptOut={SmsOptOut}, emailOptOut={EmailOptOut})",
                serviceRequestId, smsOptOut, emailOptOut);
        }
    }

    /// <inheritdoc />
    public async Task SendMagicLinkAsync(
        bool smsOptOut,
        bool emailOptOut,
        string? toEmail,
        string? toPhoneNumber,
        string magicLinkUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(magicLinkUrl);

        var sent = false;

        if (!emailOptOut && !string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Sending magic link via email");
            var subject = "Your RV Service Flow Status Page Link";
            var htmlBody = $"<p>View your service request status: <a href=\"{magicLinkUrl}\">{magicLinkUrl}</a></p>";
            await _emailService.SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
            sent = true;
        }

        if (!smsOptOut && !string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            _logger.LogInformation("Sending magic link via SMS");
            await _smsService.SendMagicLinkSmsAsync(toPhoneNumber, magicLinkUrl, cancellationToken);
            sent = true;
        }

        if (!sent)
        {
            _logger.LogWarning("Cannot send magic link: no available channel (smsOptOut={SmsOptOut}, emailOptOut={EmailOptOut})",
                smsOptOut, emailOptOut);
        }
    }
}
