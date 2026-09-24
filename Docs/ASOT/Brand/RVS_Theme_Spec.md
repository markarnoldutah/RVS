# Spec THEME-1 — RV Intake brand theme for MudBlazor

> **Status: the revised THEME-1 brief as handed over September 24 2026 (`rvs-mudblazor-theme-kit`),
> kept for its rationale.** It supersedes the #702 draft. Where this file and the code disagree,
> the code wins: `RVS.UI.Shared/Theme/RvsBrand.cs` is the authoritative copy of the palette, and
> `Docs/ASOT/RVS_FrontEnd.md` describes what was actually built. How the build departs from the
> text below:
>
> - **No `RvsThemes` class.** The palette values in §1 landed as written, but in the structure
>   #702 already built: `ManagerTheme` and `IntakeTheme`, each with a `Theme` and a `HighContrast`
>   `MudTheme`, selected by a per-app `ThemeService`. The tokens kept their #702 names: `Denim` is
>   `RvsBrand.Ink`, `Cream` is `Paper`, the text-safe `Primary` is `Accent`, and `RustLogo` is
>   `AccentLogo`. High contrast stays black/yellow/cyan and is not brand-coloured.
> - **Manager app bar (§4).** The reversed horizontal lockup from `md` up and the reversed glyph
>   below it, as specified, followed by a "Manager" label so the chrome reads *RV Intake Manager*.
>   Below `md` the label is "Intake Manager", since the glyph does not spell "RV".
> - **Intake header (§4): not done.** The app bar still carries the reversed horizontal lockup;
>   the landing page's hero band shows the stacked lockup, and the intake hero shows the glyph. Moving RV Intake to a "Powered by" footer
>   under the shop's own name and logo is a layout change and is still open.
> - **Favicons (§4).** Installed as specified, except that Manager also keeps the kit's
>   `favicon-32x32.png`, because the Auth0 Universal Login page references it by URL.
> - **Hex sweep (§3), the error-icon rule (§1), and the manual checks in §5 are still open.**
>   `PriorityBadge`'s Critical pill uses the crimson Error fill without an icon.
> - **Fonts (§2)** were already in place from #702; the kit's files are byte-identical.

**Apps:** `RVS.Blazor.Manager`, `RVS.Blazor.Intake`
**MudBlazor:** 9.x (Material Design 3)
**Supersedes:** the earlier THEME-1 draft. Changes: text-safe primary color, WCAG-audited semantic colors, `FontWeight` as string, shared token class, and a new logo integration section.

This spec is written as a Claude Code work item. Reference `Spec THEME-1` in commits and issues that implement it.

---

## 0. Before writing any code

1. Read the MudBlazor version from `RVS.Blazor.Manager.csproj` and `RVS.Blazor.Intake.csproj`. The code below targets **MudBlazor 8+ naming**, which carries into 9.x. If either project is on 7.x or earlier, stop and flag it; the type names below will not compile there.
2. Grep for existing `MudTheme` definitions and `MudThemeProvider` usages. This spec **replaces** any existing theme. Do not merge it with one.

Known API facts for 8+/9.x. Don't "correct" these from older tutorials:
- The palettes are `PaletteLight` and `PaletteDark`. The single `Palette` property is obsolete.
- The typography classes are `DefaultTypography`, `H1Typography` … `H6Typography`, `ButtonTypography`, and so on. The 7.x names `Default`, `H1`, etc. no longer exist.
- `FontWeight`, `FontSize`, `LineHeight` and `LetterSpacing` are **strings** (`FontWeight = "700"`), not ints.

---

## 1. Design tokens

### Brand (used in the logo)
| Token | Hex | Notes |
|---|---|---|
| Denim | `#2F4C6B` | Structural color: app bar, drawer, logo |
| Rust (logo) | `#C1502E` | **Logo and marketing only.** Fails AA as body text on cream (4.19:1) |
| Cream | `#F6F1E7` | Intake background; text on Denim |

### UI (used in the MudBlazor themes)
Every pairing below was checked against WCAG 2.1 AA (≥ 4.5:1 for normal text).

