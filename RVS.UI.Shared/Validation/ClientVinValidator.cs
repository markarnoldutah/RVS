using RVS.Domain.Validation;

namespace RVS.UI.Shared.Validation;

/// <summary>
/// Client-side VIN format validation helper that delegates to the domain
/// <see cref="VinValidator"/> for 17-character length, allowed-character,
/// and check-digit verification.
/// </summary>
public static class ClientVinValidator
{
    /// <summary>
    /// Validates a VIN string for correct format, allowed characters, and check digit.
    /// </summary>
    /// <param name="vin">The VIN to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var result = ClientVinValidator.Validate("1HGBH41JXMN109186");
    /// // result.IsValid == true
    /// </code>
    /// </example>
    public static ValidationResult Validate(string vin) => VinValidator.Validate(vin);
}
