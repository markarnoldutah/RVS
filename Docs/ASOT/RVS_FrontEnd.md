# RVS — Front End

**Version:** 1.0 · September 4, 2026
**Scope:** Both Blazor WASM apps and the shared client library, as built.

Stack: Blazor WebAssembly, **MudBlazor 9.x** (Material Design 3). Do not introduce `Microsoft.FluentUI.AspNetCore.Components`. Component conventions, theming rules and the MudBlazor v9 breaking-change list are in `/CLAUDE.md`.

---

## Intake app — `RVS.Blazor.Intake`

Anonymous. No auth packages, no token handler, plain `HttpClient`, plain `Router`. Keep it that way.

Shell is `Pages/IntakeWizard.razor`, routed at `/{Slug}` with an optional `?token=` magic link. Wizard state lives in `State/IntakeWizardState.cs` and persists to sessionStorage across reloads.

**The wizard has eight steps, not seven.** Older documentation said seven; the `switch` in the shell has eight and the header reads "Step N of 8". All eight are fully implemented — there are no placeholder steps.

| Step | Component | Does |
|---|---|---|
| 1 | `Step1_IntakeLanding` | `GET api/intake/{slug}/config`; applies returning-customer prefill (customer, asset, known assets); handles expired token and invalid slug |
| 2 | `Step2_CustomerInfoStep` | Name, email, phone, email/SMS opt-out. Shows a "pre-filled from your previous visit" banner |
| 3 | `Step3_VinLookupStep` | Previously-seen RVs; VIN entry; **mic → `ai/transcribe-issue`** cleaned by `VinTranscriptCleaner`; **camera → `ai/extract-vin`**, auto-filling at confidence ≥ 0.7 and auto-looking-up at ≥ 0.9; `decode-vin/{vin}` with a "Continue Anyway" fallback |
| 4 | `Step4_VehicleDetailsStep` | Manufacturer, model, year (editable after decode), extended warranty, approximate purchase date |
| 5 | `Step5_IssueDescriptionStep` | Mic → transcribe → `ai/refine-issue-text`; `ai/suggest-category` and `ai/suggest-insights` surfaced as "Suggested" chips; calls `assess-capabilities` on continue |
| 6 | `Step6_DiagnosticQuestionsStep` | `POST diagnostic-questions` → AI follow-ups, option chips plus free text, capability-mismatch warning |
| 7 | `Step7_AttachmentUploadStep` | Up to 10 files at 25 MB, buffered in-browser. Drop zone with an overlaid `InputFile` |
| 8 | `Step8_ReviewSubmitStep` | Review tables with per-section edit; `POST service-requests`; then per-file **direct-to-blob SAS upload** — request upload URL, `PUT` to SAS with `x-ms-blob-type: BlockBlob`, confirm. Tracks failed uploads |

Supporting pages: `Confirmation`, `Status` (confirmation-number entry), `StatusPage` (`/status/{Token}` → `GET api/status/{token}`), `Home`, `Error`, `NotFound`. `Intake.razor` at `/intake` is a static "use your dealer's link" notice and a dead end.

