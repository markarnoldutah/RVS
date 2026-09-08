namespace RVS.API.Options;

/// <summary>
/// Tuning for packet-email delivery (<c>Spec B-4</c>, issue #438).
/// Bound from the <c>PacketEmail</c> section of <c>appsettings.json</c>; every value has a
/// working default, so a deployment that sets nothing still gets three attempts with backoff.
/// </summary>
public sealed class PacketEmailOptions
{
    /// <summary>
    /// Base wait before the second delivery attempt. Each subsequent retry doubles it
    /// (attempt 2 waits <c>RetryBaseDelay</c>, attempt 3 waits <c>2 × RetryBaseDelay</c>), keeping
    /// three attempts well inside the <c>Spec B-4</c> 60-second P99 delivery target. Set to
    /// <see cref="System.TimeSpan.Zero"/> in tests to retry without waiting.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);
}
