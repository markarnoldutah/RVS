using RVS.Domain.Validation;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// Pure logic behind the drawer's schedule controls (<c>Spec C-12</c>, issue #844): which zone
/// the picker opens on, and which zones it offers. Conversion and display are the API's job
/// (<see cref="ServiceSchedule"/>), so nothing here touches a time-zone database.
/// </summary>
public static class ScheduleEditor
{
    /// <summary>
    /// The zone the picker opens on: the booking's own zone, else the location's (P-5), else the
    /// operator's browser zone, else UTC.
    /// </summary>
    public static string DefaultZone(string? storedZone, string? locationZone, string? browserZone) =>
        new[] { storedZone, locationZone, browserZone }
            .FirstOrDefault(z => !string.IsNullOrWhiteSpace(z))?.Trim()
        ?? "UTC";

    /// <summary>
    /// The curated zones, plus <paramref name="selected"/> at the end when it is not one of them —
    /// a browser zone or an older booking's zone must stay selectable, or the select would show blank.
    /// </summary>
    public static IReadOnlyList<ZoneOption> ZoneOptions(string? selected)
    {
        var options = DealershipTimeZones.All.Select(z => new ZoneOption(z.IanaId, z.DisplayName)).ToList();

        if (!string.IsNullOrWhiteSpace(selected) && !DealershipTimeZones.IsCurated(selected))
        {
            options.Add(new ZoneOption(selected.Trim(), selected.Trim()));
        }

        return options;
    }

    /// <summary>One entry in the zone select.</summary>
    public sealed record ZoneOption(string IanaId, string Label);
}
