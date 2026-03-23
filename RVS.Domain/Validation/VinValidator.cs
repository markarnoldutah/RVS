namespace RVS.Domain.Validation;

/// <summary>
/// Validates Vehicle Identification Numbers (VINs) per SEC-INPUT-05:
/// 17 alphanumeric characters (excluding I, O, Q) with check digit verification.
/// </summary>
public static class VinValidator
{
    private const int VinLength = 17;
    private const int CheckDigitPosition = 8; // 0-based index of the check digit (9th character)
    private static readonly int[] Weights = [8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2];

    private static readonly Dictionary<char, int> TransliterationMap = new()
    {
        ['A'] = 1, ['B'] = 2, ['C'] = 3, ['D'] = 4, ['E'] = 5,
        ['F'] = 6, ['G'] = 7, ['H'] = 8, ['J'] = 1, ['K'] = 2,
        ['L'] = 3, ['M'] = 4, ['N'] = 5, ['P'] = 7, ['R'] = 9,
        ['S'] = 2, ['T'] = 3, ['U'] = 4, ['V'] = 5, ['W'] = 6,
        ['X'] = 7, ['Y'] = 8, ['Z'] = 9
    };

    /// <summary>
    /// Validates a VIN for correct length (17 characters), allowed characters
    /// (alphanumeric, excluding I, O, Q), and check digit correctness.
    /// Input is normalized to uppercase before validation.
    /// </summary>
    /// <param name="vin">The VIN string to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with a descriptive error.</returns>
    /// <example>
    /// <code>
    /// var result = VinValidator.Validate("1HGBH41JXMN109186");
    /// // result.IsValid == true
    ///
    /// var bad = VinValidator.Validate("INVALID");
    /// // bad.IsValid == false
    /// </code>
    /// </example>
    public static ValidationResult Validate(string vin)
    {
        if (string.IsNullOrEmpty(vin))
        {
            return ValidationResult.Failure("VIN must not be null or empty.");
        }

        var normalized = vin.ToUpperInvariant();

        if (normalized.Length != VinLength)
        {
            return ValidationResult.Failure(
                $"VIN must be exactly {VinLength} characters. Received {normalized.Length}.");
        }

        foreach (var c in normalized)
        {
            if (!char.IsLetterOrDigit(c))
            {
                return ValidationResult.Failure(
                    $"VIN must contain only alphanumeric characters. Found: '{c}'.");
            }
        }

        if (normalized.IndexOfAny(['I', 'O', 'Q']) >= 0)
        {
            return ValidationResult.Failure(
                "VIN must not contain the letters I, O, or Q.");
        }

        if (!IsCheckDigitValid(normalized))
        {
            return ValidationResult.Failure(
                "VIN has an invalid check digit.");
        }

        return ValidationResult.Success;
    }

    private static bool IsCheckDigitValid(string vin)
    {
        var sum = 0;

        for (var i = 0; i < VinLength; i++)
        {
            var value = Transliterate(vin[i]);
            if (value < 0)
            {
                return false;
            }

            sum += value * Weights[i];
        }

        var remainder = sum % 11;
        var expected = remainder == 10 ? 'X' : (char)('0' + remainder);

        return vin[CheckDigitPosition] == expected;
    }

    private static int Transliterate(char c)
    {
        if (c is >= '0' and <= '9')
        {
            return c - '0';
        }

        return TransliterationMap.GetValueOrDefault(c, -1);
    }
}
