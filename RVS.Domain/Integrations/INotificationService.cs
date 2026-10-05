namespace RVS.Domain.Integrations;

/// <summary>
/// Sends transactional email notifications via SendGrid, with a no-op implementation wherever
/// no SendGrid API key is configured.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Whether this service actually sends. <c>false</c> for the no-op implementation, which is
    /// registered wherever no SendGrid API key is configured. The advisor invite dialog reads it to
    /// decide whether to offer email (<c>Spec A-14</c>, issue #693).
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sends a generic email message, fire-and-forget: a failure is logged, never thrown.
    /// Every message carries a plain-text alternative (issue #829), which some spam filters
    /// expect and which plain-text clients show instead of the HTML.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML-formatted email body.</param>
    /// <param name="plainTextBody">The same message as plain text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendEmailAsync(
        string toEmail, string subject, string htmlBody, string plainTextBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends one email and reports whether SendGrid took it, for a caller that records the outcome:
    /// today the emailed advisor intake invite (<c>Spec A-14</c>, issue #693). Unlike
    /// <see cref="SendEmailAsync"/> it awaits the submit call; unlike
    /// <see cref="SendPacketEmailAsync"/> it never throws for a failed send.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML-formatted email body.</param>
    /// <param name="plainTextBody">Plain-text alternative body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The SendGrid message id when SendGrid accepted the message; <c>null</c> when nothing was
    /// sent or SendGrid rejected it.
    /// </returns>
    /// <exception cref="System.ArgumentException">An argument is null, empty or whitespace.</exception>
    Task<string?> SendTransactionalEmailAsync(
        string toEmail, string subject, string htmlBody, string plainTextBody,
        CancellationToken cancellationToken = default);

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
