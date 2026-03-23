namespace RVS.Domain.DTOs;

/// <summary>
/// A single diagnostic question presented to the customer during intake.
/// </summary>
public sealed record DiagnosticQuestionDto
{
    public required string QuestionText { get; init; }
    public List<string> Options { get; init; } = [];
    public bool AllowFreeText { get; init; }
    public string? HelpText { get; init; }
}
