# RVS — Packet Composition

**Version:** 1.7 · September 9, 2026
**Scope:** How a service packet is assembled and rendered, end to end. Covers what is built (`#430` composition, `#431` HTML render, `#432` PDF render, `#433` photo SAS resolution, `#434` generation orchestration, `#435` per-location packet config, `#436` DMS paste block, `#437` packet email send, `#438` delivery idempotency + retry/backoff, `#439` hard-bounce recipient disabling — domain + service, `#492` masthead layout pass, `#508` HEIC/HEIF → JPEG transcode on upload, `#516` waiting for in-flight intake uploads before generating) and the one part of `#439` that is not yet wired (the inbound bounce signal).

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

### 1. Trigger — `#434` (built)

Packet generation is enqueued when a customer completes intake (`IntakeOrchestrationService` step 8) and can be re-run on demand from the manager app (`POST …/service-requests/{srId}/packet/regenerate` → `IServiceRequestService.RegeneratePacketAsync`). Generation is asynchronous and isolated: it does not delay the intake `201`, and a packet failure never rolls back the service request. Three failed attempts raise a `LogCritical` alert and leave `packetGeneration.status = "Failed"` for the manager app to surface. `Spec B-1`, target P95 under 10 s.

**Waiting for in-flight intake uploads — `#516` (built).** The intake client uploads and confirms its attachments *after* the `201` that creates the request, so a packet rendered the instant the job is dequeued carried none of the customer's photos — the bug `#516` reports. The submission therefore declares `expectedAttachmentCount`, recorded on `packetGeneration` by `IntakeOrchestrationService` (clamped at zero). Before starting an attempt, `PacketGenerationService.GenerateAsync` compares it with `serviceRequest.attachments.Count`: while fewer have arrived **and** `PacketGenerationService.AttachmentUploadWindow` (2 minutes from `createdAtUtc`) has not elapsed, it returns `PacketGenerationOutcome.WaitingForAttachments` and the worker re-queues the job — the 5 s retry delay doubles as the poll interval. The check sits *before* `MarkGenerating()`, so polling never consumes one of the three attempts.

Once the window closes the packet renders with whatever arrived: a browser upload that failed costs the packet a photo, never the packet. A request with nothing to upload declares `0` and generates immediately with no delay. Anchoring the deadline on `createdAtUtc` also means an on-demand regeneration — always long after creation — never waits, and `ResetForRegeneration` deliberately preserves the count rather than re-deriving it. `Spec B-1`.

**Queue — in-process, swap-ready.** `IPacketGenerationQueue` (`RVS.Domain/Interfaces/`) is the dispatch seam. The default `ChannelPacketGenerationQueue` (`RVS.API/Packets/`) is a bounded in-memory `System.Threading.Channels` queue — a singleton, drained by the `PacketGenerationWorker` `BackgroundService` (`RVS.API/Workers/`). `TryEnqueue` never blocks the caller; a saturated queue drops the write and the request stays `Pending`, recoverable by the regenerate endpoint. Jobs are not durable across a process restart — that is bounded by design: every request persists its `packetGeneration` state, so a lost job is visible in the manager app and re-runnable. Swapping in a durable transport (e.g. Azure Storage Queue) is a new implementation of `IPacketGenerationQueue` plus a DI change; no caller touches the channel. The worker runs one `PacketGenerationService.GenerateAsync` attempt per job in its own DI scope, and re-queues after a 5 s delay while attempts remain.

### 2. Gather — `#434` (built), with `#436` / `#433` (built) and `#427` (pending)

The orchestrator assembles everything the composer needs that is **not** on the `ServiceRequest`:

| Input | Source | Issue |
|---|---|---|
| Location display name, phone | `Location` entity (`ILocationRepository.GetByIdAsync`) | `#434` (built) |
| Submission timestamp | `ServiceRequest.CreatedAtUtc` | `#434` (built) |
| Per-photo read URLs | time-limited SAS, generated per request, never persisted, `Spec X-6` | `#433` (built) |
| Status-link URL | minted anonymous token, `Spec X-1` / `X-5` | `#427` — context carries `null` until then |
| DMS paste-block text | `PasteBlockGenerator.Generate` (`RVS.Domain/Packets/`) — ASCII-folded, fenced top and bottom, description truncated at a word boundary to the location's `pasteBlockCharacterCap` (default 1,000), `Spec B-5` | `#436` (built) — status line filled in once `#427` mints the link |

