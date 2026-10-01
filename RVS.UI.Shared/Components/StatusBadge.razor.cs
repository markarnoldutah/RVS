using Microsoft.AspNetCore.Components;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Displays a colored badge indicating the workflow status of a service request.
/// Maps the decided status vocabulary (Spec C-3 / C-8, issue #428) — New, InProgress,
/// WaitingOnParts, WaitingOnCustomer, Completed, Cancelled — to corresponding CSS token
/// classes via <see cref="StatusBadgeFormatting"/>.
/// </summary>
public partial class StatusBadge : ComponentBase
{
    /// <summary>
    /// The workflow status text to display (e.g., "New", "InProgress", "Completed").
    /// </summary>
    [Parameter, EditorRequired]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Computed CSS class based on the current <see cref="Status"/> value.
    /// </summary>
    protected string CssClass => StatusBadgeFormatting.GetCssClass(Status);
}
