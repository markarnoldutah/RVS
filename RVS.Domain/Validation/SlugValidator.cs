using System.Text.RegularExpressions;

namespace RVS.Domain.Validation;

/// <summary>
/// Validates slug strings per SEC-INPUT-02: lowercase alphanumeric characters and hyphens only.
/// </summary>
public static partial class SlugValidator
{
    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex SlugPattern();

    /// <summary>
    /// Validates that the slug matches the pattern <c>/^[a-z0-9-]+$/</c>
    /// and does not exceed the specified maximum length (default 64 per SEC-INPUT-01).
    /// </summary>
    /// <param name="slug">The slug string to validate.</param>
    /// <param name="maxLength">Maximum allowed length. Defaults to 64.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with a descriptive error.</returns>
    /// <example>
    /// <code>
    /// var result = SlugValidator.Validate("camping-world-salt-lake");
    /// // result.IsValid == true
    ///
    /// var bad = SlugValidator.Validate("Has Spaces");
    /// // bad.IsValid == false
    /// </code>
    /// </example>
    public static ValidationResult Validate(string slug, int maxLength = 64)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return ValidationResult.Failure("Slug must not be null or empty.");
        }

        if (slug.Length > maxLength)
        {
            return ValidationResult.Failure(
                $"Slug must not exceed {maxLength} characters.");
        }

        if (!SlugPattern().IsMatch(slug))
        {
            return ValidationResult.Failure(
                "Slug must contain only lowercase alphanumeric characters and hyphens.");
        }

        return ValidationResult.Success;
    }
}