These are packed into a `PacketCompositionContext` (`RVS.Domain/Packets/PacketCompositionContext.cs`) by `PacketGenerationService` (`RVS.API/Services/`). Photo URLs are a dictionary keyed by attachment id; an image attachment with no entry is dropped rather than rendered broken.

The service also downloads each resolved photo's bytes (`IBlobStorageService.DownloadAsync`, added for `#434`) and passes them to `PacketPdfRenderer.Render` keyed by URL — the PDF embeds bytes, not links (`Spec B-3`). A photo whose bytes cannot be fetched is logged and rendered as a placeholder rather than failing the packet. The generated PDF is uploaded to `rvs-attachments` at `packets/{tenantId}/{serviceRequestId}/v{n}.pdf` and its path and version recorded on `packetGeneration`. The HTML is re-rendered on demand by the manager detail view (`#443`), not stored, so its photo SAS URLs are always fresh.

**Photo read URLs — `#433` (built).** `PacketPhotoUrlResolver` (`RVS.API/Packets/`, `IPacketPhotoUrlResolver`) takes a `ServiceRequest` and returns the `PhotoUrls` dictionary: one Blob read SAS per image attachment (`ContentType` `image/*`), keyed by `AttachmentId`, from the `rvs-attachments` container. Non-image attachments and image attachments with a blank blob path are omitted. It holds no repository — nothing is written back to the request or to storage, so a SAS token is **never persisted** (`Spec X-6`); every call mints fresh URLs, so a regenerated packet gets fresh URLs.

- **TTL: 7 days.** The HTML packet is delivered by email (`Spec B-4`) and can sit unopened in a shop inbox across a weekend or a holiday; the staff-view read SAS default (1 hour, `AttachmentService`) would show a service advisor broken thumbnails. Seven days is also the ceiling Azure allows for a user-delegation-key-signed SAS. It stays genuinely time-limited and bounds exposure if the mail is forwarded. The PDF (`#432`) embeds photo bytes and is unaffected by this TTL.
- `IBlobStorageService.GenerateReadSasUrlAsync` gained a `TimeSpan lifetime` overload for this; the user delegation key is now requested for the same window as the SAS it signs (previously a fixed 15 min, shorter than the 1 h read SAS it was signing).

**Photo transcoding — `#508` (built).** Intake accepts `image/heic` / `image/heif`, which only Apple clients render. `AttachmentService.ConfirmAttachmentAsync` now transcodes such an upload to baseline JPEG *before* it is recorded on the request: it downloads the just-uploaded blob, hands the bytes to `IImageTranscoder` (`RVS.Domain/Integrations/`), stores the JPEG under a sibling `.jpg` blob name, best-effort deletes the HEIC original, and records the attachment with `ContentType` `image/jpeg`, a `.jpg` file name, and the new size. The real implementation is `MagickImageTranscoder` (`RVS.API/Integrations/`) — Magick.NET (ImageMagick, Apache-2.0) with its bundled HEIF delegate (libheif + libde265, LGPL-3.0); the `Magick.NET-Q8-AnyCPU` package ships native builds for the Linux App Service and dev machines alike. Licensing — including the LGPL compliance statement and the HEVC-patent note — is in `/THIRD-PARTY-NOTICES.md`, which ships in every deploy artifact. It bakes EXIF orientation in, downscales past `ImageTranscodeOptions.MaxEdgePixels` (default 4096), strips metadata, and re-encodes at quality 82; a payload over `MaxSourceBytes` (default 25 MB), an undecodable payload, or a download error keeps the original and logs — the packet then shows the placeholder, exactly as before. Under `Integrations:UseMocks` the seam is `NoOpImageTranscoder` (transcodes nothing).

### 3. Compose — `#430` (built)

`PacketComposer.Compose(ServiceRequest request, PacketCompositionContext context)` in `RVS.Domain/Packets/` folds the two inputs into one `ServicePacket`.

It is a **pure transform**: guard clauses, then read-only mapping. No repository or service calls, no SAS generation, no rendering, nothing persisted. It reads only the fields it needs, so pricing, quotes, labor rates, parts, and `ServiceEvent` data cannot appear in a packet — they are simply never referenced.

`ServicePacket` is an immutable `record` tree with one nested record per `Spec B-2` section, declared in B-2 order:

