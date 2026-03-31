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
    public static ValidationResult Validate(string vin) => VinValidator.Validate(vin);

    /// <summary>
    /// Validates a VIN string for correct format and allowed characters only (no check digit).
    /// Use this for blocking validation where the check digit should be a soft warning.
    /// </summary>
    /// <param name="vin">The VIN to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    public static ValidationResult ValidateFormat(string vin) => VinValidator.ValidateFormat(vin);
}
