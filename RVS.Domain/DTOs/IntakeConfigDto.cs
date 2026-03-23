namespace RVS.Domain.DTOs;

/// <summary>
/// Intake form configuration settings for a dealership or location.
/// </summary>
public sealed record IntakeConfigDto
{
    public List<string> AcceptedFileTypes { get; init; } = [];
    public int MaxFileSizeMb { get; init; }
    public int MaxAttachments { get; init; }
    public string? AiContext { get; init; }
    public bool AllowAnonymousIntake { get; init; }
}
