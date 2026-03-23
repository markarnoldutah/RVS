namespace RVS.Domain.DTOs;

/// <summary>
/// A single diagnostic response submitted by the customer during intake.
/// </summary>
public sealed record DiagnosticResponseDto
{
    public required string QuestionText { get; init; }
    public List<string> SelectedOptions { get; init; } = [];
    public string? FreeTextResponse { get; init; }
}
