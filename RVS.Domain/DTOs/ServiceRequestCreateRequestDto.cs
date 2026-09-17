namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for creating a new service request via customer intake.
/// </summary>
public sealed record ServiceRequestCreateRequestDto
{
    public required CustomerInfoDto Customer { get; init; }
    public required AssetInfoDto Asset { get; init; }
    public required string IssueCategory { get; init; }
    public required string IssueDescription { get; init; }

    /// <summary>
    /// The customer's words before AI curation (issue #601) — the raw speech-to-text transcript
    /// when the description was dictated, or the typed text when it was typed and then refined.
    /// The packet renders it as "Complaint — word for word" so the curated
    /// <see cref="IssueDescription"/> can always be checked against it. Omit it when no curation
    /// ran; the packet then shows the submitted description as the complaint.
    /// </summary>
    public string? IssueDescriptionVerbatim { get; init; }
    public string? Urgency { get; init; }
    public string? RvUsage { get; init; }
    public string? HasExtendedWarranty { get; init; }
    public string? ApproxPurchaseDate { get; init; }
    public List<DiagnosticResponseDto>? DiagnosticResponses { get; init; }

    /// <summary>
    /// When <c>true</c>, the customer has opted out of SMS notifications.
    /// Default is <c>false</c> (both channels active).
    /// </summary>
    public bool SmsOptOut { get; init; }

    /// <summary>
    /// When <c>true</c>, the customer has opted out of email notifications.
    /// Default is <c>false</c> (both channels active).
    /// </summary>
    public bool EmailOptOut { get; init; }

    /// <summary>
    /// System-generated note to prepend to the technician summary when the intake
    /// capability assessment detected that the selected location does not offer the
    /// capability required for the chosen issue category.
    /// Null when the assessment matched or was not performed.
    /// </summary>
    public string? CapabilityMismatchNote { get; init; }

    /// <summary>
    /// Distribution channel the customer arrived through (<c>Spec A-13</c>, issue #599) — the
    /// <c>src</c> the intake app was opened with, forwarded verbatim. Normalised server-side, so
    /// an unrecognised or malformed value costs the request its channel, never the submission.
    /// Absent (the default) is recorded as <c>print</c>.
    /// </summary>
    public string? IntakeSource { get; init; }

    /// <summary>
    /// How many attachments the client is about to upload (issue #516). Attachments are
    /// confirmed after this submission returns, so packet generation waits for this many to
    /// arrive before rendering — otherwise the packet ships with no photos. Leave at <c>0</c>
    /// when there is nothing to upload; the packet then generates immediately.
    /// </summary>
    public int ExpectedAttachmentCount { get; init; }
}
