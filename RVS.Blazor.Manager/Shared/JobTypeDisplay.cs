using MudBlazor;
using RVS.Domain.Validation;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// How a job type (<c>Spec C-11</c>, issue #843) looks on the board card, the list and the detail
/// view. Each value has its own palette color, and values whose hues sit close together also differ
/// in lightness: a <see cref="Variant.Filled"/> chip is a dark fill, a <see cref="Variant.Text"/>
/// chip a light tint. On-site (Rust) and Install (amber) are one of each, as are In-shop (Ink) and
/// Remote (blue). The label and an icon are always shown too, so color is never the only signal.
/// Only palette colors <c>ManagerTheme</c> audits for Light and Dark are used, never
/// <see cref="Color.Error"/>, which is reserved for error states.
/// </summary>
public static class JobTypeDisplay
{
    /// <summary>The chip for one job type.</summary>
    public sealed record Style(string Label, string Icon, Color Color, Variant Variant);

    /// <summary>One choice in a job-type filter.</summary>
    public sealed record FilterOption(string Value, string Label);

    private static readonly Dictionary<string, Style> _styles = new(StringComparer.Ordinal)
    {
        [JobTypes.Remote]     = new(JobTypes.GetLabel(JobTypes.Remote),     Icons.Material.Outlined.SupportAgent,  Color.Info,      Variant.Text),
        [JobTypes.OnSite]     = new(JobTypes.GetLabel(JobTypes.OnSite),     Icons.Material.Outlined.LocalShipping, Color.Primary,   Variant.Text),
        [JobTypes.InShop]     = new(JobTypes.GetLabel(JobTypes.InShop),     Icons.Material.Outlined.Garage,        Color.Secondary, Variant.Filled),
        [JobTypes.Inspection] = new(JobTypes.GetLabel(JobTypes.Inspection), Icons.Material.Outlined.FactCheck,     Color.Success,   Variant.Filled),
        [JobTypes.Install]    = new(JobTypes.GetLabel(JobTypes.Install),    Icons.Material.Outlined.Construction,  Color.Warning,   Variant.Filled),
        [JobTypes.Other]      = new(JobTypes.GetLabel(JobTypes.Other),      Icons.Material.Outlined.MoreHoriz,     Color.Default,   Variant.Filled),
    };

    /// <summary>
    /// The chip for <paramref name="jobType"/>. Unset reads "Not triaged" in a neutral outline; an
    /// unknown value shows as-is in the same outline.
    /// </summary>
    public static Style For(string? jobType) =>
        jobType is not null && _styles.TryGetValue(jobType, out var style)
            ? style
            : new(JobTypes.GetLabel(jobType), Icons.Material.Outlined.HelpOutline, Color.Default, Variant.Outlined);

    /// <summary>The job-type filter's choices: "Not triaged" first, then every job type.</summary>
    public static IReadOnlyList<FilterOption> FilterOptions { get; } =
    [
        new(JobTypes.NotTriagedFilter, JobTypes.NotTriagedLabel),
        .. JobTypes.All.Select(e => new FilterOption(e.Code, e.Name)),
    ];
}
