# Prompt — Create GitHub Issues for the Packet MVP

Assumes the GitHub Issue Manager custom agent and the GitHub MCP server.

**Repo:** `markarnoldutah/RVS`
**Project:** `<FILL IN — project number or URL>`
**Proceed without Issue types.** Use Acceptance Criteria, never task lists.

---

## Part 1 — Instructions to the agent

### What you are doing

Create GitHub Issues for the RVS Packet MVP and add every one of them to the project above.

Work in two passes, exactly as ordered:

1. **Pass 1 — Features.** Create the ten Features in Part 2 in order, one after another. Record the issue number each one gets.
2. **Pass 2 — Sub-issues.** Loop back through each Feature in the same order and create its sub-issues, linking each as a sub-issue of its parent.

Add every issue to the project as you create it. Do not batch this to the end.

### Before you start

Read these four files in the repo. They are the authority, and this prompt is a derivative of them — if the two disagree, the repo wins and you should say so rather than guessing:

- `Docs/RVS_Spec.md` — requirement IDs `A-n`, `B-n`, `C-n`, `X-n`. **If a requirement isn't there, it isn't in scope.**
- `Docs/RVS_Plan.md` — build order, ship criteria, the scope filter, open questions `Q1`–`Q8`
- `Docs/ASOT/RVS_Architecture.md` — what is actually built, plus a coverage table against the Spec
- `Docs/ASOT/RVS_FrontEnd.md` — the same for both Blazor apps

### Labels

Ensure these exist, creating any that don't. Do not invent others.

| Label | Colour | For |
|---|---|---|
| `type:feature` | `1D76DB` | The ten parent Features |
| `type:task` | `C5DEF5` | Sub-issues that build something |
| `type:decision` | `D4C5F9` | Sub-issues that resolve an open question |
| `type:bug` | `D73A4A` | Defects in existing code or infra |
| `type:descope` | `E4E669` | Removing archived-scope code |
| `area:api` | `0E8A16` | `RVS.API`, `RVS.Domain`, infra repositories |
| `area:intake` | `5319E7` | `RVS.Blazor.Intake` |
| `area:manager` | `B60205` | `RVS.Blazor.Manager` |
| `area:infra` | `FBCA04` | Bicep, workflows, Azure |
| `blocked` | `000000` | Cannot start until a decision issue closes |

### Conventions for every issue

- **Title:** imperative, under 70 characters. No issue-type prefixes — the labels carry that.
- **Body:** start with one or two sentences of *why*, then the specifics. Assume the reader has not read the Spec.
- **Reference the requirement** it implements, as `Spec B-4` etc. Every build sub-issue must cite at least one.
- **Acceptance Criteria:** a `## Acceptance Criteria` heading followed by checkbox lines that are observable and testable. Not a restatement of the title.
- **File paths** where this prompt supplies them, as starting points. Verify them before relying on them — they were accurate as of September 2026.
- **Blocked issues:** apply `blocked`, and say in the body which issue blocks them.
- **Tests:** this repo mandates TDD. Every build sub-issue's acceptance criteria must include failing-tests-first, per `.github/instructions/testing.instructions.md`. Do not create separate "write tests" issues.

### Guardrails

- **Do not create issues for archived scope.** Two-way SMS, DMS API integration, the technician/MAUI app, scheduling, analytics or benchmarking dashboards, OEM data licensing, multi-industry expansion, SAML/SCIM/audit-log enterprise features. If something in Part 2 looks like it needs one of these to work, stop and ask.
- **Do not invent pricing.** It is unresolved (`Q6`).
- **Do not break down Feature 10.** It is intentionally left whole.
- **Do not create issues for the GTM open questions** `Q3`, `Q4`, `Q6`. They are not engineering work and do not belong in this tracker.
- If a Feature seems to be missing work, add a comment on that Feature proposing it. Do not create the issue.

---

## Part 2 — The issues

### Feature 1 — Open decisions blocking the build

`type:feature`

Four questions block work downstream. Each is a short decision with a written outcome, not an implementation. Every one must be recorded in the Decision log in `Docs/RVS_Plan.md` when it closes.

