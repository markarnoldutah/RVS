# RV Service Flow (RVS) — Technical PRD

**Version:** 1.0
**Date:** March 20, 2026
**Status:** Draft
**Derived from:** RVS_Core_Architecture_Version3.1.md (ASOT)

---

## Companion Documents

| Document | Purpose |
|---|---|
| [RVS_Core_Architecture_Version3.1.md](RVS_Core_Architecture_Version3.1.md) | Domain model, data layer, orchestration flows, API surface |
| [RVS_Auth0_Identity_Version2.md](RVS_Auth0_Identity_Version2.md) | RBAC model, JWT structure, ClaimsService, `app_metadata` tenant scoping |
| [RVS_Context.md](RVS_Context.md) | Platform overview, business model, investor/partner context |
| [RVS_PRD.md](RVS_PRD.md) | Product goals, user personas, user stories (v1.1) |
| [RVS_implementation_plan.md](RVS_implementation_plan.md) | 8-phase build roadmap |
| [.github/copilot-instructions.md](../../../.github/copilot-instructions.md) | Coding conventions, project patterns |

---

## 1. Document Purpose

This Technical PRD translates the architecture decisions captured in `RVS_Core_Architecture_Version3.1.md` into explicit, testable technical requirements. It specifies:

- Non-functional requirements and acceptance thresholds
- Per-endpoint API contracts (request/response shapes, auth, errors)
- Data model specifications and constraints
- Security requirements and threat mitigations
- Integration contracts for external services
- Observability, testing, and deployment requirements
- Explicit constraints, known gaps, and deferred items

This document is authoritative for implementation. Anything not covered here defers to the architecture ASOT.

---

## 2. System Overview

### 2.1 Platform Summary

RVS is a B2B SaaS platform for RV dealership service management. It digitizes the customer intake workflow that currently relies on phone calls and manual notes.

