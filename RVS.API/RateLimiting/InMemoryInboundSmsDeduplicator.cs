using System.Collections.Concurrent;
using RVS.Domain.Integrations;

namespace RVS.API.RateLimiting;

/// <summary>
/// Remembers the inbound messages RVS has already replied to (issue #665), so a redelivered
/// Event Grid event cannot text the customer a second HELP reply. Registered singleton.
///
/// Per process, like the SMS rate limiters. The API runs as a single App Service instance, so
/// that is the whole memory today; scaling out could let one redelivery through per instance,
/// which costs one duplicate reply rather than anything unsafe.
/// </summary>
public sealed class InMemoryInboundSmsDeduplicator : IInboundSmsDeduplicator
{
    /// <summary>
    /// How long a message id is remembered. Matches the subscription's 24-hour retry window in
    /// <c>eventgrid-acs-sms.bicep</c>: past it, Event Grid has given up, so a repeat of the same
    /// id is a real second text rather than a redelivery.
    /// </summary>
    private static readonly TimeSpan RetryWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Ceiling on remembered ids, so an inbound flood cannot grow this without bound. Well above
    /// a day of real keyword traffic; when it is hit, the oldest entries go first.
    /// </summary>
    public const int MaxTrackedMessages = 10_000;

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryInboundSmsDeduplicator"/>.
    /// </summary>
    public InMemoryInboundSmsDeduplicator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>How many ids are currently remembered. For tests and diagnostics.</summary>
    public int Count => _seen.Count;

    /// <inheritdoc />
    public bool TryBeginHandling(string inboundMessageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inboundMessageId);

        var now = _timeProvider.GetUtcNow();
        Prune(now);

        var isFirstTime = true;
        _seen.AddOrUpdate(
            inboundMessageId,
            now,
            (_, seenAt) =>
            {
                if (now - seenAt < RetryWindow)
                {
                    isFirstTime = false;
                    return seenAt;
                }

                return now;
            });

        return isFirstTime;
    }

    /// <summary>Drops expired ids, and the oldest ones if the ceiling is reached.</summary>
    private void Prune(DateTimeOffset now)
    {
        foreach (var (id, seenAt) in _seen)
        {
            if (now - seenAt >= RetryWindow)
            {
                _seen.TryRemove(id, out _);
            }
        }

        if (_seen.Count < MaxTrackedMessages)
        {
            return;
        }

        foreach (var (id, _) in _seen.OrderBy(entry => entry.Value).Take(_seen.Count - MaxTrackedMessages + 1))
        {
            _seen.TryRemove(id, out _);
        }
    }
}
