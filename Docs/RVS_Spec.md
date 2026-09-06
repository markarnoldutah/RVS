# RVS — Spec

**Version:** 1.0 · September 4, 2026
**Scope:** Everything RVS does. If it isn't here, it isn't in scope.

Requirements are numbered `A-n` (intake), `B-n` (packet and delivery), `C-n` (manager), `X-n` (cross-cutting). Fresh numbering — the old FR-nnn and OQ-nn series had collisions across documents and are archived with them.

**Stack:** Azure, .NET, Blazor, Cosmos DB (SQL API), Auth0 (Free tier), Azure Communication Services, Blob Storage.

---

## A. Intake app — *substantially built*

Anonymous Blazor web form at `rvintake.com/{locationSlug}`. No login, ever.

| # | Requirement |
|---|---|
| **A-1** | Anonymous access. No authentication on the intake endpoint. Per-IP rate limiting. |
| **A-2** | Collects: customer name, phone, email, preferred contact method; VIN or make/model/year; free-text description of the problem; photos and short video. |
| **A-3** | VIN decode via NHTSA vPIC for make, model year, type. Failure degrades gracefully — submission still succeeds with customer-supplied values. |
| **A-4** | AI generates 2–4 follow-up questions based on the description. Hardcoded per-category fallbacks when the AI call fails. Answers are stored with the request. |
| **A-5** | AI suggests one issue category from a controlled list. Customer can override. Advisory only. |
| **A-6** | Attachments: jpeg, png, mp4, m4a, wav. Max 10, 25 MB each. Direct browser-to-Blob SAS upload — binaries never transit the API. |
| **A-7** | Returning customers (matched by email) get name, phone, and known VINs prefilled. |
| **A-8** | On submit: create the service request, append the ledger entry (X-2), generate the customer status token, enqueue the packet (B-1). Return `201` without waiting on the packet. |
| **A-9** | **Voice input.** Free-text fields accept dictation: browser microphone capture is transcribed by Azure OpenAI Whisper (`ai/transcribe-issue`) and the transcript is cleaned before it fills the field (VIN-context cleanup strips spoken punctuation and spacing). Optional and advisory — typing is always available; a transcription failure leaves the field unchanged and raises no blocking error. |
| **A-10** | **VIN from photo.** The customer can photograph the VIN plate instead of typing it. Azure OpenAI gpt-4o vision (`ai/extract-vin`) returns a VIN and a confidence score: at ≥ 0.7 the VIN field is auto-filled, at ≥ 0.9 the A-3 decode also fires automatically, below 0.7 the result is discarded. The extracted value is always editable. Failure degrades to manual entry — submission still succeeds. |
| **A-11** | **Issue insights.** From the description, AI infers urgency and RV-usage context (`ai/suggest-insights`), shown to the customer as advisory "Suggested" chips. Advisory only, never blocks submission; accepted values are stored on the request with their provider and confidence. |
| **A-12** | **Capability pre-check.** On leaving the description step, the issue is checked against the location's enabled service capabilities (`assess-capabilities`). If the location is unlikely to be able to help, intake shows a non-blocking alert; the customer may still submit. |

All four AI capabilities in A-9–A-12 are in scope — decision Q8 / issue #429. Each has a rule-based or no-op fallback behind the same interface; none is a hard dependency for a successful submission.

**Controlled vocabulary at launch: `issue-category` only.** Roughly 10–14 codes (Slide System, Electrical, Plumbing, HVAC, Generator, Appliance, Roof/Seals, Chassis, Other). The four technician-side vocabularies — component type, failure mode, repair action, part number — are archived. They were never populated at intake anyway; they filled in after a technician closed a job, which is a workflow RVS no longer has.

This means the packet's structured content is: decoded unit, one category, the customer's own words, the diagnostic Q&A, and photos. That is the honest scope, and it is enough. The diagnostic Q&A block is the part that reads as expert on paper — *"Does the slide move at all? — Motor hums, no movement"* is worth more to a service manager than any taxonomy label. Invest the effort there.

---

## B. Packet and email delivery — *current work*

The packet is the product. Everything else exists to produce it.

### B-1 — Generation

Enqueued on intake submission; must not block the `201`. Regenerated on demand. Three failed attempts raises an alert and is surfaced in the manager app. Failure never rolls back the service request.

