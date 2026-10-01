using System.Text.RegularExpressions;
using RVS.Domain.Validation;

namespace RVS.UI.Shared.Validation;

/// <summary>
/// What the intake phone field accepts as it is typed (issue #758): digits and the punctuation
/// people write phone numbers with — space, <c>+</c>, <c>(</c>, <c>)</c>, <c>-</c>, <c>.</c> — up to
/// <see cref="PhoneValidator.MaxLength"/> characters. Letters are refused at the keystroke rather
/// than reported on Continue, so a vanity number or a stray autofill never reaches the rule.
/// <para>
/// This filters input; it does not validate it. <see cref="PhoneValidator"/> still decides
/// whether there are enough digits, and the API applies that rule again.
/// </para>
/// </summary>
public static partial class PhoneInputMask
{
    /// <summary>
    /// The pattern for MudBlazor's <c>RegexMask</c>, which tests the whole text after every
    /// keystroke and so needs both anchors. An empty field matches: requiredness is the rule's job.
    /// </summary>
    public const string Pattern = @"^[0-9 ()+.\-]{0,40}$";

    /// <summary>Whether <paramref name="text"/> is something the phone field would let through.</summary>
    /// <param name="text">The field's text, or <c>null</c> when empty.</param>
    public static bool IsAllowed(string? text) => text is null || AllowedRegex().IsMatch(text);

    [GeneratedRegex(Pattern)]
    private static partial Regex AllowedRegex();
}
