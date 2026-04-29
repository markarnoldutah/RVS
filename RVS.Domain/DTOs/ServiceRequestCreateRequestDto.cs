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
}