### B-2 — Contents

One page, in this order:

1. Unit header — year, make, model, VIN (degrades if VIN absent)
2. Customer — name, phone, email, preferred contact
3. Location, submission timestamp, short reference code
4. Issue category
5. **The customer's description, verbatim**
6. **Diagnostic Q&A**
7. AI summary, labeled as AI-generated
8. Photo thumbnails, up to 6 on page one, rest on an appendix page
9. Paste block (B-5)
10. Status link + QR

Never includes: pricing, quotes, labor rates, or any other customer's data.

### B-3 — Rendering

HTML with an embedded print stylesheet is the primary rendering; it must print cleanly at Letter and A4 in greyscale. PDF is derived from the same composition model, not a second template. Images embed as time-limited SAS URLs, not base64.

### B-4 — Email delivery

| | |
|---|---|
| Transport | Azure Communication Services |
| Recipients | 1–10 addresses configured per location |
| Subject | `[RVS] {category} — {year} {make} {model} — {customer last name}` |
| Body | The packet as inline HTML, degrading to the paste block for text-only clients |
| Attachments | The PDF, plus the original photos as image attachments |
| Target | Delivered within 60 seconds of submission, P99 |
| Retry | 3 attempts, exponential backoff, then alert |
| Bounce | A hard bounce disables that recipient and notifies the owner — never the whole configuration |

Idempotent per `(serviceRequestId, packetVersion)`.

### B-5 — Paste block

A delimited plain-text block formatted for a DMS complaint field. ASCII-safe — no smart quotes, no em-dashes, no non-breaking spaces, because DMS text fields mangle Unicode. Capped at a configurable character count (default 1,000) with truncation at a word boundary. Order: category, then the customer's verbatim description, then the status link.

This is the DMS integration. It is manual, it is honest about being manual, and it eliminates the retyping that the advisor actually cares about.

### B-6 — Per-location configuration

Enable/disable, recipient list, attach-PDF, include-photos, paste-block cap, status-link TTL, optional logo. Defaults chosen so a location works with one setting changed: the recipient address.

### B-7 — PDF rendering decision