**Stack:**
- **Backend:** ASP.NET Core (.NET 10, C# 14), RESTful API, OpenAPI/Swagger
- **Database:** Azure Cosmos DB (SQL API, 9 containers)
- **Storage:** Azure Blob Storage (attachments)
- **Identity:** Auth0 (JWT Bearer; `app_metadata` tenant scoping)
- **AI:** Azure OpenAI (`gpt-4o-mini`) for issue categorization and diagnostic questions
- **Notifications:** SendGrid (behind `INotificationService`)
- **Frontend:** Blazor WebAssembly (Blazor.Intake + Blazor.Manager), MAUI Blazor Hybrid (MAUI.Tech); UI component library: **MudBlazor 9.x** (Material Design 3)

### 2.2 Multi-Tenancy Model

```
Tenant  = Corporation (e.g., Blue Compass RV)
         → 1 Cosmos DB partition (key: tenantId from app_metadata)
         → 1..N physical Locations per corporation

Customer = Anonymous (MVP); no Auth0 account required
         → Shadow profiles auto-created in: GlobalCustomerAcct + CustomerProfile
```

**Partition key invariant:** Every dealer operation is single-partition on `tenantId`. Cross-partition queries are structurally prevented in the data access layer.

### 2.3 Applications

| App | Framework | Users |
|---|---|---|
| `RVS.Blazor.Intake` | Blazor WebAssembly (Standalone PWA) | RV owners (anonymous) |
| `RVS.Blazor.Manager` | Blazor WebAssembly (Standalone) | Advisors, managers, corporate admins |
| `RVS.MAUI.Tech` | MAUI Blazor Hybrid (iOS + Android) | Technicians (offline-first) |
| `RVS.UI.Shared` | Razor Class Library | Shared components, API clients |

---

## 3. Goals and Success Metrics

### 3.1 Technical Goals

| Goal | Target | Measurement |
|---|---|---|
| Intake end-to-end latency (P95) | < 3 seconds (API round-trip from form submit to 201) | APM trace on `POST api/intake/{slug}/service-requests` |
| Intake form completion time | < 3 minutes average | Client-side telemetry (step timings) |
| Technician job close interaction | < 5 seconds (P95 for `PUT` from offline queue flush) | APM trace |
| API availability | ≥ 99.5% monthly uptime (MVP) | Azure App Insights availability tests |
| Anonymous intake error rate | < 1% of submissions result in 5xx | APM error rate dashboard |
| Cosmos RU cost per intake | ≤ 12 RU (cold path) | Cosmos diagnostics on intake controller |
| Magic-link status page load | < 500 ms TTFB (P99) | Lighthouse / App Insights |
| AI diagnostic question latency | < 1.5 seconds (P95) | APM trace on `ICategorizationService.GenerateDiagnosticQuestionsAsync` |
| Attachment upload (25 MB file) | < 10 seconds (P95; direct-to-blob via SAS) | Client telemetry |

### 3.2 MVP Ship Criteria

- 5 design partner dealerships complete full intake → advisor dashboard → technician update cycle without bugs
- Intake form usable on Safari iOS, Chrome Android, Chrome/Edge Windows
- Zero `platform:admin` intervention required to onboard a new dealership
- All OWASP Top 10 vectors addressed (see Section 9)

---

## 4. Personas and Interaction Surfaces

### 4.1 Personas

| Persona | Auth | Primary Surface | Key Actions |
|---|---|---|---|
| RV Owner | Anonymous (magic-link only) | `Blazor.Intake` WASM | Submit intake, check status |
| Service Advisor | Auth0 JWT | `Blazor.Manager` | Search/filter SRs, update status, add notes |
| Service Manager | Auth0 JWT | `Blazor.Manager` | Service Board drag-drop, batch outcomes, analytics |
| Regional Manager | Auth0 JWT | `Blazor.Manager` | Cross-location SR view, regional analytics |
| Corporate Admin | Auth0 JWT | `Blazor.Manager` | User management, all locations, all analytics |
| Technician | Auth0 JWT | `MAUI.Tech` | View assigned jobs, record Section 10A, photo capture |
| Platform Admin | Auth0 JWT | Direct API / future admin UI | Tenant provisioning, global lookups |

### 4.2 Intake URL Structure

```
https://app.rvserviceflow.com/intake/{locationSlug}   — Location intake portal
https://app.rvserviceflow.com/status/{token}           — Customer status page
https://app.rvserviceflow.com/                         — Platform landing (dealer search)
```

---

## 5. Functional Requirements

### 5.1 Customer Intake Flow

**FR-INTAKE-01 — Anonymous access**
The intake wizard at `/intake/{locationSlug}` MUST require no authentication. The `POST api/intake/{slug}/service-requests` endpoint MUST be annotated `[AllowAnonymous]`. Any authenticated call attempting to bypass rate limiting MUST be rejected.

**FR-INTAKE-02 — Intake orchestration sequence (7 steps)**
On receipt of a valid `ServiceRequestCreateRequestDto`, the API MUST execute the following atomically-ordered sequence:
1. Resolve `GlobalCustomerAcct` by email (create if absent)
2. Resolve or create `CustomerProfile` within `tenantId`
3. Resolve asset ownership (deactivate prior owner if VIN transferred)
4. Create `ServiceRequest` with embedded customer snapshot, AI categorization, and technician summary
5. Append `AssetLedgerEntry` (write-once, non-blocking on failure)
6. Update linkages (increment request count, rotate magic-link token)
7. Fire-and-forget confirmation email via `INotificationService`

Steps 1–6 MUST complete before returning `201`. Step 7 MUST NOT block the response.

**FR-INTAKE-03 — VIN decoding**
The intake API MUST call the NHTSA vPIC API (`https://vpic.nhtsa.dot.gov/api/`) to decode make, manufacturer, model year, and asset type from a submitted VIN. Decoded values MUST be stored in `AssetInfoEmbedded`. On NHTSA API failure or invalid VIN, the intake submission MUST still succeed with partial asset info (customer-supplied make/model/year).

**FR-INTAKE-04 — Attachment handling**
- Accepted MIME types: `image/jpeg`, `image/png`, `video/mp4`, `audio/m4a`, `audio/wav`
- Maximum attachments per SR: 10
- Maximum file size: configurable per location in `IntakeFormConfigEmbedded.MaxFileSizeMb` (default: 25 MB)
- Upload mechanism: customer-facing intake uses direct Azure Blob SAS upload (customer → Blob Storage, binary never transits API)
- Authenticated staff upload (technicians, advisors): API receives binary, streams to Blob
- Access: time-limited read SAS URIs (1-hour expiry), generated per request, never stored

**FR-INTAKE-05 — Diagnostic questions (AI wizard)**
- `POST api/intake/{slug}/diagnostic-questions` MUST call `ICategorizationService.GenerateDiagnosticQuestionsAsync` returning 2–4 contextual questions
- If Azure OpenAI is unavailable: return hardcoded fallback questions for the requested category (no 500)
- `diagnosticResponses` submitted with the SR MUST be embedded in `ServiceRequestEmbedded.DiagnosticResponses`
- The categorization step (Step 4) MUST use `diagnosticResponses` if present to improve accuracy

**FR-INTAKE-06 — Returning customer prefill**
On customer submission with a known email address, the intake API MUST:
- Return `isReturningCustomer: true` and `priorRequestCount` in the 201 response
- Prefill first name, last name, and phone in the intake form (fetched via `GET api/intake/{slug}?token={magicLinkToken}`)
- Offer known active VINs for one-tap selection (from `GlobalCustomerAcct.AssetsOwned`)

**FR-INTAKE-07 — Slug validation**
If `{locationSlug}` does not match a record in `slugLookup`, the API MUST return `404` with a `ProblemDetails` body. If the matched tenant's `TenantConfig.AccessGate.IsEnabled = false` or status is `Disabled`, the API MUST return `403`.

### 5.2 Customer Status Page

**FR-STATUS-01 — Magic-link validation**
`GET api/status/{token}` MUST:
1. Parse the email-hash prefix from the token to derive the partition key
2. Execute a single-partition point read on `globalCustomerAccts`
3. Validate token matches, is not expired, and `isActive = true`
4. Return `404` for invalid/missing tokens; `410 Gone` for expired tokens

**FR-STATUS-02 — Cross-dealer SR visibility**
The authenticated response MUST return all active service requests linked to the customer's `GlobalCustomerAcct` across all corporations. Response fields per SR: location name, dealership name, status, issue category, submission date, last updated date. No PII beyond first name and asset summaries MUST be exposed.

**FR-STATUS-03 — Rate limiting**
`api/status/{token}` MUST be limited to 10 requests/minute per IP. `api/intake/{slug}/service-requests` MUST be limited to 20 requests/minute per IP. Rate limit responses MUST return `429` with a `Retry-After` header.

### 5.3 Service Manager Dashboard

**FR-DASH-01 — SR search**
`POST api/dealerships/{id}/service-requests/search` MUST support filtering by: `status`, `issueCategory`, `locationId`, `assignedTechnicianId`, `assignedBayId`, `assetId`, `keyword` (customer name, VIN, description snippet), `dateFrom`/`dateTo`, `priority`. MUST return `PagedResult<ServiceRequestSummaryResponseDto>` with page size capped at 100.

**FR-DASH-02 — Batch outcome**
`PATCH api/dealerships/{id}/service-requests/batch-outcome` MUST apply a shared repair outcome to up to 25 service requests in one call. MUST validate all SR IDs belong to the caller's tenant before writing.

**FR-DASH-03 — Service Board updates**
**MVP:** The `Blazor.Manager` Service Board MUST use long polling (periodic `GET` or `POST search` calls) to detect status-change events. Polling interval MUST be configurable (default: 5 minutes). On technician SR update, the Service Board MUST reflect the change within one polling cycle.
**vNEXT:** A dedicated SignalR hub MUST replace long polling to push status-change events to all connected `Blazor.Manager` sessions within a tenant. On technician SR update, all connected sessions for that tenant MUST receive the update within 5 seconds.

**FR-DASH-04 — Analytics**
`GET api/dealerships/{id}/analytics/service-requests/summary` MUST return `ServiceRequestAnalyticsResponseDto` covering: total requests, by status, by category, by location, top failure modes, top repair actions, average repair time, top parts used, average days to complete. Supports optional `?from`, `?to`, `?locationId` query parameters.

### 5.4 Technician Mobile App

**FR-TECH-01 — Offline sync**
The `MAUI.Tech` app MUST queue failed `PUT api/dealerships/{id}/service-requests/{srId}` requests in SQLite when offline. On reconnect, queued requests MUST replay sequentially. Optimistic concurrency via `updatedAtUtc` — if the server version is newer, a conflict MUST be surfaced to the technician (not silently overwritten).

**FR-TECH-02 — VIN/QR scan to job open**
Scanning a VIN barcode or QR code MUST resolve to the matching `ServiceRequest` via `POST api/dealerships/{id}/service-requests/search` with `assetId` filter. The first matching open SR MUST open automatically.

**FR-TECH-03 — Section 10A fields**
The `PUT api/dealerships/{id}/service-requests/{srId}` endpoint MUST accept `ServiceEventEmbedded` fields: `ComponentType`, `FailureMode`, `RepairAction`, `PartsUsed`, `LaborHours`, `ServiceDateUtc`. Technicians with `dealer:technician` role MUST be able to update Section 10A fields without changing SR status.

**FR-TECH-04 — Authenticated attachment upload**
`POST api/dealerships/{id}/service-requests/{srId}/attachments` MUST accept authenticated (Bearer) multipart uploads from dealer staff. The same file type and size constraints defined in FR-INTAKE-04 apply.

### 5.5 Tenant and Location Management

**FR-TENANT-01 — Onboarding**
`POST api/tenants/config` MUST create the initial `TenantConfig` document for a new tenant. MUST be restricted to `platform:admin` or first-time bootstrap (tenant has no existing config).

**FR-TENANT-02 — Access gate**
`TenantAccessGateMiddleware` MUST run on every authenticated request. It MUST reject requests from tenants whose `AccessGate.Status` is not `Active` with `403`. The `TenantConfig` read MUST be gateway-cached (≈ 0 RU after first read per cache window).

**FR-TENANT-03 — Location slug management**
On `POST api/locations` or `PUT api/locations/{id}` (slug rename), `ILocationService` MUST atomically: delete the old `slugLookup` entry, write the new one, then update the `Location` document. A stale slug MUST never resolve a valid intake route.

**FR-TENANT-04 — QR code generation**
`GET api/locations/{id}/qr-code` MUST return a QR code image encoding `https://app.rvserviceflow.com/intake/{locationSlug}`. Acceptable response formats: `image/png` (default), `image/svg+xml`. QR code MUST encode the full HTTPS URL.

---

## 6. Non-Functional Requirements

### 6.1 Performance

| Requirement | Target | Condition |
|---|---|---|
| Intake submission (POST, cold) | P95 < 3 s | Includes all 7 orchestration steps plus AI call |
| Intake submission (POST, warm) | P95 < 1.5 s | Gateway-cached slug + returning customer |
| SR detail read (GET) | P99 < 200 ms | Single point read (1 RU) |
| SR search (POST search) | P95 < 500 ms | Up to 100 results, single-partition query |
| Magic-link status page | P99 API response < 500 ms | WASM SPA client-side route; API point read by token hash prefix, single-partition Cosmos read |
| Analytics query | P95 < 2 s | MVP volume ≤ 200 jobs/month |
| AI diagnostic questions | P95 < 1.5 s | GPT-4o-mini, 5 s timeout, fallback on breach |
| Bulk outcome patch (25 SRs) | P95 < 2 s | Sequential Cosmos writes, single partition |
| Slug resolution (cached) | < 1 ms effective latency | Gateway cache hit |

### 6.2 Scalability

- Cosmos `serviceRequests` autoscale floor: 400 RU; ceiling: 4,000 RU. Must sustain burst intake without throttling.
- Architecture MUST support ≥ 10,000 service requests/month per tenant with no schema or index changes.
- Multi-location query patterns (corporate admin view) MUST remain single-partition regardless of location count.
- Blob Storage MUST support concurrent multi-tenant attachment uploads without cross-tenant path collision.

### 6.3 Availability and Reliability

- API availability SLA (MVP): ≥ 99.5% measured monthly
- Confirmation email delivery: fire-and-forget; email failure MUST NOT cause intake submission failure
- Azure OpenAI unavailability: MUST fall back to rule-based categorization; intake MUST succeed
- NHTSA vPIC unavailability: MUST proceed with customer-supplied asset info; no 500 error
- Offline sync (`MAUI.Tech`): MUST queue locally and replay without data loss up to 72 hours offline

### 6.4 Observability

**Structured logging (every request):**
- `tenantId`, `locationId` (anonymized — no customer PII in logs)
- Request correlation ID (injected by middleware)
- HTTP status code and latency
- Cosmos RU consumed per operation (from SDK `RequestCharge`)

**App Insights telemetry:**
- Custom events: `IntakeSubmitted`, `MagicLinkValidated`, `SlugNotFound`, `TenantGateBlocked`, `AICategorizationFailed`, `AICategorizationFallback`
- Dependency tracking: Cosmos DB calls, Azure OpenAI calls, SendGrid calls, NHTSA calls
- Availability tests: `/health` endpoint pinged every 5 minutes from two Azure regions

**Health endpoint:**
`GET /health` MUST return `200 OK` when dependencies are reachable, `503` when any critical dependency (Cosmos, Blob) is unavailable. MUST NOT require authentication.

### 6.5 Error Handling

All API errors MUST return RFC 7807 `ProblemDetails`:

```json
{
  "type": "https://rvserviceflow.com/errors/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Location slug 'bad-slug' was not found.",
  "traceId": "00-a1b2c3..."
}
```

`ExceptionHandlingMiddleware` MUST catch all unhandled exceptions and convert them to `ProblemDetails`. Inner exception messages MUST NOT be exposed in production responses. Stack traces MUST be logged server-side only.

Standard error codes:

| Scenario | Status | Type slug |
|---|---|---|
| Unknown slug | 404 | `location-not-found` |
| Expired magic-link token | 410 | `token-expired` |
| Invalid magic-link token | 404 | `token-invalid` |
| Tenant access gate blocked | 403 | `tenant-access-denied` |
| Rate limit exceeded | 429 | `rate-limit-exceeded` |
| Validation failure | 400 | `validation-error` |
| Unauthorized (no/invalid JWT) | 401 | `unauthorized` |
| Forbidden (wrong role/tenant) | 403 | `forbidden` |
| Internal / unhandled | 500 | `internal-error` |

---

## 7. Data Model Requirements

### 7.1 Entity Constraints

**ServiceRequest**

| Field | Type | Constraints |
|---|---|---|
| `id` | string | GUID, globally unique |
| `tenantId` | string | Partition key; set from `app_metadata.tenantId`; required |
| `locationId` | string | Prefix `loc_`; must exist in `locations` container |
| `status` | enum | `New` → `InProgress` → `Completed` or `Cancelled`; enforced by `StatusTransitions.cs` |
| `customer.email` | string | Lowercase, trimmed; required |
| `asset.assetId` | string | Format `{AssetType}:{Identifier}` (e.g., `RV:1ABC234567`); required |
| `createdAtUtc` | DateTime | Set by server; immutable after write |
| `updatedAtUtc` | DateTime | Updated by `MarkAsUpdated` on every write; used for optimistic concurrency |
| `diagnosticResponses` | array | Optional; max 10 responses; each has `questionText`, `selectedOptions[]`, `freeTextResponse?` |
| `attachments` | array | Max 10; each has `blobUri`, `fileName`, `contentType`, `sizeBytes` |

**CustomerProfile**

| Field | Type | Constraints |
|---|---|---|
| `tenantId` | string | Partition key |
| `email` | string | Unique within partition (unique key policy) |
| `globalCustomerAcctId` | string | Required; references `GlobalCustomerAcct.id` |
| `assetsOwned` | array | Embedded; each has `assetId`, `status` (`Active`/`Inactive`), `firstSeenAtUtc`, `lastSeenAtUtc`, `requestCount` |

**GlobalCustomerAcct**

| Field | Type | Constraints |
|---|---|---|
| `id` | string | Partition key = `/email`; normalized lowercase |
| `magicLinkToken` | string | Format: `base64url(SHA256(email)[0..8]):random_bytes`; rotated on every intake |
| `magicLinkExpiresAtUtc` | DateTime | Default 30 days; configurable per tenant |
| `linkedProfiles` | array | Each has `tenantId`, `customerProfileId`, `locationId`, `locationName` |

**AssetLedgerEntry**

| Field | Type | Constraints |
|---|---|---|
| `assetId` | string | Partition key; format `{AssetType}:{Identifier}` |
| `serviceRequestId` | string | Unique within partition; cross-references `serviceRequests` |
| `section10A` | object | Optional at write time; enriched via change feed Phase 5–6. Fields: `componentType`, `failureMode`, `repairAction`, `partsUsed[]`, `laborHours`, `serviceDateUtc` |

**Location — `IntakeFormConfigEmbedded`**

| Field | Default | Constraints |
|---|---|---|
| `maxFileSizeMb` | 25 | Range: 1–100 |
| `maxAttachments` | 10 | Range: 1–10 |
| `acceptedFileTypes` | `[".jpg",".jpeg",".png",".mp4",".m4a",".wav"]` | Must be a subset of supported MIME types |
| `aiContext` | null | Optional; appended to Azure OpenAI system prompt; max 500 characters |
| `allowAnonymousIntake` | true | If false, intake requires a specific tenant-issued token (Phase 2) |

### 7.2 Status Transition Rules

Enforced by `StatusTransitions.cs`. Invalid transitions MUST return `409 Conflict`.

```
New → InProgress       (Advisor/Manager)
New → Cancelled        (Advisor/Manager)
InProgress → Completed (Advisor/Manager/Technician via Section 10A completion)
InProgress → Cancelled (Advisor/Manager)
Completed  → (immutable — no further transitions in MVP)
Cancelled  → (immutable — no further transitions in MVP)
```

### 7.3 Cosmos Container Configuration

| Container | Partition Key | RU Mode | Unique Keys | TTL |
|---|---|---|---|---|
| `serviceRequests` | `/tenantId` | Autoscale 400–4,000 | — | None |
| `customerProfiles` | `/tenantId` | Autoscale 400–1,000 | `[/tenantId, /email]` | None |
| `globalCustomerAccts` | `/email` | Manual 400 | — | None |
| `assetLedger` | `/assetId` | Autoscale 400–1,000 | `[/assetId, /serviceRequestId]` | None |
| `dealerships` | `/tenantId` | Manual 400 | — | None |
| `locations` | `/tenantId` | Autoscale 400–1,000 | `[/tenantId, /slug]` | None |
| `tenantConfigs` | `/tenantId` | Manual 400 | — | None |
| `lookupSets` | `/category` | Manual 400 | — | None |
| `slugLookup` | `/slug` | Autoscale 400–1,000 | — | None |

**SDK connection mode:** `ConnectionMode.Gateway` for all reads. Enables Cosmos server-side caching on stable point-read containers (`slugLookup`, `tenantConfigs`, `lookupSets`). No application-layer cache required.

### 7.4 Blob Storage Layout

```
Container: rvs-attachments
  {tenantId}/
    {locationId}/
      {serviceRequestId}/
        {attachmentId}_{filename}.{ext}
```

- Path is deterministic and collision-free across tenants and locations
- SAS token expiry: 1 hour for read; 15 minutes for upload
- Retention: configurable per `TenantConfig.AttachmentRetentionDays` (default: unlimited in MVP)

---

## 8. API Specification

### 8.1 Authentication

All dealer-facing endpoints MUST require `Authorization: Bearer {jwt}` from Auth0.

JWT validation requirements:
- Issuer: `https://{auth0-domain}/`
- Audience: `https://api.rvserviceflow.com` (configurable in `appsettings.json`)
- Algorithm: RS256
- Claims required: `sub`, `https://rvserviceflow.com/tenantId` (from `app_metadata`), `roles[]`, `locationIds[]`

Customer-facing endpoints (`/intake/*`, `/status/*`) MUST be `[AllowAnonymous]`.

### 8.2 Full Route Inventory

| Method | Route | Auth | Policy | Return | Notes |
|---|---|---|---|---|---|
| `GET` | `api/intake/{locationSlug}` | Anonymous | — | `IntakeConfigResponseDto` | Optional `?token=` for prefill |
| `POST` | `api/intake/{locationSlug}/diagnostic-questions` | Anonymous | — | `DiagnosticQuestionsResponseDto` | AI or fallback questions |
| `POST` | `api/intake/{locationSlug}/service-requests` | Anonymous | — | `201 ServiceRequestSummaryDto` | Full 7-step orchestration |
| `POST` | `api/intake/{locationSlug}/service-requests/{id}/attachments` | Anonymous | — | `201 AttachmentDto` | Customer photo upload |
| `GET` | `api/status/{token}` | Anonymous | — | `CustomerStatusResponseDto` | Cross-dealer SR summary |
| `GET` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | `CanReadServiceRequests` | `ServiceRequestDetailDto` | |
| `POST` | `api/dealerships/{id}/service-requests/search` | Bearer | `CanSearchServiceRequests` | `PagedResult<ServiceRequestSummaryDto>` | |
| `PUT` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | `CanUpdateServiceRequests` | `200 ServiceRequestDetailDto` | Status + Section 10A + notes |
| `PATCH` | `api/dealerships/{id}/service-requests/batch-outcome` | Bearer | `CanUpdateServiceRequests` | `200 BatchOutcomeResponseDto` | Max 25 SRs |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | `CanDeleteServiceRequests` | `204` | |
| `POST` | `api/dealerships/{id}/service-requests/{srId}/attachments` | Bearer | `CanUploadAttachments` | `201 AttachmentDto` | Authenticated upload |
| `GET` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | `CanReadAttachments` | `AttachmentSasDto` | SAS URL, 1-hour expiry |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | `CanDeleteAttachments` | `204` | |
| `GET` | `api/dealerships` | Bearer | `CanReadDealerships` | `List<DealershipSummaryDto>` | Tenant-scoped list |
| `GET` | `api/dealerships/{id}` | Bearer | `CanReadDealerships` | `DealershipDetailDto` | |
| `PUT` | `api/dealerships/{id}` | Bearer | `CanUpdateDealerships` | `200 DealershipDetailDto` | |
| `GET` | `api/locations` | Bearer | `CanReadLocations` | `List<LocationSummaryDto>` | Filtered by `locationIds` claim |
| `GET` | `api/locations/{id}` | Bearer | `CanReadLocations` | `LocationDetailDto` | |
| `POST` | `api/locations` | Bearer | `CanCreateLocations` | `201 LocationDetailDto` | Creates slug entry atomically |
| `PUT` | `api/locations/{id}` | Bearer | `CanUpdateLocations` | `200 LocationDetailDto` | Renames slug atomically |
| `GET` | `api/locations/{id}/qr-code` | Bearer | `CanReadLocations` | `image/png` | Encodes intake URL |
| `GET` | `api/dealerships/{id}/analytics/service-requests/summary` | Bearer | `CanReadAnalytics` | `ServiceRequestAnalyticsResponseDto` | `?from`, `?to`, `?locationId` |
| `POST` | `api/tenants/config` | Bearer | `CanManageTenantConfig` | `201 TenantConfigDto` | Bootstrap only |
| `GET` | `api/tenants/config` | Bearer | `CanManageTenantConfig` | `TenantConfigDto` | |
| `PUT` | `api/tenants/config` | Bearer | `CanManageTenantConfig` | `200 TenantConfigDto` | |
| `GET` | `api/tenants/access-gate` | Bearer | `CanManageTenantConfig` | `AccessGateStatusDto` | |
| `GET` | `api/lookups/{lookupSetId}` | Bearer | `CanReadLookups` | `LookupSetDto` | |
| `GET` | `/health` | None | — | `200 / 503` | Dependency health check |

### 8.3 Key Request/Response Shapes

#### `ServiceRequestCreateRequestDto`

```csharp
public record ServiceRequestCreateRequestDto(
    CustomerInfoDto Customer,          // firstName, lastName, email (required), phone
    AssetInfoDto Asset,                // assetId (RV:{VIN}), manufacturer?, model?, year?
    string IssueCategory,              // from LookupSet
    string IssueDescription,           // customer free-text; required; max 2000 chars
    UrgencyLevel Urgency,              // Routine | Urgent | Emergency
    RvUsageType RvUsage,               // PartTime | FullTime
    List<DiagnosticResponseDto>? DiagnosticResponses  // from AI wizard; optional
);
```

#### `ServiceRequestSummaryDto`

```csharp
public record ServiceRequestSummaryDto(
    string Id,
    string LocationId, string LocationName,
    string Status,
    string CustomerFullName,
    string AssetId, string AssetDisplay,   // "2023 Grand Design Momentum 395G"
    string IssueCategory,
    string TechnicianSummary,              // truncated to 150 chars
    int AttachmentCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? AssignedTechnicianId,
    string? Priority
);
```

#### `DiagnosticQuestionsResponseDto`

```csharp
public record DiagnosticQuestionsResponseDto(
    List<DiagnosticQuestionDto> Questions,
    string? SmartSuggestion              // e.g., "Upload a photo of the hydraulic area"
);

public record DiagnosticQuestionDto(
    string QuestionText,
    List<string> Options,
    bool AllowFreeText,
    string? HelpText
);
```

#### `ServiceRequestAnalyticsResponseDto`

```csharp
public record ServiceRequestAnalyticsResponseDto(
    int TotalRequests,
    Dictionary<string, int> RequestsByStatus,
    Dictionary<string, int> RequestsByCategory,
    Dictionary<string, int> RequestsByLocation,
    List<AnalyticsRankItem> TopFailureModes,
    List<AnalyticsRankItem> TopRepairActions,
    decimal? AverageRepairTimeHours,
    List<AnalyticsRankItem> TopPartsUsed,
    decimal? AverageDaysToComplete
);

public record AnalyticsRankItem(string Name, int Count);
```

### 8.4 Search Request

`POST api/dealerships/{id}/service-requests/search` body:

```csharp
public record ServiceRequestSearchRequestDto(
    string? Keyword,
    string? Status,
    string? IssueCategory,
    string? LocationId,
    string? AssignedTechnicianId,
    string? AssignedBayId,
    string? AssetId,
    DateTime? DateFrom,
    DateTime? DateTo,
    string? Priority,
    int Page = 1,
    int PageSize = 25   // max 100
);
```

Search input validation: reject any `Keyword` containing `<`, `>`, `;`, `'`, `"`, `\`, `\0` — return `400` with `validation-error`.

---

## 9. Security Requirements

### 9.1 Authentication and Authorization

**SEC-AUTH-01** — All dealer endpoints MUST validate the JWT signature using Auth0's JWKS endpoint. Token expiry MUST be enforced.

**SEC-AUTH-02** — `ClaimsService.GetTenantIdOrThrow()` MUST be called in every authenticated service method before any Cosmos query. A request whose `tenantId` does not match the resource's `tenantId` MUST return `403` (not `404`).

**SEC-AUTH-03** — Location-scoped roles (`dealer:advisor`, `dealer:technician`, `dealer:manager`) MUST have their `locationIds` claim verified before returning or modifying any resource. `ClaimsService.HasAccessToLocation(locationId)` MUST be called for location-filtered operations.

**SEC-AUTH-04** — `platform:admin` role bypasses tenant isolation and CAN access any tenant. Usage MUST be logged as a custom App Insights event `PlatformAdminAccess` with `tenantId` and `userId`.

**SEC-AUTH-05** — Auth0 access tokens MUST NOT be stored in browser `localStorage`. Use memory-only or `sessionStorage` with short token lifetimes (≤ 1 hour).

### 9.2 Input Validation

**SEC-INPUT-01** — All string inputs MUST be validated for maximum length. Strings stored in Cosmos MUST be capped: issue description 2,000 chars; advisor notes 5,000 chars; names 100 chars; slugs 64 chars (alphanumeric + hyphens only).

**SEC-INPUT-02** — Slug format MUST be enforced: `/^[a-z0-9-]+$/` (lowercase alphanumeric and hyphens only). Reject on creation/update with `400`.

**SEC-INPUT-03** — Search keyword input MUST be sanitized as described in Section 8.4. Cosmos parameterized queries MUST be used for all variable inputs (no string concatenation).

**SEC-INPUT-04** — File upload MIME type MUST be validated server-side (not just client-side). Read the first 512 bytes of the file to verify the content matches the declared MIME type (magic bytes check).

**SEC-INPUT-05** — VIN input MUST be stripped of non-alphanumeric characters and validated for length (17 chars) and check digit before calling NHTSA. An invalid VIN MUST return `400` on the intake form before orchestration runs.

### 9.3 Secrets and Credential Management

**SEC-SECRETS-01** — Azure OpenAI endpoint key, SendGrid API key, and SFTP private keys MUST be stored in Azure Key Vault. They MUST NOT appear in `appsettings.json`, environment variables, or version control.

**SEC-SECRETS-02** — All Azure services (Cosmos DB, Blob Storage, Key Vault, OpenAI) MUST authenticate via `DefaultAzureCredential` (Managed Identity in production; local dev uses Azure CLI credential). No connection string passwords in config.

**SEC-SECRETS-03** — Auth0 `Client Secret` (if used in server-to-server flows) MUST be stored in Key Vault, not `appsettings.json`.

### 9.4 Magic-Link Security

| Threat | Mitigation |
|---|---|
| Token enumeration | 256-bit cryptographic random suffix; infeasible to guess |
| Token theft via URL sharing | 30-day expiry; rotated on every intake submission |
| Cross-customer data leakage | Status page only returns data for the token's owner; partition key derived from email-hash, not walked |
| Replay after expiry | `magicLinkExpiresAtUtc` checked server-side; return `410 Gone` |
| Rate-based scanning | 10 req/min per IP on `api/status/{token}` |

### 9.5 Data Privacy

**SEC-PRIV-01** — Customer email addresses MUST be normalized (lowercased, trimmed) before storage and MUST NOT be logged in application telemetry.

**SEC-PRIV-02** — Blob Storage SAS URIs MUST expire within 1 hour. SAS URIs MUST NOT be stored permanently in the `ServiceRequest` document — generate on demand only.

**SEC-PRIV-03** — `GlobalCustomerAcct` container (partitioned by `/email`) MUST only be accessible to the service layer, never directly from a controller without going through `IGlobalCustomerAcctService`. The `platform:admin` role is the only role with direct cross-tenant query access.

**SEC-PRIV-04** — No PII (email, phone, name) MUST appear in structured logs, App Insights custom dimensions, or trace spans. Use `customerId` and `tenantId` only.

### 9.6 OWASP Top 10 Coverage

| OWASP Category | Mitigation in RVS |
|---|---|
| A01 Broken Access Control | ClaimsService tenant isolation on every authenticated call; location scope checks |
| A02 Cryptographic Failures | TLS enforced (`UseHttpsRedirection`); AES-256 at rest (Azure); SAS token 256-bit randomness |
| A03 Injection | Parameterized Cosmos queries; search input sanitization; slug regex enforcement |
| A04 Insecure Design | Anonymous intake by design; magic-link with one-way email hash; never storing SAS URIs |
| A05 Security Misconfiguration | `DefaultAzureCredential` / Managed Identity; no connection string secrets in config |
| A06 Vulnerable Components | Automated dependency scanning in CI (Dependabot); pin .NET 10 LTS patch versions |
| A07 Auth Failures | Auth0 JWT RS256; short token lifetimes; no `localStorage` for access tokens |
| A08 Integrity Failures | GitHub Actions pipeline with manual approval gate for production deployments |
| A09 Logging Failures | Structured logging on every request; App Insights; no PII in logs |
| A10 SSRF | VIN decode calls only permitted to NHTSA vPIC (allowlisted base URL); no user-supplied URLs |

---

## 10. Integration Requirements

### 10.1 Auth0

| Requirement | Spec |
|---|---|
| JWT issuer | `https://{tenant}.auth0.com/` |
| JWT audience | `https://api.rvserviceflow.com` |
| Algorithm | RS256 |
| Custom claims namespace | `https://rvserviceflow.com/` |
| MVP claim injection | Login Action injects `tenantId`, `orgName`, `locationIds`, `regionTag` from `app_metadata` |
| Tenant scoping | `app_metadata.tenantId` per user; no Auth0 Organizations |
| Token lifetime | Access token: 1 hour; Refresh token: 30 days (rolling) |

### 10.2 Azure Cosmos DB

| Requirement | Spec |
|---|---|
| API | SQL (Core) API |
| Connection mode | Gateway (enables server-side caching) |
| Consistency level | Session (default); Eventual for read-only analytics queries |
| Multi-region | Single-write region MVP; multi-write deferred to Phase 2+ |
| RU budget per intake | ≤ 12 RU (cold), ≤ 10 RU (warm) |
| Partition key invariant | `tenantId` for all dealer/customer containers; never cross-partition query in service layer |

### 10.3 Azure Blob Storage

| Requirement | Spec |
|---|---|
| Container | `rvs-attachments` |
| Authentication | DefaultAzureCredential (Managed Identity) |
| SAS expiry — customer upload | 15 minutes |
| SAS expiry — dealer read | 1 hour |
| Path format | `{tenantId}/{locationId}/{serviceRequestId}/{attachmentId}_{filename}.{ext}` |
| Redundancy | LRS (MVP); GRS for production |

### 10.4 Azure OpenAI

| Requirement | Spec |
|---|---|
| Deployment | `gpt-4o-mini` (recommended); `gpt-4o` optional |
| Timeout | 5 seconds |
| Max output tokens | 500 |
| Fallback | Rule-based categorization + hardcoded questions per category |
| Authentication | DefaultAzureCredential |
| Cost target | ≤ $0.0002/intake at GPT-4o-mini rates |
| System prompt customization | `IntakeFormConfigEmbedded.aiContext` appended to base system prompt |

### 10.5 NHTSA vPIC API

| Requirement | Spec |
|---|---|
| Endpoint | `https://vpic.nhtsa.dot.gov/api/vehicles/decodevin/{VIN}?format=json` |
| Auth | None (public API) |
| Timeout | 3 seconds |
| Failure behavior | Proceed with customer-supplied asset info; log `VinDecodeTimeout` event |
| Rate limiting | Not documented by NHTSA; avoid concurrent bursts; no caching required in MVP |

### 10.6 SendGrid (Email Notifications)

| Requirement | Spec |
|---|---|
| Injection | Via `INotificationService` — swappable provider |
| API key | Stored in Key Vault, never in config |
| Templates | Intake confirmation, status update (in-progress, completed) |
| Delivery | Fire-and-forget; failure MUST NOT fail the intake transaction |
| From address | `noreply@rvserviceflow.com` |

### 10.7 DMS Export (SFTP)

| Requirement | Spec |
|---|---|
| Trigger | Scheduled (configurable cron in `TenantConfig`) or on-demand (`POST api/tenants/export`) |
| Format | CSV; column schema mirrors `ServiceRequestCreateRequestDto` + status + advisor notes |
| Auth | Key-based and password-based SFTP; credentials in Key Vault |
| Config | Per-tenant SFTP host, port, remote path, key vault reference stored in `TenantConfig` |
| Error handling | Failed export logged; retry next scheduled run; no customer-visible impact |

---

## 11. Middleware Pipeline

Order is mandatory. Deviation MUST require architecture review sign-off.

| Order | Component | Registration | Applies To |
|---|---|---|---|
| 1 | Dev endpoints | `UseSwaggerUI()` | Development only |
| 2 | HTTPS redirect | `UseHttpsRedirection()` | Production only |
| 3 | CORS | `UseCors("AllowBlazorClient")` | All origins (Blazor.Intake WASM + Blazor.Manager WASM) |
| 4 | Rate limiting | `UseRateLimiter()` | Public intake + status endpoints |
| 5 | Exception handling | `ExceptionHandlingMiddleware` (singleton) | All exceptions → ProblemDetails |
| 6 | Authentication | `UseAuthentication()` | JWT validation |
| 7 | Authorization | `UseAuthorization()` | Policy enforcement |
| 8 | Tenant access gate | `TenantAccessGateMiddleware` (scoped) | Authenticated requests only |
| 9 | Controllers | `MapControllers()` | Terminal |

`TenantAccessGateMiddleware` MUST skip anonymous endpoints (check `IAllowAnonymous` metadata).

---

## 12. Testing Requirements

### 12.1 Unit Tests (`RVS.Tests.Unit`)

Required coverage areas:
- `StatusTransitions.cs` — All valid and invalid transition combinations
- `CustomerProfileService.ResolveOrCreateProfileAsync` — Three asset ownership branches (same owner, transfer, new asset) + reactivation
- `GlobalCustomerAcctService` — Magic-link token generation, format validation, expiry check
- `ServiceRequestService` — Orchestration step sequencing; AI fallback behavior; notification fire-and-forget
- `ClaimsService` — All claim accessor methods with valid/missing/malformed inputs
- `AzureOpenAiCategorizationService` — Timeout triggers fallback; structured JSON response parsed correctly
- Search sanitization — Blocked characters return `400`; clean input passes through

### 12.2 Integration Tests (`RVS.Tests.Integration`)

Required test scenarios:
- Full intake submission: new customer + new VIN → verify 5 Cosmos documents written
- Returning customer intake: reuse existing `GlobalCustomerAcct` + `CustomerProfile`
- VIN transfer: Customer B submits VIN previously active under Customer A → verify deactivation + reactivation
- Magic-link status page: valid token → cross-dealer SRs returned; expired token → 410
- Slug not found → 404; disabled tenant → 403
- Batch outcome: 25 SRs updated atomically; 26th SR rejected with 400
- Authenticated attachment upload: file stored at correct Blob path with correct tenant/location/SR prefix
- Rate limiting: 11th request within sliding window → 429

Integration tests MUST run against the Cosmos Emulator (Windows) or a dedicated test database (CI). They MUST NOT use production or shared dev Cosmos accounts.

### 12.3 End-to-End Tests

Minimum E2E scenarios (automated, against staging):
- RV owner submits intake → receives confirmation email with magic-link
- Advisor logs in, finds SR in queue, updates status to InProgress
- Technician opens job on `MAUI.Tech`, records Section 10A fields, job shows Completed in `Blazor.Manager`

### 12.4 Performance Baselines

Before MVP release, run load tests at:
- 50 concurrent intake submissions/minute for 5 minutes
- Verify: P95 < 3 s, no 5xx errors, Cosmos throttling (429) < 0.1%

---

## 13. Deployment Requirements

### 13.1 Infrastructure

| Component | Resource | Notes |
|---|---|---|
| API | Azure App Service (B2/B3) or Container Apps (MVP) | Enable Always On |
| `Blazor.Intake` WASM | Azure Static Web Apps | CDN-enabled; custom domain `app.rvserviceflow.com`; PWA service worker caches WASM runtime for instant repeat visits |
| `Blazor.Manager` WASM | Azure Static Web Apps | CDN-enabled; same hosting pattern as Blazor.Intake |
| Cosmos DB | Single account, single region (MVP) | 9 containers per spec |
| Blob Storage | Single account | `rvs-attachments` container with per-tenant virtual paths |
| Key Vault | 1 vault | All secrets; API Managed Identity granted `get` + `list` |
| App Insights | 1 workspace | Linked to all API and frontend deployments |

### 13.2 CI/CD

| Stage | Toolchain | Requirements |
|---|---|---|
| Build | GitHub Actions | `dotnet build` must pass; no warnings treated as errors |
| Test | GitHub Actions | Unit + integration tests must pass; coverage ≥ 80% on domain/service layers |
| Publish | GitHub Actions | Publish API + frontend apps as artifacts |
| Deploy Staging | GitHub Actions | Automatic on `main` merge |
| Deploy Production | GitHub Actions | Manual approval gate required |
| Secrets | GitHub OIDC → Azure | No long-lived credentials in GitHub secrets; use Workload Identity Federation |

### 13.3 Configuration

`appsettings.json` MUST contain only non-secret configuration:

```json
{
  "Auth0": { "Domain": "...", "Audience": "..." },
  "AzureCosmosDb": { "Endpoint": "https://..." },
  "AzureBlobStorage": { "ServiceUri": "https://..." },
  "AzureOpenAi": { "Endpoint": "https://...", "DeploymentName": "gpt-4o-mini", "MaxTokens": 500, "TimeoutSeconds": 5 },
  "Nhtsa": { "BaseUrl": "https://vpic.nhtsa.dot.gov/api/" }
}
```

Secret values (API keys, connection strings) MUST be injected from Key Vault at startup via `AddAzureKeyVault` in `Program.cs`.

---

## 14. Cosmos RU Budget Summary

| Operation | Estimated RU | Note |
|---|---|---|
| Intake — new customer, cold | ~11.8 RU | 7 Cosmos operations |
| Intake — returning customer, warm | ~10.8 RU | Gateway-cached slug |
| Magic-link status read | ~1 + N RU | N = linked SR count |
| SR detail view | ~1 RU | Point read with embedded customer snapshot |
| SR search (25 results) | ~3 RU | Single-partition indexed query |
| Slug resolution (cached) | ~0 RU | Gateway cache hit |
| TenantConfig read (cached) | ~0 RU | Gateway cache hit |
| Analytics query (MVP volume) | ~5–10 RU | Single-partition aggregate |
| Batch outcome (25 SRs) | ~25 RU | 1 RU write × 25 |
| Asset history query (10A) | ~1 RU | Single-partition point read |

**Monthly cost estimate (100 tenants, 200 intakes/month each = 20,000 intakes/month):**
- Intake RU: 20,000 × 12 RU = 240,000 RU/month → < $1/month (Autoscale)
- Storage: 20,000 × avg 3 attachments × 10 MB ≈ 600 GB → ~$12/month

---

## 15. Known Gaps and Deferred Items

| ID | Description | Target Phase | Priority |
|---|---|---|---|
| GAP-01 | SFTP private keys stored in `TenantConfig` (Cosmos) — MUST move to Key Vault | Before MVP launch | Critical |
| GAP-02 | `AssetLedgerEntry` Section 10A fields null at intake; enrichment via change feed not built | Phase 5–6 | Required for analytics |
| GAP-03 | Customer Auth0 account (persistent login, preference saving) not supported | Phase 2 | High |
| GAP-04 | Follow-up request endpoint (`POST .../follow-ups`) not implemented; advisors use phone | Phase 2 | Important |
| GAP-05 | Analytics counters run against Cosmos directly; no Azure Tables pre-aggregation | Phase 2 | Performance risk at scale |
| GAP-06 | Batch SR update endpoint (`POST batch-update`) for high-scale offline sync | Future | Low (sequential PUT sufficient for MVP) |
| GAP-07 | Labor time prediction API | Phase 5–6 | Future |
| GAP-08 | MVP uses long polling for Service Board updates; vNEXT introduces a dedicated SignalR hub (requires Azure SignalR Service at scale) | vNEXT | Medium |

---

## 16. Out of Scope (MVP)

The following are explicitly deferred and MUST NOT be implemented without a new architecture review:

- Dealer Management System (DMS) replacement features (accounting, warranty, parts inventory)
- Customer Auth0 accounts / persistent login
- Appointment scheduling calendar or bay reservation
- Technician skill-based routing algorithm
- Parts ordering or inventory integrations
- Predictive maintenance or cross-dealer benchmarking
- Change feed consumers (all deferred to Phase 2+)
- Azure SignalR Service (beyond single-instance SignalR)
- Marine, heavy equipment, or agricultural vertical-specific features

---

## 17. Open Questions

| # | Question | Owner | Due |
|---|---|---|---|
| OQ-01 | ~~What is the Auth0 plan tier at commercialization?~~ **Resolved:** Using Auth0 Free plan with `app_metadata` tenant scoping. No Organizations. | Business | Resolved |
| OQ-02 | Should the magic-link token be stored hashed or plaintext in Cosmos? (Security hardening) | Engineering | Before MVP launch |
| OQ-03 | What email template tool for SendGrid templates — managed in code or SendGrid Dynamic Templates UI? | Engineering | Phase 1 |
| OQ-04 | When is Azure SignalR Service required vs. single-instance App Service sticky sessions sufficient? | Engineering | Before first multi-instance deploy |
| OQ-05 | Confirm NHTSA vPIC rate limit behavior under burst intake — implement client-side throttle if needed | Engineering | Phase 1 load test |

---

*Last updated: March 20, 2026. Derived from RVS_Core_Architecture_Version3.1.md. For questions, contact the RVS platform team.*
