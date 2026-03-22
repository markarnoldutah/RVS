# RVS Implementation Plan — Solo Developer + AI Assistance

**Version:** 2.0
**Date:** March 21, 2026
**Duration:** 12 weeks (≈ 60 working days)
**Derived from:** RVS_Technical_PRD.md, RVS_Core_Architecture_Version3.1.md, FrontEnd feature docs

---

## Companion Documents

| Document | Purpose |
|---|---|
| [RVS_Technical_PRD.md](RVS_Technical_PRD.md) | Testable requirements, API contracts, data model, security |
| [RVS_Core_Architecture_Version3.1.md](RVS_Core_Architecture_Version3.1.md) | Domain model, Cosmos schema, orchestration flows |
| [RVS_Auth0_Identity_Version2.md](RVS_Auth0_Identity_Version2.md) | RBAC, JWT claims, Auth0 Organization model |
| [RVS_PRD.md](RVS_PRD.md) | Product goals, personas, user stories |
| [RVS_FrontEnd_Solution.md](../FrontEnd/RVS_FrontEnd_Solution.md) | App format decisions, render modes, code reuse strategy |
| [RVS_Features_Cust_Intake_App.md](../FrontEnd/RVS_Features_Cust_Intake_App.md) | Customer intake app feature spec |
| [RVS_Features_Mngr_Desktop.md](../FrontEnd/RVS_Features_Mngr_Desktop.md) | Manager desktop feature spec |
| [RVS_Features_Tech_Mobile.md](../FrontEnd/RVS_Features_Tech_Mobile.md) | Technician mobile feature spec |
| [copilot-instructions.md](../../../.github/copilot-instructions.md) | Coding conventions and patterns |

---

## 1. Current State Assessment

### 1.1 What Already Exists

The repo contains a fully implemented **healthcare benefits verification system** (Patients, Encounters, Eligibility Checks, Coverage Enrollments, Payers). While the domain is different, the following **architectural patterns are directly reusable** for RVS:

| Asset | Reusable For RVS | Notes |
|---|---|---|
| Solution structure (`API/Domain/Infra.*`) | ✅ Copy layering pattern | Same clean architecture |
| `CosmosRepositoryBase` | ✅ Inherit directly | Generic Cosmos helpers, `GetContainer()` |
| `ExceptionHandlingMiddleware` | ✅ Adapt | Add RFC 7807 `ProblemDetails` format per Tech PRD §6.5 |
| `TenantAccessGateMiddleware` | ✅ Adapt | Update allowlist paths for RVS routes |
| `ClaimsService` | ✅ Adapt | Add `HasAccessToLocation()`, RVS claim constants |
| `HttpUserContextAccessor` | ✅ Copy as-is | Same IUserContextAccessor pattern |
| `EntityBase` | ✅ Adapt | Remove `PracticeScopedEntityBase`, keep core audit fields |
| Auth0 JWT setup in `Program.cs` | ✅ Adapt | Same Auth0 config pattern, update audience |
| Cosmos DI registration pattern | ✅ Copy pattern | Same singleton client, scoped repos |
| Mapper convention (static extension methods) | ✅ Copy pattern | Same `ToDetailDto()`, `ToEntity()` convention |
| DTOs as records pattern | ✅ Copy pattern | Same naming convention |
| Blazor WASM project structure | ✅ Fork for `Cust_Intake` | Auth, HttpClient, FluentUI already wired |

### 1.2 What Must Be Built New

| Component | Scope | Effort Driver |
|---|---|---|
| 8 domain entities + embedded sub-entities | ~15 classes | AI-accelerated from PRD specs |
| ~30 DTOs (request/response records) | ~30 files | AI-generated from §8.3–8.4 |
| 9 Cosmos repositories | ~9 classes | Follow existing `CosmosPatientRepository` patterns |
| 10+ services (including intake orchestrator) | ~10 classes | Intake orchestration = highest complexity |
| 8+ controllers | ~8 classes | Mechanical once services exist |
| 6+ mapper classes | ~6 files | Mechanical transforms |
| External integrations | NHTSA, Azure OpenAI, SendGrid, Blob SAS | 4 integration clients |
| Cosmos seed tool | Rewrite for RVS containers | 9 containers with test data |
| `RVS.UI.Shared` Razor Class Library | Shared components, DTOs, CSS tokens | Foundation for all 3 frontends |
| `Cust_Intake` Blazor WASM | 7-step intake wizard + status page | Most complex frontend |
| `Mngr_Desktop` Blazor WASM (Standalone) | Dashboard + Service Board + analytics | Second priority frontend |
| `Tech_Mobile` MAUI Blazor Hybrid | My Jobs, Section 10A, photo capture | Third frontend, offline-first |
| Rate limiting middleware | ASP.NET rate limiter | Per Tech PRD §5.3 |
| Health check endpoint | Standard health checks | Cosmos + Blob probes |
| Unit + integration tests | Two test projects | 80% domain/service coverage target |

---

## 2. Scope Decisions

### 2.1 What Ships in 12 Weeks

This plan covers the **full MVP including all three frontend applications**. The 12-week schedule satisfies the MVP Ship Criteria (Tech PRD §3.2): "5 design partners complete full intake → advisor dashboard → technician update cycle."

| Feature | Tech PRD Ref | Priority | Week |
|---|---|---|---|
| Domain layer + Cosmos containers + seed data | §7.1–7.3 | P0 | 1 |
| All Cosmos repositories + CRUD services | §7.3, copilot-instructions | P0 | 2 |
| Intake orchestration (7-step) | FR-INTAKE-01–07 | P0 | 3–4 |
| External integrations (NHTSA, OpenAI, Blob SAS) | FR-INTAKE-03, 04, 05 | P0 | 3 |
| Magic-link status endpoint | FR-STATUS-01, 02 | P0 | 3 |
| All API controllers + auth policies | §8.2, §10.1 | P0 | 4 |
| Middleware (rate limiting, exception handling, access gate) | §6.4–6.5, FR-STATUS-03 | P0 | 4 |
| Shared UI library (`RVS.UI.Shared`) | FrontEnd Solution doc | P0 | 5 |
| `Cust_Intake` — intake wizard (7 steps) | FR-INTAKE-01–07 | P0 | 5–6 |
| `Cust_Intake` — status page | FR-STATUS-01, 02 | P0 | 6 |
| `Mngr_Desktop` — SR queue + detail + search | FR-DASH-01 | P0 | 7–8 |
| `Mngr_Desktop` — Service Board (polling) | FR-DASH-03 (simplified) | P0 | 8 |
| `Mngr_Desktop` — analytics dashboard | FR-DASH-04 | P1 | 8 |
| `Mngr_Desktop` — location management + QR codes | FR-TENANT-03, 04 | P1 | 8 |
| `Tech_Mobile` — project scaffold + auth | FR-TECH-01 | P0 | 9 |
| `Tech_Mobile` — My Jobs queue + SR detail | FR-TECH-02, 03 | P0 | 9 |
| `Tech_Mobile` — Section 10A outcome entry | FR-TECH-03 | P0 | 9–10 |
| `Tech_Mobile` — photo capture + VIN/QR scan | FR-TECH-02, 04 | P1 | 10 |
| `Tech_Mobile` — offline queue (SQLite) | FR-TECH-01 | P1 | 10 |
| Unit tests (80% coverage target) | §12.1 | P0 | 11 |
| Integration tests (Cosmos Emulator) | §12.2 | P0 | 11 |
| Security verification | §9 | P0 | 11 |
| Deployment + CI/CD + design partner onboarding | §13 | P0 | 12 |