**Decided (issue #426): QuestPDF.** A pure-managed .NET library with a bundled native renderer — no headless browser, no per-render network call, in-process and synchronous, nothing to health-check or recycle. The packet is built once as a code composition model (B-3); QuestPDF renders it to PDF and the same model renders to HTML. Sub-100 ms per page, well inside the B-1 / X-7 budgets.

Constraints that still hold: no per-render outbound dependency beyond Blob Storage; no headless browser process; the API container stays Debian-based (QuestPDF's native lib needs glibc ≥ 2.28 and does not support Alpine).

**Licence finding.** QuestPDF ships under the QuestPDF Community License v3.0 (source-available, *not* MIT as of the 2026.x releases). Free for an organisation with annual gross revenue under USD 1,000,000 in its last fiscal year, commercial use included — RVS qualifies today. Publicly traded companies and public-sector entities are excluded regardless of revenue; neither applies. On crossing the threshold (or an acquisition, or an IPO) there is a 90-day window to buy a paid licence: Professional USD 1,999 or Enterprise USD 4,999 — perpetual, unlimited developers, one year of updates included. That is a known, bounded, deferrable cost, not an open-ended royalty. Fallback if the terms ever become unacceptable: PDFsharp / MigraDoc (MIT, empira Software GmbH), same architectural shape, weaker text layout.

---

## C. Manager app — *thin, deliberately*

The manager app exists so a status update can happen. It is not a workspace and should not become one.

| # | Requirement |
|---|---|
| **C-1** | Authenticated (Auth0). List of service requests for the location, newest first. Filter by status. That's the whole list view. |
| **C-2** | Detail view: renders the packet, plus the status control and a resend button. Nothing else. |
| **C-3** | Set status. One fixed set, the same for every location: `New`, `In Progress`, `Waiting on Parts`, `Waiting on Customer`, `Completed`, `Cancelled` (stored as `New` / `InProgress` / `WaitingOnParts` / `WaitingOnCustomer` / `Completed` / `Cancelled`). The path is `New → In Progress → Completed`; the two `Waiting on` values are holds off that path; `Cancelled` is a manual stop. Any status may move to any other. Changing status updates what the customer sees on their status page. Per-location configurable vocabularies are rejected — see C-8. |
| **C-4** | Disposition: close a request without work (duplicate, spam, wrong location, customer withdrew), with a reason code. |
| **C-5** | Resend the packet to the configured recipients or an ad-hoc address. |
| **C-6** | Location settings: the B-6 configuration. |

### C-7 — Status updates without logging in *(recommended)*

The packet email carries one-click action links — *In Progress*, *Waiting on Parts*, *Completed* — each a tokenized single-purpose URL, and each one of the C-3 statuses. Clicking one sets the status and shows a small confirmation page. No login, no app.

This matters more than it looks. If setting status requires opening a web app every day, RVS is still a thing people have to visit — the objection the whole design is meant to answer. One-click email actions mean the manager app becomes optional for daily operation and is only opened for configuration and history.

Cost is small: the tokens, endpoints, and audit logging are the same machinery as the customer status link. Decision is Q2 in `RVS_Plan.md`.

### C-8 — Status vocabulary decision

**Decided (issue #428, closes Q5): one fixed set, adopted from the code.** The statuses are `New`, `InProgress`, `WaitingOnParts`, `WaitingOnCustomer`, `Completed`, `Cancelled` (C-3), the set already implemented in `StatusTransitions`. Every location uses it as-is.

**Per-location configurable status vocabularies are rejected.** They fail the scope filter on craft-over-architecture and on what-comes-out: a configuration surface, per-tenant migration, and customer-status-page copy that varies by location, none of which makes the packet better. A solo mobile tech and a multi-location dealership are both served by the same six values — `WaitingOnParts` and `WaitingOnCustomer` cover the real holds, and closing without work is C-4 disposition, not a status.

Transitions are unrestricted: any status may move to any other (self-transitions excepted). There is no `Received` or `Ready` state — the earlier suggested set in this doc is superseded.

---

## X. Cross-cutting

| # | Requirement |
|---|---|
| **X-1** | **Customer status page.** `rvintake.com/status/{token}`, anonymous, rate-limited. Shows unit, submission date, current status, and the location's phone number. No conversation, no messaging, no file exchange. |
| **X-2** | **Ledger write.** Append-only entry on intake submission: asset ID, tenant, location, category, timestamp, taxonomy version. Write-once; corrections are new entries referencing the original. Invisible to users. Persistent write failure raises an alert. *This exists solely so the record is there later. Nothing reads it today.* |
| **X-3** | **Anonymization license.** Terms of service and any design-partner agreement must grant a perpetual, irrevocable license to use service data in anonymized, aggregated form. **Get this into the first customer's paperwork.** It cannot be added retroactively without renegotiating with every existing customer. |
| **X-4** | **Tenancy.** Every query is tenant-scoped through the existing claims and gate middleware. Cross-tenant data never appears in any response. |
| **X-5** | **Tokens.** All anonymous-access tokens (status page, packet link, email actions): ≥128 bits entropy, stored **hashed** (SHA-256; the raw token is never persisted), TTL-bounded, rate-limited per IP, access audit-logged, read-only except the single status write in C-7. Two scopes, one shared helper: the **status token is per customer** (resolves to the identity, TTL ≤ 30 days, sliding renewal on use); **C-7 action links are per request and per action** (single-purpose, TTL ≤ 14 days or single-use). Decision Q7 / issue #427. |
| **X-6** | **Attachment access.** Time-limited read SAS, generated per request, never persisted. |

### Non-functional

Intake submission P95 under 2s. Packet generated P95 under 10s, email delivered P99 under 60s. Nothing in the packet path may block or roll back an intake submission. Free-text problem descriptions are never written to application logs.

---

## Explicitly out of scope

Two-way SMS or messaging · DMS API integration of any kind · offline mobile app · scheduling or calendars · quoting, invoicing, payments · parts and inventory · benchmarking or analytics dashboards · cross-location reporting · technician assignment and workload · SSO/SAML/SCIM · warranty claim workflows.

Some of these are specced in detail in the archive. Retrieval triggers are in `RVS_Archive_Index.md`. Adding any of them back is a decision that gets logged in `RVS_Plan.md`, not a thing that happens because a prospect asked.