**Acceptance Criteria**
- [ ] All four sub-issues are closed with a decision recorded in `Docs/RVS_Plan.md`
- [ ] Any issue labelled `blocked` by one of them has had the label removed

**Sub-issues**

1. **Choose the PDF rendering library and clear its licence** — `type:decision` `area:api`
   `Spec B-7`, `Q1`. Headless Chromium on App Service is operationally painful; a native .NET renderer avoids that but needs its licence checked for commercial use.
   - [ ] A library is chosen and the reason is written down
   - [ ] Licence reviewed for commercial use and the finding recorded
   - [ ] Choice satisfies B-7: no per-render outbound network dependency beyond Blob Storage, and no headless browser process that can't be health-checked and recycled
   - [ ] Blocks Feature 2 — remove `blocked` there when this closes

2. **Decide the anonymous token model** — `type:decision` `area:api`
   `Spec X-5`, `Q7`. Spec X-5 requires per-request, hashed, TTL-bounded tokens with ≥128 bits of entropy. What is built is a per-*customer* magic link: 90-day, stored unhashed on `GlobalCustomerAcct.magicLinkToken`, and a prior ASOT decision argued explicitly for unhashed storage. See `Docs/ARCHIVE/ASOT/RVS_MagicLink_Storage_Guidance.md` for that reasoning before overturning it.
   - [ ] Per-request vs per-customer scope is decided
   - [ ] Hashing decided; if the Spec is being changed instead, `Docs/RVS_Spec.md` X-5 is edited to match
   - [ ] Migration path for existing tokens is described
   - [ ] Confirms whether C-7 action links reuse this machinery
   - [ ] Blocks Feature 4

3. **Fix the service request status vocabulary** — `type:decision` `area:api`
   `Spec C-3`, `Q5`. The Spec suggests `New → Received → In Progress → Ready → Closed` plus `Cancelled`. The code implements `New / InProgress / WaitingOnCustomer / WaitingOnParts / Completed / Cancelled`. Prefer a single fixed set; per-location configurable status is a known complexity sink.
   - [ ] One vocabulary is chosen and works for a solo mobile tech and a dealership
   - [ ] `Docs/RVS_Spec.md` C-3 and the code agree
   - [ ] Explicitly records whether configurable sets are rejected
   - [ ] Blocks Feature 5

4. **Decide whether voice transcription and VIN photo extraction are in scope** — `type:decision` `area:intake`
   `Q8`. `ai/transcribe-issue` (Whisper) and `ai/extract-vin` (gpt-4o vision) are fully built into intake steps 3 and 5, along with `ai/suggest-insights` and `assess-capabilities` — none appear in the Spec. The Whisper account is unconditional infrastructure spend either way.
   - [ ] Each of the four capabilities is marked in-scope or archived
   - [ ] In-scope ones are added to `Docs/RVS_Spec.md` section A
   - [ ] Archived ones get a descope sub-issue on Feature 8
   - [ ] Blocks the Whisper flag work in Feature 9

---

### Feature 2 — Packet composition and rendering

`type:feature` `blocked` (by Feature 1 sub-issue 1)

Build order item 1. **The packet is the product; everything else exists to produce it.** Nothing resembling a packet exists in the codebase today — no composition, no HTML template, no PDF. This is greenfield.

`Spec B-1`, `B-2`, `B-3`, `B-7`.

**Acceptance Criteria**
- [ ] A packet can be generated from any existing service request
- [ ] It renders as HTML and as PDF from one composition model, not two templates
- [ ] It prints legibly at Letter and A4, in greyscale, on a shop printer
- [ ] Generation never blocks or rolls back an intake submission

**Sub-issues**

1. **Build the packet composition model** — `type:task` `area:api`
   `Spec B-2`. One domain model assembled from a `ServiceRequest`, feeding both renderers. Contents in order: unit header (year/make/model/VIN, degrading if VIN absent), customer and preferred contact, location + timestamp + short reference code, issue category, the customer's description verbatim, diagnostic Q&A, AI summary labelled as AI-generated, photos, paste block, status link.
   - [ ] Composition is a pure transform with no rendering concerns
   - [ ] Degrades correctly when VIN, category, photos or diagnostic answers are absent
   - [ ] Never includes pricing, quotes, labor rates, or any other customer's data
   - [ ] Failing unit tests written first, covering each degradation case

