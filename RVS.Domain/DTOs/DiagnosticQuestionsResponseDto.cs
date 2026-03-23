namespace RVS.Domain.DTOs;

/// <summary>
/// Response containing AI-generated diagnostic questions for the customer intake wizard.
/// </summary>
public sealed record DiagnosticQuestionsResponseDto
{
    public List<DiagnosticQuestionDto> Questions { get; init; } = [];
    public string? SmartSuggestion { get; init; }
}
