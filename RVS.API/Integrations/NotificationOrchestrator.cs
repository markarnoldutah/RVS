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
        string tenantId,
        string locationId,
        bool smsOptOut,
        bool emailOptOut,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string dealershipName,
        string statusUrl,
        string? dealerPhone,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipName);
        ArgumentException.ThrowIfNullOrWhiteSpace(statusUrl);

        var sent = false;

        if (!emailOptOut && !string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Sending SR confirmation via email for SR {ServiceRequestId}", serviceRequestId);
            var subject = ServiceRequestConfirmationContent.BuildEmailSubject(dealershipName);
            var htmlBody = ServiceRequestConfirmationContent.BuildEmailHtmlBody(dealershipName, statusUrl, dealerPhone);
            await _emailService.SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
            sent = true;
        }

        if (!smsOptOut && !string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            _logger.LogInformation("Sending SR confirmation via SMS for SR {ServiceRequestId}", serviceRequestId);
            var message = ServiceRequestConfirmationContent.BuildSmsBody(dealershipName, statusUrl, dealerPhone);
            await _smsService.SendSmsAsync(tenantId, locationId, toPhoneNumber, message, cancellationToken);
            sent = true;
        }

        if (!sent)
        {
            _logger.LogWarning(
                "Cannot send SR confirmation for SR {ServiceRequestId}: no available channel (smsOptOut={SmsOptOut}, emailOptOut={EmailOptOut})",
                serviceRequestId, smsOptOut, emailOptOut);
        }
    }
}
