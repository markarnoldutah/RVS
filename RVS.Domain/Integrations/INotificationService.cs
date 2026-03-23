namespace RVS.Domain.Integrations;

/// <summary>
/// Sends transactional notifications (email) via SendGrid in production,
/// with a no-op implementation for local development.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a generic email message.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML-formatted email body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a confirmation email for a newly submitted service request.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="serviceRequestId">Identifier of the confirmed service request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendServiceRequestConfirmationAsync(string toEmail, string serviceRequestId, CancellationToken cancellationToken = default);
}
