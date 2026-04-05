namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for the issue category suggestion endpoint.
/// Sent by the client as a JSON body to <c>POST api/intake/{locationSlug}/ai/suggest-category</c>.
/// </summary>
public sealed record IssueCategorySuggestionRequestDto
{
    /// <summary>
    /// Free-text issue description to analyze for category suggestions.
    /// Must be between 1 and 2,000 characters.
    /// </summary>
    public required string IssueDescription { get; init; }
}
