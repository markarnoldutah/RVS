namespace RVS.Domain.DTOs;

/// <summary>
/// Typed result payload for a speech-to-text transcription AI operation.
/// Returned inside <see cref="AiOperationResponseDto{T}"/> from the
/// <c>POST api/intake/{locationSlug}/ai/transcribe-issue</c> endpoint.
/// </summary>
public sealed record IssueTranscriptionResultDto
{
    /// <summary>
    /// Direct speech-to-text output from the recognition engine,
    /// or <c>null</c> when no speech was detected in the audio.
    /// </summary>
    public string? RawTranscript { get; init; }

    /// <summary>
    /// AI-cleaned text suitable for the <c>IssueDescription</c> field,
    /// or <c>null</c> when refinement was not performed or not available.
    /// </summary>
    public string? CleanedDescription { get; init; }
}
