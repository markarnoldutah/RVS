using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Routes transactional notifications to the email or SMS channel. The customer's preferred
/// contact method chooses the channel and <c>smsOptOut</c> / <c>emailOptOut</c> are a hard veto
/// over it (<c>Spec A-2</c>, issues #577 and #662). Exactly one confirmation is sent, or none.
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
        string? preferredContact,
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

        var smsPermitted = _smsService.IsEnabled && !smsOptOut && !string.IsNullOrWhiteSpace(toPhoneNumber);
        var emailPermitted = !emailOptOut && !string.IsNullOrWhiteSpace(toEmail);
        var prefersText = string.Equals(
            preferredContact?.Trim(), PreferredContactMethod.Text, StringComparison.OrdinalIgnoreCase);

        if (prefersText && smsPermitted)
        {
            await SendSmsAsync(tenantId, locationId, toPhoneNumber!, serviceRequestId, dealershipName, statusUrl, dealerPhone, cancellationToken);
            return;
        }

        if (emailPermitted)
        {
            if (prefersText)
            {
                _logger.LogWarning(
                    "SR {ServiceRequestId} prefers Text but SMS is unavailable (smsEnabled={SmsEnabled}, smsOptOut={SmsOptOut}, hasPhone={HasPhone}); confirming by email",
                    serviceRequestId, _smsService.IsEnabled, smsOptOut, !string.IsNullOrWhiteSpace(toPhoneNumber));
            }

            _logger.LogInformation("Sending SR confirmation via email for SR {ServiceRequestId}", serviceRequestId);
            var subject = ServiceRequestConfirmationContent.BuildEmailSubject(dealershipName);
            var htmlBody = ServiceRequestConfirmationContent.BuildEmailHtmlBody(dealershipName, statusUrl, dealerPhone);
            await _emailService.SendEmailAsync(toEmail!, subject, htmlBody, cancellationToken);
            return;
        }

        if (smsPermitted)
        {
            await SendSmsAsync(tenantId, locationId, toPhoneNumber!, serviceRequestId, dealershipName, statusUrl, dealerPhone, cancellationToken);
            return;
        }

        _logger.LogWarning(
            "Cannot send SR confirmation for SR {ServiceRequestId}: no permitted channel (preferredContact={PreferredContact}, smsEnabled={SmsEnabled}, smsOptOut={SmsOptOut}, emailOptOut={EmailOptOut})",
            serviceRequestId, preferredContact, _smsService.IsEnabled, smsOptOut, emailOptOut);
    }

    private async Task SendSmsAsync(
        string tenantId, string locationId, string toPhoneNumber, string serviceRequestId,
        string dealershipName, string statusUrl, string? dealerPhone, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending SR confirmation via SMS for SR {ServiceRequestId}", serviceRequestId);
        var message = ServiceRequestConfirmationContent.BuildSmsBody(dealershipName, statusUrl, dealerPhone);
        await _smsService.SendSmsAsync(tenantId, locationId, toPhoneNumber, message, cancellationToken);
    }
}
