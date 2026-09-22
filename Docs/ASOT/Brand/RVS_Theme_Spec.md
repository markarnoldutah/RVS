# RVS Front End — MudBlazor Theme: Denim & Rust

> **Status: the brief as handed over (issue #702), kept for its rationale.** Implemented on
> branch `702-new-ui-theme-and-palette`. Where this file and the code disagree, the code wins:
> `RVS.UI.Shared/Theme/RvsBrand.cs` is the authoritative copy of the palette, and
> `Docs/ASOT/RVS_FrontEnd.md` describes what was actually built. Two details here did not
> survive contact with MudBlazor 9.4.0 — `Typography.*.FontWeight` is typed `string`, not
> `int`, and the themes live beside a per-app `ThemeService` that selects the mode rather than
> replacing it. What this file is still worth reading for is the *why*: why Intake gets full
> cream and Manager does not, why Rust never becomes `Error`, and why the fonts are self-hosted.

**For:** `RVS.Blazor.Manager` and `RVS.Blazor.Intake` (MudBlazor 9.x, Material Design 3)
**Purpose:** Roll the "RV Intake" brand (Concept D wordmark, Denim & Rust colorway) into both apps' MudBlazor theme. This file is written to be handed to Claude Code as a work item.

Cite this file as `Spec THEME-1` in any issue/commit that implements it.

---

## 1. Design tokens

| Token | Hex | Role |
|---|---|---|
| `Ink` (Denim) | `#2F4C6B` | Structure — app bar, drawer, nav, headings, body text |
| `Accent` (Rust) | `#C1502E` | Action — primary buttons, links, focus states, active nav |
| `Accent, on dark` | `#E8956D` | Same accent, used where it sits on a dark/Ink surface |
| `Paper` (Cream) | `#F6F1E7` | Page/card background in light mode |
| `Ink, dark-mode surface` | `#1B2A3C` | Slightly deeper than Ink — dark-mode page background |

Typeface: **Space Grotesk** (Bold for headings/wordmark weight, Medium for UI labels/buttons, Regular for body text). OFL-licensed, free for commercial use.

Semantic colors are **not** derived from the brand pair — see §4. Do not let anyone reuse Rust for `Error`; the two need to stay visually distinct (see the note in §4).

---

## 2. Font hosting — self-host, do not link Google Fonts CDN

`RVS.Blazor.Intake` is a PWA aimed at customers with "poor bay connectivity" (per `RVS_FrontEnd_Solution.md`) — a Google Fonts CDN `<link>` is a network dependency that can fail exactly when the app needs to work offline-first from cache. Self-host instead.

**Acceptance criteria**
- [ ] Three WOFF2 files (`SpaceGrotesk-Bold.woff2`, `SpaceGrotesk-Medium.woff2`, `SpaceGrotesk-Regular.woff2` — provided alongside this spec) are placed in `RVS.UI.Shared/wwwroot/fonts/` so both apps reference one copy via the RCL static-asset path (`_content/RVS.UI.Shared/fonts/...`).
- [ ] A `fonts.css` in the same shared location declares:

```css
@font-face {
  font-family: 'Space Grotesk';
  src: url('SpaceGrotesk-Regular.woff2') format('woff2');
  font-weight: 400;
  font-style: normal;
  font-display: swap;
}
@font-face {
  font-family: 'Space Grotesk';
  src: url('SpaceGrotesk-Medium.woff2') format('woff2');
  font-weight: 500;
  font-style: normal;
  font-display: swap;
}
@font-face {
  font-family: 'Space Grotesk';
  src: url('SpaceGrotesk-Bold.woff2') format('woff2');
  font-weight: 700;
  font-style: normal;
  font-display: swap;
}
```

- [ ] Both `RVS.Blazor.Manager/wwwroot/index.html` and `RVS.Blazor.Intake/wwwroot/index.html` add, in `<head>`:
```html
<link rel="stylesheet" href="_content/RVS.UI.Shared/fonts/fonts.css">
```
- [ ] Both apps' service-worker asset manifest (`service-worker.published.js` / the PWA precache list) picks up the font files automatically since they're under `wwwroot` of the referenced RCL — confirm they show up in the generated `service-worker-assets.js` after a publish build, not just at dev time.
- [ ] Both `index.html` files get `<meta name="theme-color" content="#2F4C6B">` in `<head>` (brand ink in mobile browser chrome).

---

## 3. Where the theme lives

Put a shared base theme in `RVS.UI.Shared` (it's already the shared home for "CSS / design tokens" per the front-end architecture table), then let each app take it as-is or override narrowly. Do **not** duplicate the palette definition in both apps — one drifts, then nobody trusts either.

```
RVS.UI.Shared/
  Theme/
    RvsTheme.cs        <- shared base: fonts, layout, semantic colors, light+dark palettes
    ManagerTheme.cs     <- static MudTheme ManagerTheme, built from RvsTheme
    IntakeTheme.cs       <- static MudTheme IntakeTheme, built from RvsTheme
```

`MudBlazor 9.x` note: `MudTheme.Palette` is **obsolete** in v9 — it will not compile against a `Palette` property assignment in some 9.x point releases and is deprecated in all of them. Use `PaletteLight` and `PaletteDark`, both typed `PaletteLight` / `PaletteDark` (subclasses of the base `Palette`). Don't let Claude Code reach for `Palette = new Palette { ... }` from an older tutorial it might have memorized — check this repo's actual installed MudBlazor version in the `.csproj` before writing code, since property names have moved between major versions.

---

## 4. Manager app theme (`RVS.Blazor.Manager`)

Manager is an authenticated, dense, hours-at-a-time tool — advisors and admins live in the SR queue and Service Board all day. It should feel closer to a well-made B2B ops console than a marketing page: brand shows up in the app bar, the accent color, and typography — not in big cream hero blocks.

- **App bar / drawer:** Ink (`#2F4C6B`), cream/near-white text
- **Page background (light):** a very light neutral, *not* full cream — cream everywhere reads more "consumer landing page" than "8-hour-shift dashboard." Use `#FAF8F3` (a paper tone barely tinted, closer to typical Material surface) for the page background, and reserve full cream (`#F6F1E7`) for cards/panels that want to feel warmer, e.g. the AI-suggestion chips.
- **Primary:** Rust `#C1502E` — buttons, links, active tab/nav indicator, focus rings
- **Dark mode:** Manager should support the existing `MudThemeProvider @bind-IsDarkMode` toggle pattern already used in this codebase. Dark palette: background `#1B2A3C`, surface a step lighter (`#243B54`), Primary becomes the on-dark accent `#E8956D` (full-saturation Rust loses contrast on a dark background at small sizes — check this in a real browser, not just computed contrast ratio, since MudBlazor's button fill vs. text-on-fill combination matters more than the raw token-to-background ratio).

```csharp
// RVS.UI.Shared/Theme/ManagerTheme.cs
using MudBlazor;

namespace RVS.UI.Shared.Theme;

public static class ManagerTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#C1502E",
            Secondary = "#2F4C6B",
            AppbarBackground = "#2F4C6B",
            AppbarText = "#F6F1E7",
            DrawerBackground = "#2F4C6B",
            DrawerText = "rgba(246,241,231,0.85)",
            DrawerIcon = "rgba(246,241,231,0.85)",
            Background = "#FAF8F3",
            Surface = "#FFFFFF",
            TextPrimary = "#20344A",
            TextSecondary = "rgba(32,52,74,0.68)",
            Success = "#3F7D58",
            Warning = "#C98A2C",
            Error = "#B3261E",
            Info = "#3B6E91",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#E8956D",
            Secondary = "#8FA9C2",
            AppbarBackground = "#1B2A3C",
            AppbarText = "#F6F1E7",
            DrawerBackground = "#1B2A3C",
            Background = "#1B2A3C",
            Surface = "#243B54",
            TextPrimary = "#F0ECE1",
            TextSecondary = "rgba(240,236,225,0.70)",
            Success = "#5FA97A",
            Warning = "#E0A94E",
            Error = "#E5766A",
            Info = "#6FA3C4",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Space Grotesk", "Roboto", "Helvetica", "Arial", "sans-serif" },
                FontWeight = 400,
            },
            H1 = new H1Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 700 },
            H2 = new H2Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 700 },
            H3 = new H3Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 700 },
            H4 = new H4Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 500 },
            H5 = new H5Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 500 },
            H6 = new H6Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 500 },
            Button = new ButtonTypography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 500 },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "260px",
        },
    };
}
```

> **Verify the exact `Typography` sub-type names** (`DefaultTypography`, `H1Typography`, etc.) against the installed MudBlazor version — these have been renamed at least once across major versions (some releases use `Default`, `H1`...`H6` as the type names directly, without the `Typography` suffix). Run `dotnet build` after pasting this and fix any `CS0246` on the type names before touching anything else — that's a version-naming mismatch, not a logic error.

**Acceptance criteria**
- [ ] `ManagerTheme.Theme` compiles against the version of MudBlazor actually referenced in `RVS.Blazor.Manager.csproj`
- [ ] `MainLayout.razor` passes `Theme="ManagerTheme.Theme"` to `MudThemeProvider`, and the existing (or newly added) `IsDarkMode` toggle switches palettes correctly
- [ ] App bar and drawer render Ink background with legible text in both modes
- [ ] Primary buttons, active `MudNavLink`, and focus rings render Rust (light) / light-Rust (dark)
- [ ] `StatusBadge` and `PriorityBadge` (in `RVS.UI.Shared`) are reviewed by hand once this lands — confirm no badge relies on `Color.Primary` (Rust) *and* `Color.Error` (true red) being told apart by color alone at a glance. If any do, add an icon or bump one of them to a `Severity`/`Color` combination that doesn't rely on hue discrimination

---

## 5. Intake app theme (`RVS.Blazor.Intake`)

Intake is anonymous, mobile-first, filled out once by a stressed customer standing next to a broken RV — not a tool anyone lives in. Brand should show up more, not less: full cream background, bigger touch targets, warmer feel. No dark-mode toggle — a one-time form doesn't need one, and skipping it is one less thing to build and test.

```csharp
// RVS.UI.Shared/Theme/IntakeTheme.cs
using MudBlazor;

namespace RVS.UI.Shared.Theme;

public static class IntakeTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#C1502E",
            Secondary = "#2F4C6B",
            AppbarBackground = "#2F4C6B",
            AppbarText = "#F6F1E7",
            Background = "#F6F1E7",
            Surface = "#FFFFFF",
            TextPrimary = "#20344A",
            TextSecondary = "rgba(32,52,74,0.68)",
            Success = "#3F7D58",
            Warning = "#C98A2C",
            Error = "#B3261E",
            Info = "#3B6E91",
        },
        // PaletteDark intentionally omitted — no dark-mode toggle in this app.
        // If MudThemeProvider still requires one to be set, copy PaletteLight verbatim
        // rather than leaving MudBlazor's stock dark palette to clash with the brand.
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Space Grotesk", "Roboto", "Helvetica", "Arial", "sans-serif" },
                FontWeight = 400,
                FontSize = "1rem", // one notch up from MudBlazor's default — this is filled out on a phone, standing up
            },
            H1 = new H1Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 700 },
            H2 = new H2Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 700 },
            H6 = new H6Typography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 500 },
            Button = new ButtonTypography { FontFamily = new[] { "Space Grotesk" }, FontWeight = 700, FontSize = "1rem" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px", // rounder than Manager — softer, less "console"
        },
    };
}
```

**Acceptance criteria**
- [ ] `IntakeTheme.Theme` compiles against the installed MudBlazor version
- [ ] Wizard shell (`IntakeWizard.razor`) passes `Theme="IntakeTheme.Theme"` to `MudThemeProvider`
- [ ] Primary CTA buttons (step "Continue" / final "Submit") render Rust at a size/weight that's obviously tappable — check actual rendered button height against the 44px touch-target minimum on a real phone viewport, not just the theme's `FontSize`
- [ ] Background is full cream (`#F6F1E7`), step cards are white `Surface`
- [ ] No dark-mode toggle is exposed anywhere in this app's UI

---

## 6. What Claude Code should NOT do without asking first

- Don't touch `RVS.MAUI.Tech` — it's not in the repo (`build-mobile.yml` already fails for this reason per the known-gaps list); there's nothing to theme yet.
- Don't change `RVS.UI.Shared`'s existing component markup while doing this — this is a theme/token pass, not a redesign of `StatusBadge`, job cards, etc. Flag anything that looks broken under the new palette instead of silently restyling it.
- Don't invent a `Tertiary` color use case. MudBlazor 9.x's `Palette` has a `Tertiary` slot; leave it unset (falls back to MudBlazor default) unless a real third-accent need shows up later — this spec deliberately stays to a two-color brand.

---

## 7. Files provided alongside this spec

- `fonts/SpaceGrotesk-Regular.woff2`
- `fonts/SpaceGrotesk-Medium.woff2`
- `fonts/SpaceGrotesk-Bold.woff2`
- `fonts/fonts.css` (the `@font-face` block from §2, ready to drop in)

Drop the `fonts/` folder's contents into `RVS.UI.Shared/wwwroot/fonts/` as-is.
