using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// No-op SMS notification service, registered whenever SMS is disabled, mocks are on, or no ACS
/// endpoint is configured. Logs the notification details but performs no external calls.
/// </summary>
public sealed class NoOpSmsNotificationService : ISmsNotificationService
{
    private readonly ILogger<NoOpSmsNotificationService> _logger;

    public NoOpSmsNotificationService(ILogger<NoOpSmsNotificationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task SendSmsAsync(
        string tenantId, string locationId, string toPhoneNumber, string message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "NoOpSmsNotificationService: Would send SMS for tenant {TenantId}, location {LocationId} to {Recipient}: {Message}",
            tenantId, locationId, toPhoneNumber, message);
        return Task.CompletedTask;
    }
}
