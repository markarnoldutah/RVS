using System.ComponentModel.DataAnnotations;
using RVS.Domain.Entities;

namespace RVS.Domain.DTOs;

/// <summary>
/// Intake form configuration settings for a dealership or location.
/// </summary>
public sealed record IntakeConfigDto
{
    public List<string> AcceptedFileTypes { get; init; } = [];
    public int MaxFileSizeMb { get; init; }

    /// <summary>
    /// Per-request attachment cap (<c>Spec A-6</c>). Out-of-range values are rejected with 422
    /// at model binding.
    /// </summary>
    [Range(IntakeFormConfigEmbedded.MinAttachmentCap, IntakeFormConfigEmbedded.MaxAttachmentCap)]
    public int MaxAttachments { get; init; }

    public string? AiContext { get; init; }
    public bool AllowAnonymousIntake { get; init; }
}
