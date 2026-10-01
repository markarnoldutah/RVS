using System.Diagnostics.CodeAnalysis;

namespace RVS.Domain.Validation;

/// <summary>
/// Normalises a customer-entered US or Canadian phone number to E.164 (<c>+1NXXNXXXXXX</c>),
/// the only form Azure Communication Services accepts (issue #661). Every number handed to an
/// SMS send goes through here first: intake confirmations today, advisor invites next.
///
/// Accepts the ways people actually type a North American number — <c>(801) 555-1234</c>,
/// <c>801.555.1234</c>, <c>1 801 555 1234</c>, <c>+18015551234</c> — and rejects anything
/// else rather than guessing: other country codes, extensions, vanity letters, and area codes
/// or exchanges that start with 0 or 1, which NANP never assigns.
/// </summary>
public static class PhoneNumberNormalizer
{
    /// <summary>Longest raw input considered; anything longer is rejected before parsing.</summary>
    public const int MaxInputLength = 30;

    /// <summary>
    /// Tries to normalise <paramref name="input"/> to E.164.
    /// </summary>
    /// <param name="input">The phone number as entered.</param>
    /// <param name="e164">The E.164 form, e.g. <c>+18015551234</c>, when this returns <c>true</c>.</param>
    /// <returns><c>true</c> when <paramref name="input"/> is a valid US/CA number.</returns>
    /// <example>
    /// <code>
    /// PhoneNumberNormalizer.TryNormalize("(801) 555-1234", out var e164); // true, "+18015551234"
    /// PhoneNumberNormalizer.TryNormalize("555-1234", out _);               // false: no area code
    /// </code>
    /// </example>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? e164)
    {
        e164 = null;

        if (string.IsNullOrWhiteSpace(input) || input.Length > MaxInputLength)
        {
            return false;
        }

        var trimmed = input.Trim();
        var hasPlus = trimmed.StartsWith('+');
        Span<char> digits = stackalloc char[MaxInputLength];
        var count = 0;

        for (var i = hasPlus ? 1 : 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (char.IsAsciiDigit(c))
            {
                digits[count++] = c;
            }
            else if (c is not (' ' or '-' or '.' or '(' or ')'))
            {
                return false;
            }
        }

        // A leading '+' means the country code is present, so it must be the full 11 digits.
        ReadOnlySpan<char> national = count switch
        {
            10 when !hasPlus => digits[..10],
            11 when digits[0] == '1' => digits[1..11],
            _ => [],
        };

        if (national.IsEmpty || national[0] < '2' || national[3] < '2')
        {
            return false;
        }

        e164 = string.Concat("+1", national);
        return true;
    }

    /// <summary>
    /// Returns the E.164 form of <paramref name="input"/>, or <c>null</c> when it is not a
    /// valid US/CA number. Idempotent: an E.164 number normalises to itself.
    /// </summary>
    public static string? Normalize(string? input) =>
        TryNormalize(input, out var e164) ? e164 : null;
}
