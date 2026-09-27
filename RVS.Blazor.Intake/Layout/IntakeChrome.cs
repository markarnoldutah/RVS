using RVS.Domain.Branding;
using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.Blazor.Intake.Layout;

/// <summary>
/// What the Intake app's header bar shows (<c>Spec A-16</c>, issue #470): the dealer's logo and
/// colour on that location's own intake page, the RV Intake mark on Denim everywhere else.
/// "Everywhere else" includes the homepage and the policy pages even mid-session — they are
/// RV Intake's pages, not the dealer's.
///
/// High contrast ignores the dealer's colour, as it ignores the brand's own (Spec THEME-1), but
/// keeps the logo. The logo sits straight on a colour the dealer chose — they picked the two
/// together, and may well have uploaded a white logo for a dark header — and on a white plate
/// only where the colour is not theirs: the default Ink, or high contrast's black
/// (<see cref="LogoOnPlate"/>).
/// </summary>
/// <param name="DealerLogoUrl">The dealer's https logo URL, or <c>null</c> for the RV Intake mark.</param>
/// <param name="DealerName">The dealer's name, for the logo's alt text.</param>
/// <param name="AppBarBackground">The dealer's <c>#RRGGBB</c> bar colour, or <c>null</c> for the theme's.</param>
/// <param name="AppBarForeground">The foreground chosen for contrast on <paramref name="AppBarBackground"/>.</param>
public sealed record IntakeChrome(
    string? DealerLogoUrl,
    string? DealerName,
    string? AppBarBackground,
    string? AppBarForeground)
{
    /// <summary>The RV Intake chrome: its own mark, on the theme's Ink bar.</summary>
    public static readonly IntakeChrome Default = new(null, null, null, null);

    /// <summary><c>true</c> when the bar shows the dealer's logo.</summary>
    public bool HasDealerLogo => DealerLogoUrl is not null;

    /// <summary>
    /// <c>true</c> when the dealer's logo needs a white plate: it is shown on a surface whose
    /// colour the dealer did not choose, where a dark logo could vanish.
    /// </summary>
    public bool LogoOnPlate => HasDealerLogo && AppBarBackground is null;

    /// <summary>
    /// Inline style for the app bar when the dealer sets a colour, otherwise <c>null</c> so the
    /// bar takes <c>AppbarBackground</c> from the theme.
    /// </summary>
    public string? AppBarStyle => AppBarBackground is null
        ? null
        : $"background-color:{AppBarBackground};color:{AppBarForeground};";

    /// <summary>
    /// <c>true</c> when the RV Intake mark should be the reversed (light) colourway — on the
    /// theme's own bar, which is dark in every Intake mode, or on a dark dealer colour.
    /// </summary>
    public bool UseReversedMark => AppBarForeground is null or HeaderColor.LightForeground;

    /// <summary>Resolves the chrome for the page at <paramref name="currentPath"/>.</summary>
    /// <param name="config">The loaded location config, or <c>null</c> before one loads.</param>
    /// <param name="currentPath">The base-relative path, e.g. <c>acme-slc?step=2</c>.</param>
    /// <param name="highContrast">Whether the high-contrast theme is on.</param>
    public static IntakeChrome Resolve(IntakeConfigResponseDto? config, string? currentPath, bool highContrast)
    {
        if (config is null || !IsLocationPage(currentPath, config.LocationSlug))
        {
            return Default;
        }

        var logoUrl = DealerLogoUrlOf(config);
        var background = highContrast ? null : HeaderColor.Normalize(config.Branding?.HeaderColor);

        if (logoUrl is null && background is null)
        {
            return Default;
        }

        return new IntakeChrome(
            logoUrl,
            string.IsNullOrWhiteSpace(config.DealershipName) ? config.LocationName : config.DealershipName,
            background,
            background is null ? null : HeaderColor.ForegroundFor(background));
    }

    /// <summary>
    /// The chrome for the Step 0 landing hero, which is always on the location's own page and
    /// follows the same surface rules as the bar.
    /// </summary>
    public static IntakeChrome ForLanding(IntakeConfigResponseDto? config, bool highContrast) =>
        Resolve(config, config?.LocationSlug, highContrast);

    /// <summary>
    /// The location's dealer logo when it is an https URL, otherwise <c>null</c>. The landing
    /// step's hero goes through <see cref="ForLanding"/>.
    /// </summary>
    public static string? DealerLogoUrlOf(IntakeConfigResponseDto? config)
    {
        var logoUrl = config?.Branding?.LogoUrl?.Trim();
        return LocationBrandingValidator.IsHttpsUrl(logoUrl) ? logoUrl : null;
    }

    private static bool IsLocationPage(string? currentPath, string? slug)
    {
        if (string.IsNullOrWhiteSpace(currentPath) || string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        var firstSegment = currentPath.Split(['?', '#', '/'], 2)[0];
        return string.Equals(firstSegment, slug, StringComparison.OrdinalIgnoreCase);
    }
}
