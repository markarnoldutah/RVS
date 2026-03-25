using Microsoft.AspNetCore.Components;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Displays a list of diagnostic question responses from the AI-powered intake wizard.
/// Renders selected options and optional free-text answers for each question.
/// Shows a fallback message when no responses are available.
/// </summary>
public partial class DiagnosticResponseView : ComponentBase
{
    /// <summary>
    /// The list of diagnostic responses to display.
    /// </summary>
    [Parameter]
    public List<DiagnosticResponseDto>? Responses { get; set; }
}