| # | Section | Degradation |
|---|---|---|
| 1 | Unit header — year / make / model / VIN | each field independent; `HasVin` lets the renderer drop the VIN line and keep the rest |
| 2 | Customer — name, phone, email, preferred contact | fields null when absent; `PreferredContact` is one of `Phone` / `Text` / `Email` (`PreferredContactMethod`), captured at intake (`#472`), null only for pre-existing requests |
| 3 | Origin — location, submission timestamp, short reference code | location fields null if context omits them; reference code always present |
| 4 | Issue category | null when unclassified — it is advisory |
| 5 | AI summary | null when no summary; when present, always carries the AI-generated label; rendered **above** the complaint so the concise problem recreation is read first |
| 6 | Customer's description | **verbatim** — never trimmed or rewritten |
| 7 | Diagnostic Q&A | empty list when none; blank-question entries skipped |
| 8 | Photos | empty list when no image attachment has a resolved URL |
| 9 | Paste block | from context; `PasteBlockGenerator` (`#436`); the status line inside it stays absent until `#427` mints the link |
| 10 | Status link | from context; null until `#427` |

Short reference code: first hyphen-delimited segment of `ServiceRequest.Id`, upper-cased (`a1b2c3d4-…` → `A1B2C3D4`). Deterministic, stable across regenerations, no stored field or counter. Falls back to the whole id when it contains no `-`. Ratified in `Spec B-2` item 3 (`#472`); a human-friendlier sequential scheme would need a stored field + per-tenant counter + migration and remains a separate decision if ever wanted.

### 4. Render — `#431` (HTML, built) and `#432` (PDF, built)

Both renderers take one `ServicePacket` and read the same fields in the same order, so HTML and PDF cannot diverge in content or ordering. Renderers do presentation only.

- **HTML** — `PacketHtmlRenderer.Render(ServicePacket)` in `RVS.Domain/Packets/`. A pure `string`-in/`string`-out transform (no I/O, no entity access), it emits one self-contained HTML5 document with an inline print stylesheet — no external CSS, JS, or fonts. This is the primary artifact and also the delivery email body. `Spec B-3`.
  - **Letter and A4.** `@page` declares margins only and never pins a paper size, so the printer's own paper selection wins; the content column is sized to A4's narrower printable width so it fits both.
  - **Greyscale.** Every distinction is a border, a weight, or a textual label — no information is carried by colour. The AI summary carries a bordered `AI-generated` tag, not a colour badge.
  - **The diagnostic Q&A is the dominant block** — the heaviest frame on the page, bold questions, answers on ruled indents.
  - **Photos** are `<img>` referencing the context-resolved time-limited URLs (never base64); URLs are attribute-encoded. Per `Spec B-2` item 8, a CSS `break-before: page` moves the 7th photo onward to an appendix page.
  - **Degradation** is honoured as delivered by the composer: absent VIN drops just the Serial# line; absent category renders `Uncategorized`; empty diagnostics render an explicit placeholder; absent AI summary, photos, paste block, and status link omit their sections entirely.
  - Non-`http(s)` status links render as inert text rather than an anchor.
  - **IDS work-order idiom.** Layout mirrors an Integrated Dealer Systems work order so a service manager reads it on daily muscle memory: an optional logo + the brand name (`ServicePacket.Branding`, default **RV Intake**) form the letterhead top-left; the reference code sits top-right in the masthead as `RVS #` (mirroring IDS `W/O #`) with the full received timestamp (`yyyy-MM-dd HH:mm UTC`) under it; the customer name, family-name-first (`Last, First`), sits above the year/make/model headline; identity is a three-column **Customer / Location / Unit** band (the middle column no longer repeats the received time); the AI **`Preliminary assessment`** is rendered above the **`COMPLAINT`** (the verbatim customer text) so the concise problem recreation is read first (`Spec B-2` item 5); field labels use IDS/RV-industry terms (`Serial# (VIN)` not "VIN", `Manufacturer` not "Make"); a running page footer carries `RVS #` + timestamp + brand name (`@page` margin box where the print engine supports it, plus a static end-of-flow `.packet-foot` for Safari). It deliberately omits everything IDS uses for the repair-authorization contract — pricing, parts/labour tables, subtotals, signatures, arbitration text — none of which belongs in an intake packet (`Spec B-2`).
