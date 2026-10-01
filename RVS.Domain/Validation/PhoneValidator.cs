namespace RVS.Domain.Validation;

/// <summary>
/// The customer phone rule at intake (<c>Spec A-2</c>): required, at least 10 digits, at most
/// <see cref="MaxLength"/> characters. Applied by the intake wizard and by
/// <c>POST api/intake/{slug}/service-requests</c> (422), so both hold the same line and a
/// hand-built request can't omit the phone (issue #679).
///
/// Deliberately looser than <see cref="PhoneNumberNormalizer"/>. A number that doesn't
/// normalise to E.164 (an extension, a non-NANP number) is still one the shop can call. It is
/// stored as typed and only left out of SMS.
/// </summary>
public static class PhoneValidator
{
    /// <summary>Longest phone number accepted, in characters as typed.</summary>
    public const int MaxLength = 40;

    private const int MinDigits = 10;

    /// <summary>
    /// Validates a customer-entered phone number.
    /// </summary>
    /// <param name="phone">The phone number as entered, e.g. <c>(801) 555-1234</c>.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    public static ValidationResult Validate(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return ValidationResult.Failure("Phone number is required");
        }

        if (phone.Trim().Length > MaxLength)
        {
            return ValidationResult.Failure($"Phone number must not exceed {MaxLength} characters");
        }

        return phone.Count(char.IsAsciiDigit) < MinDigits
            ? ValidationResult.Failure($"Phone number must have at least {MinDigits} digits")
            : ValidationResult.Success;
    }
}