**Light palette** (Manager light mode and Intake)
| Role | Hex | Checked against | Ratio |
|---|---|---|---|
| Primary (text-safe rust) | `#A8431F` | Cream / Manager bg / white text on it | 5.35 / 5.67 / 6.02 |
| Secondary (Denim) | `#2F4C6B` | Cream text on it | 7.88 |
| TextPrimary | `#20344A` | Cream | 11.30 |
| TextSecondary | `rgba(32,52,74,0.72)` | Cream / white | 4.99 / 5.30 |
| Drawer text/icons | `rgba(246,241,231,0.85)` | Denim | 6.21 |
| Success | `#36704E` | Cream / white text on it | 5.20 / 5.86 |
| Warning | `#92600F` | Cream / white text on it | 4.78 / 5.38 |
| Error | `#A3123F` | Cream / white text on it | 6.87 / 7.73 |
| Info | `#3B6E91` | Cream / white text on it | 4.88 / 5.49 |

**Dark palette** (Manager only)
| Role | Hex | Checked against | Ratio |
|---|---|---|---|
| Background | `#1B2A3C` | — | — |
| Surface | `#243B54` | — | — |
| Primary | `#E8956D` | Surface / dark text on it | 4.89 / 6.20 |
| Secondary | `#8FA9C2` | Surface / dark text on it | 4.72 / 5.97 |
| TextPrimary | `#F0ECE1` | Surface | 9.73 |
| TextSecondary | `rgba(240,236,225,0.70)` | Surface | 5.67 |
| Success | `#6DBA88` | Surface / dark text on it | 4.94 / 6.26 |
| Warning | `#E0A94E` | Surface / dark text on it | 5.44 / 6.90 |
| Error | `#F08A8A` | Surface / dark text on it | 4.76 / 6.03 |
| Info | `#7FB2D3` | Surface / dark text on it | 5.04 / 6.38 |

In dark mode every filled color is light, so **every `*ContrastText` in `PaletteDark` must be dark (`#1B2A3C`)**. MudBlazor's default is white, which would put white text on light fills.

### Error vs. Primary: a rule, not just a color
Primary is a rust red, so any red error color sits close to it in hue. Error is shifted toward crimson (`#A3123F`) to help, but hue alone is not a reliable signal, especially for color-blind users. The rule:

> **Every error state carries an icon.** `MudAlert` does this by default; don't turn it off. For `MudChip`, `StatusBadge`, validation text, and table-row states that mean error, add `Icons.Material.Filled.ErrorOutline` (or similar). Never distinguish error from primary by color alone.

---

## 2. Fonts: self-hosted

The Intake app is a PWA used on poor connections, so the Google Fonts CDN is not allowed.

**Acceptance criteria**
- [ ] Copy everything in `fonts/` (three `.woff2` files, `fonts.css`, `OFL.txt`) into `RVS.UI.Shared/wwwroot/fonts/`. **Keep `OFL.txt`.** The font license requires it to ship with the font files.
- [ ] Both apps' `wwwroot/index.html` `<head>` includes `<link rel="stylesheet" href="_content/RVS.UI.Shared/fonts/fonts.css">`.
- [ ] After a publish build, confirm the `.woff2` files appear in `service-worker-assets.js`, so they're precached for offline use.

---

## 3. Shared tokens and themes

All of this lives in `RVS.UI.Shared/Theme/`. Colors are defined once in `RvsBrand`, and neither theme file contains a hex literal.

```csharp
// RVS.UI.Shared/Theme/RvsBrand.cs
namespace RVS.UI.Shared.Theme;

/// <summary>RV Intake brand + UI color tokens. Spec THEME-1. Every UI pairing audited to WCAG AA.</summary>
public static class RvsBrand
{
    // Brand (logo / marketing)
    public const string Denim = "#2F4C6B";
    public const string RustLogo = "#C1502E";   // logo only — fails AA as text on cream
    public const string Cream = "#F6F1E7";

    // Light UI
    public const string Primary = "#A8431F";    // text-safe rust
    public const string TextPrimary = "#20344A";
    public const string TextSecondary = "rgba(32,52,74,0.72)";
    public const string ManagerBackground = "#FAF8F3";
    public const string Success = "#36704E";
    public const string Warning = "#92600F";
    public const string Error = "#A3123F";
    public const string Info = "#3B6E91";

    // Dark UI (Manager)
    public const string DarkBackground = "#1B2A3C";
    public const string DarkSurface = "#243B54";
    public const string DarkPrimary = "#E8956D";
    public const string DarkSecondary = "#8FA9C2";
    public const string DarkTextPrimary = "#F0ECE1";
    public const string DarkTextSecondary = "rgba(240,236,225,0.70)";
    public const string DarkSuccess = "#6DBA88";
    public const string DarkWarning = "#E0A94E";
    public const string DarkError = "#F08A8A";
    public const string DarkInfo = "#7FB2D3";

    public static readonly string[] FontStack = { "Space Grotesk", "Roboto", "Helvetica", "Arial", "sans-serif" };
}
```