- **PDF** — `PacketPdfRenderer.Render(ServicePacket, photoImages?)` in `RVS.API/Packets/`, rendered by QuestPDF: pure-managed .NET, in-process, synchronous, no headless browser, no per-render network call. `Spec B-7` (decision `#426`).
  - **Not in `RVS.Domain`.** Unlike the HTML renderer, this one carries the QuestPDF dependency, so it lives in `RVS.API` — `RVS.Domain` is referenced by the Blazor WASM apps and must not drag a native PDF engine into a browser bundle.
  - **One content model, two renderers, held apart by `PacketPdfLayout`** (`RVS.API/Packets/`). `PacketPdfLayout.Build(ServicePacket)` is a pure transform to an ordered, degradation-resolved section model whose section set, section order (including the `Preliminary assessment` above the `Complaint`) and headings match `PacketHtmlRenderer` line for line; `PacketPdfRenderer` only paints it. The layout model is unit-tested as plain data and its `ToPlainText()` projection is diffed against the HTML output so the two renderings cannot diverge in content or order.
  - **Identity band.** `PacketPdfRenderer` paints sections 1–3 as the same IDS masthead the HTML uses — an optional logo + brand letterhead (`ServicePacket.Branding`) with `RVS #` + the full received timestamp top-right, the `Last, First` customer name and the year/make/model headline, then a three-column `Customer / Location / Unit` band closed by a rule — rather than three stacked blocks. The received timestamp and the customer headline are carried on `PacketPdfLayout` (`ReceivedDisplay`, `CustomerHeadline`) so `ToPlainText()` stays in parity with the HTML. Section 4 onward (category, assessment, complaint, …) render linearly below it.
  - **Page box.** A PDF has one fixed media box and cannot defer the paper choice to the printer the way the HTML `@page` rule does, so the content is sized to 210 mm × 279 mm — the intersection of A4 and US Letter — with 14 mm margins, so it prints inside the margins of either sheet.
  - **Greyscale** — the diagnostic block gets the heaviest frame; the AI summary carries a bordered `AI-GENERATED` tag; no information is carried by colour.
  - **Photos** are passed in as bytes keyed by URL (`photoImages`); a photo with no bytes — or bytes whose magic number is not a QuestPDF-decodable raster (JPEG/PNG/GIF/BMP/WebP) — renders as a labelled placeholder cell rather than a blank cell or a failed render. `#433` mints the per-photo read SAS URLs; fetching those URLs to bytes for the PDF is the orchestrator's job (`#434`). Up to six on page one, the rest after a `PageBreak` (`Spec B-2` item 8). An iPhone **HEIC/HEIF** upload used to hit that placeholder path (`#492` item 8); as of `#508` it is transcoded to JPEG on upload-confirm (see below), so the stored blob and every downstream consumer — the PDF embed, the HTML `<img>`, and the emailed photo attachment — get a universally-renderable raster. The magic-number guard stays as the backstop for a transcode that could not run.
  - **Determinism** — document metadata dates are pinned to the packet's submission time so the same packet renders byte-for-byte identically.
  - **Fonts** — QuestPDF's bundled Lato only; no font assets are vendored. Verbatim and paste blocks render in a bordered box rather than a monospace face (cosmetic; not a `Spec` requirement).

### 5. Deliver — Feature 3 (`#437`, `#438` built; `#439` domain + service built, inbound signal not wired)

`#434` stores the PDF and stamps `packetGeneration.packetVersion`; `#435` adds the per-location `packetConfig` (recipients, attach-PDF, include-photos, paste-block cap, status-link TTL, logo) that delivery reads; `#436` produces the paste block the text-only fallback uses.

**`#437` — the send itself, built.** After a successful generation `PacketGenerationService` composes the email with `PacketEmailComposer` (`RVS.Domain/Packets/`, pure) and hands a transport-agnostic `PacketEmailMessage` (`RVS.Domain/Integrations/`) to `INotificationService.SendPacketEmailAsync`, implemented on the existing ACS integration (`AcsEmailNotificationService`) and the `NoOp` fallback — no new transport. Subject `[RVS] {category} — {year} {make} {model} — {customer last name}`, each segment degrading independently (`Uncategorized` / `Unknown vehicle` / `Unknown`). Body is the packet HTML inline with the paste block as the plain-text alternative for text-only clients. The rendered PDF and the original photo bytes (already downloaded for the PDF render) attach per `packetConfig.attachPdf` / `includePhotos`; the mail goes to `packetConfig.recipients`. Delivery no-ops when the location has no config, `enabled` is `false`, or the recipient list is empty.

