using RVS.Domain.DTOs;

namespace RVS.Domain.Validation;

/// <summary>
/// Validates the problems in an intake submission (<c>Spec A-17</c>, issue #806): at most
/// <see cref="MaxIssuesPerSubmission"/> of them, and each with a description the intake wizard
/// would accept on its own. Errors name the problem by its 1-based position, as the customer
/// saw it on the review step.
/// </summary>
public static class IntakeIssuesValidator
{
    /// <summary>The most problems one submission may report, counting the first.</summary>
    public const int MaxIssuesPerSubmission = 10;

    /// <summary>The longest description intake accepts for one problem.</summary>
    public const int MaxDescriptionLength = 2000;

    /// <summary>Validates every problem in <paramref name="request"/>.</summary>
    /// <param name="request">The intake submission.</param>
    public static ValidationResult Validate(ServiceRequestCreateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var additionalCount = request.AdditionalIssues?.Count ?? 0;
        if (1 + additionalCount > MaxIssuesPerSubmission)
        {
            return ValidationResult.Failure(
                $"A submission may report at most {MaxIssuesPerSubmission} problems.");
        }

        if (request.AdditionalIssues?.Any(i => i is null) == true)
        {
            return ValidationResult.Failure("Every additional problem must be supplied.");
        }

        var issues = request.AllIssues();
        for (var i = 0; i < issues.Count; i++)
        {
            var issue = issues[i];
            var label = $"Problem {i + 1}";

            if (string.IsNullOrWhiteSpace(issue.IssueDescription))
            {
                return ValidationResult.Failure($"{label}: a description is required.");
            }

            // A negative ExpectedAttachmentCount is not refused: orchestration clamps it to zero,
            // as it always has for a single problem, so it can never stall generation.
            if (issue.IssueDescription.Length > MaxDescriptionLength)
            {
                return ValidationResult.Failure(
                    $"{label}: the description must not exceed {MaxDescriptionLength:N0} characters.");
            }
        }

        return ValidationResult.Success;
    }
}