### 2.2 What Is Deferred Beyond 12 Weeks

| Feature | Reason |
|---|---|
| Dedicated SignalR Service Board hub | MVP ships with configurable long polling (default 5m); dedicated SignalR hub added in vNEXT |
| DMS SFTP export (§10.7) | Manual CSV export sufficient for MVP |
| SendGrid styled email templates | Intake confirmation = plain-text via `INotificationService`; styled templates post-MVP |
| Azure Tables pre-aggregation (GAP-05) | Direct Cosmos analytics sufficient at MVP volume |
| Change feed asset ledger enrichment (GAP-02) | Phase 5–6 per architecture |
| Load testing (§12.4) | Stretch goal Week 12; otherwise immediate post-MVP |
| E2E automated tests (§12.3) | Manual E2E with design partners; automated post-MVP |
| `Tech_Mobile` voice notes | Platform speech-to-text via MAUI Essentials; post-MVP polish |
| `Tech_Mobile` bay-based tablet kiosk mode | MDM auto-provisioning; Phase 2 |
| Customer Auth0 accounts (persistent login) | Phase 2 per Context doc §10.2 |

### 2.3 Key Difference from v1.0 Plan

The v1.0 plan (8 weeks) deferred `Tech_Mobile` entirely and had technicians use `Mngr_Desktop` on a tablet. This v2.0 plan adds 4 weeks to deliver all three frontend applications, satisfying the requirement that the MVP includes a purpose-built technician experience with native scanning, photo capture, and offline support.

---

## 3. Phase Plan

### Phase 1 — Foundation (Days 1–5) {Week 1}

**Goal:** RVS domain layer compiles, Cosmos containers exist, seed data loads, API skeleton returns health check.

#### 1.1 Solution Structure

Create or adapt the following projects:

```
RVS.sln
├── src/
│   ├── RVS.API/                    (ASP.NET Core Web API)
│   ├── RVS.Domain/                 (Entities, DTOs, Interfaces, Validation)
│   ├── RVS.Infra.AzCosmosRepository/  (Cosmos DB repositories)
│   ├── RVS.Infra.AzBlobRepository/    (Blob Storage service)
│   ├── RVS.UI.Shared/             (Razor Class Library — shared components, API clients)
│   ├── RVS.Cust_Intake/           (Blazor WASM — customer portal)
│   ├── RVS.Mngr_Desktop/          (Blazor WASM Standalone — manager dashboard)
│   └── RVS.Tech_Mobile/           (MAUI Blazor Hybrid — technician app)
├── tools/
│   └── RVS.Data.Cosmos.Seed/      (Cosmos seed tool)
└── tests/
    ├── RVS.Tests.Unit/
    └── RVS.Tests.Integration/
```

#### 1.2 Domain Entities

Create all entities in `RVS.Domain/Entities/` per Tech PRD §7.1 and Architecture §3:

| Entity | File | Key Properties | Embedded Types |
|---|---|---|---|
| `EntityBase` | `EntityBase.cs` | Adapt existing; remove `PracticeScopedEntityBase` | — |
| `ServiceRequest` | `ServiceRequest.cs` | id (`sr_` prefix), tenantId, locationId, status, priority | `CustomerSnapshotEmbedded`, `AssetInfoEmbedded`, `ServiceEventEmbedded`, `DiagnosticResponseEmbedded` |
| `CustomerProfile` | `CustomerProfile.cs` | tenantId (PK), email, globalCustomerAcctId | `AssetOwnershipEmbedded` (list) |
| `GlobalCustomerAcct` | `GlobalCustomerAcct.cs` | email (PK), magicLinkToken, magicLinkExpiresAtUtc | `LinkedProfileEmbedded` (list) |
| `AssetLedgerEntry` | `AssetLedgerEntry.cs` | assetId (PK), serviceRequestId | `Section10AEmbedded` (optional) |
| `Dealership` | `Dealership.cs` | tenantId (PK) | — |
| `Location` | `Location.cs` | tenantId (PK), slug | `IntakeFormConfigEmbedded`, `AddressEmbedded` |
| `TenantConfig` | `TenantConfig.cs` | Adapt existing; add AccessGate, SFTP config stubs | `TenantAccessGateEmbedded` |
| `LookupSet` | `LookupSet.cs` | Adapt existing; category (PK) | `LookupItem` (list) |
| `SlugLookup` | `SlugLookup.cs` | slug (PK), tenantId, locationId | — |

**StatusTransitions.cs** — Static class enforcing valid status changes per §7.2.

#### 1.3 DTOs

Create all DTOs in `RVS.Domain/DTOs/` per §8.3–8.4:

- `ServiceRequestCreateRequestDto`, `ServiceRequestSearchRequestDto`, `ServiceRequestDetailResponseDto`, `ServiceRequestSummaryResponseDto`
- `CustomerInfoDto`, `AssetInfoDto`, `DiagnosticResponseDto`
- `DiagnosticQuestionsResponseDto`, `DiagnosticQuestionDto`
- `IntakeConfigResponseDto`
- `CustomerStatusResponseDto`
- `BatchOutcomeRequestDto`, `BatchOutcomeResponseDto`
- `ServiceRequestAnalyticsResponseDto`, `AnalyticsRankItem`
- `DealershipDetailDto`, `DealershipSummaryDto`
- `LocationDetailDto`, `LocationSummaryDto`
- `TenantConfigDto`, `TenantConfigCreateRequestDto`, `TenantConfigUpdateRequestDto`
- `AccessGateStatusDto`
- `AttachmentDto`, `AttachmentSasDto`
- `LookupSetDto`
- `PagedResult<T>` — adapt existing

