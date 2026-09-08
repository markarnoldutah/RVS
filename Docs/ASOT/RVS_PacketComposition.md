# RVS — Packet Composition

**Version:** 1.3 · September 7, 2026
**Scope:** How a service packet is assembled and rendered, end to end. Covers what is built (`#430` composition, `#431` HTML render, `#432` PDF render, `#433` photo SAS resolution) and the design of the stage that is not yet (`#434` orchestration).

Product canon is `../RVS_Overview.md`, `../RVS_Spec.md`, `../RVS_Plan.md`. Requirements referenced here as `Spec B-2` etc. live in `../RVS_Spec.md` section B. This document describes the intended mechanism; where a stage is not yet built it says so.

---

## Why this exists

The packet is the product. Everything else in RVS exists to put a one-page document in a service department's inbox. The Spec requires that document to render **both** as print-ready HTML and as a PDF, with identical content and ordering (`Spec B-3`). The way to guarantee that is to build the packet **once** as a single in-memory model and hand that one model to two renderers — never to maintain two templates that can drift apart.

---

## The stages

```
 intake submit / manager regenerate
            │
            ▼
   ┌─────────────────┐     enqueue, never blocks the 201
   │  1. Trigger     │────────────────────────────────────┐
   └─────────────────┘                                    │
                                                          ▼
   ┌──────────────────────────────────────────────────────────────┐
   │  2. Gather                                                    │
   │  load ServiceRequest + Location; resolve status-link URL,     │
   │  paste-block text, per-photo read SAS URLs                    │
   │  → PacketCompositionContext                                   │
   └──────────────────────────────────────────────────────────────┘
                                                          │
                                                          ▼
   ┌──────────────────────────────────────────────────────────────┐
   │  3. Compose        PacketComposer.Compose(request, context)   │
   │  pure transform → ServicePacket (one record tree, B-2 order)  │
   └──────────────────────────────────────────────────────────────┘
                                     │
                        ┌────────────┴────────────┐
                        ▼                         ▼
              ┌───────────────────┐     ┌───────────────────┐
              │  4a. HTML render  │     │  4b. PDF render   │
              │  print stylesheet │     │  QuestPDF         │
              └───────────────────┘     └───────────────────┘
                        │                         │
                        └────────────┬────────────┘
                                     ▼
   ┌──────────────────────────────────────────────────────────────┐
   │  5. Deliver   store PDF; email HTML packet + attachments to   │
   │  the location's service address; idempotent per              │
   │  (serviceRequestId, packetVersion)                           │
   └──────────────────────────────────────────────────────────────┘
```

### 1. Trigger — `#434` (planned)

Packet generation is enqueued when a customer completes intake, and can be re-run on demand from the manager app. Generation is asynchronous and isolated: it must not delay the intake `201`, and a packet failure never rolls back the service request. Three failed attempts raise an alert surfaced in the manager app. `Spec B-1`, target P95 under 10 s.

### 2. Gather — `#434` (planned), with `#427` / `#437` / `#433`

The orchestrator assembles everything the composer needs that is **not** on the `ServiceRequest`:

| Input | Source | Issue |
|---|---|---|
| Location display name, phone | `Location` entity | `#434` |
| Submission timestamp | `ServiceRequest.CreatedAtUtc` | `#434` |
| Status-link URL | minted anonymous token, `Spec X-1` / `X-5` | `#427` |
| DMS paste-block text | generated, ASCII-safe, `Spec B-5` | `#437` |
| Per-photo read URLs | time-limited SAS, generated per request, never persisted, `Spec X-6` | `#433` |

These are packed into a `PacketCompositionContext` (`RVS.Domain/Packets/PacketCompositionContext.cs`). Photo URLs are a dictionary keyed by attachment id; an image attachment with no entry is dropped rather than rendered broken.

**Photo read URLs — `#433` (built).** `PacketPhotoUrlResolver` (`RVS.API/Packets/`, `IPacketPhotoUrlResolver`) takes a `ServiceRequest` and returns the `PhotoUrls` dictionary: one Blob read SAS per image attachment (`ContentType` `image/*`), keyed by `AttachmentId`, from the `rvs-attachments` container. Non-image attachments and image attachments with a blank blob path are omitted. It holds no repository — nothing is written back to the request or to storage, so a SAS token is **never persisted** (`Spec X-6`); every call mints fresh URLs, so a regenerated packet gets fresh URLs.

