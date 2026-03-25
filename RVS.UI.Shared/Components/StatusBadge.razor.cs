using Microsoft.AspNetCore.Components;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Displays a colored badge indicating the workflow status of a service request.
/// Maps known statuses (New, In Progress, Awaiting Parts, Completed, Cancelled)
/// to corresponding CSS token classes.
/// </summary>
public partial class StatusBadge : ComponentBase
{
    /// <summary>
    /// The workflow status text to display (e.g., "New", "In Progress", "Completed").
    /// </summary>
    [Parameter, EditorRequired]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Computed CSS class based on the current <see cref="Status"/> value.
    /// </summary>
    protected string CssClass => Status?.Trim().ToLowerInvariant().Replace(" ", "-") switch
    {
        "new" => "rvs-status-new",
        "in-progress" => "rvs-status-in-progress",
        "awaiting-parts" => "rvs-status-awaiting-parts",
        "completed" => "rvs-status-completed",
        "cancelled" => "rvs-status-cancelled",
        _ => "rvs-status-default"
    };
}
