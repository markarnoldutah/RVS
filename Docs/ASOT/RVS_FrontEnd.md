# RVS — Front End

**Version:** 1.0 · September 4, 2026
**Scope:** Both Blazor WASM apps and the shared client library, as built.

Stack: Blazor WebAssembly, **MudBlazor 9.x** (Material Design 3). Do not introduce `Microsoft.FluentUI.AspNetCore.Components`. Component conventions, theming rules and the MudBlazor v9 breaking-change list are in `/CLAUDE.md`.

---

## Intake app — `RVS.Blazor.Intake`

Anonymous. No auth packages, no token handler, plain `HttpClient`, plain `Router`. Keep it that way.

Shell is `Pages/IntakeWizard.razor`, routed at `/{Slug}` with an optional `?token=` magic link and an optional `?src=` channel tag (`Spec A-13`, `#599` — put there by the `go.rvintake.com` redirect). Wizard state lives in `State/IntakeWizardState.cs` and persists to sessionStorage across reloads; `src` rides along on `IntakeWizardState.IntakeSource` and is sent with the submission, where the API normalises it. A `src` on the current URL wins over a restored one — the link just used is the truer account of how the customer got here — but a bare reload never erases the channel of the link that started the session.

**Invite prefill (`Spec A-14`, `#664`).** An advisor's texted link lands with `?src=advisor&inv={token}`. The shell puts `inv` on `IntakeWizardState.InviteToken` (persisted, and replaced only by a different `inv` on the URL, same rule as `src`). After loading config, Step 1 calls `GET api/intake/{slug}/invites/{token}` and hands the result to `IntakeWizardState.ApplyInvitePrefill`, which fills **first name and phone only, and only where blank**, then sets `IsInvitePrefilled`. That is deliberately separate from A-7's returning-customer path (`?token=` → `ApplyPrefill` → `IsPrefilled` and its "pre-filled from your previous visit" banner): an invite knows only what the advisor typed. Opening does not spend the invite (link previews fetch the URL too); the token goes out with the submission and the API spends it there. Any failure (404 for expired / used / unknown, 429, network) is swallowed by `IntakeApiClient.GetInvitePrefillAsync` returning `null` or by Step 1's catch, so the customer gets the blank form, never an error.

**The wizard has eight steps, not seven.** Older documentation said seven; the `switch` in the shell has eight and the header reads "Step N of 8". All eight are fully implemented — there are no placeholder steps.

| Step | Component | Does |
|---|---|---|
| 1 | `Step1_IntakeLanding` | `GET api/intake/{slug}/config`; applies returning-customer prefill (customer, asset, known assets); then, with an `inv`, `GET api/intake/{slug}/invites/{token}` → invite prefill (first name, phone); handles expired token and invalid slug |
| 2 | `Step2_CustomerInfoStep` | Name, email, phone, then Notification Preferences (text/email opt-outs, texting disclosure linking `/sms-terms`) above Preferred contact method. An opt-out disables its radio and clears a conflicting selection (`Spec A-2`, `#662`). Shows a "pre-filled from your previous visit" banner |
| 3 | `Step3_VinLookupStep` | Previously-seen RVs; VIN entry; **mic → `ai/transcribe-issue`** cleaned by `VinTranscriptCleaner`; **camera → `ai/extract-vin`**, auto-filling at confidence ≥ 0.7 and auto-looking-up at ≥ 0.9; `decode-vin/{vin}` with a "Continue Anyway" fallback |
| 4 | `Step4_VehicleDetailsStep` | Manufacturer, model, year (editable after decode), extended warranty, approximate purchase date |
| 5 | `Step5_IssueDescriptionStep` | Mic → transcribe → `ai/refine-issue-text`; `ai/suggest-category` and `ai/suggest-insights` surfaced as "Suggested" chips; calls `assess-capabilities` on continue |
| 6 | `Step6_DiagnosticQuestionsStep` | `POST diagnostic-questions` → AI follow-ups, option chips plus free text, capability-mismatch warning |
| 7 | `Step7_AttachmentUploadStep` | Up to 10 files at 25 MB, buffered in-browser. Drop zone with an overlaid `InputFile` |
| 8 | `Step8_ReviewSubmitStep` | Review tables with per-section edit; `POST service-requests`; then per-file **direct-to-blob SAS upload** — request upload URL, `PUT` to SAS with `x-ms-blob-type: BlockBlob`, confirm. Tracks failed uploads |