**`#438` — idempotency + retry, built.** The send runs through a delivery state machine on `ServiceRequest.packetEmailDelivery` (`PacketEmailDeliveryEmbedded`): `status` / `attemptCount` / `lastAttemptAtUtc` / `deliveredPacketVersion` / `deliveredAtUtc` / `lastError` (never customer issue text — `Spec X-7`) / `alertRaised`. Before sending, `PacketGenerationService` checks `IsDeliveredFor(packetGeneration.packetVersion)` — if this exact packet version is already recorded delivered, the send is skipped, so a repeat generation run (duplicate queue job, restart) never double-sends. Otherwise it attempts `SendPacketEmailAsync` up to `MaxAttempts` (3) times, waiting `PacketEmailOptions.RetryBaseDelay` (default 2 s) doubled each retry between tries — three attempts land inside the `Spec B-4` 60 s P99 target. Every attempt logs under a `BeginScope` carrying a `CorrelationId` (the ambient trace id, or the service request id when generation runs off the HTTP thread), so retries are traceable. Exhausting all three logs one `LogCritical` (`alertRaised` guards repeats) and leaves `status = "Failed"`. Delivery state is persisted after the run. A delivery failure never fails generation — the PDF is already generated and stored. `Spec B-4`.

**`#439` — hard-bounce recipient disabling, domain + service built.** A hard bounce disables **that one address**, never the whole configuration. `PacketConfigEmbedded` gains `disabledRecipients[]` (`DisabledRecipientEmbedded`: `email` / `reason` — non-PII, `Spec X-7` / `disabledAtUtc`) alongside the active `recipients[]`. `PacketConfigEmbedded.DisableRecipient(email, reason, whenUtc)` moves an active address to `disabledRecipients` (case- and whitespace-insensitive, idempotent); `ReEnableRecipient(email)` moves it back. `LocationService.DisableRecipientForBounceAsync` loads the location, disables the one recipient, persists, then **notifies every remaining active recipient** (one `INotificationService.SendEmailAsync` each, non-PII body; a send that throws is logged and skipped) and `LogWarning`s under `EventId` 439001; when the bounce leaves the location with **no** active recipients it `LogCritical`s under `EventId` 439002 for App Insights and sends nothing. `LocationService.ReEnableRecipientAsync` is the counterpart. Disabled recipients surface read-only on `PacketConfigDto.disabledRecipients` (mapped out, never mapped in); `LocationService.UpdateAsync` carries the disabled list across a settings save, and treats an address the caller puts back into `recipients` as an explicit re-enable — so AC "visible and re-enablable in location settings" needs no new endpoint. **Not wired:** the inbound bounce signal. Nothing calls `DisableRecipientForBounceAsync` yet — an ACS Email delivery-report path (Event Grid `Microsoft.Communication.EmailDeliveryReportReceived`, hard-bounce filtering, a message-id → location map recorded at send time) is a follow-up. `Spec B-4`.

---

## Build status

