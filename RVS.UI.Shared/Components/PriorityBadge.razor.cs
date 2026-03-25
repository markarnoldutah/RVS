using Microsoft.AspNetCore.Components;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Displays a colored badge indicating the priority level of a service request.
/// Maps known priorities (Critical, High, Medium, Low) to corresponding CSS token classes.
/// </summary>
public partial class PriorityBadge : ComponentBase
{
    /// <summary>
    /// The priority text to display (e.g., "Critical", "High", "Medium", "Low").
    /// </summary>
    [Parameter, EditorRequired]
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Computed CSS class based on the current <see cref="Priority"/> value.
    /// </summary>
    protected string CssClass => Priority?.Trim().ToLowerInvariant() switch
    {
        "critical" => "rvs-priority-critical",
        "high" => "rvs-priority-high",
        "medium" => "rvs-priority-medium",
        "low" => "rvs-priority-low",
        _ => "rvs-priority-default"
    };
}