#### 1.4 Domain Interfaces

Create all interfaces in `RVS.Domain/Interfaces/`:

- `IServiceRequestService`, `IServiceRequestRepository`
- `ICustomerProfileService`, `ICustomerProfileRepository`
- `IGlobalCustomerAcctService`, `IGlobalCustomerAcctRepository`
- `IAssetLedgerRepository`
- `IDealershipService`, `IDealershipRepository`
- `ILocationService`, `ILocationRepository`
- `ISlugLookupRepository`
- `ITenantConfigService`, `ITenantConfigRepository`
- `ILookupService`, `ILookupRepository`
- `ICategorizationService` (Azure OpenAI + fallback)
- `INotificationService` (SendGrid + no-op dev)
- `IVinDecoderService` (NHTSA)
- `IBlobStorageService` (SAS generation + upload)
- `IUserContextAccessor` — adapt existing

#### 1.5 Validation

- `SearchInputValidator.cs` — Blocked characters (`<`, `>`, `;`, `'`, `"`, `\`, `\0`) per §8.4
- `SlugValidator.cs` — Regex `/^[a-z0-9-]+$/` per SEC-INPUT-02
- `VinValidator.cs` — 17 chars, alphanumeric, check digit per SEC-INPUT-05

#### 1.6 Cosmos Seed Tool

Rewrite `RVS.Data.Cosmos.Seed/Program.cs` to create 9 containers with partition keys, unique key policies, and indexing policies per §7.3. Seed with:

- 2 tenants (Blue Compass RV multi-location, Happy Trails RV single-location)
- 5 locations across the two tenants
- 10 sample service requests in various statuses
- 5 customer profiles + 5 global customer accounts
- Asset ledger entries for the sample SRs
- LookupSets: issue categories, component types, failure modes, repair actions
- Slug lookup entries for all locations
- TenantConfig for each tenant

#### 1.7 Infrastructure Skeleton

- Update `Program.cs` to register RVS services (remove healthcare registrations)
- Verify Auth0 JWT configuration with RVS audience
- Update `ExceptionHandlingMiddleware` to return RFC 7807 `ProblemDetails` (Tech PRD §6.5)
- Add `/health` endpoint with Cosmos + Blob probes (§6.4)
- Add structured logging with `tenantId`, `locationId`, correlation ID (§6.4)

**Exit Criteria:** `dotnet build` succeeds. Seed tool creates all 9 Cosmos containers with indexes. Health endpoint returns 200.

---

### Phase 2 — Core Repositories & Services (Days 6–12) {Week 2}

**Goal:** All Cosmos repositories implemented. Core service logic for CRUD operations works.

#### 2.1 Cosmos Repositories

Implement in `RVS.Infra.AzCosmosRepository/Repositories/`:

| Repository | Key Operations | Complexity |
|---|---|---|
| `CosmosServiceRequestRepository` | CRUD, search with 10 filter params, pagination | High — most complex query |
| `CosmosCustomerProfileRepository` | GetByEmail, GetByGlobalAcctId, Upsert | Medium |
| `CosmosGlobalCustomerAcctRepository` | GetByEmail, GetByToken (email-hash prefix), Upsert | Medium |
| `CosmosAssetLedgerRepository` | Append (write-once), GetByAssetId | Low |
| `CosmosDealershipRepository` | GetByTenantId, GetById, Update | Low |
| `CosmosLocationRepository` | GetByTenantId, GetById, Create, Update | Medium |
| `CosmosSlugLookupRepository` | PointRead by slug, Create, Delete | Low |
| `CosmosTenantConfigRepository` | PointRead by tenantId, Create, Update | Low — adapt existing |
| `CosmosLookupRepository` | GetByCategory | Low — adapt existing |

All repositories must:
- Use parameterized queries (no string concatenation) — SEC-INPUT-03
- Accept `tenantId` as first parameter on all tenant-scoped operations
- Return `RequestCharge` for logging (§6.4)
- Support `CancellationToken`

#### 2.2 Mappers

Create in `RVS.API/Mappers/`:

| Mapper | Transforms |
|---|---|
| `ServiceRequestMapper` | Entity ↔ DetailDto, SummaryDto, CreateDto→Entity |
| `CustomerProfileMapper` | Entity ↔ response shapes |
| `LocationMapper` | Entity ↔ DetailDto, SummaryDto |
| `DealershipMapper` | Entity ↔ DetailDto, SummaryDto |
| `TenantConfigMapper` | Entity ↔ ConfigDto (adapt existing) |
| `LookupMapper` | Adapt existing |

#### 2.3 CRUD Services

Implement in `RVS.API/Services/`:

| Service | Methods | Notes |
|---|---|---|
| `DealershipService` | Get, GetAll, Update | Straightforward CRUD |
| `LocationService` | Get, GetAll, Create, Update | Atomic slug management (FR-TENANT-03) |
| `TenantConfigService` | Get, Create, Update, GetAccessGate | Adapt existing |
| `LookupService` | GetByCategory | Adapt existing |

**Exit Criteria:** All repositories pass manual Postman tests against seeded data. CRUD services operate correctly. Location create/update atomically manages slug entries.

---

### Phase 3 — Intake Orchestration & Integrations (Days 13–20) {Weeks 3–4 first half}

**Goal:** The 7-step intake orchestration works end-to-end. External integrations (NHTSA, Azure OpenAI, Blob SAS) are functional with fallbacks.

This is the **highest complexity phase** — the intake orchestrator is the core business logic of the platform.

#### 3.1 External Integration Clients

| Client | Location | Implementation |
|---|---|---|
| `NhtsaVinDecoderClient` : `IVinDecoderService` | `RVS.API/Integrations/` | HTTP GET to vPIC API; 3s timeout; on failure return null (FR-INTAKE-03) |
| `AzureOpenAiCategorizationService` : `ICategorizationService` | `RVS.API/Integrations/` | Azure OpenAI `gpt-4o-mini`; 5s timeout; structured JSON response; fallback to rule-based (FR-INTAKE-05) |
| `RuleBasedCategorizationService` : `ICategorizationService` | `RVS.API/Integrations/` | Hardcoded question sets per issue category; used as fallback |
| `SendGridNotificationService` : `INotificationService` | `RVS.API/Integrations/` | Fire-and-forget email; plain-text MVP; `INotificationService` for swappability |
| `NoOpNotificationService` : `INotificationService` | `RVS.API/Integrations/` | Dev/test no-op implementation |
| `BlobStorageService` : `IBlobStorageService` | `RVS.Infra.AzBlobRepository/` | SAS generation (upload: 15min, read: 1hr); path: `{tenantId}/{locationId}/{srId}/{attId}_{filename}` |

Each integration client should use `IHttpClientFactory` with `AddStandardResilienceHandler` per copilot-instructions.

Create mock implementations for local dev (toggled via `appsettings.Development.json`):
- `MockVinDecoderService` — returns hardcoded Grand Design Momentum data
- `MockCategorizationService` — returns 3 stock diagnostic questions
- `MockBlobStorageService` — returns fake SAS URIs

#### 3.2 Customer Resolution Services

| Service | Key Logic |
|---|---|
| `GlobalCustomerAcctService` | Resolve by email (create if absent); generate magic-link token; validate token for status page |
| `CustomerProfileService` | Resolve within tenant (create if absent); asset ownership tracking with transfer logic |

**Asset ownership transfer logic** (Architecture §5.2):
1. If VIN already active under a different `CustomerProfile` in same tenant → deactivate old, activate on new
2. If VIN is new → add to `assetsOwned` with `Active` status
3. If VIN already active under same profile → increment `requestCount`, update `lastSeenAtUtc`

**Magic-link token format:** `base64url(SHA256(email)[0..8]):random_bytes` — the email-hash prefix enables O(1) partition-key derivation on the status page read (FR-STATUS-01).

#### 3.3 Intake Orchestration Service

`IntakeOrchestrationService` — the core 7-step sequence per FR-INTAKE-02:

```
Step 1: Resolve slug → tenantId + locationId (SlugLookupRepository)
Step 2: Resolve GlobalCustomerAcct by email (create if absent)
Step 3: Resolve CustomerProfile within tenant (create if absent) + asset ownership
Step 4: Create ServiceRequest with:
        - Customer snapshot (denormalized)
        - AI categorization (ICategorizationService) or rule-based fallback
        - Technician summary (generated from issue description + diagnostic responses)
        - Embedded diagnostic responses (if provided)
Step 5: Append AssetLedgerEntry (write-once; non-blocking on failure)
Step 6: Update linkages:
        - Increment CustomerProfile.requestCount
        - Rotate GlobalCustomerAcct.magicLinkToken
Step 7: Fire-and-forget INotificationService.SendIntakeConfirmationAsync()
```

Steps 1–6 must complete before returning `201`. Step 7 must not block the response.

**Error handling:** If Step 5 fails (AssetLedger write), log the failure but do NOT fail the intake. Steps 1–4 and 6 are retry-sensitive — any failure returns 500.

#### 3.4 Service Request Service

`ServiceRequestService`:
- `SearchAsync` — 10 filter parameters, paginated, single-partition query (FR-DASH-01)
- `GetByIdAsync` — point read (1 RU)
- `UpdateAsync` — status transition validation via `StatusTransitions.cs`; Section 10A update (FR-TECH-03); optimistic concurrency via `updatedAtUtc`
- `BatchOutcomeAsync` — apply shared repair outcome to ≤25 SRs (FR-DASH-02); validate all belong to caller's tenant
- `DeleteAsync` — soft or hard delete

#### 3.5 Attachment Service

`AttachmentService`:
- `GenerateUploadSasAsync` — 15-minute upload SAS for customer intake
- `GenerateReadSasAsync` — 1-hour read SAS for staff viewing
- `CreateAttachmentAsync` — authenticated upload from staff; validate MIME type server-side (magic bytes check per SEC-INPUT-04)
- File constraints: MIME types per FR-INTAKE-04; max 10 per SR; max size from `IntakeFormConfigEmbedded.MaxFileSizeMb`

#### 3.6 Analytics Service

`AnalyticsService`:
- `GetServiceRequestSummaryAsync` — Direct Cosmos aggregate query per FR-DASH-04
- Supports `?from`, `?to`, `?locationId` filters
- Returns `ServiceRequestAnalyticsResponseDto` with all rollups

**Exit Criteria:** Full intake orchestration succeeds via Postman — submitting a `ServiceRequestCreateRequestDto` creates 5 Cosmos documents (GlobalCustomerAcct, CustomerProfile, ServiceRequest, AssetLedgerEntry, updated linkages). VIN decoding returns enriched asset data. AI questions return from OpenAI or fallback. Magic-link status page returns cross-dealer SRs.

---

### Phase 4 — API Controllers & Middleware (Days 21–25) {Week 4 second half – Week 5 start}

**Goal:** All API routes from §8.2 are wired up. Security middleware is complete.

#### 4.1 Controllers

Implement per §8.2 route inventory, following copilot-instructions conventions:

| Controller | Route Prefix | Endpoints |
|---|---|---|
| `IntakeController` | `api/intake/{locationSlug}` | GET config, POST diagnostic-questions, POST service-requests, POST attachments |
| `StatusController` | `api/status/{token}` | GET (magic-link validation + cross-dealer SRs) |
| `ServiceRequestsController` | `api/dealerships/{id}/service-requests` | GET {srId}, POST search, PUT {srId}, PATCH batch-outcome, DELETE {srId} |
| `AttachmentsController` | `api/dealerships/{id}/service-requests/{srId}/attachments` | POST upload, GET {attId} (SAS), DELETE {attId} |
| `DealershipsController` | `api/dealerships` | GET list, GET {id}, PUT {id} |
| `LocationsController` | `api/locations` | GET list, GET {id}, POST, PUT {id}, GET {id}/qr-code |
| `TenantsController` | `api/tenants` | POST/GET/PUT config, GET access-gate |
| `LookupsController` | `api/lookups` | GET {lookupSetId} |
| `AnalyticsController` | `api/dealerships/{id}/analytics` | GET service-requests/summary |

`IntakeController` and `StatusController` use `[AllowAnonymous]`. All others use `[Authorize]`.

#### 4.2 Authorization Policies

Register ASP.NET authorization policies in `Program.cs` mapped to JWT `permissions[]` per Auth0 Identity doc §10.1:

| Policy Name | Required Permission |
|---|---|
| `CanReadServiceRequests` | `service-requests:read` |
| `CanSearchServiceRequests` | `service-requests:search` |
| `CanUpdateServiceRequests` | `service-requests:update` |
| `CanUpdateServiceEvent` | `service-requests:update-service-event` |
| `CanDeleteServiceRequests` | `service-requests:delete` |
| `CanUploadAttachments` | `attachments:upload` |
| `CanReadAttachments` | `attachments:read` |
| `CanDeleteAttachments` | `attachments:delete` |
| `CanReadDealerships` | `dealerships:read` |
| `CanUpdateDealerships` | `dealerships:update` |
| `CanReadLocations` | `locations:read` |
| `CanCreateLocations` | `locations:create` |
| `CanUpdateLocations` | `locations:update` |
| `CanReadAnalytics` | `analytics:read` |
| `CanManageTenantConfig` | `tenants:config:read` OR `tenants:config:create` OR `tenants:config:update` |
| `CanReadLookups` | `lookups:read` |
| `PlatformAdmin` | `platform:tenants:manage` |

#### 4.3 Middleware Updates

| Middleware | Change |
|---|---|
| `ClaimsService` | Add `GetLocationIdsOrThrow()`, `HasAccessToLocation(locationId)`, `GetRegionTagOrDefault()` per Auth0 Identity doc §6 |
| `TenantAccessGateMiddleware` | Update allowlist: `/api/intake/*`, `/api/status/*`, `/health`, `/swagger` |
| Rate limiting | Add `UseRateLimiter()` with fixed-window policies: `api/status/*` → 10 req/min/IP; `api/intake/*/service-requests` → 20 req/min/IP (FR-STATUS-03) |
| `ExceptionHandlingMiddleware` | Return `ProblemDetails` with `type` slug URIs per §6.5 table |

#### 4.4 Program.cs Final Wiring

Complete DI registration for all services, repositories, integration clients, and middleware. Update `appsettings.json` with non-secret config per §13.3:

```json
{
  "Auth0": { "Domain": "...", "Audience": "https://api.rvserviceflow.com" },
  "AzureCosmosDb": { "Endpoint": "https://..." },
  "AzureBlobStorage": { "ServiceUri": "https://..." },
  "AzureOpenAi": { "Endpoint": "...", "DeploymentName": "gpt-4o-mini", "MaxTokens": 500, "TimeoutSeconds": 5 },
  "Nhtsa": { "BaseUrl": "https://vpic.nhtsa.dot.gov/api/" },
  "SendGrid": { "FromAddress": "noreply@rvserviceflow.com" }
}
```

**Exit Criteria:** All 28 routes from §8.2 respond correctly via Postman. Auth policies reject unauthorized requests. Rate limiter returns 429 when exceeded. Tenant access gate returns 403 for disabled tenants.

---

### Phase 5 — Shared UI Library & Customer Intake Frontend (Days 26–35) {Weeks 5–7 first half}

**Goal:** `RVS.UI.Shared` Razor Class Library built. `Cust_Intake` Blazor WASM delivers the complete anonymous intake wizard and status page.

#### 5.1 RVS.UI.Shared — Razor Class Library

Create `RVS.UI.Shared` as the shared foundation consumed by all three frontends:

| Component | Description | Consumers |
|---|---|---|
| **Typed API client services** | `ServiceRequestApiClient`, `IntakeApiClient`, `LookupApiClient`, `AttachmentApiClient`, `AnalyticsApiClient` | All 3 apps |
| **Shared Razor components** | `StatusBadge`, `PriorityBadge`, `AssetDisplay`, `AttachmentThumbnail`, `DiagnosticResponseView` | All 3 apps |
| **CSS design tokens** | Colors, spacing, typography variables | All 3 apps |
| **DTO re-exports** | References `RVS.Domain` DTOs for frontend consumption | All 3 apps |
| **Shared validation helpers** | Client-side VIN format check, email format, search input sanitization | Cust_Intake, Tech_Mobile |

#### 5.2 Cust_Intake Project Setup

Create `RVS.Cust_Intake` as Blazor WebAssembly:
- Remove healthcare check-in components from forked project
- Keep: FluentUI, HttpClient setup, layout skeleton
- Update `Program.cs`: anonymous HttpClient (no Auth0 OIDC), pointing to RVS.API
- Configure mixed render modes per FrontEnd Solution doc:
  - **Static SSR:** landing page, confirmation page, status page
  - **Interactive WebAssembly:** intake wizard steps
- Preload WASM bundle on the Static SSR landing page for instant wizard transition

#### 5.3 Intake Wizard (7-Step Form)

Build the core intake experience. Each step is a separate Razor component with shared state:

| Step | Component | API Call | Key UI |
|---|---|---|---|
| 1. Location Landing | `IntakeLanding.razor` | `GET api/intake/{slug}` | Location branding, "Start Service Request" CTA, WASM preload |
| 2. Customer Info | `CustomerInfoStep.razor` | — (local state) | First name, last name, email (required), phone. Returning customer prefill if magic-link token present (FR-INTAKE-06) |
| 3. Asset Info | `AssetInfoStep.razor` | — (local state) | VIN input (17-char validation), make/model/year. Returning customer sees known VINs for one-tap selection |
| 4. Issue Description | `IssueDescriptionStep.razor` | — (local state) | Category dropdown (from LookupSet), free-text description (max 2000 chars), urgency selector, RV usage type |
| 5. AI Diagnostic Questions | `DiagnosticQuestionsStep.razor` | `POST api/intake/{slug}/diagnostic-questions` | 2–4 AI-generated questions with option buttons + free-text; smart suggestion |
| 6. Attachments | `AttachmentUploadStep.razor` | SAS upload direct to Blob | Drag-and-drop or camera capture; progress bar; max 10 files; MIME validation client-side |
| 7. Review & Submit | `ReviewSubmitStep.razor` | `POST api/intake/{slug}/service-requests` | Summary of all entered data; edit buttons per section; Submit button → 201 → confirmation page |

**Shared State:** `IntakeWizardState.cs` — holds all step data, current step index, validation state, returning customer prefill data. Persisted in `sessionStorage` for offline resilience.

#### 5.4 Status Page

`StatusPage.razor` at `/status/{token}`:
- Calls `GET api/status/{token}`
- Displays all active SRs across all dealerships (FR-STATUS-02)
- Shows: location name, status, issue category, submission date, last updated
- Handles 404 (invalid token), 410 (expired token) gracefully
- Rendered as Static SSR (no WASM required)

#### 5.5 Mobile-First UX

- Mobile-first responsive layout (Safari iOS, Chrome Android primary targets)
- Loading skeletons during API calls
- Client-side VIN format validation before submit
- Large tap targets, progressive steps, minimal typing

**Exit Criteria:** A user can navigate to `/intake/{slug}`, complete all 7 steps, submit a service request, and view their SR on the status page via magic-link. Works on mobile Safari and Chrome.

---

### Phase 6 — Manager Desktop Frontend (Days 36–45) {Weeks 7 second half – Week 9 start}

**Goal:** `Mngr_Desktop` Blazor WASM (Standalone) delivers the advisor/manager dashboard with auth.

#### 6.1 Project Setup

Create `RVS.Mngr_Desktop` as Blazor WebAssembly (Standalone):
- Auth0 OIDC authentication (PKCE flow) via `Microsoft.AspNetCore.Components.WebAssembly.Authentication`
- `HttpClient` with Bearer token injection via `AuthorizationMessageHandler`
- FluentUI component library
- Reference `RVS.UI.Shared` for shared components and API clients
- Same hosting model as `Cust_Intake` — deployed to Azure Static Web Apps, cached after first load

#### 6.2 Core Pages

| Page | Route | Key Features |
|---|---|---|
| Login/Landing | `/` | Auth0 redirect, location selector for location-scoped roles |
| SR Queue | `/service-requests` | Search/filter with 10 parameters (FR-DASH-01); paginated table; click → detail |
| SR Detail | `/service-requests/{id}` | Full SR view; status update dropdown (with transition validation); advisor notes; attachment gallery with SAS links; Section 10A fields (read-only for advisors) |
| SR Edit | `/service-requests/{id}/edit` | Status change, assign technician, assign bay, priority, notes |
| Batch Outcome | `/service-requests/batch-outcome` | Select multiple SRs → apply shared repair outcome (FR-DASH-02); max 25 |
| Analytics | `/analytics` | Summary dashboard per FR-DASH-04; breakdowns by status, category, location, top failure modes |
| Locations | `/locations` | Location list; create/edit location; slug management; QR code download (FR-TENANT-04) |
| Settings | `/settings` | Tenant config; intake form settings; access gate toggle |

#### 6.3 Service Board (Long Polling MVP)

Service Board at `/board`:
- Kanban-style columns: New → In Progress → Completed / Cancelled
- Cards show SR summary (customer name, asset, category, age, priority badge)
- Click card → SR Detail
- **Long polling refresh** at configurable interval (default 5m) to detect Tech_Mobile updates (dedicated SignalR hub deferred to vNEXT per §2.2)
- Filter by location for multi-location tenants

#### 6.4 Outcome Compliance Monitoring

Dashboard widget showing "Jobs Completed Without Outcomes":
- Filters SRs with `status: Completed` and `hasOutcome: false`
- Quick-open to record outcomes managers can fill when technicians miss entries

**Exit Criteria:** Advisor can log in, see SR queue, filter/search, view SR detail, update status, add notes, view attachments. Manager can view analytics, Service Board, manage locations, and batch-update outcomes. Location management with slug creation and QR codes works.

---

### Phase 7 — Technician Mobile App (Days 46–55) {Weeks 9 second half – Week 11 start}

**Goal:** `Tech_Mobile` MAUI Blazor Hybrid delivers the core technician workflow: view assigned jobs, record Section 10A outcomes, capture photos, scan VINs, and queue updates offline.

#### 7.1 Project Setup

Create `RVS.Tech_Mobile` as MAUI Blazor Hybrid (iOS + Android):
- Auth0 OIDC authentication (PKCE flow, `WebAuthenticator`)
- `HttpClient` with Bearer token injection via `DelegatingHandler`
- Reference `RVS.UI.Shared` for shared components and API clients
- Reference `RVS.Domain` for DTOs and validation
- Configure MAUI Essentials registrations (Camera, Barcode, Connectivity)

#### 7.2 Core Views

| View | Route | Key Features |
|---|---|---|
| Login | `/login` | Auth0 redirect via native browser; token stored in SecureStorage |
| My Jobs | `/` (home) | Assigned SRs via `POST search` with `assignedTechnicianId` filter; pull-to-refresh; Bay # and priority shown |
| SR Detail | `/jobs/{id}` | Full intake review: vehicle info, customer issue, photos, diagnostics, technician summary. Large tap targets. |
| Section 10A Entry | `/jobs/{id}/outcome` | Single-screen outcome form: failure mode picker, repair action picker, labor hours (+/- buttons), parts (optional), notes (optional), "Complete Job" button |
| Photo Capture | Modal overlay | MAUI camera API; capture + attach to SR; direct SAS upload when online |

#### 7.3 Job Access Methods

Per Feature spec (`RVS_Features_Tech_Mobile.md`):

| Method | Implementation |
|---|---|
| **My Jobs queue** | Default home view; `POST search` with `assignedTechnicianId = userId` claim |
| **VIN/QR scan** | MAUI barcode SDK (ZXing.NET.MAUI or BarcodeReader); scanned value → `POST search` with `assetId` filter; first open SR auto-opens |
| **Tap from list** | Simple navigation from My Jobs list → SR Detail |

#### 7.4 Section 10A Outcome Entry

The technician's primary workflow — must complete in **3–5 seconds** for common cases:

1. Tap "Complete Job" on SR Detail
2. Outcome form pre-suggests: failure mode, repair action (from AI technician summary if available)
3. Technician confirms or adjusts via dropdown pickers (large touch targets)
4. Labor hours: +/- stepper buttons (0.5 hr increments)
5. Tap "Submit" → `PUT api/dealerships/{id}/service-requests/{srId}` with `ServiceEventEmbedded` fields
6. Success toast → return to My Jobs

Lookup values (failure modes, repair actions, component types) loaded from `GET api/lookups/{category}` and cached locally.

#### 7.5 Photo Capture

- MAUI `MediaPicker.CapturePhotoAsync()` for camera access
- Preview → confirm → upload via SAS URL (same as Cust_Intake attachment flow)
- `POST api/dealerships/{id}/service-requests/{srId}/attachments` for metadata registration
- Photos queued locally if offline (uploaded on reconnect)

#### 7.6 Offline Queue (SQLite)

Per FR-TECH-01:
- Monitor connectivity via `Connectivity.Current.NetworkAccess`
- When offline: `PUT` and `POST attachments` requests serialized to SQLite table (entity: `PendingRequest` with URL, method, body, timestamp)
- When connectivity restored: replay queue sequentially in FIFO order
- Optimistic concurrency: if server returns `409 Conflict` (newer `updatedAtUtc`), surface conflict to technician with option to force-overwrite or discard local changes
- Queue retention: up to 72 hours per NFR-AVAIL-05

#### 7.7 Glove-Friendly UX

- Minimum tap target: 48x48dp (recommended 56dp for glove use)
- Large font sizes for bay readability
- High contrast color scheme
- Pull-to-refresh on My Jobs
- No complex gestures — tap and swipe only

**Exit Criteria:** Technician can log in, see My Jobs, scan a VIN/QR to open a job, review customer intake, record Section 10A outcome in under 5 seconds, capture and upload a photo, and have pending updates survive an offline period and sync on reconnect.

---

### Phase 8 — Testing & Security Hardening (Days 56–60) {Week 11 second half – Week 12 first half}

**Goal:** 80% test coverage on domain/service layers. All security requirements verified.

#### 8.1 Unit Test Project (`RVS.Tests.Unit`)

Create xUnit project. Priority test areas per §12.1:

| Test Class | Covers | Count (est.) |
|---|---|---|
| `StatusTransitionsTests` | All valid + invalid transitions (§7.2) | ~10 |
| `CustomerProfileServiceTests` | 3 asset ownership branches + reactivation | ~8 |
| `GlobalCustomerAcctServiceTests` | Token generation, format, expiry | ~6 |
| `IntakeOrchestrationServiceTests` | Step sequencing, AI fallback, notification fire-and-forget | ~12 |
| `ClaimsServiceTests` | All claim accessors with valid/missing/malformed | ~10 |
| `CategorizationServiceTests` | Timeout → fallback; structured JSON parsing | ~6 |
| `SearchInputValidatorTests` | Blocked chars → 400; clean input passes | ~8 |
| `VinValidatorTests` | Format, length, check digit | ~6 |
| `SlugValidatorTests` | Regex enforcement | ~4 |
| `ServiceRequestServiceTests` | CRUD, batch outcome validation, concurrency | ~10 |
| `AttachmentServiceTests` | MIME validation, size limits, SAS generation | ~6 |
| `LocationServiceTests` | Atomic slug create/rename/delete | ~6 |

**Mocking strategy:** Use NSubstitute or Moq for repository interfaces. Services under test get mocked dependencies injected.

#### 8.2 Integration Test Project (`RVS.Tests.Integration`)

Create xUnit project targeting Cosmos Emulator. Priority scenarios per §12.2:

| Test | Validates |
|---|---|
| Full intake — new customer + new VIN | 5 Cosmos docs created; 201 response correct |
| Returning customer intake | Reuses GlobalCustomerAcct + CustomerProfile |
| VIN transfer | Old owner deactivated, new activated |
| Magic-link status — valid token | Cross-dealer SRs returned |
| Magic-link status — expired token | 410 Gone |
| Slug not found | 404 |
| Disabled tenant | 403 |
| Batch outcome 25 SRs | All updated; 26th rejected |
| Attachment upload | Correct blob path |
| Rate limiting | 429 on threshold breach |

#### 8.3 Security Checklist

Verify against Tech PRD §9:

| Requirement | Verification |
|---|---|
| SEC-AUTH-01: JWT RS256 validation | Integration test with invalid/expired tokens |
| SEC-AUTH-02: tenantId on every query | Code review + unit tests |
| SEC-AUTH-03: locationIds claim check | Unit test ClaimsService.HasAccessToLocation |
| SEC-INPUT-01: Max length enforcement | Unit tests for all string fields |
| SEC-INPUT-02: Slug regex | Unit test |
| SEC-INPUT-03: Parameterized queries | Code review (grep for string concatenation in queries) |
| SEC-INPUT-04: MIME magic bytes | Unit test with spoofed content-type |
| SEC-INPUT-05: VIN validation | Unit test |
| SEC-PRIV-01: No PII in logs | Code review + log output inspection |
| SEC-PRIV-02: SAS expiry | Integration test |
| SEC-PRIV-04: No PII in telemetry | Code review |

**Exit Criteria:** `dotnet test` passes. ≥80% coverage on `RVS.Domain` and `RVS.API/Services`. All security checklist items verified.

---

### Phase 9 — Deployment, CI/CD & Design Partner Onboarding (Days 61–65) {Week 12 second half – Week 13}

**Goal:** MVP deployed to Azure staging. All three apps accessible. Design partners onboarded.

#### 9.1 Infrastructure Setup

| Resource | Action |
|---|---|
| Azure App Service (API) | Deploy API; configure Managed Identity; Always On |
| Azure Static Web Apps (`Cust_Intake`) | Deploy WASM; custom domain `app.rvserviceflow.com` |
| Azure Static Web Apps (`Mngr_Desktop`) | Deploy WASM; same hosting pattern as Cust_Intake |
| Cosmos DB | Provision account; run seed tool for 9 containers; verify index policies |
| Azure Blob Storage | Create `rvs-attachments` container; configure CORS for direct SAS upload |
| Azure Key Vault | Store: OpenAI key, SendGrid key; grant API Managed Identity `get` + `list` |
| App Insights | Link to API + frontends; configure availability test on `/health` |
| Auth0 | Configure API audience; create dev Organization; seed test users with roles |

**Tech_Mobile** distribution:
- Android: internal testing track on Google Play or direct APK sideload for design partners
- iOS: TestFlight for design partner technicians
- No public app store listing required for MVP

#### 9.2 GitHub Actions CI/CD

| Workflow | Trigger | Steps |
|---|---|---|
| `build-test.yml` | Push to any branch / PR | `dotnet build`, `dotnet test`, coverage report |
| `deploy-staging.yml` | Merge to `main` | Build → publish → deploy API + Cust_Intake + Mngr_Desktop to staging |
| `deploy-production.yml` | Manual approval | Same as staging with production config |
| `build-mobile.yml` | Tag `mobile-v*` | Build MAUI → produce APK + IPA; upload to release artifacts |

Use GitHub OIDC → Azure Workload Identity Federation (no long-lived secrets).

#### 9.3 Design Partner Onboarding Checklist

For each of the 5 design partner dealerships:
1. Create Auth0 user with `app_metadata.tenantId` (MVP hybrid strategy)
2. Create TenantConfig via `POST api/tenants/config`
3. Create Dealership and Location(s) via API
4. Generate QR codes for each location
5. Invite staff users: assign roles (`dealer:owner`, `dealer:advisor`, `dealer:technician`) with appropriate `locationIds`
6. Install `Tech_Mobile` on technician devices (APK/TestFlight)
7. Verify full cycle end-to-end: intake → dashboard → SR update → Section 10A → status page

#### 9.4 Post-MVP Backlog (Prioritized)

| Priority | Item | Effort |
|---|---|---|
| P0 | Dedicated SignalR hub for real-time Service Board (replaces long polling) | 3–5 days |
| P1 | `Tech_Mobile` voice notes (MAUI Essentials speech-to-text) | 3–5 days |
| P1 | `Tech_Mobile` bay-based tablet kiosk mode | 3–5 days |
| P1 | SendGrid styled email templates | 2–3 days |
| P1 | DMS SFTP export | 1 week |
| P2 | Azure Tables analytics pre-aggregation | 1 week |
| P2 | Load testing (50 concurrent/min) | 2–3 days |
| P2 | E2E automated tests | 1 week |
| P2 | Customer Auth0 accounts (optional persistent login) | 1 week |
| P3 | Change feed for AssetLedger enrichment | 1–2 weeks |

**Exit Criteria:** 5 design partners can complete the full cycle: customer submits intake → advisor sees in dashboard → technician receives on mobile → technician updates Section 10A → customer sees status via magic-link. Zero `platform:admin` intervention after initial setup.

---

## 4. Week-by-Week Summary

| Week | Phase | Deliverable | Risk |
|---|---|---|---|
| **1** | Phase 1 | Domain compiles, Cosmos seeded, health check works | Low — mechanical |
| **2** | Phase 2 | All repositories + CRUD services work via Postman | Low — follows existing patterns |
| **3** | Phase 3a | Integration clients (NHTSA, OpenAI, Blob) with mocks + customer services | Medium — external API quirks |
| **4** | Phase 3b–4a | Intake orchestration E2E + controllers started | **High** — orchestration complexity |
| **5** | Phase 4b–5a | Controllers complete + RVS.UI.Shared + Cust_Intake scaffold | Medium — render mode mixing |
| **6** | Phase 5b | Intake wizard complete (all 7 steps) | Medium — SAS upload integration |
| **7** | Phase 5c–6a | Status page + Mngr_Desktop scaffold + auth + SR queue | Medium — WASM project setup |
| **8** | Phase 6b | Mngr_Desktop Service Board + analytics + locations | Medium — long polling integration |
| **9** | Phase 6c–7a | Mngr_Desktop polish + Tech_Mobile scaffold + auth + My Jobs | Medium — MAUI setup |
| **10** | Phase 7b | Tech_Mobile Section 10A + photo + VIN scan + offline queue | **High** — native APIs + SQLite |
| **11** | Phase 8 | Unit tests + integration tests + security hardening | Medium — coverage target |
| **12** | Phase 9 | Deployment + CI/CD + design partner onboarding | Medium — infra provisioning |

---

## 5. Risk Register

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Intake orchestration complexity (7 steps, 5 docs) | Schedule slip Weeks 3–4 | Medium | Build integration test first as executable spec; test each step independently |
| Cosmos Emulator issues on Windows | Blocks integration testing | Medium | Fall back to dedicated Azure test DB; budget $5/month |
| Auth0 free-plan Organization limit | Blocks multi-tenant testing | Low | Use `app_metadata` hybrid strategy (already designed) |
| Blazor WASM mixed render mode (SSR + WASM) | Frontend complexity in Cust_Intake | Medium | Start with pure WASM; add SSR for landing/status pages last |
| SAS direct-upload from WASM to Blob | CORS complexity | Medium | Test SAS upload in Week 3 (Phase 3); don't defer to frontend phase |
| MAUI build pipeline stability | Blocks Tech_Mobile delivery | **High** | Pin MAUI workload version; test build pipeline in Week 1 as spike |
| MAUI Blazor Hybrid + native APIs (camera, barcode) | Integration bugs | Medium | Use well-supported libraries (ZXing.NET.MAUI); build native spike in Week 1 |
| Offline SQLite sync conflicts | Data integrity risk | Medium | Design conflict UI upfront; test with simulated offline in Week 10 |
| Azure OpenAI quota/availability | AI questions fail in demo | Low | Rule-based fallback already designed; test fallback path explicitly |
| .NET 10 preview instability | Build issues | Low | Pin SDK version; avoid preview-only APIs |
| Three frontends for one developer | Schedule pressure | **High** | Aggressively share code via RVS.UI.Shared; keep Tech_Mobile MVP-minimal; defer polish to post-MVP |

---

## 6. AI-Assistance Strategy

The following tasks are highly accelerated by AI coding tools and should be delegated aggressively:

| Task | AI Leverage | Human Focus |
|---|---|---|
| Entity classes from PRD specs | ~90% AI-generated | Review Cosmos JSON annotations |
| DTOs (30 record types) | ~95% AI-generated | Review naming consistency |
| Mapper classes | ~90% AI-generated | Review null handling |
| Repository boilerplate | ~80% AI-generated | Review query correctness, parameterization |
| Controller scaffolding | ~85% AI-generated | Review auth policies, route conventions |
| Unit test generation | ~80% AI-generated | Review edge cases, add failure scenarios |
| Seed data JSON | ~90% AI-generated | Review referential integrity |
| Blazor component HTML/CSS | ~70% AI-generated | UX polish, mobile testing |
| Shared API client services | ~85% AI-generated | Review error handling, retry logic |
| MAUI project scaffold + native service wiring | ~60% AI-generated | **Human debugs platform-specific issues** |
| MAUI offline SQLite queue | ~50% AI-generated | **Human designs conflict resolution, tests sync edge cases** |
| Intake orchestration logic | ~50% AI-generated | **Human designs step ordering, error handling, concurrency** |
| Integration client error handling | ~60% AI-generated | **Human designs fallback behavior, timeout strategy** |
| Auth policy wiring | ~70% AI-generated | **Human verifies security boundaries** |

**Rule of thumb:** Use AI to generate the first draft of any file with a clear spec (entity → PRD §7.1, DTO → §8.3, route → §8.2). Human reviews security boundaries, error paths, concurrency, and native platform integration.

---

## 7. MAUI Early Spike (Risk Mitigation)

Because `Tech_Mobile` is the highest-risk frontend, perform a **1-day spike on Day 1** (parallel to Phase 1 entity work) to validate:

1. MAUI Blazor Hybrid project template builds and runs on Android emulator
2. Auth0 OIDC login works via `WebAuthenticator`
3. Camera capture works via `MediaPicker.CapturePhotoAsync()`
4. ZXing.NET.MAUI (or equivalent) barcode scanner works
5. SQLite package installs and basic read/write works

This spike catches build pipeline or compatibility issues 9 weeks before Tech_Mobile is scheduled. If MAUI proves unstable, the fallback is `Mngr_Desktop` on a tablet (same as v1.0 plan) with no impact on the backend schedule.

---

*Last updated: March 21, 2026. Derived from RVS_Technical_PRD.md v1.0 and FrontEnd feature documentation.*
