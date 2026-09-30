namespace RVS.Domain.DTOs;

/// <summary>
/// One issue reported in an intake submission (<c>Spec A-17</c>, issue #806): the Steps 5–7
/// answers the customer gives once per issue. Each becomes its own service request. The first
/// issue travels on <see cref="ServiceRequestCreateRequestDto"/>'s own issue fields; this record
/// carries each one after it in <see cref="ServiceRequestCreateRequestDto.AdditionalIssues"/>.
/// </summary>
public sealed record IntakeIssueDto
{
    public required string IssueCategory { get; init; }
    public required string IssueDescription { get; init; }

    /// <summary>
    /// The customer's words before AI curation (issue #601). See
    /// <see cref="ServiceRequestCreateRequestDto.IssueDescriptionVerbatim"/>.
    /// </summary>
    public string? IssueDescriptionVerbatim { get; init; }
    public string? Urgency { get; init; }
    public List<DiagnosticResponseDto>? DiagnosticResponses { get; init; }

    /// <summary>
    /// The capability pre-check's note for this issue (<c>Spec A-12</c>). See
    /// <see cref="ServiceRequestCreateRequestDto.CapabilityMismatchNote"/>.
    /// </summary>
    public string? CapabilityMismatchNote { get; init; }

    /// <summary>
    /// How many attachments the client will upload to this issue's request (issue #516). The
    /// <c>Spec A-6</c> cap applies per issue.
    /// </summary>
    public int ExpectedAttachmentCount { get; init; }
}
