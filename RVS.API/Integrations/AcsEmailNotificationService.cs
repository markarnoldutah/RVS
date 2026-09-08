using Azure.Communication.Email;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Sends transactional emails via Azure Communication Services Email.
/// Uses fire-and-forget semantics — errors are logged but never thrown to the caller.
/// Replaces the previous SendGrid-based implementation.
/// </summary>
public sealed class AcsEmailNotificationService : INotificationService
{
    private readonly EmailClient _emailClient;
    private readonly ILogger<AcsEmailNotificationService> _logger;
    private readonly string _fromAddress;
    private readonly string _senderDisplayName;

    public AcsEmailNotificationService(
        EmailClient emailClient,
        ILogger<AcsEmailNotificationService> logger,
        IConfiguration configuration)
    {
        _emailClient = emailClient;
        _logger = logger;
        _fromAddress = configuration["AzureCommunicationServices:Email:FromAddress"]
            ?? "noreply@notifications.rvserviceflow.com";
        _senderDisplayName = configuration["AzureCommunicationServices:Email:SenderDisplayName"]
            ?? "RV Service Flow";
    }

    /// <inheritdoc />
    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);

        _ = FireAndForgetAsync(toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendServiceRequestConfirmationAsync(string toEmail, string serviceRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        var subject = $"Service Request {serviceRequestId} Confirmed";
        var htmlBody = $"<p>Your service request <strong>{serviceRequestId}</strong> has been received and is being processed.</p>";

        _ = FireAndForgetAsync(toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendPacketEmailAsync(PacketEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.HtmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.PlainTextBody);

        var recipients = message.Recipients
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => new EmailAddress(r))
            .ToList();

        if (recipients.Count == 0)
        {
            throw new ArgumentException("A packet email needs at least one recipient.", nameof(message));
        }

        var emailMessage = new EmailMessage(
            senderAddress: _fromAddress,
            content: new EmailContent(message.Subject)
            {
                Html = message.HtmlBody,
                PlainText = message.PlainTextBody,
            },
            recipients: new EmailRecipients(recipients));

        foreach (var attachment in message.Attachments)
        {
            emailMessage.Attachments.Add(new EmailAttachment(
                attachment.FileName,
                attachment.ContentType,
                BinaryData.FromBytes(attachment.Content)));
        }

        // Await the submit (WaitUntil.Started) and let failures propagate: the caller owns
        // idempotency and retry-with-backoff (Spec B-4, issue #438).
        var operation = await _emailClient.SendAsync(Azure.WaitUntil.Started, emailMessage, cancellationToken);

        _logger.LogInformation(
            "ACS packet email send initiated to {RecipientCount} recipient(s) with {AttachmentCount} attachment(s), operation {OperationId}",
            recipients.Count, message.Attachments.Count, operation.Id);
    }

    private async Task FireAndForgetAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var emailMessage = new EmailMessage(
                senderAddress: _fromAddress,
                recipientAddress: toEmail,
                content: new EmailContent(subject)
                {
                    Html = htmlBody
                });

            var operation = await _emailClient.SendAsync(Azure.WaitUntil.Started, emailMessage);

            _logger.LogInformation(
                "ACS Email send initiated to {Recipient} with operation {OperationId}",
                toEmail, operation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via ACS to {Recipient}", toEmail);
        }
    }
}
