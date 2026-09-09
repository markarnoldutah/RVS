namespace RVS.Domain.Validation;

/// <summary>
/// Validates a manager-authored customer status note (<c>Spec C-9</c>) before it is stored on a
/// <see cref="Entities.ServiceRequest"/> and rendered on the anonymous customer status page
/// (<c>Spec X-1</c>).
///
/// The note is optional: a null, empty, or whitespace-only value is <b>valid</b> and means
/// "clear the current note". A non-blank value must be at most <see cref="MaxLength"/> characters
/// (measured after trimming) and must not contain any of the blocked characters
/// (<c>&lt; &gt; ; ' " \ \0</c>) — the same sanitisation rule applied to search input and the
/// customer's own free-text problem description.
/// </summary>
public static class CustomerStatusNoteValidator
{
    /// <summary>Maximum length of the note, in characters, measured after trimming.</summary>
    public const int MaxLength = 280;

    private static readonly HashSet<char> BlockedCharacters = ['<', '>', ';', '\'', '"', '\\', '\0'];

    /// <summary>
    /// Validates a status note. Blank input (null / empty / whitespace) is valid and signals
    /// that the note should be cleared.
    /// </summary>
    /// <param name="note">The proposed note text, or null/blank to clear.</param>
    /// <param name="maxLength">Maximum allowed length after trimming. Defaults to <see cref="MaxLength"/>.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with a safe error message.</returns>
    public static ValidationResult Validate(string? note, int maxLength = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return ValidationResult.Success;
        }

        var trimmed = note.Trim();

        if (trimmed.Length > maxLength)
        {
            return ValidationResult.Failure(
                $"Customer status note must not exceed {maxLength} characters.");
        }

        foreach (var c in trimmed)
        {
            if (BlockedCharacters.Contains(c))
            {
                return ValidationResult.Failure(
                    $"Customer status note contains a blocked character: '{(c == '\0' ? "\\0" : c.ToString())}'.");
            }
        }

        return ValidationResult.Success;
    }
}
