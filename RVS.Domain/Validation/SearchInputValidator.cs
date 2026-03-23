namespace RVS.Domain.Validation;

/// <summary>
/// Validates search keyword input by blocking dangerous characters per SEC-INPUT-02
/// and enforcing maximum length per SEC-INPUT-01.
/// </summary>
public static class SearchInputValidator
{
    private static readonly char[] BlockedCharacters = ['<', '>', ';', '\'', '"', '\\', '\0'];

    /// <summary>
    /// Validates the search input string. Rejects inputs containing dangerous characters
    /// (<c>&lt;</c>, <c>&gt;</c>, <c>;</c>, <c>'</c>, <c>"</c>, <c>\</c>, <c>\0</c>)
    /// and inputs exceeding the specified maximum length.
    /// </summary>
    /// <param name="input">The search input to validate.</param>
    /// <param name="maxLength">Maximum allowed length. Defaults to 500.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with a descriptive error.</returns>
    /// <example>
    /// <code>
    /// var result = SearchInputValidator.Validate("safe query");
    /// // result.IsValid == true
    ///
    /// var bad = SearchInputValidator.Validate("&lt;script&gt;");
    /// // bad.IsValid == false
    /// </code>
    /// </example>
    public static ValidationResult Validate(string input, int maxLength = 500)
    {
        if (input is null)
        {
            return ValidationResult.Failure("Search input must not be null.");
        }

        if (input.Length > maxLength)
        {
            return ValidationResult.Failure(
                $"Search input must not exceed {maxLength} characters.");
        }

        foreach (var c in input)
        {
            if (Array.IndexOf(BlockedCharacters, c) >= 0)
            {
                return ValidationResult.Failure(
                    $"Search input contains a blocked character: '{(c == '\0' ? "\\0" : c.ToString())}'.");
            }
        }

        return ValidationResult.Success;
    }
}
