namespace RVS.Domain.DTOs;

/// <summary>
/// Typed result payload for an issue category suggestion AI operation.
/// Returned inside <see cref="AiOperationResponseDto{T}"/> from the
/// <c>POST api/intake/{locationSlug}/ai/suggest-category</c> endpoint.
/// </summary>
public sealed record IssueCategorySuggestionResultDto
{
    /// <summary>
    /// Suggested issue category string,
    /// or <c>null</c> when no confident suggestion is available.
    /// </summary>
    public string? IssueCategory { get; init; }
}