2. **Render the packet as HTML with a print stylesheet** — `type:task` `area:api`
   `Spec B-3`. HTML is the primary rendering.
   - [ ] Prints cleanly at Letter and A4
   - [ ] Legible in greyscale — no information conveyed by colour alone
   - [ ] Diagnostic Q&A is visually prominent; it is the block that reads as expert
   - [ ] Verified against a real printout, not a browser preview

3. **Render the packet as PDF** — `type:task` `area:api`
   `Spec B-3`, `B-7`. Derived from the same composition model as the HTML.
   - [ ] Uses the library chosen in Feature 1
   - [ ] Output matches the HTML rendering in content and order
   - [ ] No headless browser process that cannot be health-checked and recycled
   - [ ] Renders a 6-photo packet within the B-1 latency budget

4. **Embed photos as time-limited SAS URLs** — `type:task` `area:api`
   `Spec B-2`, `B-3`, `X-6`. Up to six thumbnails on page one, the rest on an appendix page.
   - [ ] Images embed as time-limited SAS URLs, **not** base64
   - [ ] SAS is generated per request and never persisted
   - [ ] Appendix page appears only when there are more than six photos

5. **Orchestrate packet generation** — `type:task` `area:api`
   `Spec B-1`, `A-8`. Enqueued on intake submission; regenerated on demand. Intake currently returns `201` from `IntakeOrchestrationService`; the enqueue must not delay it.
   - [ ] Enqueued on submission and does not block the `201`
   - [ ] Can be regenerated on demand for an existing request
   - [ ] Three failed attempts raises an alert and surfaces in the manager app
   - [ ] Failure never rolls back the service request
   - [ ] Packet generated P95 under 10s

---

### Feature 3 — Email delivery to the service department

`type:feature`

Build order item 2. Email exists in the codebase but **only ever sends a customer confirmation** from the last intake step (`AcsEmailNotificationService`, invoked from `IntakeOrchestrationService`). Nothing emails a service manager. `Dealership.ServiceEmail` exists and is mapped but is read by no code path.

Together with Feature 2 this is the demo — both prospects can be pitched the moment these work end to end, before the manager app exists.

`Spec B-4`, `B-5`, `B-6`.

**Acceptance Criteria**
- [ ] A customer completes intake on a phone and the service manager has a readable email with photos and a printable PDF within a minute
- [ ] A location works after changing exactly one setting: the recipient address
- [ ] Delivery is idempotent and survives transient ACS failures

**Sub-issues**

1. **Add per-location packet configuration** — `type:task` `area:api`
   `Spec B-6`, `C-6`. None of this exists; the only email address in the model today is `Dealership.ServiceEmail`, at the wrong level and unused.
   - [ ] `Location` carries: enable/disable, recipient list (1–10), attach-PDF, include-photos, paste-block cap, status-link TTL, optional logo
   - [ ] Defaults are chosen so only the recipient address must be set
   - [ ] Read and write through the existing locations API, tenant-scoped
   - [ ] Failing tests first, including the 10-recipient bound

2. **Generate the DMS paste block** — `type:task` `area:api`
   `Spec B-5`. **This is the DMS integration** — manual, honest about being manual, and it eliminates the retyping the advisor actually cares about. Order: category, the customer's verbatim description, then the status link. The nearest existing code is `BuildTechnicianSummary()` in `IntakeOrchestrationService`, which is not a substitute.
   - [ ] ASCII-safe — no smart quotes, em-dashes, or non-breaking spaces, because DMS text fields mangle Unicode
   - [ ] Truncates at a word boundary to a configurable cap, default 1,000 characters
   - [ ] Delimited so it can be selected cleanly
   - [ ] Failing tests first, covering Unicode substitution and boundary truncation

