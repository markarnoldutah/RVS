namespace RVS.Domain.Validation;

/// <summary>
/// Builds the asset ID that keys a rig's vehicle history: ownership on the
/// <c>CustomerProfile</c>, the X-2 asset ledger and <c>GlobalCustomerAcct.AllKnownAssetIds</c>.
/// A VIN keys history on its own. A serial number is not unique across manufacturers, so it
/// keys history only together with the manufacturer, as <c>LANCE:152263</c> (issue #808).
/// </summary>
/// <remarks>
/// The key is internal. The request, the packet and the Manager show the serial number as the
/// customer gave it; <see cref="ToIdentifier"/> recovers it from a key.
/// </remarks>
public static class VehicleHistoryKey
{
    private const char Separator = ':';

    // Words that say what kind of company it is rather than which one, stripped from the end of
    // the name only. Brand words such as "Coach" and "Motor" stay: dropping them would merge
    // Thor Motor Coach with Thor Industries, and a false merge is worse than a split history.
    private static readonly HashSet<string> TrailingNoise = new(StringComparer.Ordinal)
    {
        "INC", "INCORPORATED", "LLC", "LTD", "LIMITED", "CORP", "CORPORATION", "CO", "COMPANY",
        "MFG", "MFR", "MANUFACTURING", "INDUSTRIES", "GROUP",
        "CAMPER", "CAMPERS", "TRAILER", "TRAILERS", "RV", "RVS",
    };

    /// <summary>
    /// Reduces a manufacturer name to a stable slug, so "Lance", "Lance Camper" and
    /// "LANCE CAMPER MFG. CORP" are one manufacturer: uppercase ASCII letters and digits only,
    /// a leading "The" and trailing corporate or product-type words removed, spaces closed up.
    /// Returns an empty string when nothing identifying is left.
    /// </summary>
    public static string NormalizeManufacturer(string? manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return string.Empty;
        }

        var words = new string(manufacturer
                .Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ')
                .ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (words.Count > 0 && words[0] == "THE")
        {
            words.RemoveAt(0);
        }

        while (words.Count > 0 && TrailingNoise.Contains(words[^1]))
        {
            words.RemoveAt(words.Count - 1);
        }

        return string.Concat(words);
    }

    /// <summary>
    /// The vehicle-history key for an identifier: the normalised VIN for a well-formed VIN;
    /// <c>MANUFACTURER:SERIAL</c> for a serial number with a manufacturer; otherwise
    /// <see langword="null"/>, meaning the request records no vehicle history. That covers a
    /// blank or invalid identifier and a serial number without a usable manufacturer.
    /// </summary>
    public static string? For(string? identifier, string? manufacturer)
    {
        var normalized = VehicleIdentifierValidator.Normalize(identifier);
        if (normalized.Length == 0 || !VehicleIdentifierValidator.Validate(normalized).IsValid)
        {
            return null;
        }

        if (VehicleIdentifierValidator.IsVin(normalized))
        {
            return normalized;
        }

        var maker = NormalizeManufacturer(manufacturer);
        return maker.Length == 0 ? null : $"{maker}{Separator}{normalized}";
    }

    /// <summary>
    /// The VIN or serial number a key was built from: everything after the manufacturer of a
    /// composite key, or the key itself for a VIN.
    /// </summary>
    public static string ToIdentifier(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var separatorIndex = key.LastIndexOf(Separator);
        return separatorIndex < 0 ? key : key[(separatorIndex + 1)..];
    }
}
