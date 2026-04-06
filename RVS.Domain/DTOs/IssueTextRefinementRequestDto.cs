namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for the transcript refinement endpoint.
/// Sent by the client as a JSON body to <c>POST api/intake/{locationSlug}/ai/refine-issue-text</c>.
/// </summary>
public sealed record IssueTextRefinementRequestDto
{
    /// <summary>
    /// Raw speech-to-text transcript to refine.
    /// Must be between 1 and 4,000 characters.
    /// </summary>
    public required string RawTranscript { get; init; }

    /// <summary>
    /// Optional issue category that provides context for cleanup
    /// (e.g. <c>"Electrical"</c>, <c>"Engine"</c>).
    /// </summary>
    public string? IssueCategory { get; init; }
}
