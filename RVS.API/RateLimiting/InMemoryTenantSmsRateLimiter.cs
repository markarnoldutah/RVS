using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.RateLimiting;

/// <summary>
/// Sliding one-hour window of accepted sends per tenant, capped at
/// <see cref="SmsOptions.MaxMessagesPerTenantPerHour"/> (issue #661). Registered singleton.
///
/// The count is per process. The API runs as a single App Service instance, so that is the
/// whole count today; scaling out would multiply the effective limit by the instance count.
/// </summary>
public sealed class InMemoryTenantSmsRateLimiter : ITenantSmsRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly int _maxPerHour;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _sends = new(StringComparer.Ordinal);

    public InMemoryTenantSmsRateLimiter(IOptions<SmsOptions> options, TimeProvider timeProvider)
    {
        _maxPerHour = options.Value.MaxMessagesPerTenantPerHour;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public bool TryAcquire(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var now = _timeProvider.GetUtcNow();
        var sends = _sends.GetOrAdd(tenantId, _ => new Queue<DateTimeOffset>());

        lock (sends)
        {
            while (sends.Count > 0 && now - sends.Peek() >= Window)
            {
                sends.Dequeue();
            }

            if (sends.Count >= _maxPerHour)
            {
                return false;
            }

            sends.Enqueue(now);
            return true;
        }
    }
}
