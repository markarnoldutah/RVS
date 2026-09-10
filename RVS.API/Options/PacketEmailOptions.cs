using RVS.Domain.Packets;

namespace RVS.API.Options;

/// <summary>
/// Tuning for packet-email delivery (<c>Spec B-4</c>, issues #438 and #521).
/// Bound from the <c>PacketEmail</c> section of <c>appsettings.json</c>; every value has a
/// working default, so a deployment that sets nothing still gets three attempts with backoff
/// and a send sized to what Azure Communication Services accepts.
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

    /// <summary>
    /// Total size budget for one packet email — bodies plus base64-encoded attachments — used
    /// by <see cref="PacketEmailSizeFitter"/> to decide what can be attached (issue #521).
    ///
    /// ACS rejects a request over <see cref="PacketEmailSizeFitter.AcsMaxRequestBytes"/>
    /// (10 MB), and base64 inflates attachment bytes by about a third, so the realistic
    /// payload of raw photo bytes is near 7.5 MB. The default leaves a 500 KB margin under
    /// the hard cap. Raise it only alongside an approved ACS attachment-size increase.
    /// </summary>
    public long MaxRequestBytes { get; set; } = PacketEmailSizeFitter.DefaultMaxRequestBytes;
}
