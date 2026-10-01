using System.Collections.Frozen;

namespace RVS.Domain.Validation;

/// <summary>
/// The curated IANA time-zone vocabulary for a dealership location (issue #506).
///
/// It exists because <see cref="TimeZoneInfo"/> has no abbreviation API:
/// <see cref="TimeZoneInfo.StandardName"/> and <see cref="TimeZoneInfo.DaylightName"/> are ICU
/// strings — long ("Mountain Daylight Time"), locale-dependent, and free to carry non-ASCII.
/// The packet's Received line is interpolated into a CSS <c>content:</c> literal and has to
/// render byte-for-byte identically on every host, so the short spelling comes from this table
/// rather than from the operating system.
///
/// The table is a pure static list — it makes no <see cref="TimeZoneInfo"/> call — so it is
/// equally safe in the Blazor WASM bundle, where the runtime time-zone database may not ship.
/// It is deliberately not the set of storable values: see <see cref="TimeZoneValidator"/>,
/// which also accepts any zone the host can resolve.
/// </summary>
public static class DealershipTimeZones
{
    /// <summary>A single curated zone.</summary>
    /// <param name="IanaId">The IANA (Olson) identifier persisted on <c>Location.TimeZoneId</c>.</param>
    /// <param name="DisplayName">Label for the manager app's picker.</param>
    /// <param name="StandardAbbreviation">Short spelling outside daylight saving, e.g. <c>MST</c>.</param>
    /// <param name="DaylightAbbreviation">
    /// Short spelling during daylight saving, e.g. <c>MDT</c>; <c>null</c> for a zone that does
    /// not observe it (Arizona, Hawaii).
    /// </param>
    public sealed record DealershipTimeZone(
        string IanaId,
        string DisplayName,
        string StandardAbbreviation,
        string? DaylightAbbreviation);

    /// <summary>The curated zones, in picker display order: US east to west, then AK/HI, then Canada.</summary>
    public static IReadOnlyList<DealershipTimeZone> All { get; } =
    [
        new("America/New_York",             "Eastern Time — New York",        "EST", "EDT"),
        new("America/Detroit",              "Eastern Time — Detroit",         "EST", "EDT"),
        new("America/Indiana/Indianapolis", "Eastern Time — Indianapolis",    "EST", "EDT"),
        new("America/Chicago",              "Central Time — Chicago",         "CST", "CDT"),
        new("America/Denver",               "Mountain Time — Denver",         "MST", "MDT"),
        new("America/Boise",                "Mountain Time — Boise",          "MST", "MDT"),
        new("America/Phoenix",              "Mountain Time — Phoenix (no DST)", "MST", null),
        new("America/Los_Angeles",          "Pacific Time — Los Angeles",     "PST", "PDT"),
        new("America/Anchorage",            "Alaska Time — Anchorage",        "AKST", "AKDT"),
        new("Pacific/Honolulu",             "Hawaii Time — Honolulu (no DST)", "HST", null),
        new("America/Toronto",              "Eastern Time — Toronto",         "EST", "EDT"),
    ];

    /// <summary>
    /// Case-insensitive, matching <see cref="TimeZoneInfo"/>'s own lookup behaviour so a
    /// hand-typed <c>america/denver</c> still resolves.
    /// </summary>
    private static readonly FrozenDictionary<string, DealershipTimeZone> ById =
        All.ToFrozenDictionary(zone => zone.IanaId, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether this id is one the product curates an abbreviation and a label for.</summary>
    public static bool IsCurated(string? ianaId) =>
        !string.IsNullOrWhiteSpace(ianaId) && ById.ContainsKey(ianaId);

    /// <summary>Looks up a curated zone by its IANA id.</summary>
    public static bool TryGet(string? ianaId, out DealershipTimeZone zone)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
        {
            zone = null!;
            return false;
        }

        return ById.TryGetValue(ianaId, out zone!);
    }

    /// <summary>
    /// The short spelling for this zone at a given instant. A zone with no daylight spelling
    /// keeps its standard one whatever <paramref name="isDaylightSaving"/> says — that is the
    /// defensive path if tzdata ever gives Arizona a daylight rule.
    /// </summary>
    public static bool TryGetAbbreviation(string? ianaId, bool isDaylightSaving, out string abbreviation)
    {
        if (!TryGet(ianaId, out var zone))
        {
            abbreviation = string.Empty;
            return false;
        }

        abbreviation = isDaylightSaving
            ? zone.DaylightAbbreviation ?? zone.StandardAbbreviation
            : zone.StandardAbbreviation;
        return true;
    }
}
