namespace RVS.Domain.Validation;

/// <summary>
/// Validates a manager-authored customer status note (<c>Spec C-9</c>) before it is stored on a
/// <see cref="Entities.ServiceRequest"/> and rendered on the anonymous customer status page
/// (<c>Spec X-1</c>).
///
/// The note is optional: a null, empty, or whitespace-only value is <b>valid</b> and means
/// "clear the current note". A non-blank value must be at most <see cref="MaxLength"/> characters
/// (measured after trimming). It is deliberately permissive about punctuation — apostrophes,
/// quotation marks, and the like are ordinary in a human sentence ("We've ordered a
/// replacement…") and the note is rendered through Blazor's output encoding, so it is inert
/// against HTML injection. Only two classes of character are rejected: the angle brackets
/// <c>&lt;</c> and <c>&gt;</c> (belt-and-braces against HTML; a status note never needs them),
/// and control characters (<c>\0</c> and other C0 controls), which are never legitimately typed.
/// Common whitespace — space, tab, carriage return, newline — is allowed.
/// </summary>
public static class CustomerStatusNoteValidator
{
    /// <summary>Maximum length of the note, in characters, measured after trimming.</summary>
    public const int MaxLength = 280;

    private static readonly HashSet<char> BlockedCharacters = ['<', '>'];

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
                    $"Customer status note contains a blocked character: '{c}'.");
            }

            if (char.IsControl(c) && c is not ('\t' or '\r' or '\n'))
            {
                return ValidationResult.Failure(
                    "Customer status note contains a control character that is not allowed.");
            }
        }

        return ValidationResult.Success;
    }
}
