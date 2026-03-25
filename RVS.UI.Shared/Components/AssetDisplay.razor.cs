using Microsoft.AspNetCore.Components;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Displays vehicle/asset information including year, manufacturer, model, and asset ID.
/// Renders a fallback message when no asset data is provided.
/// </summary>
public partial class AssetDisplay : ComponentBase
{
    /// <summary>
    /// The asset information to display. When null, a fallback "No asset information" message is shown.
    /// </summary>
    [Parameter]
    public AssetInfoDto? Asset { get; set; }
}