3. **Send the packet email via ACS** — `type:task` `area:api`
   `Spec B-4`. Extend the existing ACS integration rather than adding a transport.
   - [ ] Subject is `[RVS] {category} — {year} {make} {model} — {customer last name}`
   - [ ] Body is the packet as inline HTML, degrading to the paste block for text-only clients
   - [ ] Attaches the PDF and the original photos, per the location's configuration
   - [ ] Sends to the configured recipient list
   - [ ] Delivered within 60 seconds of submission at P99

4. **Make delivery idempotent and retried** — `type:task` `area:api`
   `Spec B-4`.
   - [ ] Idempotent per `(serviceRequestId, packetVersion)` — a repeat never double-sends
   - [ ] Three attempts with exponential backoff, then an alert
   - [ ] Retries are observable in logs with the correlation ID

5. **Handle hard bounces** — `type:task` `area:api`
   `Spec B-4`. A misconfigured address must not silently black-hole every future packet for that location.
   - [ ] A hard bounce disables **that recipient only**, never the whole configuration
   - [ ] The location owner is notified
   - [ ] A disabled recipient is visible and re-enablable in location settings

---

### Feature 4 — Customer status page and token model

`type:feature` `blocked` (by Feature 1 sub-issue 2)

Build order item 3. A status page exists end to end today — `StatusController` and `RVS.Blazor.Intake/Pages/StatusPage.razor` — but on the per-customer magic-link model, which conflicts with Spec X-5. This Feature reconciles them.

`Spec X-1`, `X-5`.

**Acceptance Criteria**
- [ ] A customer can check status from the link without logging in
- [ ] Token storage and lifetime match `Docs/RVS_Spec.md` X-5 as it reads when Feature 1 closes
- [ ] Existing issued tokens keep working, or are migrated deliberately

**Sub-issues**

1. **Implement the decided token model** — `type:task` `area:api`
   `Spec X-5`.
   - [ ] ≥128 bits of entropy, TTL-bounded, rate-limited per IP
   - [ ] Stored per the Feature 1 decision; if hashed, lookup works without a cross-partition scan
   - [ ] Read-only except the single status write in C-7
   - [ ] Token access is audit-logged
   - [ ] Failing tests first, including an expired-token path

2. **Migrate existing magic-link tokens** — `type:task` `area:api`
   Existing `GlobalCustomerAcct.magicLinkToken` values are live and 90-day.
   - [ ] Migration is idempotent and re-runnable
   - [ ] Already-issued links either keep working or are deliberately invalidated, per the Feature 1 decision
   - [ ] Rollback is documented

3. **Harden the status endpoint** — `type:task` `area:api`
   `Spec X-1`. Already anonymous with a `StatusEndpoint` limiter at 10/min.
   - [ ] Shows unit, submission date, current status, and the location's phone number — nothing more
   - [ ] No conversation, no messaging, no file exchange
   - [ ] Free-text problem descriptions are never written to application logs

---

### Feature 5 — Manager app, cut to thin scope

`type:feature` `blocked` (by Feature 1 sub-issue 3)

Build order item 4. **The manager app exists so a status update can happen. It is not a workspace and should not become one.** What is built is considerably heavier than Spec C, and three pieces of Spec C are missing entirely.

`Spec C-1` – `C-6`.

**Acceptance Criteria**
- [ ] A service manager can run a full week on email alone, never opening the app — the real test
- [ ] Every route that remains maps to a `C-n` requirement
- [ ] Packet preview, resend, and disposition all work

**Sub-issues**

1. **Show the packet in the request detail view** — `type:task` `area:manager`
   `Spec C-2`. The detail drawer is `Shared/ServiceRequestDetailDialog.razor`.
   - [ ] Renders the packet, plus the status control and a resend button — nothing else
   - [ ] Shows generation failure state when B-1 has exhausted its retries

2. **Add resend** — `type:task` `area:manager` `area:api`
   `Spec C-5`. Does not exist anywhere today.
   - [ ] Resends to the configured recipients or an ad-hoc address
   - [ ] Uses the same idempotency key semantics, with a new packet version
   - [ ] Resends are audit-logged with the acting user

