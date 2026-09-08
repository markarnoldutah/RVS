namespace RVS.Domain.Validation;

/// <summary>
/// Controlled vocabulary for the customer's preferred contact method, collected at intake
/// (<c>Spec A-2</c>) and shown on the service packet (<c>Spec B-2</c> item 2).
///
/// The choice is required in the intake wizard, but the stored value is optional: service
/// requests created before this field existed carry <c>null</c>, and the packet simply omits
/// the line when it is absent.
/// </summary>
public static class PreferredContactMethod
{
    /// <summary>Phone call to the customer's number.</summary>
    public const string Phone = "Phone";

    /// <summary>Text (SMS) message to the customer's number.</summary>
    public const string Text = "Text";

    /// <summary>Email to the customer's address.</summary>
    public const string Email = "Email";

    /// <summary>The canonical values, in display order.</summary>
    public static readonly IReadOnlyList<string> AllowedValues = [Phone, Text, Email];

    /// <summary>
    /// Returns the canonical spelling of <paramref name="value"/> — matched case-insensitively
    /// and with surrounding whitespace ignored — or <c>null</c> when it is blank.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is non-blank but is not one of <see cref="AllowedValues"/>.
    /// </exception>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var match = AllowedValues.FirstOrDefault(
            v => string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase));

        return match ?? throw new ArgumentException(
            $"Preferred contact method must be one of: {string.Join(", ", AllowedValues)}.",
            nameof(value));
    }

    /// <summary>
    /// Whether <paramref name="value"/> is one of <see cref="AllowedValues"/>
    /// (case-insensitive, surrounding whitespace ignored).
    /// </summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && AllowedValues.Any(v => string.Equals(v, value.Trim(), StringComparison.OrdinalIgnoreCase));
}
