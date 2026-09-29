using System.Text.Json.Serialization;
using RVS.Domain.DTOs;

namespace RVS.Blazor.Intake.State;

/// <summary>
/// One issue's Steps 5–7 answers while the customer is still in the wizard (<c>Spec A-17</c>,
/// issue #806). The issue being edited lives in <see cref="IntakeWizardState"/>'s own issue
/// properties, which the step components bind to; the drafts hold every issue, and the active
/// one is refreshed from those properties whenever the list is read.
/// </summary>
public sealed class IntakeIssueDraft
{
    public string IssueCategory { get; set; } = string.Empty;
    public bool IsCategorySuggestedByAi { get; set; }
    public bool IsUrgencySuggestedByAi { get; set; }
    public string IssueDescription { get; set; } = string.Empty;
    public string? IssueDescriptionVerbatim { get; set; }
    public string? Urgency { get; set; }
    public List<DiagnosticQuestionDto> DiagnosticQuestions { get; set; } = [];
    public List<DiagnosticResponseDto> DiagnosticResponses { get; set; } = [];
    public string? SmartSuggestion { get; set; }
    public CapabilityAssessmentResponseDto? CapabilityAssessment { get; set; }

    /// <summary>
    /// This issue's attachments. Not persisted, like the wizard's own: their bytes live only in
    /// the current page, and a reload loses them for every issue alike.
    /// </summary>
    [JsonIgnore]
    public List<AttachmentFileInfo> Attachments { get; set; } = [];

    /// <summary>Whether the customer has entered nothing for this issue yet.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(IssueCategory)
        && string.IsNullOrWhiteSpace(IssueDescription)
        && Attachments.Count == 0;

    /// <summary>
    /// Attachments still to upload after submission (issue #516): buffered in the browser and
    /// not yet sent. Files whose bytes were lost are excluded, as they will never arrive.
    /// </summary>
    [JsonIgnore]
    public int PendingUploadCount => Attachments.Count(a => a.FileData is not null && !a.IsUploaded);
}
