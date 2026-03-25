using RVS.Domain.Validation;

namespace RVS.UI.Shared.Validation;

/// <summary>
/// Client-side search input sanitization helper that delegates to the domain
/// <see cref="SearchInputValidator"/> for dangerous-character blocking and length enforcement.
/// </summary>
public static class ClientSearchInputSanitizer
{
    /// <summary>
    /// Validates search input by blocking dangerous characters and enforcing maximum length.
    /// Delegates to <see cref="SearchInputValidator.Validate"/>.
    /// </summary>
    /// <param name="input">The search input to validate.</param>
    /// <param name="maxLength">Maximum allowed length. Defaults to 500.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var result = ClientSearchInputSanitizer.Validate("safe search");
    /// // result.IsValid == true
    ///
    /// var bad = ClientSearchInputSanitizer.Validate("&lt;script&gt;");
    /// // bad.IsValid == false
    /// </code>
    /// </example>
    public static ValidationResult Validate(string input, int maxLength = 500) =>
        SearchInputValidator.Validate(input, maxLength);
}
