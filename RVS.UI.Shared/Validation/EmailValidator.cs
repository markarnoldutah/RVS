using RVS.Domain.Validation;

namespace RVS.UI.Shared.Validation;

/// <summary>
/// Client-side email format validation helper.
/// Performs basic structural validation without relying on external libraries.
/// </summary>
public static class EmailValidator
{
    /// <summary>
    /// Validates an email address for basic format correctness:
    /// non-empty, contains exactly one <c>@</c>, non-empty local and domain parts,
    /// domain contains at least one <c>.</c>, and no dangerous characters.
    /// </summary>
    /// <param name="email">The email address to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var result = EmailValidator.Validate("user@example.com");
    /// // result.IsValid == true
    ///
    /// var bad = EmailValidator.Validate("not-an-email");
    /// // bad.IsValid == false
    /// </code>
    /// </example>
    public static ValidationResult Validate(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return ValidationResult.Failure("Email must not be null or empty.");
        }

        var trimmed = email.Trim();

        if (trimmed.Length > 254)
        {
            return ValidationResult.Failure("Email must not exceed 254 characters.");
        }

        var atIndex = trimmed.IndexOf('@');
        if (atIndex < 0)
        {
            return ValidationResult.Failure("Email must contain an '@' character.");
        }

        if (trimmed.IndexOf('@', atIndex + 1) >= 0)
        {
            return ValidationResult.Failure("Email must contain exactly one '@' character.");
        }

        var localPart = trimmed[..atIndex];
        var domainPart = trimmed[(atIndex + 1)..];

        if (localPart.Length == 0)
        {
            return ValidationResult.Failure("Email local part (before '@') must not be empty.");
        }

        if (domainPart.Length == 0)
        {
            return ValidationResult.Failure("Email domain part (after '@') must not be empty.");
        }

        if (!domainPart.Contains('.'))
        {
            return ValidationResult.Failure("Email domain must contain at least one '.' character.");
        }

        if (domainPart.StartsWith('.') || domainPart.EndsWith('.'))
        {
            return ValidationResult.Failure("Email domain must not start or end with '.'.");
        }

        if (HasDangerousCharacters(trimmed))
        {
            return ValidationResult.Failure("Email contains blocked characters.");
        }

        return ValidationResult.Success;
    }

    private static bool HasDangerousCharacters(string input)
    {
        foreach (var c in input)
        {
            if (c is '<' or '>' or ';' or '\'' or '"' or '\\' or '\0')
            {
                return true;
            }
        }

        return false;
    }
}
