using System.Globalization;
using RVS.Domain.Packets;

namespace RVS.Domain.Validation;

/// <summary>
/// The one booked start on a service request (<c>Spec C-12</c>, issue #844): a date, an optional
/// time, and the IANA zone the operator booked it in.
///
/// The operator enters wall-clock values; the API converts them here, against the host's
/// time-zone database, so the WASM client never has to (its runtime may ship without one, see
/// <see cref="DealershipTimeZones"/>). The display string is built here too, once, so the board
/// card, the drawer, the list and the customer status page cannot disagree. It is always in the
/// stored zone — never the viewer's.
/// </summary>
public static class ServiceSchedule
{
    /// <summary>Earliest bookable year. Keeps every conversion well inside <see cref="DateTime"/>'s range.</summary>
    public const int MinYear = 2000;

    /// <summary>Latest bookable year.</summary>
    public const int MaxYear = 2100;

    /// <summary>What a surface shows when nothing is booked (<c>Spec C-12</c>).</summary>
    public const string NotScheduledLabel = "Not scheduled";

    /// <summary>
    /// Validates the operator's input. No date means "clear the schedule", whatever zone the form
    /// still carries. A date needs a zone; a time needs a date; and a time the clocks skip on a
    /// spring-forward day is rejected rather than silently moved.
    /// </summary>
    public static ValidationResult Validate(DateOnly? date, TimeOnly? time, string? timeZoneId)
    {
        if (date is null)
        {
            return time is null
                ? ValidationResult.Success
                : ValidationResult.Failure("A scheduled time needs a scheduled date.");
        }

        if (date.Value.Year is < MinYear or > MaxYear)
        {
            return ValidationResult.Failure($"Scheduled date must be between {MinYear} and {MaxYear}.");
        }

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return ValidationResult.Failure("A scheduled date needs a time zone.");
        }

        var zoneResult = TimeZoneValidator.Validate(timeZoneId.Trim());
        if (!zoneResult.IsValid)
        {
            return zoneResult;
        }

        // A curated zone passes TimeZoneValidator even on a host without tzdata; converting needs
        // the real rules, so this is the stricter check.
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var zone))
        {
            return ValidationResult.Failure($"'{timeZoneId.Trim()}' cannot be resolved on this server.");
        }

        if (time is { } t && zone.IsInvalidTime(date.Value.ToDateTime(t, DateTimeKind.Unspecified)))
        {
            return ValidationResult.Failure(
                $"{t.ToString("h:mm tt", CultureInfo.InvariantCulture)} does not exist on "
                + $"{date.Value.ToString("MMM d", CultureInfo.InvariantCulture)} in {timeZoneId.Trim()}: the clocks skip that hour.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Converts validated input to what the request stores, or <c>null</c> to clear it. A
    /// date-only booking is stored as that day's first valid instant in the zone (midnight in
    /// every US zone). A repeated fall-back time is taken at its first occurrence.
    /// </summary>
    /// <exception cref="ArgumentException">The input fails <see cref="Validate"/>.</exception>
    public static ScheduledStart? Resolve(DateOnly? date, TimeOnly? time, string? timeZoneId)
    {
        var result = Validate(date, time, timeZoneId);
        if (!result.IsValid)
        {
            throw new ArgumentException(result.ErrorMessage);
        }

        if (date is null)
        {
            return null;
        }

        var zoneId = timeZoneId!.Trim();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var local = date.Value.ToDateTime(time ?? TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Only reachable for a date-only booking in a zone whose clocks change at midnight.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(15);
        }

        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);

        var startUtc = DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        return new ScheduledStart(startUtc, zoneId, TimeIsSet: time is not null);
    }

    /// <summary>
    /// "Thu Oct 15 · 9:00 AM MDT", or "Thu Oct 15" for a date-only booking; <c>null</c> when
    /// nothing is booked. The zone's short spelling comes from <see cref="DealershipTimeZones"/>;
    /// any other zone shows its numeric offset. Never throws: a zone the host cannot resolve is
    /// shown in UTC.
    /// </summary>
    public static string? Format(DateTime? startUtc, string? timeZoneId, bool timeIsSet)
    {
        if (startUtc is null)
        {
            return null;
        }

        var instant = new DateTimeOffset(DateTime.SpecifyKind(startUtc.Value, DateTimeKind.Utc));
        var zone = ResolveOrUtc(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(instant, zone);

        var date = local.ToString("ddd MMM d", CultureInfo.InvariantCulture);
        if (!timeIsSet)
        {
            return date;
        }

        var time = local.ToString("h:mm tt", CultureInfo.InvariantCulture);
        return $"{date} · {time} {ZoneLabel(zone, timeZoneId, instant, local.Offset)}";
    }

    /// <summary>
    /// The stored instant as wall-clock values in the stored zone, for the drawer's editor. The
    /// time is <c>null</c> for a date-only booking.
    /// </summary>
    public static (DateOnly? Date, TimeOnly? Time) ToLocal(DateTime? startUtc, string? timeZoneId, bool timeIsSet)
    {
        if (startUtc is null)
        {
            return (null, null);
        }

        var instant = new DateTimeOffset(DateTime.SpecifyKind(startUtc.Value, DateTimeKind.Utc));
        var local = TimeZoneInfo.ConvertTime(instant, ResolveOrUtc(timeZoneId)).DateTime;

        return (DateOnly.FromDateTime(local), timeIsSet ? TimeOnly.FromDateTime(local) : null);
    }

    private static TimeZoneInfo ResolveOrUtc(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var zone)
            ? zone
            : TimeZoneInfo.Utc;

    private static string ZoneLabel(TimeZoneInfo zone, string? timeZoneId, DateTimeOffset instant, TimeSpan offset)
    {
        if (DealershipTimeZones.TryGetAbbreviation(timeZoneId?.Trim(), zone.IsDaylightSavingTime(instant), out var abbreviation))
        {
            return abbreviation;
        }

        return zone.BaseUtcOffset == TimeSpan.Zero && !zone.SupportsDaylightSavingTime
            ? "UTC"
            : PacketReceivedFormatter.OffsetLabel(offset);
    }
}

/// <summary>A resolved booking, as <see cref="Entities.ServiceRequest"/> stores it.</summary>
/// <param name="StartUtc">The booked instant; local midnight in the zone for a date-only booking.</param>
/// <param name="TimeZoneId">The IANA zone it was booked in, and is always shown in.</param>
/// <param name="TimeIsSet"><c>false</c> for a date-only booking.</param>
public sealed record ScheduledStart(DateTime StartUtc, string TimeZoneId, bool TimeIsSet);
