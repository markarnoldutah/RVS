# RVS — Spec

**Version:** 1.0 · September 4, 2026
**Scope:** Everything RVS does. If it isn't here, it isn't in scope.

Requirements are numbered `A-n` (intake), `B-n` (packet and delivery), `C-n` (manager), `X-n` (cross-cutting), `P-n` (platform provisioning, RVS staff only). Fresh numbering — the old FR-nnn and OQ-nn series had collisions across documents and are archived with them.

**Stack:** Azure, .NET, Blazor, Cosmos DB (SQL API), Auth0 (Free tier), Azure Communication Services, Blob Storage.

---

## A. Intake app — *substantially built*

Anonymous Blazor web form at `rvintake.com/{locationSlug}`. No login, ever. Customers reach it through `go.rvintake.com/{locationSlug}`, which redirects and records the channel (A-13).

| # | Requirement |
|---|---|
| **A-1** | Anonymous access. No authentication on the intake endpoint. Per-IP rate limiting. |
| **A-2** | Collects: customer name, phone, email, preferred contact method, and two notification opt-outs (*Do not send text messages*, *Do not send email*); VIN or make/model/year; free-text description of the problem; photos and short video. The preferred contact method and the opt-outs together route the customer's confirmation, and an opted-out channel cannot be the preferred one. See "Contact preference and confirmation routing" below. |
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
| **A-13** | **Channel-tagged intake links.** Every distribution path routes through `go.rvintake.com/{locationSlug}`, which redirects to the location's intake URL and logs the hit. A `src` query parameter names the channel: `textrepl` (Text Replacement snippet sent from a Recents entry), `quickreply` (iOS Respond with Text / Android Quick Response, sent from an incoming call), `qr` (QR sticker or NFC tag), and no `src` at all for `print` — printed material cannot carry a query string, so its absence *is* the print channel. The redirect never fails: an unknown `src`, an unknown slug, or a hit-log outage all still send the customer to the form. `src` is persisted on the resulting `ServiceRequest` and reported per location. |
| **A-14** | **Advisor-initiated intake invite.** During a live call, a service advisor or mobile tech sends the caller a prefilled, single-use intake link by text from the manager app, with the caller's verbal consent. The resulting request is tagged `src=advisor` and attributed to the advisor and the invite. Detail below. |

All four AI capabilities in A-9–A-12 are in scope — decision Q8 / issue #429. Each has a rule-based or no-op fallback behind the same interface; none is a hard dependency for a successful submission.

**Controlled vocabulary at launch: `issue-category` only.** 13 codes, finalised in issue #452: Slides, Electrical, Plumbing (Plumbing & Water), HVAC, Generator, LPGas (LP / Propane), Appliances (Appliances & Refrigerator), Roof (Roof & Seals), Awning, Chassis (Chassis & Running Gear), Exterior (Body & Exterior), Interior (Interior & Cabinetry), Other. The single source of truth is `RVS.Domain.Validation.IssueCategoryVocabulary`; the Cosmos `lookup-sets` seed, the rule-based categorization fallbacks and the capability map all derive from it. A submitted or AI-suggested value outside the list is coerced to `Other` before it is stored, so the packet never shows a phantom category. The four technician-side vocabularies — component type, failure mode, repair action, part number — are archived. They were never populated at intake anyway; they filled in after a technician closed a job, which is a workflow RVS no longer has.

**Two stores, because they are two different things (A-13).** Redirect hits and service requests have different volumes and lifecycles, and most hits never convert. `src` on the `ServiceRequest` is the authoritative source-of-job record and lives in Cosmos with the request. Raw redirect hits go to an append-only Azure Table Storage table partitioned by location, where a write costs storage rather than Cosmos request units on a path that is written constantly and read occasionally. A conversion rate per channel is computable from the two together.

**Redirect hits are not opens, and are never reported as such.** iMessage and most messaging clients fetch a URL to build a link preview the moment it is composed — before anybody taps anything, possibly once per send, at a rate that varies by client. Obvious bot user agents are flagged and excluded from reported counts, but the filter is coarse by design and the residue is still not a count of people. **Submissions by source** is the metric a dealer is shown. Raw open counts are not.

This means the packet's structured content is: decoded unit, one category, the customer's own words, the diagnostic Q&A, and photos. That is the honest scope, and it is enough. The diagnostic Q&A block is the part that reads as expert on paper — *"Does the slide move at all? — Motor hums, no movement"* is worth more to a service manager than any taxonomy label. Invest the effort there.

