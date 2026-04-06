namespace RVS.Domain.DTOs;

/// <summary>
/// Typed result payload for a transcript refinement AI operation.
/// Returned inside <see cref="AiOperationResponseDto{T}"/> from the
/// <c>POST api/intake/{locationSlug}/ai/refine-issue-text</c> endpoint.
/// </summary>
public sealed record IssueTextRefinementResultDto
{
    /// <summary>
    /// Customer-editable final draft of the issue description,
    /// or <c>null</c> when refinement could not produce a meaningful result.
    /// </summary>
    public string? CleanedDescription { get; init; }
}
