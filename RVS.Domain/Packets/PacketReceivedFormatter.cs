using System.Globalization;
using RVS.Domain.Validation;

namespace RVS.Domain.Packets;

/// <summary>
/// Composes the packet's <c>Received</c> line (<c>Spec B-2</c>, issue #506 finishing #492
/// item 4). This is the only place the line is formatted: both renderers reach it through
/// <see cref="PacketOrigin.ReceivedDisplay"/> rather than building the string themselves, so
/// the HTML and the PDF cannot drift.
///
/// Output is always ASCII with no quote or backslash — the HTML renderer interpolates it into
/// a CSS <c>content:</c> literal for the running footer. That holds because the zone's short
/// spelling comes from <see cref="DealershipTimeZones"/> and never from
/// <see cref="TimeZoneInfo"/>'s locale-dependent display names.
/// </summary>
public static class PacketReceivedFormatter
{
    private const string Pattern = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// The Received string for a packet masthead. Never throws: an unset, unrecognised, or
    /// unresolvable zone falls back to <see cref="FormatUtc"/>, which is what every packet
    /// rendered before a location could carry a time zone.
    /// </summary>
    /// <param name="submittedAtUtc">When the request was submitted.</param>
    /// <param name="locationTimeZoneId">The location's IANA id, or <c>null</c> when unset.</param>
    public static string Format(DateTimeOffset submittedAtUtc, string? locationTimeZoneId)
    {
        if (string.IsNullOrWhiteSpace(locationTimeZoneId))
        {
            return FormatUtc(submittedAtUtc);
        }

        // TryFind… never throws, so there is no exception path to swallow here.
        return !TimeZoneInfo.TryFindSystemTimeZoneById(locationTimeZoneId, out var zone)
            ? FormatUtc(submittedAtUtc)
            : Format(submittedAtUtc, zone, locationTimeZoneId);
    }

    /// <summary>
    /// The Received string for an already-resolved zone. The deterministic seam: no host
    /// lookup, so a caller can pin the zone regardless of the machine's time-zone database.
    /// </summary>
    /// <param name="submittedAtUtc">When the request was submitted.</param>
    /// <param name="zone">The resolved zone, or <c>null</c> for the UTC form.</param>
    /// <param name="ianaIdForAbbreviation">
    /// The IANA id to look the short spelling up under. When it is not curated the offset is
    /// rendered numerically instead.
    /// </param>
    public static string Format(DateTimeOffset submittedAtUtc, TimeZoneInfo? zone, string? ianaIdForAbbreviation = null)
    {
        if (zone is null)
        {
            return FormatUtc(submittedAtUtc);
        }

        // Both DateTimeOffset overloads: we convert from an instant, so the fall-back-hour
        // ambiguity that bites local-to-UTC conversion cannot arise and there is no
        // DateTime.Kind to misread.
        var local = TimeZoneInfo.ConvertTime(submittedAtUtc, zone);
        var stamp = local.ToString(Pattern, CultureInfo.InvariantCulture);
        var isDaylightSaving = zone.IsDaylightSavingTime(submittedAtUtc);

        return DealershipTimeZones.TryGetAbbreviation(ianaIdForAbbreviation, isDaylightSaving, out var abbreviation)
            ? $"{stamp} {abbreviation}"
            : $"{stamp} ({OffsetLabel(local.Offset)})";
    }

    /// <summary>
    /// The always-UTC form, <c>yyyy-MM-dd HH:mm UTC</c> — the documented fallback, and the
    /// exact string every packet carried before issue #506.
    /// </summary>
    public static string FormatUtc(DateTimeOffset submittedAtUtc) =>
        submittedAtUtc.ToUniversalTime().ToString(Pattern, CultureInfo.InvariantCulture) + " UTC";

    /// <summary>
    /// <c>UTC-06:00</c> / <c>UTC+05:30</c>. Built explicitly rather than through
    /// <see cref="TimeSpan"/>'s default formatting so the sign is always present and the width
    /// never varies.
    /// </summary>
    private static string OffsetLabel(TimeSpan offset) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"UTC{(offset < TimeSpan.Zero ? '-' : '+')}{offset.Duration():hh\\:mm}");
}
