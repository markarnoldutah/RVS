using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Sends transactional emails via the SendGrid API.
/// Uses fire-and-forget semantics — errors are logged but never thrown to the caller.
/// </summary>
public sealed class SendGridNotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SendGridNotificationService> _logger;
    private readonly string _fromEmail;

    public SendGridNotificationService(
        HttpClient httpClient,
        ILogger<SendGridNotificationService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fromEmail = configuration["SendGrid:FromEmail"] ?? "noreply@rvserviceflow.com";
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

    private async Task FireAndForgetAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var payload = new
            {
                personalizations = new[]
                {
                    new { to = new[] { new { email = toEmail } } }
                },
                from = new { email = _fromEmail },
                subject,
                content = new[]
                {
                    new { type = "text/html", value = htmlBody }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("v3/mail/send", payload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SendGrid returned {StatusCode} when sending email to {Recipient}", response.StatusCode, toEmail);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SendGrid to {Recipient}", toEmail);
        }
    }
}
