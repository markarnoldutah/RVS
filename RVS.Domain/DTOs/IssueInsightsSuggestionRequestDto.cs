namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for the issue insights suggestion endpoint.
/// Sent by the client as a JSON body to <c>POST api/intake/{locationSlug}/ai/suggest-insights</c>.
/// </summary>
public sealed record IssueInsightsSuggestionRequestDto
{
    /// <summary>
    /// Free-text issue description to analyze for urgency and RV usage inference.
    /// Must be between 1 and 2,000 characters.
    /// </summary>
    public required string IssueDescription { get; init; }
}
