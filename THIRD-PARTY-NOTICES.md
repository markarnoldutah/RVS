# Third-Party Notices

RVS is proprietary software. It incorporates third-party open-source components,
listed below with their licenses. This file and the verbatim license texts under
[`licenses/`](licenses/) are distributed with every deployable build so that the
notices travel with the binaries they cover.

**Scope.** This covers components that are *redistributed* — i.e. shipped inside a
deployed artifact:

| Deployed app | Target | What ships |
| --- | --- | --- |
| `RVS.API` | Azure App Service (Linux) | Managed assemblies **and** native libraries (ImageMagick, SkiaSharp, qpdf) under `runtimes/*/native/` |
| `RVS.Blazor.Intake` | Azure Static Web Apps | .NET WASM runtime + managed assemblies |
| `RVS.Blazor.Manager` | Azure Static Web Apps | .NET WASM runtime + managed assemblies |

Build- and test-only packages (xUnit, Moq, FluentAssertions, coverlet, the WASM
dev server, `Microsoft.NET.Test.Sdk`) and internal tools that are never deployed
(`RVS.Data.Cosmos.Seed`, `RVS.PacketDump`) are **not** redistributed and are not
listed here.

Regenerate the pointers in this file after changing a package version — see
[Maintenance](#maintenance).

---

## 1. ImageMagick / Magick.NET — requires attention

`RVS.API` uses **Magick.NET** (`Magick.NET-Q8-AnyCPU` 14.17.1) to transcode
HEIC/HEIF photo uploads to JPEG (`RVS.API/Integrations/MagickImageTranscoder.cs`,
issue #508). The package bundles a native **ImageMagick 7.1.2** build with ~35
codec/delegate libraries, several of which carry their own licenses.

| Layer | Component | License |
| --- | --- | --- |
| .NET wrapper | Magick.NET, Magick.NET.Core — © Dirk Lemstra | Apache-2.0 |
| Native core | ImageMagick 7.1.2 — © ImageMagick Studio LLC | [ImageMagick License](https://imagemagick.org/script/license.php) (Apache-2.0-style, OSI-approved) |
| Native delegate — **HEIC decode** | `libheif` 1.23.2, `libde265` 1.1.1 | **LGPL-3.0-or-later** |
| Native delegates — everything else | `aom`, `brotli`, `bzip2`, `cairo`, `libcroco`, `openexr`, `libffi`, `fontconfig`, `freetype`, `fribidi`, `gdk-pixbuf`, `glib`, `harfbuzz`, `libhwy`, `imath`, `libjpeg-turbo`, `libjxl`, `lcms`, `liblqr`, `liblzma`, `openh264`, `openjpeg`, `openjph`, `pango`, `pixman`, `libpng`, `libraqm`, `libraw`, `librsvg`, `libtiff`, `libwebp`, `libxml2`, `libzip`, `zlib` | Permissive (BSD / MIT / zlib / MPL-2.0 / Apache-2.0-style). Full texts in the vendored notice file. |

The complete, verbatim upstream notice — every component above with its full
license text — is vendored at:

> [`licenses/Magick.NET.THIRD-PARTY-NOTICES.txt`](licenses/Magick.NET.THIRD-PARTY-NOTICES.txt)
> (copied unmodified from `Magick.NET-Q8-AnyCPU` 14.17.1 `Notice.txt`)

### LGPL-3.0 compliance (`libheif`, `libde265`)

The LGPL is satisfied for a hosted service that links these libraries as separate
shared objects (which `Magick.Native` does) provided the recipient can relink
against a modified version and the library source is available. Accordingly:

- **Written offer / source location.** The source for the native build, including
  `libheif` and `libde265` and the scripts that produce `Magick.Native`, is
  published at <https://github.com/dlemstra/Magick.Native> (see its `ImageMagick`
  submodule and `build` directory). Upstream: <https://github.com/strukturag/libheif>,
  <https://github.com/strukturag/libde265>.
- **Relinking.** The native libraries ship as distinct files under
  `RVS.API/bin/**/runtimes/<rid>/native/Magick.Native-Q8-*.{so,dylib,dll}`; a
  recipient may substitute a self-built `Magick.Native` of the same version.
- **License text.** LGPL-3.0 and GPL-3.0 full texts are included in the vendored
  notice file above.

### Not bundled: HEVC *encoder* (x265)

This package includes only the HEVC **decoder** (`libde265`, LGPL). The `x265`
encoder (GPL-2.0-or-later) is **not** present — HEIC *encoding* is unavailable
and RVS only ever decodes. There is therefore no GPL obligation.

### Patent note (not a copyright matter)

HEIC decoding exercises the HEVC / H.265 standard, which is subject to patent
pools (Access Advance, MPEG LA, Velos Media) independent of the open-source
licenses above. Exposure for server-side, decode-only use is generally assessed
as low, but this should be confirmed by whoever owns IP/legal review. AVIF (AV1,
royalty-free) is not an option for inbound iPhone photos, which are HEIC.

---

## 2. QuestPDF — dual-licensed, RVS uses the Community License

`RVS.API` uses **QuestPDF** 2026.8.0 to render the service-packet PDF
(`RVS.API/Packets/PacketPdfRenderer.cs`). QuestPDF is **dual-licensed**
(Community / Professional / Enterprise). RVS runs under the **Community License**
and asserts it in code (`QuestPDF.Settings.License = LicenseType.Community`).

> **Eligibility must be re-checked as the company grows.** The Community License
> is conditioned on thresholds (annual gross revenue and number of developers
> working with QuestPDF). If RVS crosses them, a paid Professional/Enterprise
> license is required. See [`licenses/QuestPDF.LICENSE.md`](licenses/QuestPDF.LICENSE.md)
> (vendored verbatim) and <https://www.questpdf.com/pricing>.

QuestPDF bundles native **SkiaSharp** and **qpdf** builds and their transitive
native dependencies (`skia`, `harfbuzz`, `libjpeg-turbo`, `libpng`, `libwebp`,
`zlib`, `expat`, `wuffs`, `libgrapheme`, `qpdf` — BSD / MIT / Apache-2.0 /
zlib). Upstream texts are vendored at
[`licenses/QuestPDF.ExternalDependencyLicenses/`](licenses/QuestPDF.ExternalDependencyLicenses/).

---

## 3. Other redistributed components

All permissively licensed; no copyleft. License bodies are in
[§5](#5-license-texts).

### Server — `RVS.API`

| Component | Version | License | © |
| --- | --- | --- | --- |
| QRCoder | 1.8.0 | MIT | © Raffael Herrmann |
| Newtonsoft.Json | 13.0.4 | MIT | © James Newton-King |
| Swashbuckle.AspNetCore.SwaggerUI | 10.1.7 | MIT | © Richard Morris — embeds swagger-ui © SmartBear Software (Apache-2.0) |
| OpenTelemetry.Api | 1.15.3 | Apache-2.0 | © The OpenTelemetry Authors |
| Azure.Communication.Email | 1.1.0 | MIT | © Microsoft |
| Azure.Communication.Sms | 1.0.2 | MIT | © Microsoft |
| Azure.Identity | 1.21.0 | MIT | © Microsoft |
| Azure.Storage.Blobs | 12.27.0 | MIT | © Microsoft |
| Azure.Extensions.AspNetCore.Configuration.Secrets | 1.5.0 | MIT | © Microsoft |
| Microsoft.Azure.Cosmos | 3.59.0 | MIT | © Microsoft — see package for its own third-party notices |
| Microsoft.ApplicationInsights.AspNetCore | 3.1.0 | MIT | © Microsoft |
| Microsoft.AspNetCore.* / Microsoft.Extensions.* (ASP.NET Core 10, incl. `Authentication.JwtBearer`, `OpenApi`, `Http.Resilience`) | 10.0.x / 10.5.0 | MIT | © .NET Foundation and Contributors |

### Clients — `RVS.Blazor.Intake`, `RVS.Blazor.Manager`, `RVS.UI.Shared`

| Component | Version | License | © |
| --- | --- | --- | --- |
| MudBlazor | 9.4.0 | MIT | © MudBlazor |
| Microsoft.AspNetCore.Components.WebAssembly (+ `.Web`, `.Authentication`) | 10.0.7 | MIT | © .NET Foundation and Contributors |
| Microsoft.Extensions.Http | 10.0.7 | MIT | © .NET Foundation and Contributors |
| .NET runtime for WebAssembly (emitted by the SDK into the published bundle) | 10.0.x | MIT | © .NET Foundation and Contributors — see <https://github.com/dotnet/runtime> `THIRD-PARTY-NOTICES.TXT` |

### Shared native — `RVS.Infra.*`, `RVS.Domain`

Covered above (Cosmos, Blob, Newtonsoft.Json, `Microsoft.Extensions.Logging.Abstractions` — MIT).

---

## 4. Where the notices ship

`RVS.API/RVS.API.csproj` copies this file and [`licenses/`](licenses/) into the
build **and publish** output. In a deployed App Service the files sit next to the
assemblies and `runtimes/` folder they describe:

```
/site/wwwroot/THIRD-PARTY-NOTICES.md
/site/wwwroot/licenses/Magick.NET.THIRD-PARTY-NOTICES.txt
/site/wwwroot/licenses/QuestPDF.LICENSE.md
/site/wwwroot/licenses/QuestPDF.ExternalDependencyLicenses/*.txt
/site/wwwroot/licenses/Apache-2.0.txt
```

No end-user-facing display is required: the RVS apps interact with users only
over the network and never convey these components to a browser or device
(GPLv3/LGPLv3 §"Mere interaction … is not conveying"; Apache-2.0 / MIT attach to
distribution). The client WASM bundles ship only MIT-licensed components. If an
OSS bill of materials is ever needed for procurement, generate an SBOM
(CycloneDX / SPDX) in CI rather than adding a UI page.

---

## 5. License texts

### MIT License

Applies to every component marked **MIT** above. Each retains its own copyright
line (listed in the tables); the permission text is identical:

```
Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

### Apache License 2.0

Applies to **OpenTelemetry.Api**, the **ImageMagick License** components (which
follow the Apache-2.0 template), and swagger-ui. Full text:
[`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt).

### ImageMagick License

Apache-2.0-style, OSI-approved. Full text in
[`licenses/Magick.NET.THIRD-PARTY-NOTICES.txt`](licenses/Magick.NET.THIRD-PARTY-NOTICES.txt)
(first section).

### LGPL-3.0 / GPL-3.0 (`libheif`, `libde265`)

Full texts in
[`licenses/Magick.NET.THIRD-PARTY-NOTICES.txt`](licenses/Magick.NET.THIRD-PARTY-NOTICES.txt)
(the `libde265` and `libheif` sections). Compliance statement in
[§1](#lgpl-30-compliance-libheif-libde265).

### BSD-3-Clause (SkiaSharp / skia, Moq is test-only)

```
Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this
   list of conditions and the following disclaimer in the documentation and/or
   other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors may
   be used to endorse or promote products derived from this software without
   specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES ... IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY,
OR CONSEQUENTIAL DAMAGES ... EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

---

## Maintenance

- **Adding / bumping a redistributed package:** update the relevant table row.
  If it carries a custom or copyleft license, vendor its verbatim text under
  `licenses/` and add a subsection.
- **Bumping `Magick.NET-Q8-AnyCPU`:** replace
  `licenses/Magick.NET.THIRD-PARTY-NOTICES.txt` with the new package's
  `Notice.txt` (`~/.nuget/packages/magick.net-q8-anycpu/<version>/Notice.txt`)
  and re-check the delegate list in [§1](#1-imagemagick--magicknet--requires-attention),
  especially whether an HEVC encoder (`x265`, GPL) was added.
- **Bumping `QuestPDF`:** replace `licenses/QuestPDF.LICENSE.md` and
  `licenses/QuestPDF.ExternalDependencyLicenses/`, and re-confirm Community
  License eligibility.
- A CI SBOM step (`dotnet CycloneDX`) is the durable way to keep this honest.
