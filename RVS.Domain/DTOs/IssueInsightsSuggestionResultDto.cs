namespace RVS.Domain.DTOs;

/// <summary>
/// Typed result payload for an issue insights suggestion AI operation.
/// Returned inside <see cref="AiOperationResponseDto{T}"/> from the
/// <c>POST api/intake/{locationSlug}/ai/suggest-insights</c> endpoint.
/// </summary>
public sealed record IssueInsightsSuggestionResultDto
{
    /// <summary>
    /// Inferred urgency level (e.g. "Low", "Medium", "High", "Critical"),
    /// or <c>null</c> when no confident inference is possible.
    /// </summary>
    public string? Urgency { get; init; }

    /// <summary>
    /// Inferred RV usage pattern (e.g. "Full-Time", "Part-Time", "Seasonal", "Occasional"),
    /// or <c>null</c> when no confident inference is possible.
    /// </summary>
    public string? RvUsage { get; init; }
}
