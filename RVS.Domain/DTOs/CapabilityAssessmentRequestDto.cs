namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for assessing whether the selected intake location can handle
/// the customer's issue based on its enabled service capabilities.
/// </summary>
public sealed record CapabilityAssessmentRequestDto
{
    /// <summary>
    /// Free-text issue description provided by the customer in Step 5 of the intake wizard.
    /// 1..2000 characters.
    /// </summary>
    public string IssueDescription { get; init; } = string.Empty;

    /// <summary>
    /// Optional issue category previously suggested or selected for the issue. When supplied,
    /// the API skips the categorization step and uses this value directly.
    /// </summary>
    public string? IssueCategory { get; init; }
}
