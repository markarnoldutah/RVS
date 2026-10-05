using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Sends email through the SendGrid v3 <c>mail/send</c> API. A typed <see cref="HttpClient"/>
/// whose base address and bearer key are set at registration (<c>Program.cs</c>).
///
/// The three <see cref="INotificationService"/> contracts differ in how a failure surfaces:
/// <see cref="SendEmailAsync"/> is fire-and-forget and only logs, <see cref="SendTransactionalEmailAsync"/>
/// returns <c>null</c>, and <see cref="SendPacketEmailAsync"/> throws so the packet pipeline's
/// own retry with backoff runs (<c>Spec B-4</c>, issue #438).
///
/// Every message turns click, open and subscription tracking off. Click tracking rewrites links
/// through <c>sendgrid.net</c>, which would break the packet's photo SAS links and the status link.
/// </summary>
public sealed class SendGridEmailNotificationService : INotificationService
{
    private const string MailSendPath = "v3/mail/send";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SendGridEmailNotificationService> _logger;
    private readonly SendGridAddress _from;

    public SendGridEmailNotificationService(
        HttpClient httpClient,
        IOptions<EmailOptions> options,
        ILogger<SendGridEmailNotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _logger = logger;

        var email = options.Value;
        // No default for the From address, on purpose. The sending domain is per environment —
        // mail.rvintake.com in prod, mail-staging.rvintake.com in staging — and SendGrid refuses
        // a sender on a domain it has not authenticated. PacketGenerationService only logs a
        // packet email that exhausts its retries, so a wrong sender surfaces as a packet that
        // simply never arrives. Better to fail where the cause is legible.
        if (string.IsNullOrWhiteSpace(email.FromAddress))
        {
            throw new InvalidOperationException(
                "Email:FromAddress is not configured. In Azure it is injected by Bicep "
                + "(app-service-config.bicep) as Email__FromAddress; locally it is set in "
                + "appsettings.Development.json.");
        }

        _from = new SendGridAddress(
            email.FromAddress,
            string.IsNullOrWhiteSpace(email.SenderDisplayName) ? null : email.SenderDisplayName);
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task<string?> SendTransactionalEmailAsync(
        string toEmail, string subject, string htmlBody, string plainTextBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextBody);

        try
        {
            using var response = await PostAsync(
                BuildRequest([toEmail], subject, htmlBody, plainTextBody, attachments: null), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Recipient left out on purpose: the caller has the invite id, and this is a customer address.
                await LogRejectionAsync(response, "transactional email", cancellationToken);
                return null;
            }

            var messageId = MessageIdOf(response);
            _logger.LogInformation("SendGrid accepted transactional email {MessageId}", messageId);
            return messageId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to send transactional email via SendGrid");
            return null;
        }
    }

    /// <inheritdoc />
    public Task SendEmailAsync(
        string toEmail, string subject, string htmlBody, string plainTextBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextBody);

        _ = FireAndForgetAsync(toEmail, subject, htmlBody, plainTextBody);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendPacketEmailAsync(PacketEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.HtmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.PlainTextBody);