- **TTL: 7 days.** The HTML packet is delivered by email (`Spec B-4`) and can sit unopened in a shop inbox across a weekend or a holiday; the staff-view read SAS default (1 hour, `AttachmentService`) would show a service advisor broken thumbnails. Seven days is also the ceiling Azure allows for a user-delegation-key-signed SAS. It stays genuinely time-limited and bounds exposure if the mail is forwarded. The PDF (`#432`) embeds photo bytes and is unaffected by this TTL.
- `IBlobStorageService.GenerateReadSasUrlAsync` gained a `TimeSpan lifetime` overload for this; the user delegation key is now requested for the same window as the SAS it signs (previously a fixed 15 min, shorter than the 1 h read SAS it was signing).

### 3. Compose — `#430` (built)

`PacketComposer.Compose(ServiceRequest request, PacketCompositionContext context)` in `RVS.Domain/Packets/` folds the two inputs into one `ServicePacket`.

It is a **pure transform**: guard clauses, then read-only mapping. No repository or service calls, no SAS generation, no rendering, nothing persisted. It reads only the fields it needs, so pricing, quotes, labor rates, parts, and `ServiceEvent` data cannot appear in a packet — they are simply never referenced.

`ServicePacket` is an immutable `record` tree with one nested record per `Spec B-2` section, declared in B-2 order:

| # | Section | Degradation |
|---|---|---|
| 1 | Unit header — year / make / model / VIN | each field independent; `HasVin` lets the renderer drop the VIN line and keep the rest |
| 2 | Customer — name, phone, email, preferred contact | fields null when absent; `PreferredContact` always null today (`#472`) |
| 3 | Origin — location, submission timestamp, short reference code | location fields null if context omits them; reference code always present |
| 4 | Issue category | null when unclassified — it is advisory |
| 5 | AI summary | null when no summary; when present, always carries the AI-generated label; rendered **above** the complaint so the concise problem recreation is read first |
| 6 | Customer's description | **verbatim** — never trimmed or rewritten |
| 7 | Diagnostic Q&A | empty list when none; blank-question entries skipped |
| 8 | Photos | empty list when no image attachment has a resolved URL |
| 9 | Paste block | from context; null until `#437` |
| 10 | Status link | from context; null until `#427` |

Short reference code: first hyphen-delimited segment of `ServiceRequest.Id`, upper-cased (`a1b2c3d4-…` → `A1B2C3D4`). Deterministic, stable across regenerations, no stored field. The Spec does not yet define a format — see `#472`.

### 4. Render — `#431` (HTML, built) and `#432` (PDF, built)

Both renderers take one `ServicePacket` and read the same fields in the same order, so HTML and PDF cannot diverge in content or ordering. Renderers do presentation only.

- **HTML** — `PacketHtmlRenderer.Render(ServicePacket)` in `RVS.Domain/Packets/`. A pure `string`-in/`string`-out transform (no I/O, no entity access), it emits one self-contained HTML5 document with an inline print stylesheet — no external CSS, JS, or fonts. This is the primary artifact and also the delivery email body. `Spec B-3`.
  - **Letter and A4.** `@page` declares margins only and never pins a paper size, so the printer's own paper selection wins; the content column is sized to A4's narrower printable width so it fits both.
  - **Greyscale.** Every distinction is a border, a weight, or a textual label — no information is carried by colour. The AI summary carries a bordered `AI-generated` tag, not a colour badge.
  - **The diagnostic Q&A is the dominant block** — the heaviest frame on the page, bold questions, answers on ruled indents.
  - **Photos** are `<img>` referencing the context-resolved time-limited URLs (never base64); URLs are attribute-encoded. Per `Spec B-2` item 8, a CSS `break-before: page` moves the 7th photo onward to an appendix page.
  - **Degradation** is honoured as delivered by the composer: absent VIN drops just the Serial# line; absent category renders `Uncategorized`; empty diagnostics render an explicit placeholder; absent AI summary, photos, paste block, and status link omit their sections entirely.
  - Non-`http(s)` status links render as inert text rather than an anchor.
  - **IDS work-order idiom.** Layout mirrors an Integrated Dealer Systems work order so a service manager reads it on daily muscle memory: the reference code sits top-right in the masthead as `RVS #` (mirroring IDS `W/O #`) with the received date under it; identity is a three-column **Customer / Location &amp; received / Unit** band; the AI **`Preliminary assessment`** is rendered above the **`COMPLAINT`** (the verbatim customer text) so the concise problem recreation is read first (`Spec B-2` item 5); field labels use IDS/RV-industry terms (`Serial# (VIN)` not "VIN", `Manufacturer` not "Make"); a running page footer carries `RVS #` + timestamp (`@page` margin box where the print engine supports it, plus a static end-of-flow `.packet-foot` for Safari). It deliberately omits everything IDS uses for the repair-authorization contract — pricing, parts/labour tables, subtotals, signatures, arbitration text — none of which belongs in an intake packet (`Spec B-2`).
