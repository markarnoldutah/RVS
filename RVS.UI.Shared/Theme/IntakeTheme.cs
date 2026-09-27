using System.Collections.Concurrent;
using MudBlazor;
using RVS.Domain.Branding;
using RVS.Domain.Validation;

namespace RVS.UI.Shared.Theme;

/// <summary>
/// The Denim &amp; Rust theme for <c>RVS.Blazor.Intake</c> (Spec THEME-1).
/// <para>
/// Intake is anonymous and mobile-first, filled out once by a stressed customer standing next
/// to a broken RV — not a tool anyone lives in. Rounder corners and a notch more type than
/// Manager. The page ground is white, not cream (issue #470): a location's intake page carries
/// the dealer's own logo and header colour (Spec A-16), and a neutral ground suits every dealer's
/// brand where cream suits only ours. Denim and Rust still carry structure and action. There is
/// no dark-mode toggle; the high-contrast mode below is an accessibility choice, not a second look.
/// </para>
/// </summary>
public static class IntakeTheme
{
    /// <summary>The Rust theme every location gets unless it sets an accent.</summary>
    public static readonly MudTheme Theme = Build(RvsBrand.Accent);

    /// <summary>One theme per dealer accent, so a layout re-render hands MudThemeProvider the same instance.</summary>
    private static readonly ConcurrentDictionary<string, MudTheme> AccentThemes = new(StringComparer.Ordinal);

    /// <summary>
    /// The Intake theme with a dealer's accent in place of Rust (<c>Spec A-16</c>, issue #470
    /// follow-up): buttons, links, focus rings and checked controls. Everything else — Ink
    /// structure, the white ground, the audited semantic colours, type, corners — stays the brand's.
    /// The accent must already clear 4.5:1 against white, the rule the API enforces, which is also
    /// why white is always legible on it as a button fill.
    /// </summary>
    /// <param name="accentColor">A <c>#RRGGBB</c> colour that clears 4.5:1 against white.</param>
    /// <exception cref="ArgumentException">The colour is not <c>#RRGGBB</c>, or is too light.</exception>
    public static MudTheme WithAccent(string accentColor)
    {
        var result = LocationBrandingValidator.ValidateAccentColor(accentColor);
        if (string.IsNullOrWhiteSpace(accentColor) || !result.IsValid)
        {
            throw new ArgumentException(result.ErrorMessage ?? "An accent colour is required.", nameof(accentColor));
        }

        return AccentThemes.GetOrAdd(HeaderColor.Normalize(accentColor)!, Build);
    }

    private static MudTheme Build(string primary)
    {
        var palette = new PaletteLight
        {
            Primary = primary,
            PrimaryContrastText = "#FFFFFF",
            Secondary = RvsBrand.Ink,
            SecondaryContrastText = RvsBrand.Paper,
            AppbarBackground = RvsBrand.Ink,
            AppbarText = RvsBrand.Paper,
            Background = "#FFFFFF",
            Surface = "#FFFFFF",
            TextPrimary = RvsBrand.TextOnPaper,
            TextSecondary = RvsBrand.TextSecondaryOnPaper,
            Success = RvsBrand.Success,
            SuccessContrastText = "#FFFFFF",
            Warning = RvsBrand.Warning,
            WarningContrastText = "#FFFFFF",
            Error = RvsBrand.Error,
            ErrorContrastText = "#FFFFFF",
            Info = RvsBrand.Info,
            InfoContrastText = "#FFFFFF"
        };

        return new MudTheme
        {
            PaletteLight = palette,

            // Intake exposes no dark-mode toggle. The dark palette is the light one restated
            // rather than left unset: if anything ever resolves it — an OS preference, a future
            // MudThemeProvider default — the customer still sees the brand, not MudBlazor's
            // stock dark grey clashing with the form.
            PaletteDark = new PaletteDark
            {
                Primary = palette.Primary,
                PrimaryContrastText = palette.PrimaryContrastText,
                Secondary = palette.Secondary,
                SecondaryContrastText = palette.SecondaryContrastText,
                AppbarBackground = palette.AppbarBackground,
                AppbarText = palette.AppbarText,
                Background = palette.Background,
                Surface = palette.Surface,
                TextPrimary = palette.TextPrimary,
                TextSecondary = palette.TextSecondary,
                Success = palette.Success,
                SuccessContrastText = palette.SuccessContrastText,
                Warning = palette.Warning,
                WarningContrastText = palette.WarningContrastText,
                Error = palette.Error,
                ErrorContrastText = palette.ErrorContrastText,
                Info = palette.Info,
                InfoContrastText = palette.InfoContrastText
            },

            // One notch up from MudBlazor's default: this is filled out on a phone, standing up.
            Typography = RvsTypography.Build(
                baseFontSize: "1rem",
                buttonFontSize: "1rem",
                buttonFontWeight: "700"),

            LayoutProperties = new LayoutProperties
            {
                // Rounder than Manager — softer, less "console".
                DefaultBorderRadius = "14px"
            }
        };
    }

    /// <summary>The accessibility theme. See <see cref="RvsHighContrastPalette"/> for why it is not brand-coloured.</summary>
    public static readonly MudTheme HighContrast = new()
    {
        PaletteLight = RvsHighContrastPalette.Create(withDrawer: false),
        Typography = RvsTypography.Build(baseFontSize: "1rem", buttonFontSize: "1rem", buttonFontWeight: "700"),
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px"
        }
    };
}