```csharp
// RVS.UI.Shared/Theme/RvsThemes.cs
using MudBlazor;

namespace RVS.UI.Shared.Theme;

public static class RvsThemes
{
    private static PaletteLight Light(string background) => new()
    {
        Primary = RvsBrand.Primary,           PrimaryContrastText = "#FFFFFF",
        Secondary = RvsBrand.Denim,           SecondaryContrastText = RvsBrand.Cream,
        AppbarBackground = RvsBrand.Denim,    AppbarText = RvsBrand.Cream,
        DrawerBackground = RvsBrand.Denim,
        DrawerText = "rgba(246,241,231,0.85)", DrawerIcon = "rgba(246,241,231,0.85)",
        Background = background,
        Surface = "#FFFFFF",
        TextPrimary = RvsBrand.TextPrimary,
        TextSecondary = RvsBrand.TextSecondary,
        Success = RvsBrand.Success,           SuccessContrastText = "#FFFFFF",
        Warning = RvsBrand.Warning,           WarningContrastText = "#FFFFFF",
        Error = RvsBrand.Error,               ErrorContrastText = "#FFFFFF",
        Info = RvsBrand.Info,                 InfoContrastText = "#FFFFFF",
    };

    private static Typography BuildTypography(string baseSize, string buttonWeight) => new()
    {
        Default = new DefaultTypography { FontFamily = RvsBrand.FontStack, FontWeight = "400", FontSize = baseSize },
        H1 = new H1Typography { FontFamily = RvsBrand.FontStack, FontWeight = "700" },
        H2 = new H2Typography { FontFamily = RvsBrand.FontStack, FontWeight = "700" },
        H3 = new H3Typography { FontFamily = RvsBrand.FontStack, FontWeight = "700" },
        H4 = new H4Typography { FontFamily = RvsBrand.FontStack, FontWeight = "500" },
        H5 = new H5Typography { FontFamily = RvsBrand.FontStack, FontWeight = "500" },
        H6 = new H6Typography { FontFamily = RvsBrand.FontStack, FontWeight = "500" },
        Button = new ButtonTypography { FontFamily = RvsBrand.FontStack, FontWeight = buttonWeight },
    };

    /// <summary>Manager: dense all-day ops console. Light + dark.</summary>
    public static readonly MudTheme Manager = new()
    {
        PaletteLight = Light(RvsBrand.ManagerBackground),
        PaletteDark = new PaletteDark
        {
            Primary = RvsBrand.DarkPrimary,         PrimaryContrastText = RvsBrand.DarkBackground,
            Secondary = RvsBrand.DarkSecondary,     SecondaryContrastText = RvsBrand.DarkBackground,
            AppbarBackground = RvsBrand.DarkBackground, AppbarText = RvsBrand.Cream,
            DrawerBackground = RvsBrand.DarkBackground,
            Background = RvsBrand.DarkBackground,
            Surface = RvsBrand.DarkSurface,
            TextPrimary = RvsBrand.DarkTextPrimary,
            TextSecondary = RvsBrand.DarkTextSecondary,
            Success = RvsBrand.DarkSuccess,         SuccessContrastText = RvsBrand.DarkBackground,
            Warning = RvsBrand.DarkWarning,         WarningContrastText = RvsBrand.DarkBackground,
            Error = RvsBrand.DarkError,             ErrorContrastText = RvsBrand.DarkBackground,
            Info = RvsBrand.DarkInfo,               InfoContrastText = RvsBrand.DarkBackground,
        },
        Typography = BuildTypography(baseSize: "0.875rem", buttonWeight: "500"),
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "10px", DrawerWidthLeft = "260px" },
    };

    /// <summary>Intake: anonymous, mobile, one-time form. Light only, larger type, softer corners.</summary>
    public static readonly MudTheme Intake = new()
    {
        PaletteLight = Light(RvsBrand.Cream),
        Typography = BuildTypography(baseSize: "1rem", buttonWeight: "700"),
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "14px" },
    };
}
```

