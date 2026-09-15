namespace RVS.Domain.Validation;

/// <summary>
/// Composes the stored name of a location provisioned through the platform-admin tool
/// (Spec P-1 / P-5, issue #623): the business name always comes first, e.g. tenant
/// <c>Blue Compass</c> + location <c>Tucson</c> → <c>Blue Compass - Tucson</c>.
/// <para>
/// A location name that already starts with the business name (whole words, any case, followed by
/// a separator such as <c>-</c>, <c>—</c> or <c>:</c>) is normalized instead of prefixed twice.
/// The resulting name slugifies the same as the bare location name under
/// <see cref="SlugGenerator.ForLocation"/>, so intake slugs are unaffected.
/// </para>
/// </summary>
public static class LocationNameGenerator
{
    /// <summary>Joins the business name and the location name.</summary>
    public const string Separator = " - ";

    /// <summary>
    /// Returns <c>{businessName} - {locationName}</c>, both trimmed. When the location name is only
    /// the business name, the business name is returned on its own.
    /// </summary>
    /// <exception cref="ArgumentException">Either name is null, empty or whitespace.</exception>
    public static string ForBusiness(string businessName, string locationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(businessName);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);

        var business = businessName.Trim();
        var location = StripBusinessPrefix(business, locationName.Trim());

        return location.Length == 0 ? business : $"{business}{Separator}{location}";
    }

    private static string StripBusinessPrefix(string business, string location)
    {
        if (!location.StartsWith(business, StringComparison.OrdinalIgnoreCase))
        {
            return location;
        }

        var rest = location[business.Length..];

        // "Nova" is not a prefix of "Novato": the business name must end on a word boundary.
        if (rest.Length > 0 && char.IsLetterOrDigit(rest[0]))
        {
            return location;
        }

        return rest.TrimStart(' ', '\t', '-', '–', '—', ':', ',', '|', '/').TrimEnd();
    }
}
