namespace RVS.Domain.Validation;

/// <summary>
/// Validates the questions a location adds to every intake's diagnostic step, after the AI's
/// (<c>Spec A-18</c>, issue #785): at most <see cref="MaxQuestions"/>, none blank, none longer than
/// <see cref="MaxQuestionLength"/>, and no two alike.
/// </summary>
public static class DealerQuestionsValidator
{
    /// <summary>Most questions a location may add.</summary>
    public const int MaxQuestions = 2;

    /// <summary>Longest question accepted, in characters.</summary>
    public const int MaxQuestionLength = 200;

    /// <summary>
    /// Validates <paramref name="questions"/> and returns the first problem found, or
    /// <see cref="ValidationResult.Success"/> when every rule passes.
    /// </summary>
    /// <param name="questions">The questions to validate. Must not be null.</param>
    public static ValidationResult Validate(IReadOnlyList<string> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);

        if (questions.Count > MaxQuestions)
        {
            return ValidationResult.Failure($"A location may add at most {MaxQuestions} questions.");
        }

        foreach (var question in questions)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return ValidationResult.Failure("A question must not be blank.");
            }

            var result = ValidateQuestion(question);
            if (!result.IsValid)
            {
                return result;
            }
        }

        // Step 6 keys each answer by its question's text, so two alike would share one answer.
        if (questions.Select(q => q.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() < questions.Count)
        {
            return ValidationResult.Failure("The questions must be different.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates one question's length: blank is allowed (it is dropped on save); otherwise at most
    /// <see cref="MaxQuestionLength"/> characters. Exposed for the manager settings form.
    /// </summary>
    /// <param name="question">The question to check.</param>
    public static ValidationResult ValidateQuestion(string? question) =>
        question is not null && question.Trim().Length > MaxQuestionLength
            ? ValidationResult.Failure($"A question must not exceed {MaxQuestionLength} characters.")
            : ValidationResult.Success;

    /// <summary>The questions trimmed, in order, with blank ones dropped.</summary>
    /// <param name="questions">The questions as entered, or <c>null</c> for none.</param>
    public static List<string> Normalize(IEnumerable<string?>? questions) =>
        questions is null
            ? []
            : [.. questions.Where(q => !string.IsNullOrWhiteSpace(q)).Select(q => q!.Trim())];
}
