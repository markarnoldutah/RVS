# RVS — Architecture

**Version:** 1.0 · September 4, 2026
**Scope:** The backend as it exists in this repository. Verified against source, not aspiration.

Product canon is `../RVS_Overview.md`, `../RVS_Spec.md`, `../RVS_Plan.md`. This document describes *what is built*. Where the code and the Spec disagree, the Spec is the target and this document records the gap.

---

## Solution layout

| Project | Role | Under new scope |
|---|---|---|
| `RVS.API` | ASP.NET Core 10 Web API — controllers, services, mappers, middleware, integrations | Core |
| `RVS.Domain` | Entities, DTOs, interfaces, validation. Zero infra dependencies | Core |
| `RVS.Infra.AzCosmosRepository` | Cosmos repositories | Core |
| `RVS.Infra.AzBlobRepository` | Blob storage, SAS generation | Core |
| `RVS.Blazor.Intake` | Anonymous intake WASM app | Core |
| `RVS.Blazor.Manager` | Authenticated manager WASM app | Core, needs descoping |
| `RVS.UI.Shared` | Typed API clients, validators, badge components | Core |
| `RVS.Data.Cosmos.Seed` | Idempotent container creation + test data | Core |

---

## Request flow

Intake (anonymous) and Manager (bearer token) both call `RVS.API`. Middleware order is load-bearing and lives in `RVS.API/Program.cs`:

1. Dev-only OpenAPI / Swagger UI
2. HTTPS redirection (non-dev)
3. CORS — named, environment-specific policy. Never `AllowAnyOrigin`
4. Rate limiter — `IntakeEndpoint` (20/min), `StatusEndpoint` (10/min)
5. `ExceptionHandlingMiddleware` — `IMiddleware`, singleton
6. Authentication → Authorization
7. `CorrelationLoggingMiddleware` — after auth so claims are populated
8. `TenantAccessGateMiddleware`
9. `/health` (anonymous)
10. `MapControllers()`

Controllers open with `_claimsService.GetTenantIdOrThrow()`, delegate to a sealed scoped service, and map entities to DTOs. No `try/catch` in controllers — exceptions map centrally (`ArgumentException` → 400, `UnauthorizedAccessException` → 401, `KeyNotFoundException` → 404, `MagicLinkExpiredException` → 410, else 500), returning `{ message, errorId }`.

Every Cosmos query is single-partition on `tenantId`. Cross-partition access is structurally prevented rather than policed.

---

## API surface as built

**Anonymous**

| Route | Notes |
|---|---|
| `GET /health` | — |
| `GET api/intake/{slug}/config` | Optional `?token=` magic link for returning-customer prefill |
| `POST api/intake/{slug}/service-requests` | Create |
| `POST api/intake/{slug}/service-requests/{srId}/attachments/upload-url` · `.../confirm` | Direct-to-blob SAS |
| `GET api/intake/{slug}/decode-vin/{vin}` | NHTSA vPIC |
| `POST api/intake/{slug}/ai/extract-vin` · `transcribe-issue` · `refine-issue-text` · `suggest-category` · `suggest-insights` | See AI surface below |
| `POST api/intake/{slug}/diagnostic-questions` · `assess-capabilities` | — |
| `GET api/status/{token}` | Customer status feed |

**Authenticated** (per-permission policies, not roles)

`api/dealerships/{dealershipId}/service-requests` — POST, GET `{srId}`, POST `search`, PUT `{srId}`, PATCH `batch-outcome`, DELETE `{srId}`
`.../service-requests/{srId}/attachments` — POST `upload-url`, POST `confirm`, GET `{attachmentId}/sas`, DELETE `{attachmentId}`
`api/dealerships` — GET, GET `{id}`, PUT `{id}`
`api/locations` — GET, GET `{id}`, POST, PUT `{id}`, GET `{id}/qr-code`
`api/lookups/{category}` · `api/tenants/config` (POST/GET/PUT) · `api/tenants/access-gate`
`api/dealerships/{dealershipId}/analytics/service-requests/summary`