3. **Add disposition with a reason code** — `type:task` `area:manager` `area:api`
   `Spec C-4`. Today closing is only setting status to Completed or Cancelled.
   - [ ] Close without work, with a reason: duplicate, spam, wrong location, customer withdrew
   - [ ] Reason is stored and visible on the request
   - [ ] Disposition is distinguishable from a normal close in the list

4. **Apply the decided status vocabulary** — `type:task` `area:manager` `area:api`
   `Spec C-3`.
   - [ ] Code, Spec and any seeded lookup data agree
   - [ ] Existing requests migrate to the new values
   - [ ] Changing status updates what the customer sees on their status page

5. **Add per-location packet settings to the UI** — `type:task` `area:manager`
   `Spec C-6`, `B-6`. Extends `Pages/Locations.razor`.
   - [ ] Every B-6 setting is editable
   - [ ] Recipient list validates addresses and enforces the 1–10 bound
   - [ ] A newly created location is usable after setting one field

6. **Reduce the list view to Spec C-1** — `type:task` `area:manager`
   `Spec C-1`. `Pages/ServiceRequestQueue.razor` has a ten-field search panel against a specced "list, newest first, filter by status".
   - [ ] List is newest-first with a status filter
   - [ ] Filters for technician, bay and priority are removed alongside Feature 8
   - [ ] Any filter kept beyond C-1 is justified in the PR description

---

### Feature 6 — One-click status updates from the email

`type:feature`

Build order item 5. **This matters more than it looks.** If setting status requires opening a web app every day, RVS is still a thing people have to visit — the objection the whole design answers. Days of work if the token machinery from Feature 4 is reused.

`Spec C-7`.

**Acceptance Criteria**
- [ ] A manager sets status from the email without logging in
- [ ] The manager app becomes optional for daily operation

**Sub-issues**

1. **Add tokenized single-purpose action endpoints** — `type:task` `area:api`
   `Spec C-7`, `X-5`. One URL per action — Received, In Progress, Ready.
   - [ ] Each token performs exactly one status transition and nothing else
   - [ ] Reuses the Feature 4 token machinery
   - [ ] Replay of a used token is safe and idempotent
   - [ ] Every use is audit-logged

2. **Add the confirmation page** — `type:task` `area:intake`
   `Spec C-7`. A small page, no app shell — that is the point.
   - [ ] Confirms what changed
   - [ ] Handles expired and already-used tokens without an error page
   - [ ] Requires no login

3. **Put the action links in the packet email** — `type:task` `area:api`
   `Spec C-7`, `B-4`.
   - [ ] Links render in the HTML body and survive the text-only degradation
   - [ ] Links are per-request and expire with the location's configured TTL

---

### Feature 7 — Issue categories and the diagnostic question bank

`type:feature`

Build order item 6. Domain work, parallel to everything, not engineering-blocked. **Don't let it slip to the end** — the fallback question bank is what makes the packet read as expert rather than generic, and it is the cheapest quality lever in the product.

`Spec A-4`, `A-5`.

**Acceptance Criteria**
- [ ] Every category has a hand-written fallback question set that a service manager would call useful
- [ ] The packet reads as expert with the AI switched off entirely

**Sub-issues**

1. **Finalise the issue-category vocabulary** — `type:task` `area:api`
   `Spec A-5`. Roughly 10–14 codes. The starting list is Slide System, Electrical, Plumbing, HVAC, Generator, Appliance, Roof/Seals, Chassis, Other.
   - [ ] Final list agreed and seeded into `lookup-sets`
   - [ ] Categories map to how RV failures actually present, checked with a working technician
   - [ ] Customer can override the AI suggestion; it stays advisory

2. **Write per-category fallback diagnostic questions** — `type:task` `area:api`
   `Spec A-4`. Used when the AI call fails. 2–4 questions per category.
   - [ ] Every category has a hand-written set
   - [ ] Questions elicit specifics a technician would ask — *"Does the slide move at all?"* beats *"Describe the problem"*
   - [ ] Reviewed by someone who repairs RVs