### Contact preference and confirmation routing (A-2)

**Decided (issue #577, via #600).** The preferred contact method chooses the channel for the customer's submission confirmation. The two opt-outs act as a **hard veto**: RVS never sends on an opted-out channel, whatever the preference says. Both fields are kept.

| Preferred contact | Confirmation goes by |
|---|---|
| `Text`, with texting enabled (see below), a phone number, and no SMS opt-out | Text |
| `Text`, but texting is unavailable | Email. The fallback is logged at Warning |
| `Email`, `Phone`, or none recorded (requests from before the preference was captured) | Email |
| Any preference, but email is opted out or there is no address | Text if it is permitted. Otherwise nothing is sent, and that is logged |

`Phone` means *call me*. RVS does not place calls, so the confirmation goes by email.

**Step 2 rule.** Notification preferences sit **above** the preferred contact method on the form. *Do not send text messages* disables the **Text** option and *Do not send email* disables the **Email** option. Ticking an opt-out that conflicts with the current selection clears the selection. **Phone** is always available. The intake app and the API both enforce the rule, so a hand-built request that pairs `Text` with an SMS opt-out, or `Email` with an email opt-out, is rejected as a validation error (422).

**Disclosure.** Wherever intake offers texting, the copy states how often RVS texts, that message and data rates may apply, *Reply STOP to opt out, HELP for help*, and links to the terms.

**Texting is off until the number is verified.** Each environment's toll-free sending number needs carrier verification before it can text. Until that clears, texting is disabled for that environment by a flag that defaults to off. While it is off, RVS makes no SMS call anywhere, and a `Text` customer's confirmation goes by email.

### A-14: Advisor-initiated intake invite

**Decided (issue #600).** A-13's links are for customers who haven't been spoken to yet. A-14 covers the customer already on the phone. The advisor types the caller's first name and phone number, confirms that the caller agreed to receive the text, and RVS sends a prefilled, single-use link. The send action lives in the manager app. It is the only manager surface A-14 adds, and a shop that never opens the manager app loses nothing: A-13's links still work.

- **Consent first.** The advisor reads the published verbal consent script (`Docs/Guides/Advisor_Text_Consent_Script.md`) and ticks a consent box whose label references it. Send stays disabled until the box is ticked. The invite records when consent was captured and by which advisor, separately from when it was sent and whether it was delivered. **The consent record never expires**, because it is the evidence of opt-in for the toll-free verification and for any complaint. It outlives the invite.
- **Sender.** Every invite goes out through Azure Communication Services from the shared toll-free number, never from the advisor's own phone. The sending number is found by a resolver keyed on the location, never hardcoded. Today every location resolves to the same number, which leaves room for local numbers later without a schema change.
- **Refusals.** RVS refuses a send to a number that has opted out of texts. While texting is disabled for the environment, the send is refused with a clear message (409), and the manager app says texting is not yet enabled instead of showing a dead button. Every phone number is normalised to E.164 (US/Canada) before it is stored or sent.
- **Rate limits.** Sends are capped per advisor, per location and per tenant. The caps are configuration, not constants.
- **Token.** Each invite carries a random token of at least 128 bits. Only its SHA-256 hash is stored (X-5's rules). The token expires about **72 hours** after the send and is **single use**. The link is `go.rvintake.com/{locationSlug}?src=advisor&inv={token}`. The A-13 redirect passes `inv` through to the intake app.
- **Redeemed on submission, not on open.** Opening a valid invite prefills the caller's first name and phone. The intake app gets them from an anonymous, rate-limited lookup that returns **only** those two fields, and **only** while the invite is unexpired and unredeemed. The invite is marked redeemed when the intake is **submitted**, because messaging clients fetch links to build previews (the same caveat as A-13), and burning the token on open would kill it before the customer tapped it. This prefill is separate from A-7's returning-customer match.
- **Never a dead end.** An expired, unknown, already-used or failed invite lands on a working, blank intake form, which extends A-13's "the redirect never fails" rule to invites.
- **Attribution.** The resulting `ServiceRequest` carries `src=advisor`, the invite ID and the advisor's user ID. `advisor` joins the known `src` values.
- **Delivery status.** Delivery reports from ACS update the invite as queued, delivered or failed, and the send dialog shows that status inline, along with the current shift's recent sends. **Resend** mints a new invite, because tokens are single use.
- **Self-entry.** A *Fill it in myself* action mints an invite **without** texting it and opens the same prefilled form for the advisor, who fills it in during the call. It works even while texting is disabled.
- **Permission.** Sending requires a dedicated permission. Being signed in to the manager app is not enough.

---

## B. Packet and email delivery — *current work*

The packet is the product. Everything else exists to produce it.

### B-1 — Generation

Enqueued on intake submission; must not block the `201`. The customer's attachments upload after that `201`, so the submission declares how many are coming and generation waits for them to land before rendering — bounded by a short window, after which it renders whatever arrived. A failed upload costs the packet a photo, never the packet. Regenerated on demand. Three failed attempts raises an alert and is surfaced in the manager app. Failure never rolls back the service request.

### B-2 — Contents

One page, in this order:

1. Unit header — year, make, model, VIN (degrades if VIN absent)
2. Customer — name, phone, email, preferred contact. Preferred contact is one of `Phone` / `Text` / `Email`, required at intake; it is omitted from the packet only for requests created before it was captured. The same field routes the customer's confirmation, with the opt-outs as a hard veto, and an opted-out channel can never be the preference (A-2). So the packet never tells the shop to text a customer who has opted out of texts
3. Location, submission timestamp, short reference code. The reference code is the first hyphen-delimited segment of the service request id, upper-cased (e.g. `A1B2C3D4`) — deterministic, stable across regenerations, no stored field or counter
4. Issue category
5. AI summary, labeled as AI-generated — placed here so the service manager reads the concise recreation of the problem first. Headed **Preliminary assessment**, it also carries, when the model will offer one, a probable cause, a confidence (high / medium / low), **possible fixes** — plural and ordered most plausible first, never a single "recommended" fix, because the unit has not been inspected — and likely parts as generic names (never part numbers or prices). Advisory only, and says so. When the information is too thin the model abstains and only the summary renders. Generated once in the packet pipeline, not on the intake path, and reused on regeneration; a rule-based per-category fallback (low confidence) covers an unavailable model
6. **The customer's description, verbatim**
7. **Diagnostic Q&A**
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
| Attachments | The PDF, plus the original photos as image attachments — trimmed to fit the transport's message ceiling, PDF first |
| Size ceiling | The send is budgeted at 9.5 MB of a 10 MB transport limit, counting both bodies and attachments **after** base64 encoding. Over budget, attachments are dropped to fit rather than failing the send: the PDF is kept first if it fits, then photos fill what is left in order, dropping from the last one back. An email always goes out. The budget is checked at startup — at least 5 MB so the PDF always fits, and never above the transport limit, where every send would fail again |
| Target | Delivered within 60 seconds of submission, P99 |
| Retry | 3 attempts, exponential backoff, then alert |
| Bounce | A hard bounce disables that recipient and notifies the owner — never the whole configuration |
| Sending rate | The transport's per-subscription send quota is a deployment constraint, not a code one. A quota too low for pilot volume must be raised before launch, not after — see `RVS_Infrastructure.md` |

Idempotent per `(serviceRequestId, packetVersion)`.

Dropping an attachment costs the recipient a local copy, not the content: `A-6` allows ten
25 MB uploads, so a photo-heavy submission's original photos can exceed the ceiling on their
own, but `B-3` already embeds every photo in the HTML body as a time-limited SAS URL (`X-6`),
and the PDF stays downloadable from the manager app. The PDF itself stays small — its renderer
resamples embedded images — so in practice it is the photos that get dropped. A submission
that overruns the ceiling must still deliver a packet — silently failing every retry and leaving
the service department a request with no packet at all is the outcome this rule exists to prevent.

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

### C-7 — Status updates without a daily login

**Decided (issue #498, supersedes the anonymous-token design this section originally described — see #421, closed `not_planned`).** The packet email carries deep links into the manager app — *In Progress*, *Waiting on Parts*, *Completed*, plus a plain link to the request — as `…/sr/{id}?action=…` and `…/sr/{id}`. The manager app keeps a persistent, rotating session (target ~30 days, silent renewal) so tapping an email link lands the manager already signed in; one more tap confirms the status change. There is no anonymous status-write endpoint — every change is attributed to the signed-in user.

This matters more than it looks. If setting status requires an interactive login every day, RVS is still a thing people have to visit — the objection the whole design is meant to answer. The original version of C-7 (anonymous, single-purpose tokenized links, no app at all) ran into two problems: corporate mail-security link-prescanning (Defender Safe Links, Proofpoint, Mimecast auto-fetch every link in inbound mail, silently burning single-use tokens and firing transitions from a bot, not the manager) and an anonymous write surface on the token model. The persistent-session deep-link design avoids both and keeps the same outcome for the manager: no interactive login, no proactive visit to the app as a workspace — just a tap in the email.

Cost is not small: a persistent-session / refresh-token mechanism, an installable PWA so the tap is one hop, and deep-link routes with an `action` query parameter. This does not reuse the anonymous customer-status-token machinery (X-5) — it is authenticated, session-based work.

### C-8 — Status vocabulary decision

**Decided (issue #428, closes Q5): one fixed set, adopted from the code.** The statuses are `New`, `InProgress`, `WaitingOnParts`, `WaitingOnCustomer`, `Completed`, `Cancelled` (C-3), the set already implemented in `StatusTransitions`. Every location uses it as-is.

**Per-location configurable status vocabularies are rejected.** They fail the scope filter on craft-over-architecture and on what-comes-out: a configuration surface, per-tenant migration, and customer-status-page copy that varies by location, none of which makes the packet better. A solo mobile tech and a multi-location dealership are both served by the same six values — `WaitingOnParts` and `WaitingOnCustomer` cover the real holds, and closing without work is C-4 disposition, not a status.

Transitions are unrestricted: any status may move to any other (self-transitions excepted). There is no `Received` or `Ready` state — the earlier suggested set in this doc is superseded.

### C-9 — Customer-facing status notes

**Decided (issue #500).** The manager can attach free-text notes to a service request that render on the customer status page (X-1), alongside the fixed C-3 status — a short human note like "waiting on a back-ordered slide motor, ETA Friday". This overrides the original X-1 restriction ("no conversation, no messaging, no file exchange"), which was never a deliberate decision — see the decision log.

Constraints:

- **Manager-authored only. One-directional.** The customer cannot reply, message, or upload anything — the status page stays display-only for the customer (X-1). This is the one hard line; everything else about the surface is free to change.
- Notes are optional. The page reads cleanly with none.
- Plain text, length-capped (default 280 characters). Sanitised on input: reject the angle brackets `<` `>` and control characters (`\0` and other C0 controls); ordinary punctuation — apostrophes, quotation marks, semicolons — is allowed, since the note is a human sentence and is rendered through output encoding.
- Never written to application logs (same rule as the customer's own free-text problem description).
- Not the customer's problem description — that stays invisible to the customer-facing surface. This is a separate, deliberately-authored field.

The entry surface is the manager app detail view (C-2). It also fits a C-7 deep link so the manager can land straight on it from the packet email.

| # | Requirement |
|---|---|
| **X-1** | **Customer status page.** `rvintake.com/status/{token}`, anonymous, rate-limited. Shows unit, submission date, current status, the location's phone number, and any manager-authored notes (C-9). The page is free to grow to show whatever is useful for the customer to see about their job. **One hard constraint: it is display-only for the customer.** There is no path for the customer to send anything back — no reply, no inbound message, no file upload. Communication is one-directional, manager → customer. (This does not reopen two-way messaging; see "Explicitly out of scope".) |
| **X-2** | **Ledger write.** Append-only entry on intake submission: asset ID, tenant, location, category, timestamp, taxonomy version. Write-once; corrections are new entries referencing the original. Invisible to users. Persistent write failure raises an alert. *This exists solely so the record is there later. Nothing reads it today.* |
| **X-3** | **Anonymization license.** Terms of service and any design-partner agreement must grant a perpetual, irrevocable license to use service data in anonymized, aggregated form. **Get this into the first customer's paperwork.** It cannot be added retroactively without renegotiating with every existing customer. Drafted at `Docs/Legal/RVS_Pilot_Agreement.md` — one page, phone-signable, single variant. |
| **X-4** | **Tenancy.** Every query is tenant-scoped through the existing claims and gate middleware. Cross-tenant data never appears in any response. |
| **X-5** | **Tokens.** The anonymous status-page token (X-1): ≥128 bits entropy, stored **hashed** (SHA-256; the raw token is never persisted), TTL ≤ 30 days with sliding renewal on use, rate-limited per IP, access audit-logged, **read-only** — resolves to the customer identity on `GlobalCustomerAcct`. One generation/hash/TTL/audit helper. Decision Q7 / issue #427. **C-7 status changes are not on this token model** — they run through an authenticated manager-app session (issue #498); there is no anonymous status-write surface. The **A-14 invite token** is the one other anonymous token scope. It follows the same entropy and hashing rules, but it expires about 72 hours after the send, is single use, and its lookup returns only the invitee's first name and phone. |
| **X-6** | **Attachment access.** Time-limited read SAS, generated per request, never persisted. |

### Non-functional

Intake submission P95 under 2s. Packet generated P95 under 10s, email delivered P99 under 60s. Nothing in the packet path may block or roll back an intake submission. Free-text problem descriptions are never written to application logs.

---

## P. Platform provisioning — *internal tool, RVS staff only*

A pilot closes with the customer set up live, in the room, from a phone. This is RVS's own tool, not a customer surface: a hidden `/admin` area in the manager app, backed by `api/admin/tenants` on the existing API (issue #563). It adds no Azure resources.

| # | Requirement |
|---|---|
| **P-1** | **Create tenant.** One form, submitted once, creates the `Tenant` (`id == tenantId`, name, billing email, status `Pilot`, plan `mobile` or `location`, notes), the `TenantConfig` with logins enabled, the `Dealership`, the first `Location` (named `{business name} - {location name}`, as in P-5; its packet recipient defaults to the first user's email), that location's `slug-lookups` entry with dealership and location names filled in, and the first Auth0 user (P-2). `tenantId` is derived as `ten_{snake_name}` and can be edited. An existing id with the same name is a retry (P-6); with a different name it is rejected. |
| **P-2** | **Add user to tenant.** Email, display name, role and, for location-scoped roles, locations. Roles offered: `dealer:owner`, `dealer:manager`, `dealer:advisor`, `dealer:readonly`; archived roles are not. The user is created on `Username-Password-Authentication` with a random password that is never shown or stored, gets `app_metadata {tenantId, orgName, locationIds}` and the role, and the response carries a set-password ticket URL: 7-day lifetime, email marked verified, returning to the manager app. |
| **P-3** | **Resend set-password link** for an existing user of the tenant. |
| **P-4** | **Enable/disable tenant.** Toggles `AccessGate.LoginsEnabled` with a reason; disabling sets `DisabledAtUtc`. This controls access only. `Tenant.status` (`Pilot` / `Active` / `Churned`) is the commercial state and is edited separately; neither sets the other. |
| **P-5** | **Add location.** Name, optional slug, phone, 1–10 packet recipients. The stored name always leads with the tenant's business name: `Tucson` for tenant `Blue Compass` is saved as `Blue Compass - Tucson`. A name that already starts with the business name is not prefixed twice. The generated slug is unchanged (`blue-compass-tucson`). |
| **P-6** | **Safe to retry.** Re-submitting after a partial failure finishes the job without duplicates. Cosmos writes run first, with fixed ids; Auth0 runs last. An existing email is updated when its `tenantId` matches and rejected when it doesn't. The response lists each step as `created`, `already existed` or `failed` — or `skipped`, when an earlier step failed and the step was not attempted. |
| **P-7** | **Security.** Every `api/admin/*` endpoint requires the `platform:tenants:manage` permission **and** a caller `sub` on `Admin:AllowedUserIds`; otherwise 403. MFA is required on the admin Auth0 account. A dedicated Auth0 M2M application holds only `read:users create:users update:users update:users_app_metadata read:roles create:role_members create:user_tickets`, and its secret lives only in Key Vault. Admin writes are logged (who, what, tenantId); passwords and ticket URLs never are. Missing Auth0 settings fail loudly — there is no no-op fallback. |
| **P-8** | **Mobile.** Single-column forms, usable at 390px. On success a result card shows the intake URL and the ticket URL, each with a Copy button. Nothing is emailed automatically. |

Out of scope for v1: self-serve signup, bulk import, editing or deleting users, billing automation, branding upload (FS-5), and splitting the shared Auth0 tenant (FS-9).

---

## Explicitly out of scope

Two-way SMS or messaging (one-directional manager → customer status notes are C-9) · DMS API integration of any kind · offline mobile app · scheduling or calendars · quoting, invoicing, payments · parts and inventory · benchmarking or analytics dashboards · cross-location reporting · technician assignment and workload · SSO/SAML/SCIM · warranty claim workflows.

**The STOP/HELP handler does not reopen two-way SMS.** Outbound texting (A-2 confirmations, A-14 invites) receives exactly one kind of inbound traffic: carrier keywords on the sending number. `STOP` and its standard synonyms set the SMS opt-out on that number's customer records, so later sends to it are refused. `HELP` gets a fixed reply. No other inbound text is read, stored, routed to anyone or answered. There is no inbox and no conversation.

Some of these are specced in detail in the archive. Retrieval triggers are in `RVS_Archive_Index.md`. Adding any of them back is a decision that gets logged in `RVS_Plan.md`, not a thing that happens because a prospect asked.