Note: the `{dealershipId}` route segment is decorative. Scoping always comes from the token, never the URL.

---

## Integrations

One flag, `Integrations:UseMocks`, read once at startup. **It is `false` in every appsettings file, including Development.** The practical fallback is secondary: if an endpoint setting is absent, registration silently degrades to the rule-based or no-op implementation.

| Capability | Real | Fallback |
|---|---|---|
| VIN decode | NHTSA vPIC | — (graceful failure) |
| VIN extraction from photo | Azure OpenAI gpt-4o vision | Mock |
| Speech-to-text | Azure OpenAI Whisper (northcentralus) | Mock |
| Issue-text refinement | Azure OpenAI | `RuleBasedIssueTextRefinementService` (deliberately thin) |
| Categorization + diagnostic questions | Azure OpenAI | `RuleBasedCategorizationService` |
| Email | Azure Communication Services | NoOp |
| SMS | Azure Communication Services | NoOp — **archived scope** |

All external clients use `AddStandardResilienceHandler` with per-client timeouts.

---

## Coverage against the Spec

This is the honest state of `../RVS_Spec.md`.

| Req | Status | Notes |
|---|---|---|
| A-1 anonymous + rate limit | **Built** | |
| A-2 collects contact, VIN, description, media | **Built** | 8-step wizard, not 7 |
| A-3 VIN decode, graceful degrade | **Built** | "Continue Anyway" path exists |
| A-4 AI follow-up questions | **Built** | |
| A-5 AI category suggestion | **Built** | |
| A-6 attachments, SAS direct upload | **Built** | Binaries never transit the API |
| A-7 returning-customer prefill | **Built** | Keyed off the magic-link token, not email alone |
| A-8 create + ledger + token + enqueue packet | **Built** | Create, ledger, token, and packet enqueue (`IntakeOrchestrationService` step 8) all built. Enqueue is non-blocking; issue #434 |
| B-1 packet generation | **Built** | `IPacketGenerationQueue` (in-process channel) → `PacketGenerationWorker` → `PacketGenerationService`: compose, render HTML + PDF, store PDF, attempt-tracked on `ServiceRequest.packetGeneration`, 3-strikes `LogCritical` alert, on-demand regen endpoint. Issue #434 |
| B-2 … B-7 packet contents, render, delivery | **Partial** | Composition (#430), HTML (#431), PDF (#432), photo SAS (#433), generation (#434), DMS paste block (#436), packet email send (#437), delivery idempotency + retry/backoff (#438) built. Per-location packet config on `Location.packetConfig` + `api/locations` (#435) built; paste-block cap consumed by #436, and `enabled` / `recipients` / `attachPdf` / `includePhotos` consumed by #437. #437 emails the HTML packet (paste-block text fallback) with PDF + photo attachments to the configured recipients via ACS from `PacketGenerationService` after a successful generation; #438 makes that send idempotent per `(serviceRequestId, packetVersion)` and retries it three times with exponential backoff then a `LogCritical` alert, tracked on `ServiceRequest.packetEmailDelivery`. #439 adds hard-bounce recipient disabling at the domain + service layer (`PacketConfigEmbedded.disabledRecipients[]`, `LocationService.DisableRecipientForBounceAsync` / `ReEnableRecipientAsync`: disable one address, notify the rest, `LogWarning`/`LogCritical`) — the inbound bounce signal that would invoke it is not wired yet. See `RVS_PacketComposition.md` |
| C-1 list, filter | **Built** | Far heavier than specced — 10 search fields |
| C-2 detail + status + resend | **Partial** | Detail and status exist. **No resend** |
| C-3 set status | **Built** | Vocabulary matches Spec C-3 / C-8 (aligned to code in issue #428) |
| C-4 disposition + reason code | **Not built** | Only status → Cancelled |
| C-5 resend packet | **Not built** | |
| C-6 per-location settings | **Partial** | Location CRUD + capabilities + B-6 `packetConfig` (recipients, attach-PDF, include-photos, paste-block cap, status-link TTL, logo) read/written via `api/locations` (#435). No manager-app settings UI yet |
| C-7 one-click email status links | **Not built** | |
| X-1 customer status page | **Built, different design** | See token model below |
| X-2 ledger write on submission | **Built** | `IntakeOrchestrationService` appends per intake, best-effort |
| X-3 anonymization license | **Paperwork** | Not a code item. Highest-leverage open item in the whole set |
| X-4 tenancy | **Built** | |
| X-5 tokens ≥128 bits, hashed, TTL | **Status token built (#440); C-7 links pending** | `AnonymousTokenHelper` mints ≥128-bit base64url tokens; the per-customer status token is stored as `magicLinkTokenHash` (SHA-256, raw never persisted), 30-day TTL with sliding renewal, every validation audit-logged, both anonymous limiters partitioned per IP. Per-request C-7 links reuse the helper but are not built. Plaintext migration in #441 |
| X-6 time-limited read SAS | **Built** | |
| A-9 voice input (Whisper transcription) | **Built** | `ai/transcribe-issue`, steps 3 and 5; `VinTranscriptCleaner` on the VIN field. Specced in issue #429 |
| A-10 VIN from photo (gpt-4o vision) | **Built** | `ai/extract-vin`, step 3; auto-fill ≥ 0.7, auto-decode ≥ 0.9. Specced in issue #429 |
| A-11 issue insights (urgency, RV usage) | **Built** | `ai/suggest-insights`, step 5; persisted on `ServiceRequest` with provider/confidence. Specced in issue #429 |
| A-12 capability pre-check | **Built** | `assess-capabilities`, step 5 → 6 boundary; non-blocking alert. Specced in issue #429 |

**B is built through idempotent, retried email delivery; hard-bounce disabling is built at the domain + service layer with its inbound signal still to wire.** Composition, both renderers, photo SAS, generation orchestration (#430–#434), the DMS paste block (#436), the packet email send (#437), and delivery idempotency + retry/backoff (#438) are in. The paste block is `PasteBlockGenerator` (`RVS.Domain/Packets/`) — a fenced, ASCII-safe block ordered category → verbatim description → status link, with the description truncated at a word boundary to the location's `pasteBlockCharacterCap`; `PacketGenerationService` assembles it into `PacketCompositionContext.PasteBlock`. #437 emails a service manager: after a successful generation `PacketGenerationService` builds a `PacketEmailMessage` with `PacketEmailComposer` (`RVS.Domain/Packets/`, pure) and sends it through `INotificationService.SendPacketEmailAsync` on the existing ACS integration — subject `[RVS] {category} — {year} {make} {model} — {last name}`, packet HTML inline with the paste block as the text-only alternative, PDF + original photos attached per `packetConfig`, to `packetConfig.recipients`. It no-ops when the location config is absent, disabled, or has no recipients. #438 wraps that send in a delivery state machine on `ServiceRequest.packetEmailDelivery` (`PacketEmailDeliveryEmbedded`): skip if the current `packetVersion` is already recorded delivered (idempotent per `(serviceRequestId, packetVersion)`); otherwise up to 3 attempts with an exponential backoff (`PacketEmailOptions.RetryBaseDelay`, default 2 s, doubled each retry), every attempt logged under a `CorrelationId` scope, then one `LogCritical` alert on exhaustion. A delivery failure never fails generation. `Dealership.ServiceEmail` is still populated and mapped but read by no code path — recipients live on `Location.packetConfig` (#435). #439 adds `packetConfig.disabledRecipients[]` and `LocationService.DisableRecipientForBounceAsync` / `ReEnableRecipientAsync`: a hard bounce parks one address (never the whole config), the remaining recipients are notified, and losing the last recipient raises a `LogCritical` for App Insights; the disabled list survives a settings save and re-enables when its address is re-added to `recipients`. What remains for #439: the inbound ACS delivery-report path that would call the disable method. The `statusLinkTtlDays` and `logoUrl` config fields also still await their consumers.

---

## Conflicts to resolve before building B

**1. Token model — resolved (issue #427, closes Q7).** X-5 is met by: SHA-256-hashed storage with the raw token never persisted; the **status token staying per-customer** on `GlobalCustomerAcct` (TTL cut to ≤ 30 days, sliding renewal on use); and **C-7 one-click action links being per-request and per-action** (single-purpose, short fixed TTL or single-use). Both scopes share one generation / hash / TTL / audit helper — the "same machinery" the Plan calls for, at the X-5 bar. The prior ASOT decision that chose unhashed storage is overturned: its own stated trigger — a token that can write — is met by C-7. Migration (#441): backfill hashes from the current plaintext pre-GA, then drop the plaintext `magicLinkToken` field; issued links keep working.

**Status: the shared helper (`RVS.Domain/Security/AnonymousTokenHelper.cs`) and the per-customer status token are built (#440)** — hashed `magicLinkTokenHash` storage, 30-day sliding-renewal TTL, per-attempt audit logging, per-IP rate limiting. Intake re-mints the status token each submission. The C-7 per-request/per-action links and the plaintext data migration (#441) are still open.

**2. Voice and vision AI — resolved (issue #429, closes Q8).** `ai/transcribe-issue` (Whisper), `ai/extract-vin` (gpt-4o vision), `ai/suggest-insights`, and `assess-capabilities` are all in scope and are now specced as Spec A-9–A-12. Nothing archived; no descope sub-issue on #423. Each keeps its rule-based / no-op fallback and none blocks submission. The Whisper and gpt-4o accounts stay but move behind a `deployWhisper` (and gpt-4o) flag in issue #467, defaulted on, so the spend is per-environment and reversible.

---

## Descope backlog

Built for capability the Overview archives. Deleting this is real work and is not currently in the build order.

- **Analytics** — `AnalyticsController`, `AnalyticsService`, `IAnalyticsService`, `GetForAnalyticsAsync`, `ServiceRequestAnalyticsResponseDto`
- **Technician outcome workflow** — `ServiceEventEmbedded`, `PATCH batch-outcome`, `BatchOutcome*Dto`, `AssetLedgerEntry.Section10A`
- **Scheduling / assignment fields** on `ServiceRequest` — `assignedTechnicianId`, `assignedBayId`, `scheduledDateUtc`, `requiredSkills`, `boardSequence`
- **Messaging** — `MessageEmbedded` is defined and referenced nowhere
- **SMS** — `AcsSmsNotificationService`, `ISmsNotificationService`, `NoOpSmsNotificationService`, opt-out plumbing
- **Never-called** — `NotificationOrchestrator.SendStatusChangeAsync`, `SendMagicLinkAsync`
- **Scaffolding** — `WeatherForecastController`, `WeatherForecast.cs`
- **`rv-warranty-rules`** — seeded, no repository, never read

---

## Known gaps and defects

| Item | Detail |
|---|---|
| Tenant access gate | `TenantAccessGateMiddleware` reads `LoginsEnabled` from Cosmos `tenant-configs` via `ITenantConfigService.GetAccessGateAsync` and returns 403 for a disabled tenant. The dead `ITenantAccessRepository` interface and the `RVS.Infra.AzTablesRepository` / `RVS.Infra.AzCredentials` projects (plus the `AzureTables--ConnectionString` secret) were removed in issue #462. Remaining work is the end-to-end "disabled tenant → 403" test tracked in #465 |
| `build-mobile.yml` | Builds `RVS.MAUI.Tech`, which is not in the repo. The workflow cannot succeed |
| Prod Azure OpenAI | `publicNetworkAccess: Disabled` with no private endpoint declared — unreachable as written. See `RVS_Infrastructure.md` |
| Container naming | Bicep and seeder agree on 10 kebab-case containers. Older docs claimed 9 camelCase |

---

## Backend patterns

Controller, service, mapper, entity, DTO and DI conventions are specified in `/CLAUDE.md` and `.github/instructions/`. They are not restated here — that file is the one developers and agents actually load.