**Acceptance criteria**
- [ ] `dotnet build` passes for both apps with zero new warnings. If a `*ContrastText` property doesn't exist in the installed version, remove that line and note it in the PR. Don't substitute a guess.
- [ ] Manager `MainLayout.razor`: `<MudThemeProvider Theme="RvsThemes.Manager" @bind-IsDarkMode="_isDarkMode" />`, with a working toggle.
- [ ] Intake layout: `<MudThemeProvider Theme="RvsThemes.Intake" />` with **no** `IsDarkMode` binding and no toggle. The app stays light even when the OS is in dark mode.
- [ ] `grep -rn "#[0-9A-Fa-f]\{6\}" --include=*.razor --include=*.cs` shows no hex colors outside `RvsBrand.cs`. Existing inline colors in components get replaced with `Color.*` enums or theme CSS variables (`var(--mud-palette-primary)`).
- [ ] Rust `#C1502E` appears **nowhere** in app UI code. It exists only in `RvsBrand.RustLogo` and the logo files.

---

## 4. Logo in the apps

Assets come from `rv-intake-logo-kit` (delivered separately). All text in the logo SVGs is outlined, so they don't depend on the font loading.

**Acceptance criteria**
- [ ] Copy the kit's `svg/logo-horizontal-reversed.svg`, `svg/glyph-reversed.svg`, `svg/logo-horizontal.svg` and `svg/logo-stacked.svg` into `RVS.UI.Shared/wwwroot/brand/`.
- [ ] **Manager app bar:** `logo-horizontal-reversed.svg` at 28–32px tall (cream on Denim). Below the `md` breakpoint, swap to `glyph-reversed.svg` alone.
- [ ] **Intake header:** the shop's name and logo are the primary branding, because this is the shop's form. RV Intake appears only as a small "Powered by" footer: `logo-horizontal.svg` at about 20px tall, `TextSecondary` label. Don't put RV Intake's logo above the shop's.
- [ ] Both logos get `alt="RV Intake"`. Decorative-only glyph uses get `alt=""` and `aria-hidden="true"`.
- [ ] **Favicons, per app:** copy `favicon/favicon.ico`, `favicon/favicon.svg`, `apple/apple-touch-icon.png` and the four `android-pwa/*.png` files into each app's `wwwroot/`, replacing the template placeholders. Merge `android-pwa/manifest-icons-snippet.json` into each `manifest.json`.
- [ ] Each `index.html` `<head>` contains:
```html
<link rel="icon" href="favicon.ico" sizes="48x48">
<link rel="icon" href="favicon.svg" type="image/svg+xml">
<link rel="apple-touch-icon" href="apple-touch-icon.png">
<meta name="theme-color" content="#2F4C6B">
```

---

## 5. Manual checks after it builds

- [ ] **Intake on a real phone** (or a 390px DevTools viewport): the Continue and Submit buttons are at least 44px tall, the cream background shows, and the step cards are white.
- [ ] **Manager, both modes:** the app bar, drawer, active nav item, primary buttons and focus rings all look correct. In dark mode, primary buttons have **dark** text on light-rust fills.
- [ ] **`StatusBadge` / `PriorityBadge` review:** every error-type status shows an icon, per §1. List any badge changed in the PR description.
- [ ] Run Lighthouse accessibility on one Intake page and one Manager page, and paste the scores into the PR. Any contrast failure there is a bug in this spec. Report it back rather than hand-tuning a color in a component.

## 6. Out of scope — ask before doing

- `RVS.MAUI.Tech` is not in the repo yet.
- Redesigning `RVS.UI.Shared` components. This is a token and theme pass; flag anything that looks wrong under the new palette.
- `Tertiary`. Leave it unset; the brand is two colors.