        // SendGrid rejects the whole request when one personalization names an address twice.
        var recipients = message.Recipients
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            throw new ArgumentException("A packet email needs at least one recipient.", nameof(message));
        }

        var attachments = message.Attachments
            .Select(a => new SendGridAttachment(Convert.ToBase64String(a.Content.Span), a.ContentType, a.FileName))
            .ToList();

        using var response = await PostAsync(
            BuildRequest(recipients, message.Subject, message.HtmlBody, message.PlainTextBody, attachments),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogRejectionAsync(response, "packet email", cancellationToken);
            // Let it propagate: the caller owns idempotency and retry-with-backoff (Spec B-4, issue #438).
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation(
            "SendGrid accepted packet email {MessageId} to {RecipientCount} recipient(s) with {AttachmentCount} attachment(s)",
            MessageIdOf(response), recipients.Count, attachments.Count);
    }

    private async Task FireAndForgetAsync(string toEmail, string subject, string htmlBody, string plainTextBody)
    {
        try
        {
            using var response = await PostAsync(
                BuildRequest([toEmail], subject, htmlBody, plainTextBody, attachments: null),
                CancellationToken.None);

            if (!response.IsSuccessStatusCode)
            {
                await LogRejectionAsync(response, "email", CancellationToken.None);
                return;
            }

            _logger.LogInformation("SendGrid accepted email {MessageId} to {Recipient}", MessageIdOf(response), toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SendGrid to {Recipient}", toEmail);
        }
    }

    private Task<HttpResponseMessage> PostAsync(SendGridMailRequest request, CancellationToken cancellationToken) =>
        _httpClient.PostAsJsonAsync(MailSendPath, request, cancellationToken);

    private SendGridMailRequest BuildRequest(
        IReadOnlyList<string> recipients,
        string subject,
        string htmlBody,
        string plainTextBody,
        IReadOnlyList<SendGridAttachment>? attachments)
    {
        // SendGrid requires text/plain to come before text/html.
        IReadOnlyList<SendGridContent> content =
        [
            new SendGridContent("text/plain", plainTextBody),
            new SendGridContent("text/html", htmlBody),
        ];

        return new SendGridMailRequest(
            Personalizations: [new SendGridPersonalization(recipients.Select(r => new SendGridAddress(r, null)).ToList())],
            From: _from,
            Subject: subject,
            Content: content,
            // SendGrid rejects an empty attachments array, so leave it out entirely.
            Attachments: attachments is { Count: > 0 } ? attachments : null,
            TrackingSettings: SendGridTrackingSettings.AllOff);
    }

    /// <summary>
    /// SendGrid returns the message id in a header on its <c>202 Accepted</c>. It always has in
    /// practice; an empty string still records the send as accepted rather than failed.
    /// </summary>
    private static string MessageIdOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("X-Message-Id", out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;

    /// <summary>
    /// Logs a refusal with SendGrid's error body, which names the field at fault and carries no
    /// message content. Truncated so a pathological body cannot flood the log.
    /// </summary>
    private async Task LogRejectionAsync(HttpResponseMessage response, string kind, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "SendGrid refused {Kind}: HTTP {StatusCode}: {Body}",
            kind, (int)response.StatusCode, body.Length > 1000 ? body[..1000] : body);
    }

    private sealed record SendGridMailRequest(
        [property: JsonPropertyName("personalizations")] IReadOnlyList<SendGridPersonalization> Personalizations,
        [property: JsonPropertyName("from")] SendGridAddress From,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("content")] IReadOnlyList<SendGridContent> Content,
        [property: JsonPropertyName("attachments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<SendGridAttachment>? Attachments,
        [property: JsonPropertyName("tracking_settings")] SendGridTrackingSettings TrackingSettings);

    private sealed record SendGridPersonalization(
        [property: JsonPropertyName("to")] IReadOnlyList<SendGridAddress> To);

    private sealed record SendGridAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name);

    private sealed record SendGridContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value);

    private sealed record SendGridAttachment(
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("filename")] string FileName)
    {
        [JsonPropertyName("disposition")]
        public string Disposition => "attachment";
    }

    private sealed record SendGridTrackingSettings(
        [property: JsonPropertyName("click_tracking")] SendGridClickTracking ClickTracking,
        [property: JsonPropertyName("open_tracking")] SendGridToggle OpenTracking,
        [property: JsonPropertyName("subscription_tracking")] SendGridToggle SubscriptionTracking)
    {
        public static readonly SendGridTrackingSettings AllOff = new(
            new SendGridClickTracking(Enable: false, EnableText: false),
            new SendGridToggle(Enable: false),
            new SendGridToggle(Enable: false));
    }

    private sealed record SendGridClickTracking(
        [property: JsonPropertyName("enable")] bool Enable,
        [property: JsonPropertyName("enable_text")] bool EnableText);

    private sealed record SendGridToggle(
        [property: JsonPropertyName("enable")] bool Enable);
}