| Stage | Issue | State |
|---|---|---|
| Composition model + composer | `#430` | **Built** |
| HTML render + print stylesheet | `#431` | **Built** |
| PDF render (QuestPDF) | `#432` | **Built** |
| Photo SAS resolution (`PacketPhotoUrlResolver`) | `#433` | **Built** |
| Generation orchestration (queue + worker + `PacketGenerationService`) | `#434` | **Built** |
| Per-location packet config (`Location.packetConfig`) | `#435` | **Built** — recipients (0–10), attach-PDF, include-photos, paste-block cap, status-link TTL, logo; read/written via `api/locations`. Paste-block cap now consumed by `#436`; the rest awaits delivery |
| DMS paste block (`PasteBlockGenerator`) | `#436` | **Built** — fenced ASCII-safe block, order category → verbatim description → status link, description truncated at a word boundary to `pasteBlockCharacterCap`; assembled in `PacketGenerationService` into `PacketCompositionContext.PasteBlock` |
| Packet email send (subject, inline HTML + paste-block text, PDF + photo attachments, recipient list) | `#437` | **Built** — `PacketEmailComposer` + `INotificationService.SendPacketEmailAsync` on the ACS integration; invoked from `PacketGenerationService` after a successful generation |
| Delivery idempotency + retry/backoff | `#438` | **Built** — `PacketEmailDeliveryEmbedded` state on the request; skip when `deliveredPacketVersion` matches the current version, else 3 attempts with exponential backoff then a `LogCritical` alert; every attempt logged under a correlation-id scope |
| Hard-bounce recipient disabling | `#439` | **Partial** — domain (`PacketConfigEmbedded.disabledRecipients[]` + `DisableRecipient` / `ReEnableRecipient`) and service (`LocationService.DisableRecipientForBounceAsync` / `ReEnableRecipientAsync`: disable one address, notify the remaining recipients, `LogWarning`/`LogCritical`; disabled list carried across a settings save and re-enablable by re-adding the address) built. **Not wired:** the inbound ACS delivery-report / bounce signal — nothing invokes the disable path yet |
| Preferred-contact + reference-code gaps | `#472` | **Built** — preferred contact captured at intake (`Phone` / `Text` / `Email`); reference code ratified as the id-derived convention |
| HEIC/HEIF → JPEG transcode | `#508` | **Built** — `AttachmentService.ConfirmAttachmentAsync` transcodes an `image/heic` / `image/heif` upload to JPEG via `IImageTranscoder` (`MagickImageTranscoder`, Magick.NET + libheif) before recording it: JPEG stored under a sibling `.jpg` blob, original deleted, attachment `ContentType` corrected. EXIF orientation baked in, downscaled past `MaxEdgePixels`, size-capped; transcode failure keeps the original and the packet's placeholder path. Closes `#492` item 8 |
| Wait for in-flight intake uploads before generating | `#516` | **Built** — intake declares `expectedAttachmentCount` on the submission; `GenerateAsync` returns `WaitingForAttachments` (re-queued by the worker, no attempt consumed) while fewer have arrived and the 2-minute `AttachmentUploadWindow` from `createdAtUtc` is still open, then renders whatever arrived. Fixes packets shipping with no photos |
| Masthead layout pass | `#492` | **Partial** — items 1, 3, 4, 5, 6 built: brand name from `ServicePacket.Branding` (default `RV Intake`, no more `RV ServiceFlow`); `Last, First` customer name above the headline; full received timestamp in the top refbox; middle identity column renamed `Location` with the Received row dropped; `BuildTechnicianSummary` no longer duplicates the diagnostic Q&A. Item 2 wired as a seam only — `PacketBranding.LogoDataUri` / `PacketCompositionContext` carry an optional logo + brand override, null by default; per-location config + manager UI are `#470`. Item 8 done — mitigated here (HEIC → placeholder, not a failed render) and closed by `#508` (HEIC/HEIF → JPEG on upload-confirm). Item 7 (structured preliminary assessment) is a design-note + follow-up, not built |

---

## Design notes

- **One model, two renderers.** The composition step is the single point where content and ordering are decided. Adding a section means changing `ServicePacket`, the HTML renderer, and `PacketPdfLayout` — never keeping two templates in sync. `PacketPdfLayout`'s `ToPlainText()` is diffed against the HTML output in tests to catch any drift.
- **Composer stays pure.** Everything that needs I/O — token minting, SAS generation, paste-block assembly, loading the location — happens in the orchestrator and arrives as data in `PacketCompositionContext`. This keeps composition fully unit-testable with no mocks (`Tests/RVS.Domain.Tests/Packets/`).
- **Degradation is the composer's job, not the renderer's.** A renderer receives a `ServicePacket` whose absent sections are already null or empty; it never inspects a `ServiceRequest`.
- **Exclusion by omission.** The packet cannot leak pricing or another customer's data because the composer never reads those fields — not because a filter removes them later.
- **Generation state lives on the request, not the queue.** `ServiceRequest.packetGeneration` (`PacketGenerationEmbedded`) records `status` / `attemptCount` / `lastError` (never customer issue text — `Spec X-7`) / `packetVersion` / `pdfBlobPath` / `alertRaised`. This is what makes an in-process queue acceptable: a job lost to a restart is still visible and re-runnable, and it is the surface the manager app reads for the failure state (`#443`).
- **Attempt before work.** `GenerateAsync` marks `Generating` and persists *before* composing/rendering, so a mid-render crash still records that the attempt happened. On the third failed attempt it logs `LogCritical` once (`alertRaised` guards against repeats) and returns `Exhausted`; earlier failures return `Retry` and the worker re-queues.
- **Delivery idempotency is versioned, not a dedupe key.** `packetEmailDelivery.deliveredPacketVersion` records the last version emailed; a send is skipped only when it equals the current `packetGeneration.packetVersion`. A regeneration bumps the version and legitimately re-sends (the new packet is a different artifact); a duplicate job for the *same* version is the case the check suppresses. The manager "resend" (`Spec C-5`) is a deliberate re-send and is out of this path. Delivery retry with backoff runs inline in the generation attempt — it does not re-enter the queue — because generation has already succeeded and only the email is outstanding.
