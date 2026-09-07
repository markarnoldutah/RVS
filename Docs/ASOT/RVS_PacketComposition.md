# RVS — Packet Composition

**Version:** 1.1 · September 7, 2026
**Scope:** How a service packet is assembled and rendered, end to end. Covers what is built (`#430` composition, `#431` HTML render) and the design of the stages that are not yet (`#432`–`#434`).

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
| 5 | Customer's description | **verbatim** — never trimmed or rewritten |
| 6 | Diagnostic Q&A | empty list when none; blank-question entries skipped |
| 7 | AI summary | null when no summary; when present, always carries the AI-generated label |
| 8 | Photos | empty list when no image attachment has a resolved URL |
| 9 | Paste block | from context; null until `#437` |
| 10 | Status link | from context; null until `#427` |

Short reference code: first hyphen-delimited segment of `ServiceRequest.Id`, upper-cased (`a1b2c3d4-…` → `A1B2C3D4`). Deterministic, stable across regenerations, no stored field. The Spec does not yet define a format — see `#472`.

### 4. Render — `#431` (HTML, built) and `#432` (PDF, planned)

Both renderers take one `ServicePacket` and read the same fields in the same order, so HTML and PDF cannot diverge in content or ordering. Renderers do presentation only.

- **HTML** — `PacketHtmlRenderer.Render(ServicePacket)` in `RVS.Domain/Packets/`. A pure `string`-in/`string`-out transform (no I/O, no entity access), it emits one self-contained HTML5 document with an inline print stylesheet — no external CSS, JS, or fonts. This is the primary artifact and also the delivery email body. `Spec B-3`.
  - **Letter and A4.** `@page` declares margins only and never pins a paper size, so the printer's own paper selection wins; the content column is sized to A4's narrower printable width so it fits both.
  - **Greyscale.** Every distinction is a border, a weight, or a textual label — no information is carried by colour. The AI summary carries a bordered `AI-generated` tag, not a colour badge.
  - **The diagnostic Q&A is the dominant block** — the heaviest frame on the page, bold questions, answers on ruled indents.
  - **Photos** are `<img>` referencing the context-resolved time-limited URLs (never base64); URLs are attribute-encoded. Per `Spec B-2` item 8, a CSS `break-before: page` moves the 7th photo onward to an appendix page.
  - **Degradation** is honoured as delivered by the composer: absent VIN drops just the VIN line; absent category renders `Uncategorized`; empty diagnostics render an explicit placeholder; absent AI summary, photos, paste block, and status link omit their sections entirely.
  - Non-`http(s)` status links render as inert text rather than an anchor.
- **PDF** is rendered by QuestPDF — pure-managed .NET, in-process, synchronous, no headless browser, no per-render network call. `Spec B-7` (decision `#426`).

### 5. Deliver — Feature 3 (`#435`–`#439`), planned

The PDF is stored; the HTML packet is emailed to the location's configured service address via Azure Communication Services, with the PDF and the original photos attached per the location's configuration. Subject `[RVS] {category} — {year} {make} {model} — {customer last name}`. Text-only clients degrade to the paste block. Delivery is idempotent per `(serviceRequestId, packetVersion)`, retried three times with exponential backoff, then alerted. `Spec B-4`.

---

## Build status

| Stage | Issue | State |
|---|---|---|
| Composition model + composer | `#430` | **Built** |
| HTML render + print stylesheet | `#431` | **Built** |
| PDF render (QuestPDF) | `#432` | Planned |
| Photo SAS embedding | `#433` | Planned |
| Generation orchestration | `#434` | Planned |
| Email delivery | `#435`–`#439` | Planned |
| Preferred-contact + reference-code gaps | `#472` | Deferred |

---

## Design notes

- **One model, two renderers.** The composition step is the single point where content and ordering are decided. Adding a section means changing one record and two render methods, not keeping two templates in sync.
- **Composer stays pure.** Everything that needs I/O — token minting, SAS generation, paste-block assembly, loading the location — happens in the orchestrator and arrives as data in `PacketCompositionContext`. This keeps composition fully unit-testable with no mocks (`Tests/RVS.Domain.Tests/Packets/`).
- **Degradation is the composer's job, not the renderer's.** A renderer receives a `ServicePacket` whose absent sections are already null or empty; it never inspects a `ServiceRequest`.
- **Exclusion by omission.** The packet cannot leak pricing or another customer's data because the composer never reads those fields — not because a filter removes them later.
