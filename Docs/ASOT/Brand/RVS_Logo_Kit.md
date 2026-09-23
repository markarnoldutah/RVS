# RV Intake — Logo Asset Kit

> **Status: the logo kit's README as shipped, kept as the asset inventory. Re-issued
> September 22 2026 with the revised lockup** — the badge already reads "RV", so the text
> beside it now says only "Intake"; the full two-tone "RV Intake" survives only in the
> text-only files, which have no badge to repeat. Only the SVG lockups changed; the favicon,
> Apple touch and Android/PWA rasters are byte-identical to the first issue (#702) and were
> left alone.
>
> The SVG sources live in `RVS.UI.Shared/wwwroot/brand/`; the raster icons were dropped into
> each app's `wwwroot/`. Two departures from the advice below. App chrome does **not**
> reference the SVGs by URL: they carry live `<text>`, and a browser will not load an external
> `@font-face` into an SVG used as an image, so the wordmark would render in a system font —
> the `BrandWordmark` component inlines the mark instead. And the repo's copies name the
> typeface as a CSS font stack with `font-weight="700"` rather than the kit's
> `font-family="Space Grotesk Bold"`, which no browser resolves: the family is
> "Space Grotesk", the boldness is a weight. The `email/`, `print/` and `social/` assets are
> not wired up yet — packet letterhead was left to its own issue.

Concept: Confident Wordmark (Concept D). Colorway: **Denim & Rust**.

| Token | Hex | Use |
|---|---|---|
| Ink (Denim) | `#2F4C6B` | Wordmark, icon background, primary UI ink |
| Accent (Rust) | `#C1502E` | The "RV" in the wordmark, on light backgrounds |
| Accent, on dark | `#E8956D` | The "RV" in the wordmark, on dark/navy backgrounds |
| Paper (Cream) | `#F6F1E7` | Background, icon glyph on dark badge |

Font: **Space Grotesk**, Bold weight for all lockups (Google Fonts, OFL-licensed — free for commercial use). Medium weight used for the OG-image tagline only.

---

## What's in each folder

### `svg/` — vector sources, edit these first
- `icon.svg` — square badge, rounded corners baked in. Use for favicons, touch icons, anywhere a "classic" square icon is wanted.
- `icon-maskable.svg` — same mark, full-bleed square background, content kept inside Android's safe zone. Use **only** for Android/PWA maskable icons — the OS applies its own mask shape.
- `wordmark-horizontal.svg` / `wordmark-horizontal-reversed.svg` — badge + "Intake" (the badge already reads "RV," so the text completes it rather than repeating it), for light and dark backgrounds respectively.
- `wordmark-text-only.svg` / `wordmark-text-only-reversed.svg` — no badge, just the wordmark. Use in narrow headers, letterhead, print, anywhere a square badge doesn't fit.
- `wordmark-stacked.svg` — badge above "Intake" (same reasoning as the horizontal lockup), centered. Use for square placements (splash screens, social profile pictures if you ever want text baked in).
- `wordmark-text-only.svg` / `wordmark-text-only-reversed.svg` — the only files that still show the full two-tone "RV Intake" — there's no badge alongside them, so no repetition. Use these wherever you want the full name spelled out in text.

All are plain SVG with hex colors — no external font dependency at render time (text is live, not outlined, so if you edit these in another tool you'll need Space Grotesk Bold installed, or convert text to paths first).

### `favicon/`
- `favicon.ico` — multi-resolution (16/32/48px). Drop in `wwwroot/favicon.ico`; referenced by the default Blazor template's `<link rel="icon" href="favicon.ico"/>`.
- `favicon-16x16.png`, `favicon-32x32.png`, `favicon-48x48.png` — standalone PNGs if you want explicit `<link rel="icon" sizes="32x32" type="image/png">` tags instead of/alongside the `.ico`.

### `apple/`
- `apple-touch-icon-180x180.png` — opaque (iOS fills transparent pixels with black otherwise). Drop in `wwwroot/`, reference with `<link rel="apple-touch-icon" href="apple-touch-icon-180x180.png">` in `index.html`'s `<head>`. iOS scales this down for older devices — one size is enough today.

### `android-pwa/`
Matches the filenames the default **Blazor WASM PWA template** already expects in `manifest.json` — you can drop these straight over the placeholder `icon-192.png` / `icon-512.png` in `wwwroot/`.
- `icon-192.png`, `icon-512.png` — standard install icons.
- `icon-192-maskable.png`, `icon-512-maskable.png` — for adaptive/maskable display (Android applies a circle, squircle, or other shape mask — these keep the "RV" safely inside).
- `manifest-icons-snippet.json` — the `icons` array (plus suggested `theme_color`/`background_color`) to merge into your existing `wwwroot/manifest.json`.

### `social/`
- `og-image-1200x630.png` — Open Graph / Twitter card image. Reference with `<meta property="og:image" content="...">` and `<meta name="twitter:image" content="...">` on marketing pages. Includes the positioning line so it reads standalone in a link preview.

### `email/`
- `logo-horizontal@1x.png` (300px wide), `logo-horizontal@2x.png` (600px wide) — transparent background, for the header of transactional emails (submission confirmations, status-change notices) sent via ACS. Use the `@2x` file with `width="300"` set in the `<img>` tag's HTML/CSS attribute for a crisp look on retina screens without bloating the HTML email payload — email clients generally don't support `srcset`.

### `print/`
- `logo-horizontal-print.png` (2400px wide), `wordmark-text-only-print.png` (2000px wide) — transparent background, high-resolution for the printed/PDF service-packet letterhead (`PacketPdfRenderer` / `PacketHtmlRenderer`). At 2400px wide this holds up sharp at roughly 8 inches wide and 300dpi — scale down, not up.

---

## Things you'll still want to do

- **Convert text to outlines** if you ever hand these SVGs to a print shop or a designer without Space Grotesk installed — right now the SVGs have live `<text>` elements, which is convenient to edit but will fall back to a default font on a machine that doesn't have Space Grotesk.
- **Trademark check** before you get attached: a basic USPTO/state search on "RV Intake" hasn't been done here — worth 20 minutes before you sink real money into signage or paid ads under the name.
- **`theme-color` meta tag**: add `<meta name="theme-color" content="#2F4C6B">` to `index.html` so mobile browser chrome (Android's address bar, iOS status bar area) picks up the brand ink color when someone has the site open.
