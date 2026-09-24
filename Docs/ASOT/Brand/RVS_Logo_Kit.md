# RV Intake — Logo Kit

> **Status: the logo kit's README as shipped, kept as the asset inventory. Re-issued
> September 24 2026 with a new mark:** Concept A, "Service Tag", replaces the Concept D "RV"
> badge. Because the glyph no longer spells "RV", every lockup now sets the full two-tone
> "RV Intake". Every raster was replaced along with the SVGs.
>
> Where the files went:
>
> - **`svg/`**: everything except `og-image.svg` is in `RVS.UI.Shared/wwwroot/brand/`. Unlike the
>   previous kit, the text is outlined, so app chrome now loads these files directly: the
>   `BrandWordmark` component renders an `<img>` of the lockup it is asked for
>   (`BrandMarkAssets.PathFor`), and no inlined copy of the geometry remains.
> - **`favicon/`, `apple/`, `android-pwa/`**: in each app's `wwwroot/`, with the `<head>` tags
>   below. The separate 16/32/48 PNGs were dropped, since `favicon.ico` carries all three, except
>   that Manager keeps `favicon-32x32.png` because the Auth0 login page references it by URL.
>   The manifests already matched the snippet; they only gained `"purpose": "any"`.
> - **`email/`, `print/`, `social/`: not in the repo.** Nothing consumes them yet. The packet
>   masthead carries the *dealer's* logo (`PacketBranding.LogoDataUri`), not RV Intake's, and no
>   page emits `og:image`. Take them from the kit zip when a consumer appears.
>
> "Rust `#C1502E` is the logo color" below is `RvsBrand.AccentLogo`; the text-safe `#A8431F` is
> `RvsBrand.Accent`.

**Mark:** Concept A, "Service Tag". A check-in tag with a checkmark, meaning "checked in, verified, ready."
**Palette:** Denim & Rust.

| Token | Hex | Where it's used in the logo |
|---|---|---|
| Denim | `#2F4C6B` | App-icon background, glyph and "Intake" on light backgrounds |
| Rust | `#C1502E` | "RV" in the wordmark on light backgrounds |
| Light rust | `#E8956D` | "RV" in the wordmark on dark backgrounds |
| Cream | `#F6F1E7` | Glyph and "Intake" on dark backgrounds; page background |

Rust `#C1502E` is the **logo** color. In app UI text and buttons, use the darker `#A8431F` from the theme spec, because `#C1502E` fails contrast as body text on cream.

All text in these SVGs is **converted to outlines**. The files render identically on any machine, whether or not Space Grotesk is installed. To change the wording, regenerate the file; don't edit the paths.

---

## `svg/` — vector masters

| File | Use |
|---|---|
| `icon.svg` | App icon: glyph on a rounded Denim square. Use at 180px and larger. |
| `icon-small.svg` | The same icon with heavier strokes, tuned for 16–48px. Use **only** for favicons. The standard stroke weight blurs at those sizes. |
| `icon-maskable.svg` | Full-bleed square with the glyph inside Android's safe zone. Use only for PWA maskable icons. |
| `glyph.svg` / `glyph-reversed.svg` | The bare tag mark with no background, in Denim or Cream. |
| `logo-horizontal.svg` / `-reversed.svg` | Glyph + "RV Intake". Primary logo for headers and email. |
| `logo-stacked.svg` / `-reversed.svg` | Glyph above "RV Intake". For square spaces, splash screens, and print. |
| `wordmark.svg` / `-reversed.svg` | "RV Intake" alone, with no glyph. For narrow spaces and inline use. |
| `og-image.svg` | Source for the social card. |

The glyph no longer spells "RV", so the full two-tone "RV Intake" name can sit next to it without repetition.

## `favicon/`

Drop these files into each app's `wwwroot/`:

```html
<link rel="icon" href="favicon.ico" sizes="48x48">
<link rel="icon" href="favicon.svg" type="image/svg+xml">
<link rel="apple-touch-icon" href="apple-touch-icon.png">
<meta name="theme-color" content="#2F4C6B">
```

`favicon.ico` contains the 16, 32, and 48px sizes. Modern browsers prefer `favicon.svg`. Both use the heavier small-size strokes. The icon was tested against Chrome's dark tab color: the Denim square blends into it, but the cream glyph stays clearly visible.

## `apple/`
`apple-touch-icon.png` is 180×180 and fully opaque, because iOS fills transparent pixels with black.

## `android-pwa/`
The filenames match the Blazor WASM PWA template's defaults, so these files replace `icon-192.png` and `icon-512.png` directly in `wwwroot/`. Merge `manifest-icons-snippet.json` into `wwwroot/manifest.json`; it adds the two maskable icons plus `theme_color` and `background_color`.

## `social/`
`og-image-1200x630.png` is for `og:image` and `twitter:image` on marketing pages.

## `email/`
Transparent PNGs for ACS transactional email headers. Use the `@2x` file with `width="300"` set on the `<img>`. Email clients don't reliably support `srcset`. `logo-horizontal-reversed@2x.png` is for dark header bands.

## `print/`
High-resolution transparent PNGs for the service-packet letterhead (`PacketPdfRenderer` / `PacketHtmlRenderer`). If the renderer accepts SVG, use `svg/logo-horizontal.svg` instead; it stays sharp at any size.

---

## Still open

- **Trademark.** "RV Intake" is likely to be treated as *merely descriptive* by the USPTO. Spend an hour with a trademark attorney before investing in signage or paid ads. Also have them check whether a similar tag-plus-checkmark logo is already registered in software.
- **Physical key tags.** A tag-shaped key tag printed with the shop's intake QR code would match the mark. Worth pricing once dealers are onboard.
