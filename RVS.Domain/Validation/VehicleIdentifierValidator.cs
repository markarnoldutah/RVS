namespace RVS.Domain.Validation;

/// <summary>
/// Validates what a customer gives as their RV's identifier: a 17-character VIN or, for a rig
/// that has none, the manufacturer's serial number. A truck camper has no wheels, so it is not a
/// motor vehicle and carries a short "Vehicle Serial No." instead of a VIN (issue #807).
/// </summary>
/// <remarks>
/// A 17-character value must be a well-formed VIN: at that length a letter O or I is a misread
/// or a typo, not a serial number. The VIN check digit is not enforced here — a decode that
/// fails already degrades to manual entry (Spec A-3).
/// </remarks>
public static class VehicleIdentifierValidator
{
    /// <summary>The longest identifier accepted.</summary>
    public const int MaxLength = 20;

    private const int VinLength = 17;

    /// <summary>
    /// Trims, uppercases and removes whitespace, so "152 263" and "152263" are the same serial.
    /// Returns an empty string for a null or blank value.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    }

    /// <summary>
    /// Validates a VIN or serial number: letters, digits and hyphens only, at least one digit,
    /// at most <see cref="MaxLength"/> characters, and a well-formed VIN when 17 characters long.
    /// The value is normalized first.
    /// </summary>
    public static ValidationResult Validate(string? value)
    {
        var normalized = Normalize(value);

        if (normalized.Length == 0)
        {
            return ValidationResult.Failure("Enter your RV's VIN or serial number.");
        }

        if (normalized.Length > MaxLength)
        {
            return ValidationResult.Failure(
                $"A VIN or serial number must not exceed {MaxLength} characters.");
        }

        if (!normalized.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-'))
        {
            return ValidationResult.Failure(
                "A VIN or serial number may contain only letters, numbers and hyphens.");
        }

        if (!normalized.Any(char.IsAsciiDigit))
        {
            return ValidationResult.Failure(
                "A VIN or serial number contains at least one number.");
        }

        if (normalized.Length == VinLength && !VinValidator.ValidateFormat(normalized).IsValid)
        {
            return ValidationResult.Failure(
                "This looks like a VIN, but a VIN never contains the letters I, O or Q. Please double-check it.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Whether the value is a well-formed 17-character VIN — the only kind of identifier worth
    /// sending to the VIN decoder, and the only kind unique enough to key vehicle history on.
    /// </summary>
    public static bool IsVin(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length == VinLength && VinValidator.ValidateFormat(normalized).IsValid;
    }
}