**Steps 3 and 5 depend on AI capability now specced as Spec A-9–A-12** — Whisper transcription, gpt-4o VIN extraction, urgency/usage insights, capability pre-check. All four are in scope (issue #429, closes Q8); nothing here is archived. See `RVS_Architecture.md`.

---

## Manager app — `RVS.Blazor.Manager`

Spec C calls for a deliberately thin app: list, detail, status, disposition, resend, per-location settings. **What exists is considerably more than that.**

| Route | Component | In Spec C scope |
|---|---|---|
| `/service-requests` | `ServiceRequestQueue` | Yes — but with a ten-field search panel (keyword, status, category, location, technician, bay, VIN, priority, two dates) against a specced "filter by status" |
| drawer | `ServiceRequestDetailDialog` (~1,800 lines) | Yes — detail, status select, inline edit, comment thread, attachment viewing via read-SAS, diagnostic responses |
| `/locations` | `Locations` | Yes — location CRUD, capability checkboxes, QR download |
| `/` | `Home` | Partly — embeds `OutcomeComplianceWidget`, which is archived scope |
| `/settings` | `Settings` | Partly — tenant-level config and access gate, not the per-location packet settings B-6 needs |
| `/service-requests/{id}/edit` | `ServiceRequestEdit` | Partly — largely duplicates the detail drawer and carries technician, bay and scheduled-date fields |
| `/board` | `ServiceBoard` + `BoardLayout` | **No** — Kanban with drag-drop status change |
| `/analytics` | `Analytics` | **No** — dashboard with summary cards and top-category tables |
| `/service-requests/batch-outcome` | `BatchOutcome` | **No** — bulk repair-outcome entry |
| `/claims-debug` | `ClaimsDebug` | **No** — self-labelled "remove before production" |
| `/authentication/{action}` | `Authentication` | Yes |

Missing from Spec C: **no resend action anywhere** (C-5), and **no disposition flow with a reason code** (C-4) — closing is just setting status to Completed or Cancelled. Status vocabulary is `New / InProgress / WaitingOnCustomer / WaitingOnParts / Completed / Cancelled`; the Spec (C-3 / C-8) was aligned to this set in issue #428, closing Q5.

Nothing in either app renders a packet, a PDF, an email preview, or a paste block. Searching the frontend for "packet" returns nothing.

---

## RVS.UI.Shared

Typed clients in `Services/`:

| Client | Targets |
|---|---|
| `IntakeApiClient` | All anonymous intake endpoints, plus `api/status/{token}` |
| `ServiceRequestApiClient` | Service request CRUD, `search`, `batch-outcome` |
| `AttachmentApiClient` | Upload, read-SAS, delete |
| `LookupApiClient` | Lookups, locations, QR code, dealerships, tenant config, access gate |
| `AnalyticsApiClient` | The analytics summary endpoint — **archived**, its only consumer is `Analytics.razor` |

Also `Validation/` (`ClientVinValidator`, `EmailValidator`, `VinTranscriptCleaner`, `ClientSearchInputSanitizer`) and `Components/` — `StatusBadge` and `PriorityBadge` are used; `AssetDisplay`, `AttachmentThumbnail` and `DiagnosticResponseView` are referenced nowhere.

**`ThemeService` is not in `RVS.UI.Shared`.** Each app has its own copy at `RVS.Blazor.{Intake,Manager}/Services/ThemeService.cs`. Documentation claiming otherwise is wrong.

`RVS.UI.Shared` and its test project are excluded from Debug builds in the solution and build only on demand.

---

## Descope backlog

Deleting these is the front-end half of aligning the code to the Overview.

**Manager** — `Analytics.razor`, `ServiceBoard.razor` + `BoardLayout.razor`, `BatchOutcome.razor`, `ClaimsDebug.razor`, `OutcomeComplianceWidget.razor`, and the matching `NavMenu` links. `ServiceRequestEdit.razor` should either absorb the detail drawer or go. The technician, bay and priority search filters lose meaning once the fields behind them are archived.

**Shared** — `AnalyticsApiClient`, `ServiceRequestApiClient.BatchOutcomeAsync`, and the three unreferenced components.

**Intake** — nothing is clearly dead. `/intake` is a dead-end page and `/` is unreachable from the normal slug flow.

---

## What B needs from the front end

Packet work is mostly server-side, but three front-end items fall out of it:

1. A packet preview in the manager detail view (C-2).
2. A resend control, to configured recipients or an ad-hoc address (C-5).
3. Per-location packet settings in `Locations` — recipients, attach-PDF, include-photos, paste-block cap, status-link TTL, logo (B-6, C-6).

The one-click email status links in C-7 need a confirmation page, but no app shell — that is the point of them.
