namespace RVS.Domain.Integrations;

/// <summary>
/// Sends transactional email notifications via Azure Communication Services in production,
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
    /// Sends the composed service-packet email to the service department (<c>Spec B-4</c>,
    /// issue #437): the packet HTML as the inline body, its paste block as the plain-text
    /// alternative, the PDF and original photos as attachments, to every address in
    /// <see cref="PacketEmailMessage.Recipients"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="SendEmailAsync"/> this awaits the transport's submit call and lets a
    /// failure propagate, so the caller can react. Idempotency and retry with backoff are
    /// layered on by the caller (issue #438), not here.
    /// </remarks>
    /// <param name="message">The fully-composed packet email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
    /// <exception cref="System.ArgumentException"><paramref name="message"/> has no recipients or an empty body.</exception>
    Task SendPacketEmailAsync(PacketEmailMessage message, CancellationToken cancellationToken = default);
}