3. **Retire the four technician-side vocabularies** — `type:descope` `area:api`
   Component type, failure mode, repair action and part number were never populated at intake — they filled in after a technician closed a job, a workflow RVS no longer has.
   - [ ] Only `issue-category` is seeded and maintained
   - [ ] The packet has no "Not yet classified" fields
   - [ ] Coordinated with the Feature 8 removal of `ServiceEventEmbedded`

---

### Feature 8 — Descope: delete archived-scope code

`type:feature`

Build order item 8. Roughly a sprint, parallelisable. This code was built for capability the product no longer has. It is a descope target, not a feature — do not extend any of it.

Full inventory is in the Descope backlog sections of `Docs/ASOT/RVS_Architecture.md` and `Docs/ASOT/RVS_FrontEnd.md`.

**Acceptance Criteria**
- [ ] No route, endpoint or permission remains for archived capability
- [ ] The solution builds and all tests pass after each sub-issue
- [ ] Cosmos document changes are sequenced as migrations, not silent edits

**Sub-issues**

1. **Remove analytics end to end** — `type:descope` `area:api` `area:manager`
   `AnalyticsController`, `AnalyticsService`, `IAnalyticsService`, `GetForAnalyticsAsync`, `ServiceRequestAnalyticsResponseDto`, `AnalyticsApiClient`, `Pages/Analytics.razor`, the nav link, and the `analytics:read` permission.
   - [ ] No analytics route or client method remains
   - [ ] `analytics:read` removed from `Program.cs` and from Auth0 roles

2. **Remove the Kanban board** — `type:descope` `area:manager`
   `Pages/ServiceBoard.razor`, `Layout/BoardLayout.razor`, the nav link, and `ServiceRequest.boardSequence`.
   - [ ] Board route and its dedicated layout are gone
   - [ ] `boardSequence` removal is handled as a document migration

3. **Remove technician outcome capture** — `type:descope` `area:api` `area:manager`
   `ServiceEventEmbedded`, `PATCH batch-outcome`, `BatchOutcome*Dto`, `AssetLedgerEntry.Section10A`, `Pages/BatchOutcome.razor`, `Shared/OutcomeComplianceWidget.razor`, `ServiceRequestApiClient.BatchOutcomeAsync`, and the `service-requests:update-service-event` permission.
   - [ ] No outcome-entry surface remains
   - [ ] **The ledger entry itself stays** — Spec X-2. Only the `Section10A` block goes

4. **Remove the SMS stack** — `type:descope` `area:api`
   `AcsSmsNotificationService`, `NoOpSmsNotificationService`, `ISmsNotificationService`, SMS opt-out plumbing, and the never-called `NotificationOrchestrator.SendStatusChangeAsync` and `SendMagicLinkAsync`.
   - [ ] No SMS code path remains
   - [ ] **The ACS resource stays** — email now rides on it
   - [ ] SMS opt-out fields removed from intake step 2 and the customer entities

5. **Remove scheduling and assignment fields** — `type:descope` `area:api` `area:manager`
   `assignedTechnicianId`, `assignedBayId`, `scheduledDateUtc`, `requiredSkills` on `ServiceRequest` and its update DTO, the corresponding controls in `Pages/ServiceRequestEdit.razor`, and the matching search filters.
   - [ ] Fields removed from entity, DTO, UI and search
   - [ ] Sequenced as a document migration
   - [ ] Decide whether `ServiceRequestEdit.razor` survives at all — it largely duplicates the detail drawer

6. **Remove dead scaffolding and unreferenced code** — `type:descope` `area:api` `area:manager`
   `WeatherForecastController` and `WeatherForecast.cs`; `Pages/ClaimsDebug.razor`, which is self-labelled "remove before production"; `MessageEmbedded`, which is defined and referenced nowhere; and the unreferenced shared components `AssetDisplay`, `AttachmentThumbnail`, `DiagnosticResponseView`.
   - [ ] All removed, solution builds
   - [ ] Confirmed unreferenced by search before each deletion

7. **Delete the mobile build workflow** — `type:descope` `area:infra`
   `.github/workflows/build-mobile.yml` builds `RVS.MAUI.Tech` on `mobile-v*` tags. That project is not in the repo, so the workflow cannot succeed.
   - [ ] Workflow deleted
   - [ ] No other workflow references MAUI

