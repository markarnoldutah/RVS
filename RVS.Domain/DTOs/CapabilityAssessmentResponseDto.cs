namespace RVS.Domain.DTOs;

/// <summary>
/// Response DTO describing whether the intake location's enabled capabilities
/// satisfy the capabilities required to address the customer's issue.
/// </summary>
public sealed record CapabilityAssessmentResponseDto
{
    /// <summary>
    /// True when the location's enabled capabilities cover every capability required for
    /// the resolved issue category. Also true when no specific capabilities are required.
    /// </summary>
    public bool Matched { get; init; }

    /// <summary>
    /// AI-resolved issue category used to derive the required capability list. Null when
    /// the categorization service could not produce a result.
    /// </summary>
    public string? IssueCategory { get; init; }

    /// <summary>
    /// Capability codes considered necessary to service the issue. Empty when the resolved
    /// category is not associated with any specific capability requirement.
    /// </summary>
    public List<string> RequiredCapabilities { get; init; } = [];

    /// <summary>
    /// Required capability codes that are NOT enabled at the selected location. Empty when
    /// <see cref="Matched"/> is true.
    /// </summary>
    public List<string> MissingCapabilities { get; init; } = [];

    /// <summary>
    /// Phone number for the selected location, surfaced so the Intake UI can include it in
    /// the customer-facing alert when capabilities are not satisfied. Null when the location
    /// has no phone configured.
    /// </summary>
    public string? LocationPhone { get; init; }
}