Supporting pages: `Confirmation`, `Status` (confirmation-number entry), `StatusPage` (`/status/{Token}` → `GET api/status/{token}`; shows unit, submitted date, status sentence, location `tel:` link, and the manager-authored status note when present — C-9, #500), `Home`, `Error`, `NotFound`. `Intake.razor` at `/intake` is a static "use your dealer's link" notice and a dead end.

**Steps 3 and 5 depend on AI capability now specced as Spec A-9–A-12** — Whisper transcription, gpt-4o VIN extraction, urgency/usage insights, capability pre-check. All four are in scope (issue #429, closes Q8); nothing here is archived. See `RVS_Architecture.md`.

---

## Manager app — `RVS.Blazor.Manager`

Spec C calls for a deliberately thin app: list, detail, status, disposition, resend, per-location settings. **What exists is considerably more than that.**

| Route | Component | In Spec C scope |
|---|---|---|
| `/service-requests` | `ServiceRequestQueue` | Yes — but with a ten-field search panel (keyword, status, category, location, technician, bay, VIN, priority, two dates) against a specced "filter by status" |
| drawer | `ServiceRequestDetailDialog` (~1,800 lines) | Yes — detail, status select, customer status note (C-9, #500), inline edit, comment thread, attachment viewing via read-SAS, diagnostic responses |
| `/locations` | `Locations` | Yes — location CRUD, capability checkboxes, QR download, and a **Send intake link** row action (A-14, #666) |
| `/` | `Home` | Yes — welcome, `LocationSelector`, and the **Send intake link** button beside it (A-14, #666) |
| dialog | `SendIntakeLinkDialog` | Yes — A-14 (#666), see below |
| `/settings` | `Settings` | Partly — tenant-level config and access gate, not the per-location packet settings B-6 needs |
| `/service-requests/{id}/edit` | `ServiceRequestEdit` | Partly — largely duplicates the detail drawer and carries technician, bay and scheduled-date fields |
| `/board` | `ServiceBoard` + `BoardLayout` | Yes — kept, not a descope target (#456 closed `not_planned`; Plan decision log Sep 21 2026). Kanban with drag-drop status change (C-3), ordered within a column by `boardSequence`. It is the manager app's landing page: lands on an **Actionable today** view (`ActionableRequestFilter`: open requests plus anything closed today, toggleable), opens the detail drawer from `?sr={id}` (#498), and is the PWA start URL. Not to be extended |
| `/sr/{id}` | `ServiceRequestDeepLink` | Yes — C-7 (#498). Packet-email landing: without `action` it forwards to `/board?sr={id}`; with `?action=in-progress\|waiting-on-parts\|completed` (`ManagerDeepLinks`) it shows a one-tap confirm and writes through the authenticated update endpoint. Nothing is written on page load |
| `/analytics` | `Analytics` | **No** — dashboard with summary cards and top-category tables |
| `/claims-debug` | `ClaimsDebug` | **No** — self-labelled "remove before production" |
| `/authentication/{action}` | `Authentication` | Yes |

**Send intake link (`Spec A-14`, `#666`).** `Shared/SendIntakeLinkDialog.razor`, opened from the button beside `LocationSelector` on `Home` (shown once a location is selected) and from the SMS icon in the `Locations` row actions. On open it reads `GET …/intake-invites/capability` and the advisor's recent sends; it reloads both on every open, since delivery reports land while it is closed. First name and mobile number, then a consent box labelled after the published script (`Docs/Guides/Advisor_Text_Consent_Script.md`), with the script itself — store name and typed number filled in — in a **What to say** panel under it. **Send** is disabled until the box is ticked and the number normalises through `PhoneNumberNormalizer`. While `Sms:Enabled` is off, the dialog shows a *Texting is not yet enabled* alert and no Send button, and hides the consent box. **Fill it in myself** mints a self-entry invite and opens the returned `intakeUrl` in a new tab through `rvs_openInNewTab` (`js/download.js`); if a popup blocker refuses it — the call lands after an await — the URL is shown in a `CopyField` instead. The **Sent this shift** list labels each invite through `IntakeInviteStatusFormatting` (*Sending… / Sent / Delivered / Not delivered / Not texted*, outranked by *Form submitted* and *Expired*) and re-reads every 4 s, up to 15 times, while any invite is still `pending` or `queued`. **Resend** does not send: it puts that invite's name and number back in the form, and the advisor ticks consent and taps Send, because a new token is a new invite with its own consent record and the list spans the shift, not one call. The manager app cannot see `permissions` (Auth0 RBAC writes them to the access token only, never the id token), so a user without `intake-invites:send` sees the button and gets a *your role doesn't have permission* alert from the first 403.

Missing from Spec C: **no resend action anywhere** (C-5), and **no disposition flow with a reason code** (C-4) — closing is just setting status to Completed or Cancelled. Status vocabulary is `New / InProgress / WaitingOnCustomer / WaitingOnParts / Completed / Cancelled`; the Spec (C-3 / C-8) was aligned to this set in issue #428, closing Q5.

Nothing in either app renders a packet, a PDF, an email preview, or a paste block. Searching the frontend for "packet" returns nothing.

**Installable PWA and persistent session (#498).** The manager app ships `manifest.webmanifest` (start URL `/board`), `icon-192.png` / `icon-512.png` (copied from the intake app), and a **network-only** `service-worker.js` — it makes the app installable and caches nothing, since offline use is out of scope and a cached build would go stale after deploy. HTTP caching is set in `staticwebapp.config.json`: fingerprinted `_framework/*` files are `immutable`, but `_framework/dotnet.js` (which embeds the boot manifest naming that build's assemblies) and `_framework/blazor.webassembly.js` keep the same name every build, so they — and the unfingerprinted `_content/*` assets — are `no-cache`. Marking them immutable made a soft refresh after a deploy start the previous build, whose router lacked newly added pages ("Page Not Found"). `js/session-persist.js` keeps the sign-in across browser and PWA restarts; see "Manager app authentication" in `RVS_Identity.md`.

---

## RVS.UI.Shared

Typed clients in `Services/`:

| Client | Targets |
|---|---|
| `IntakeApiClient` | All anonymous intake endpoints, plus `api/status/{token}` |
| `ServiceRequestApiClient` | Service request CRUD, `search` |
| `AttachmentApiClient` | Upload, read-SAS, delete |
| `LookupApiClient` | Lookups, locations, QR code, dealerships, tenant config, access gate |
| `IntakeInviteApiClient` | `api/locations/{locationId}/intake-invites` — send, recent sends, one invite, `capability` (A-14, #666). Refusals surface as `IntakeInviteApiException` carrying the API's ProblemDetails `detail`, or a fixed sentence for a body-less 403 |
| `AnalyticsApiClient` | The analytics summary endpoint — **archived**, its only consumer is `Analytics.razor` |

Also `Validation/` (`ClientVinValidator`, `VinTranscriptCleaner`, `ClientSearchInputSanitizer`; the intake contact rules, `EmailValidator` and `PhoneValidator`, live in `RVS.Domain/Validation` so the API applies the same ones, #679) and `Components/` — `StatusBadge`, `PriorityBadge` and `IntakeInviteStatusFormatting` (a MudBlazor-free label/tone helper for the send dialog) are used; `AssetDisplay`, `AttachmentThumbnail` and `DiagnosticResponseView` are referenced nowhere.

**`ThemeService` is not in `RVS.UI.Shared`.** Each app has its own copy at `RVS.Blazor.{Intake,Manager}/Services/ThemeService.cs`. Documentation claiming otherwise is wrong.

`RVS.UI.Shared` and its test project are excluded from Debug builds in the solution and build only on demand.

---

## Descope backlog

Deleting these is the front-end half of aligning the code to the Overview.

**Manager** — `Analytics.razor`, `ClaimsDebug.razor`, and the matching `NavMenu` link. `ServiceRequestEdit.razor` should either absorb the detail drawer or go. The technician, bay and priority search filters lose meaning once the fields behind them are archived.

**Shared** — `AnalyticsApiClient` and the three unreferenced components.

**Intake** — nothing is clearly dead. `/intake` is a dead-end page and `/` is unreachable from the normal slug flow.

---

## What B needs from the front end

Packet work is mostly server-side, but three front-end items fall out of it:

1. A packet preview in the manager detail view (C-2).
2. A resend control, to configured recipients or an ad-hoc address (C-5).
3. Per-location packet settings in `Locations` — recipients, attach-PDF, include-photos, paste-block cap, status-link TTL, logo (B-6, C-6).

The one-click email status links in C-7 need a confirmation page, but no app shell — that is the point of them.
