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

    /// <summary>The display name shown as the sender on every outgoing message.</summary>
    internal string SenderDisplayName => _senderDisplayName;

    public AcsEmailNotificationService(
        EmailClient emailClient,
        ILogger<AcsEmailNotificationService> logger,
        IConfiguration configuration)
    {
        _emailClient = emailClient;
        _logger = logger;
        // No default for the From address, on purpose. The sending domain is per-environment
        // — mail.rvintake.com in prod, mail.staging.rvintake.com in staging — and Bicep injects
        // it as an app setting. Any hardcoded fallback is wrong somewhere, and a wrong sender is
        // not a soft failure: ACS rejects the send outright for an unverified domain, and
        // PacketGenerationService swallows packet-email failures, so it surfaces as a packet that
        // simply never arrives. Better to fail where the cause is legible.
        _fromAddress = configuration["AzureCommunicationServices:Email:FromAddress"] is { } configured
                       && !string.IsNullOrWhiteSpace(configured)
            ? configured
            : throw new InvalidOperationException(
                "AzureCommunicationServices:Email:FromAddress is not configured. In Azure it is injected "
                + "by Bicep (app-service-config.bicep) as AzureCommunicationServices__Email__FromAddress; "
                + "locally it is set in appsettings.Development.json.");

        // A display name does have a sensible default, but it is the customer-facing brand,
        // matching appsettings.json — the packet email is read by a service advisor.
        _senderDisplayName = configuration["AzureCommunicationServices:Email:SenderDisplayName"]
            ?? "RV Intake";
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