- **PDF** — `PacketPdfRenderer.Render(ServicePacket, photoImages?)` in `RVS.API/Packets/`, rendered by QuestPDF: pure-managed .NET, in-process, synchronous, no headless browser, no per-render network call. `Spec B-7` (decision `#426`).
  - **Not in `RVS.Domain`.** Unlike the HTML renderer, this one carries the QuestPDF dependency, so it lives in `RVS.API` — `RVS.Domain` is referenced by the Blazor WASM apps and must not drag a native PDF engine into a browser bundle.
  - **One content model, two renderers, held apart by `PacketPdfLayout`** (`RVS.API/Packets/`). `PacketPdfLayout.Build(ServicePacket)` is a pure transform to an ordered, degradation-resolved section model whose section set, section order (including the `Preliminary assessment` above the `Complaint`) and headings match `PacketHtmlRenderer` line for line; `PacketPdfRenderer` only paints it. The layout model is unit-tested as plain data and its `ToPlainText()` projection is diffed against the HTML output so the two renderings cannot diverge in content or order.
  - **Identity band.** `PacketPdfRenderer` paints sections 1–3 as the same IDS masthead the HTML uses — a `RV ServiceFlow` letterhead with `RVS #` + received date top-right, the year/make/model headline, then a three-column `Customer / Location & received / Unit` band closed by a rule — rather than three stacked blocks. Section 4 onward (category, assessment, complaint, …) render linearly below it.
  - **Page box.** A PDF has one fixed media box and cannot defer the paper choice to the printer the way the HTML `@page` rule does, so the content is sized to 210 mm × 279 mm — the intersection of A4 and US Letter — with 14 mm margins, so it prints inside the margins of either sheet.
  - **Greyscale** — the diagnostic block gets the heaviest frame; the AI summary carries a bordered `AI-GENERATED` tag; no information is carried by colour.
  - **Photos** are passed in as bytes keyed by URL (`photoImages`); a photo with no bytes renders as a labelled placeholder cell. `#433` mints the per-photo read SAS URLs; fetching those URLs to bytes for the PDF is the orchestrator's job (`#434`). Up to six on page one, the rest after a `PageBreak` (`Spec B-2` item 8).
  - **Determinism** — document metadata dates are pinned to the packet's submission time so the same packet renders byte-for-byte identically.
  - **Fonts** — QuestPDF's bundled Lato only; no font assets are vendored. Verbatim and paste blocks render in a bordered box rather than a monospace face (cosmetic; not a `Spec` requirement).

### 5. Deliver — Feature 3 (`#435`–`#439`), planned

The PDF is stored; the HTML packet is emailed to the location's configured service address via Azure Communication Services, with the PDF and the original photos attached per the location's configuration. Subject `[RVS] {category} — {year} {make} {model} — {customer last name}`. Text-only clients degrade to the paste block. Delivery is idempotent per `(serviceRequestId, packetVersion)`, retried three times with exponential backoff, then alerted. `Spec B-4`.

---

## Build status

| Stage | Issue | State |
|---|---|---|
| Composition model + composer | `#430` | **Built** |
| HTML render + print stylesheet | `#431` | **Built** |
| PDF render (QuestPDF) | `#432` | **Built** |
| Photo SAS resolution (`PacketPhotoUrlResolver`) | `#433` | **Built** |
| Generation orchestration | `#434` | Planned |
| Email delivery | `#435`–`#439` | Planned |
| Preferred-contact + reference-code gaps | `#472` | Deferred |

---

## Design notes

- **One model, two renderers.** The composition step is the single point where content and ordering are decided. Adding a section means changing `ServicePacket`, the HTML renderer, and `PacketPdfLayout` — never keeping two templates in sync. `PacketPdfLayout`'s `ToPlainText()` is diffed against the HTML output in tests to catch any drift.
- **Composer stays pure.** Everything that needs I/O — token minting, SAS generation, paste-block assembly, loading the location — happens in the orchestrator and arrives as data in `PacketCompositionContext`. This keeps composition fully unit-testable with no mocks (`Tests/RVS.Domain.Tests/Packets/`).
- **Degradation is the composer's job, not the renderer's.** A renderer receives a `ServicePacket` whose absent sections are already null or empty; it never inspects a `ServiceRequest`.
- **Exclusion by omission.** The packet cannot leak pricing or another customer's data because the composer never reads those fields — not because a filter removes them later.
