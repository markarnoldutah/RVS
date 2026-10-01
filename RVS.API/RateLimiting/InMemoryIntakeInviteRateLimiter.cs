using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.RateLimiting;

/// <summary>
/// Sliding one-hour windows of texted intake invites per advisor, per location and per tenant,
/// capped by <see cref="IntakeInviteOptions"/> (<c>Spec A-14</c>, issue #663). Registered singleton.
///
/// One lock covers all three windows, so a send either takes a slot from every allowance or from
/// none of them. Invites are sent by hand during a phone call; contention is not a concern.
///
/// The count is per process, like <see cref="InMemoryTenantSmsRateLimiter"/>: the API runs as a
/// single App Service instance, and scaling out would multiply the effective limits.
/// </summary>
public sealed class InMemoryIntakeInviteRateLimiter : IIntakeInviteRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly IntakeInviteOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, Queue<DateTimeOffset>> _sends = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public InMemoryIntakeInviteRateLimiter(IOptions<IntakeInviteOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public IntakeInviteRateLimitResult TryAcquire(string tenantId, string locationId, string advisorUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(advisorUserId);

        // Keys are prefixed by scope and nested under the tenant, so ids from different
        // tenants or scopes can never share a window.
        (string Key, int Max, IntakeInviteRateLimitResult Reached)[] scopes =
        [
            ($"a|{tenantId}|{advisorUserId}", _options.MaxPerAdvisorPerHour, IntakeInviteRateLimitResult.AdvisorLimitReached),
            ($"l|{tenantId}|{locationId}", _options.MaxPerLocationPerHour, IntakeInviteRateLimitResult.LocationLimitReached),
            ($"t|{tenantId}", _options.MaxPerTenantPerHour, IntakeInviteRateLimitResult.TenantLimitReached),
        ];

        var now = _timeProvider.GetUtcNow();

        lock (_lock)
        {
            foreach (var (key, max, reached) in scopes)
            {
                if (Prune(key, now) >= max)
                {
                    return reached;
                }
            }

            foreach (var (key, _, _) in scopes)
            {
                if (!_sends.TryGetValue(key, out var sends))
                {
                    sends = new Queue<DateTimeOffset>();
                    _sends[key] = sends;
                }

                sends.Enqueue(now);
            }

            return IntakeInviteRateLimitResult.Allowed;
        }
    }

    /// <summary>Drops sends older than the window and returns how many remain.</summary>
    private int Prune(string key, DateTimeOffset now)
    {
        if (!_sends.TryGetValue(key, out var sends))
        {
            return 0;
        }

        while (sends.Count > 0 && now - sends.Peek() >= Window)
        {
            sends.Dequeue();
        }

        if (sends.Count == 0)
        {
            _sends.Remove(key);
        }

        return sends.Count;
    }
}