8. **Decide the fate of two dead projects** — `type:decision` `area:api`
   `RVS.Infra.AzTablesRepository` — `ITenantAccessRepository` has no implementation, its registration is commented out in `Program.cs`, and `TablesAuditRepository` throws `NotImplementedException` from every method. `RVS.Infra.AzCredentials` is referenced by two csproj files and called by nothing.
   - [ ] Each project is removed, or given a real implementation with a reason
   - [ ] Coordinated with Feature 9 sub-issue 3 — the tenant access gate depends on this

---

### Feature 9 — Infrastructure and platform defects

`type:feature`

Real defects found by reading the Bicep and the code against each other. Detail and file paths are in `Docs/ASOT/RVS_Infrastructure.md` under Known defects.

**Acceptance Criteria**
- [ ] A production deployment of the Bicep succeeds and the app can reach every dependency
- [ ] No parameter file contains placeholder values presented as real ones

**Sub-issues**

1. **Fix unreachable production Azure OpenAI** — `type:bug` `area:infra`
   Both OpenAI modules set `publicNetworkAccess: Disabled` and `networkAcls.defaultAction: Deny` when `environmentName == 'prod'`, and **no private endpoint is declared anywhere**. As written, production cannot reach either account.
   - [ ] Either private endpoints are added, or the access policy is corrected
   - [ ] Verified by an actual prod-parameters deployment, not by inspection

2. **Resolve the Tables connection string contradiction** — `type:bug` `area:infra`
   `AzureTables--ConnectionString` is built from an account key while `allowSharedKeyAccess=false` in every environment. The secret cannot work as written.
   - [ ] Secret is removed, or shared-key access is enabled deliberately with a reason
   - [ ] Depends on Feature 8 sub-issue 8

3. **Give the tenant access gate a backing store** — `type:bug` `area:api`
   `TenantAccessGateMiddleware` runs on every request, but `ITenantAccessRepository` has no implementation and its registration is commented out. The gate cannot currently block a disabled tenant.
   - [ ] Gate either works against a real store or is removed
   - [ ] `Spec X-4` still holds: cross-tenant data never appears in any response
   - [ ] Test proves a disabled tenant receives 403

4. **Replace the prod_phase2 placeholder values** — `type:bug` `area:infra`
   `intakeApexIpv4Addresses` is `['0.0.0.0']` and `intakeApexValidationValues` is `['REPLACE_WITH_AZURE_TXT_TOKEN']`, so the file is not deployable as committed.
   - [ ] Real values in, or the file clearly marked as a two-phase template
   - [ ] The apex TXT-token sequence is documented where someone will find it

5. **Put the Whisper deployment behind a flag** — `type:task` `area:infra` `blocked` (by Feature 1 sub-issue 4)
   The Whisper account and `rg-rvs-{env}-ncus` are **unconditional** — they deploy in every environment with no flag. If voice is archived, this is pure spend.
   - [ ] A `deployWhisper` flag gates the account and its resource group
   - [ ] Set per environment according to the Q8 decision
   - [ ] Same treatment considered for the gpt-4o account

---

### Feature 10 — Billing and trial

`type:feature` `blocked`

Build order item 7. **Do not break this down and do not create sub-issues for it.**

Pricing at the reduced scope is unresolved — `Q6` in `Docs/RVS_Plan.md`, owned by GTM. The archived four-tier model priced a product that no longer exists, and the archived metering architecture (`Docs/ARCHIVE/ASOT/Architecture/RVS_Billing_Metering_Architecture.md`) was built for those tiers. Breaking this down before pricing is settled would bake in assumptions that are almost certainly wrong.

**Acceptance Criteria**
- [ ] `Q6` is resolved and recorded in the Decision log
- [ ] This Feature is then broken down in a follow-up pass

---

## Part 3 — When you are finished

Report back with:

1. A table of every Feature — number, title, sub-issue count
2. Total issue count created
3. Confirmation that all of them are on the project
4. Any issue you could not create, and why
5. Anything in this prompt that contradicted the repo, quoted, with what you did about it

Do not close, rename, or edit any pre-existing issue in the repo.
