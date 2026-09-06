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
| `RVS.Infra.AzTablesRepository` | Tables access | **Dead** — see Known gaps |
| `RVS.Infra.AzCredentials` | Credential helper | **Dead** — referenced by two csproj, called by nothing |

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
| A-8 create + ledger + token + enqueue packet | **Partial** | Create, ledger and token are built. There is no packet to enqueue |
| **B-1 … B-7 packet and delivery** | **Not built** | No composition, no PDF, no manager email, no paste block. See below |
| C-1 list, filter | **Built** | Far heavier than specced — 10 search fields |
| C-2 detail + status + resend | **Partial** | Detail and status exist. **No resend** |
| C-3 set status | **Built** | Vocabulary differs from Spec — see Q5 |
| C-4 disposition + reason code | **Not built** | Only status → Cancelled |
| C-5 resend packet | **Not built** | |
| C-6 per-location settings | **Partial** | Location CRUD + capabilities exist; the B-6 packet settings do not |
| C-7 one-click email status links | **Not built** | |
| X-1 customer status page | **Built, different design** | See token model below |
| X-2 ledger write on submission | **Built** | `IntakeOrchestrationService` appends per intake, best-effort |
| X-3 anonymization license | **Paperwork** | Not a code item. Highest-leverage open item in the whole set |
| X-4 tenancy | **Built** | |
| X-5 tokens ≥128 bits, hashed, TTL | **Conflict** | Tokens are stored **unhashed** today |
| X-6 time-limited read SAS | **Built** | |

**B is genuinely greenfield.** Email exists, but it only ever sends a *customer confirmation* from the last intake step. Nothing emails a service manager. `Dealership.ServiceEmail` is populated and mapped but read by no code path.

---

## Two conflicts to resolve before building B

**1. Token model.** X-1/X-5 describe a per-request, hashed, TTL-bounded status token. What exists is a per-*customer* magic link: a 90-day token on `GlobalCustomerAcct` (partitioned by email, cross-tenant), stored unhashed, indexed for lookup. The prior ASOT decision explicitly chose unhashed storage and argued it was adequate. The Spec now says hashed. The Spec wins, but this is a code and data-migration change, not a doc edit — and the per-request vs per-customer question is the bigger of the two, because C-7's one-click action links reuse this machinery.

**2. Voice and vision AI are in the code but not in the Spec.** `ai/transcribe-issue` (Whisper) and `ai/extract-vin` (gpt-4o vision) are fully wired into intake steps 3 and 5, along with `ai/suggest-insights` and `assess-capabilities`. The Spec's A section does not mention them. They are either in scope and should be specced, or archived and should be removed — the Whisper account is unconditional infrastructure spend either way.

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
| Tenant access gate | `ITenantAccessRepository` has no implementation and its registration is commented out in `Program.cs`. `TablesAuditRepository` throws `NotImplementedException` from every method and is unregistered. The middleware runs; the backing store does not exist |
| `RVS.Infra.AzTablesRepository`, `RVS.Infra.AzCredentials` | Effectively dead projects |
| `build-mobile.yml` | Builds `RVS.MAUI.Tech`, which is not in the repo. The workflow cannot succeed |
| Prod Azure OpenAI | `publicNetworkAccess: Disabled` with no private endpoint declared — unreachable as written. See `RVS_Infrastructure.md` |
| `AzureTables--ConnectionString` | Built from an account key while `allowSharedKeyAccess=false` in every environment |
| Container naming | Bicep and seeder agree on 10 kebab-case containers. Older docs claimed 9 camelCase |

---

## Backend patterns

Controller, service, mapper, entity, DTO and DI conventions are specified in `/CLAUDE.md` and `.github/instructions/`. They are not restated here — that file is the one developers and agents actually load.
