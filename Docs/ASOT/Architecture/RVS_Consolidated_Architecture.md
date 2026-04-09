# RV Service Flow (RVS) — Consolidated Architecture

**Authoritative Source of Truth (ASOT) — March 18, 2026**

This document consolidates the Core Backend Architecture, SaaS WAF Assessment, and ASOT Gap Analysis into a single reference. For Auth0 identity, RBAC roles/permissions, ClaimsService, and authorization policies, see the companion document **RVS_Auth0_Identity_Version2.md**.

---

## Executive Summary

RVS is a B2B SaaS platform for RV dealership service management. The backend is built on ASP.NET Core (.NET 10), Azure Cosmos DB (NoSQL), and Auth0 identity. The frontend consists of three purpose-built applications: a standalone Blazor WebAssembly PWA customer intake portal (Blazor.Intake), a standalone Blazor WebAssembly service manager desktop app (Blazor.Manager), and a MAUI Blazor Hybrid technician mobile app (MAUI.Tech).

**Multi-tenancy Model:** Tenant = Corporation (e.g., Blue Compass RV). One tenant maps to:

- One Auth0 Organization
- One Cosmos DB partition keyed on `tenantId`
- One to many Physical Locations (intake URLs per location)
- Cross-location analytics, shared customer profiles

**Key Aggregates:**

1. **ServiceRequest** — Customer intake (Cosmos: `/tenantId`)
2. **CustomerProfile** — Tenant-scoped shadow record (Cosmos: `/tenantId`)
3. **GlobalCustomerAcct** — Cross-dealer identity by email (Cosmos: `/email`)
4. **AssetLedgerEntry** — Append-only service history (Cosmos: `/assetId`, the "data moat")
5. **Location**, **Dealership**, **TenantConfig**, **LookupSet** — Supporting aggregates

**Data Moat:** Append-only `AssetLedgerEntry` accumulates proprietary service intelligence indexed by asset ID. Powers Section 10A service failure/repair analytics (Phase 5–6).

**Intake Flow (7 Steps, ~11 RU):**

1. Slug → Location & Tenant lookup
2. Customer email → Global identity (create if first visit)
3. Tenant-scoped profile resolution + asset ownership tracking
4. ServiceRequest creation with auto-categorization (AI + rule-based fallback)
5. Append to asset ledger
6. Update linkages (customer request count, stable magic-link token)
7. Send confirmation email

**Magic-Link Design:** Customer sees all their SRs across corporations via `api/status/{token}`. Token embeds email-hash prefix enabling O(1) partition-key derivation (no cross-partition query).

### What Is Already Well-Designed

These decisions are architecturally sound and should be preserved:

- **Tenant = Corporation (not Location)** keeps Blue Compass as one Auth0 Organization, one Cosmos partition, and enables cross-location analytics cheaply.
- **Three-container identity split** (`serviceRequests`/`tenantId` + `globalCustomerAccts`/`email` + `assetLedger`/`assetId`) correctly serves three distinct access patterns without forcing a bad partition key on any of them.
- **Cosmos Gateway mode** for server-side caching of `slugLookup`, `tenantConfigs`, and `lookupSets` is elegant and free — no Redis needed at this scale.
- **CustomerSnapshotEmbedded denormalization** eliminates joins from the dealer dashboard read path (~1 RU/view).
- **Azure OpenAI → rule-based fallback** pattern on `ICategorizationService` correctly treats the external dependency as optional, not critical-path.
- **Email-hash prefix in magic-link token** (`base64url(SHA256(email)[0..8]):random_bytes`) avoids cross-partition scan on token validation.
- **Append-only AssetLedger** is the right strategic bet — proprietary cross-dealer service event data is the structural data moat.
- **Auth0 hybrid MVP strategy** (`app_metadata` → full Organizations on commercialization) is pragmatic and preserves backend compatibility.

---

## 1. Solution Structure and Layering

**Technology Stack:** ASP.NET Core (.NET 10, C# 14), Azure Cosmos DB (SQL API), Azure Blob Storage, Auth0 identity. Three front-end applications: Blazor WebAssembly Standalone PWA (Blazor.Intake), Blazor WebAssembly Standalone (Blazor.Manager), MAUI Blazor Hybrid (MAUI.Tech). All Blazor frontends use **MudBlazor 9.x** (Material Design 3) as the UI component library.

**Layered Architecture:**

- **RVS.API** — ASP.NET Core REST API; request handlers, service layer, middleware pipeline
- **RVS.Domain** — Zero infrastructure dependencies; entities, DTOs, interfaces, validation rules
- **RVS.Infra.*** — Azure service implementations (Cosmos repositories, Blob Storage, Table Storage, credential management)
- **RVS.Data.Cosmos.Seed** — Development seed data
- **RVS.UI.Shared** — Razor Class Library; shared Razor components, CSS design tokens, API client services (consumed by all three front-end apps)
- **RVS.Blazor.Intake** — Blazor WebAssembly (Standalone PWA); customer-facing intake portal. All pages (landing, wizard, confirmation, status) are routes within a single WASM SPA. A service worker caches the WASM runtime after first load. No SSR, no SignalR.
- **RVS.Blazor.Manager** — Blazor WebAssembly (Standalone); service manager desktop app for triage, Service Board, analytics, and batch operations. Long polling (configurable interval) for near-real-time updates; SignalR deferred to vNEXT.
- **RVS.MAUI.Tech** — MAUI Blazor Hybrid (iOS + Android); technician mobile app with offline sync, native camera/barcode/voice, and glove-friendly UI

**Design Patterns:**

- Clean architecture with clear dependency direction (API → Service → Domain; Infra injected via DI)
- Repository pattern for data access; Service layer for orchestration and business rules
- Mapper classes for entity ↔ DTO transformations
- Middleware pipeline for cross-cutting concerns (exception handling, authentication, tenant access gating)
- Interface-driven service dependencies for testability and fallback strategies

See `.github/copilot-instructions.md` for detailed coding patterns.

---

## 2. Multi-Location Tenancy Model

### 2.1 The Blue Compass Problem

Large dealer groups like Blue Compass RV operate 100+ locations under a single corporation. The architecture must support both single-location independents and multi-location enterprises without separate code paths.

```
Blue Compass RV (Corporation / Auth0 Organization / Cosmos Partition)
│
├── Blue Compass RV - Salt Lake City     (Location)
├── Blue Compass RV - Denver             (Location)
├── Blue Compass RV - Tampa              (Location)
├── ExploreUSA RV - San Antonio          (Location, subsidiary brand)
├── Motor Home Specialist - Alvarado, TX (Location, subsidiary brand)
└── ... 100+ more locations
```

### 2.2 Key Mapping

| Concept | RVS Entity | Auth0 Mapping | Cosmos Role | Example |
|---|---|---|---|---|
| Corporation / Dealer Group | `Dealership` | Auth0 Organization | Partition key (`tenantId`) | `org_blue_compass_rv` |
| Physical service site | `Location` | Custom claim `locationIds` | Filter within partition | `loc_blue_compass_slc` |
| Independent single-location | 1 `Dealership` + 1 `Location` | 1 Organization | Same as above | `org_happy_trails_rv` |

### 2.3 Design Rationale

- **Blue Compass = 1 Auth0 Organization, not 100+.** Keeps Auth0 costs sane, allows corporate-wide user management.
- **`tenantId` remains the Cosmos partition key.** All locations for a corporation share the same partition. Cross-location queries within a corporation are single-partition (~3 RU).
- **Customer profiles are tenant-scoped (per corporation, not per location).** Blue Compass sees one John Doe record across all their locations.
- **Asset ownership transfers within a corporation** are intra-partition operations.
- **`locationId` is a filter within the partition**, not a partition boundary.

---

## 3. Domain Model Overview

**Core Aggregates:**

1. **ServiceRequest** — Central aggregate. Represents a customer service submission at a specific location. Partitioned by `/tenantId`. Key embedded sub-entities are `CustomerSnapshotEmbedded` (denormalized for read efficiency), `AssetInfoEmbedded`, `ServiceEventEmbedded` (technician repair outcomes), and `DiagnosticResponseEmbedded` (AI diagnostic responses).

2. **CustomerProfile** — Tenant-scoped shadow record created automatically on first intake. No customer sign-up. Maps to a global identity via `GlobalCustomerAcctId`. Tracks asset ownership transitions within a corporation.

3. **GlobalCustomerAcct** — Cross-dealer global identity by email. Enables customers to see their service history across all corporations via magic-link status page.

4. **AssetLedgerEntry** — Append-only service event log (data moat). One entry per service request, partitioned by `/assetId`. Enables Section 10A asset service intelligence. Written at intake; enriched asynchronously when repair is completed (Phase 5–6 via change feed).

5. **Dealership** — Corporation / dealer group. One Auth0 Organization per dealership. Many Locations per Dealership.

6. **Location** — Physical service site. Slug-based intake URL. Configurable intake form settings.

7. **TenantConfig** — Tenant settings, access gate, SFTP export config, billing config. **⚠️ SFTP private keys must move to Key Vault** — store only `privateKeySecretUri` in `TenantConfig`.

8. **LookupSet** — Reference data (issue categories, component types, failure modes).

**Embedding Strategy:**

- Entities embed related data that is always retrieved together (no joins on hot paths)
- Denormalized snapshots (e.g., `CustomerSnapshotEmbedded` in `ServiceRequest`) are point-in-time copies, by design — they do not auto-update
- Array fields (e.g., `assetsOwned`, `diagnosticResponses`) are queryable via Cosmos indexing

For detailed entity specifications and DTOs, see `RVS.Domain/Entities/` and `RVS.Domain/DTOs/` source code.

---

## 4. Cosmos DB Container Design

### 4.1 Container Summary

| Container | Partition Key | Documents | RU Mode | Purpose |
|---|---|---|---|---|
| `serviceRequests` | `/tenantId` | `serviceRequest` | Autoscale 400–4000 | Core service request data |
| `customerProfiles` | `/tenantId` | `customerProfile` | Autoscale 400–1000 | Tenant-scoped customer view |
| `globalCustomerAccts` | `/email` | `globalCustomerAcct` | Autoscale 400–1000 | Cross-dealer identity federation |
| `assetLedger` | `/assetId` | `assetLedgerEntry` | Autoscale 400–1000 | Section 10A data moat |
| `dealerships` | `/tenantId` | `dealership` | Autoscale 400–1000 | Corporation profiles |
| `locations` | `/tenantId` | `location` | Autoscale 400–1000 | Physical service locations |
| `tenantConfigs` | `/tenantId` | `tenantConfig` | Autoscale 400–1000 | Tenant settings, access gate, billing config |
| `lookupSets` | `/category` | `lookupSet` | Autoscale 400–1000 | Issue categories, component types |
| `slugLookup` | `/slug` | `slugLookup` | Autoscale 400–1,000 | Slug → tenantId + locationId point read |

> **Note:** `globalCustomerAccts`, `dealerships`, `tenantConfigs`, and `lookupSets` updated to Autoscale 400–1,000 per SaaS Assessment recommendation. Autoscale reduces per-container monthly floor from ~$25 (manual 400) to ~$5.84/month (billed at 10% of max). Total floor drops from ~$225/month to ~$52/month for the 9 containers.

### 4.2 Why Three Identity Containers?

One document cannot serve three different access patterns:

| Identity Layer | Partition Key | Optimized For |
|---|---|---|
| **Tenant-scoped customer** (Corp A's view of John) | `/tenantId` | Dashboard, asset ownership, search |
| **Global customer** (John across all corporations) | `/email` | Intake email resolution (~1 RU), cross-dealer status |
| **Global asset** (RV:1ABC across all owners/dealers) | `/assetId` | Asset service history (~1 RU), Section 10A analytics |

### 4.3 Multi-Location Query Patterns

| Scenario | Query | Partition Behavior |
|---|---|---|
| Location advisor views their SRs | `WHERE tenantId = @t AND locationId = @loc` | Single-partition, filtered |
| Corporate admin views ALL SRs | `WHERE tenantId = @t` | Single-partition, no location filter |
| Regional manager views West SRs | `WHERE tenantId = @t AND locationId IN (@loc1, @loc2, @loc3)` | Single-partition, IN filter |
| Customer profile resolution | `WHERE tenantId = @t AND globalCustomerAcctId = @id` | Single-partition — one profile per corporation |
| Asset history across all dealers | `WHERE assetId = @a` | Single-partition in assetLedger |

### 4.4 Key Indexing Policies

**`serviceRequests`** — Included paths: `/tenantId/?`, `/locationId/?`, `/status/?`, `/customerProfileId/?`, `/createdAtUtc/?`, `/issueCategory/?`, `/assignedTechnicianId/?`, `/assignedBayId/?`, `/asset/assetId/?`, `/scheduledDateUtc/?`, `/priority/?`, `/diagnosticResponses/[]/selectedOptions/?`. Composite indexes: `[tenantId ASC, locationId ASC, createdAtUtc DESC]`, `[tenantId ASC, locationId ASC, status ASC, createdAtUtc DESC]`, `[tenantId ASC, assignedTechnicianId ASC, status ASC, createdAtUtc DESC]`, `[tenantId ASC, locationId ASC, scheduledDateUtc ASC]`, `[tenantId ASC, priority ASC, status ASC, createdAtUtc DESC]`.

> **Gap:** `ServiceRequestSearchRequestDto` supports filtering by `CustomerName` but there is no `customer/firstName/?` or `customer/lastName/?` index path and no composite index for name search. This becomes a full-partition scan. Either add a composite index on `[tenantId ASC, customer/lastName ASC, customer/firstName ASC]` or explicitly document that `CustomerName` triggers a full scan (acceptable at MVP volume).

**`customerProfiles`** — Included paths: `/tenantId/?`, `/email/?`, `/globalCustomerAcctId/?`, `/assetsOwned/[]/assetId/?`, `/assetsOwned/[]/status/?`. Composite index: `[tenantId ASC, email ASC]`. Unique key: `[/tenantId, /email]`.

**`locations`** — Included paths: `/tenantId/?`, `/slug/?`, `/regionTag/?`.

**`slugLookup`** — No secondary indexes required. All reads are point reads by `/slug` (partition key = document id). Index policy: excluded paths `/*`, included paths `/_etag/?` only.

### 4.5 Cosmos DB Document Examples

**Service Request:**

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "type": "serviceRequest",
  "tenantId": "org_blue_compass_rv",
  "locationId": "loc_blue_compass_slc",
  "status": "New",
  "customerProfileId": "cp_001",
  "customer": {
    "firstName": "John",
    "lastName": "Doe",
    "email": "john@example.com",
    "phone": "555-0123",
    "isReturningCustomer": true,
    "priorRequestCount": 2
  },
  "asset": {
    "assetId": "RV:1ABC234567",
    "manufacturer": "Grand Design",
    "model": "Momentum 395G",
    "year": 2023
  },
  "issueDescription": "Slide-out hydraulic pump makes grinding noise...",
  "issueCategory": "Slide System",
  "technicianSummary": "Possible hydraulic pump failure on slide-out mechanism...",
  "diagnosticResponses": [
    {
      "questionText": "What happens when you try to extend the slide?",
      "selectedOptions": ["Grinding noise"],
      "freeTextResponse": null
    },
    {
      "questionText": "Does the slide operate on hydraulic or electric mechanisms?",
      "selectedOptions": ["Hydraulic"],
      "freeTextResponse": null
    },
    {
      "questionText": "Have you noticed any fluid leaks near the slide mechanism?",
      "selectedOptions": ["Yes"],
      "freeTextResponse": "Small puddle under the RV near the slide"
    }
  ]
}
```

---

## 5. Service & Repository Layer

All services are `sealed`, injected via dependency injection. All repositories follow the repository pattern for data access abstraction. For detailed method signatures, DTOs, and implementation patterns, see:

- Service interfaces in `RVS.Domain/Interfaces/`
- Repository implementations in `RVS.Infra.AzCosmosRepository/`, `RVS.Infra.AzBlobRepository/`, `RVS.Infra.AzTablesRepository/`
- Service implementations in `RVS.API/Services/`
- DTO definitions in `RVS.Domain/DTOs/`

**Key service responsibilities:**

- **`IServiceRequestService`** — Orchestrates the core intake flow (Steps 1–6), status transitions, and batch outcome operations. Validates transitions via `StatusTransitions.cs`.
- **`ICustomerProfileService`** — Profile resolution, asset ownership transfer, reactivation logic.
- **`IGlobalCustomerAcctService`** — Global identity federation, magic-link token generation/validation.
- **`IAssetLedgerService`** — Append-only event recording for the data moat.
- **`IAttachmentService`** — Blob upload/download, SAS URI generation.
- **`ICategorizationService`** — Issue auto-categorization (AI-powered with rule-based fallback), technician summary generation, and transcript cleanup (see Section 16).
- **`ILocationService`** — Location CRUD with slug-lookup table synchronization.

---

## 6. Intake Orchestration Flow

**Seven-step sequence when a customer submits a service request:**

1. **Slug Resolution** — O(1) lookup in `slugLookup` container (gateway-cached) → returns `tenantId` + `locationId` (~1 RU cold, ~0 RU cached)
2. **Global Identity Resolution** — Point read in `globalCustomerAccts` by email. Create if first visit. (~1 RU)
3. **Profile Resolution & Asset Ownership** — Query `customerProfiles` by tenant + identity. Handle asset transfer logic (deactivate old owner, activate current). (~2–3 RU)
4. **ServiceRequest Creation** — Create SR in `serviceRequests` container. Embed customer snapshot. Call `ICategorizationService` for issue categorization. (~1 RU)
5. **Data Moat (Append-Only Ledger)** — Write `AssetLedgerEntry` to `assetLedger` container (partitioned by assetId). (~1 RU)
6. **Linkage Updates** — Increment customer request count, rotate magic-link token, add linked profile reference to global identity. (~2 RU)
7. **Notification** — Send confirmation email with magic-link (fire-and-forget, no Cosmos cost)

**Total RU cost per intake: ~10.8–11.8 RU** (accounting for gateway caching and cold misses)

### Intake Race Condition — Compensation Strategy

Steps 1–5 execute sequentially without a transaction. If Step 4 (ServiceRequest write) succeeds but Step 5 (AssetLedger write) fails, the system is in a partially inconsistent state.

**Architectural decision:** Accept eventual consistency for the AssetLedger. The Phase 5–6 change feed enrichment pattern already assumes the ledger entry may be incomplete at intake time. If Step 5 fails, the SR exists, the customer is notified, and the ledger gap is recoverable via the change feed consumer that watches `serviceRequests` for completions. Step 7 (Notification) is gated only on the SR write succeeding — a ledger write failure does not block the customer confirmation.

---

## 7. Service Layer

All services are `sealed`, inject repository interfaces + `IUserContextAccessor`, guard clauses first, return domain entities.

### 7.1 ServiceRequestService (Primary Orchestrator)

Implements the 7-step intake flow from Section 6. Injects: `IServiceRequestRepository`, `IGlobalCustomerAcctService`, `ICustomerProfileService`, `IAssetLedgerService`, `IDealershipService`, `ILocationService`, `ICategorizationService`, `INotificationService`, `ISmsNotificationService`, `INotificationOrchestrator`, `IUserContextAccessor`.

**`CreateServiceRequestAsync(tenantId, locationId, request)`** executes Steps 1–6 sequentially:

1. Calls `IGlobalCustomerAcctService.ResolveOrCreateIdentityAsync` with customer email/name/phone from the request DTO.
2. Calls `ICustomerProfileService.ResolveOrCreateProfileAsync` with the resolved identity, asset identifier, and asset info. This handles shadow profile creation and asset ownership transfer.
3. Builds the `ServiceRequest` entity. Stamps `tenantId` and `locationId`. Embeds `CustomerSnapshotEmbedded`. Embeds `DiagnosticResponses` from the request DTO. Calls `ICategorizationService.CategorizeAsync` for auto-categorization and technician summary.
4. Calls `IAssetLedgerService.RecordServiceEventAsync` to append the data moat entry.
5. Updates linkages: increments `TotalRequestCount`, conditionally updates the magic-link token on the global identity. Token format: `base64url(SHA256(email)[0..8]):random_bytes`.
6. Fires `INotificationOrchestrator.SendIntakeConfirmationAsync` which routes to email and/or SMS based on customer preference (fire-and-forget).

### 7.2 CustomerProfileService (Shadow Profile + Asset Ownership)

Implements `ResolveOrCreateProfileAsync`. Two phases:

**Phase 1 — Profile Resolution:**

- Find by `globalCustomerAcctId` within tenant partition.
- If not found → create new shadow profile with all customer fields, no assets owned, zero request count.
- If found → update contact info (firstName, lastName, phone) from the latest submission.

**Phase 2 — Asset Ownership Resolution (three branches):**

- **Same customer, same asset** → update `LastSeenAtUtc`, increment `RequestCount` on the existing Active ownership.
- **Different customer at same corporation owns this asset** → deactivate the previous owner's `AssetsOwnedEmbedded`. Then create or reactivate on the current profile.
- **Brand new asset** → create new Active `AssetsOwnedEmbedded`.

Also handles **reactivation** — if the current customer previously had an Inactive ownership for this asset, the existing ownership is reactivated rather than creating a duplicate.

### 7.3 LocationService

Implements physical location CRUD. `UpsertSlugLookupAsync(slug, tenantId, locationId)` is called within `CreateLocationAsync` and `UpdateLocationAsync`.

**Slug rename sequencing:** On slug rename, the correct order is **WriteNewSlug → UpdateLocation → DeleteOldSlug**. This ensures at least one valid slug always resolves to the location and eliminates the unreachability window. If the new slug write fails, the old slug remains valid. If the old slug deletion fails after the update, the location is reachable via both slugs (safe degradation).

---

## 8. Controllers

Authorization policies referenced below are defined in **RVS_Auth0_Identity_Version2.md**.

### 8.1 Customer-Facing (Unauthenticated)

**IntakeController** — Route: `api/intake/{locationSlug}`. `[AllowAnonymous]`. Resolves location slug → `tenantId` + `locationId`. Endpoints: `GET` (intake config + optional prefill via `?token=`), `POST diagnostic-questions` (AI-generated follow-up questions), `POST service-requests` (submit), `POST service-requests/{id}/attachments` (upload).

**CustomerStatusController** — Route: `api/status`. `[AllowAnonymous]`. Endpoint: `GET {token}` parses the email-hash prefix from the token to derive the partition key, validates the magic link via single-partition point read, returns requests across all dealerships/locations.

### 8.2 Dealer-Facing (Authenticated)

**ServiceRequestsController** — Route: `api/dealerships/{dealershipId}/service-requests`. Actions: `GET {id}` (CanReadServiceRequests), `POST search` (CanSearchServiceRequests), `PUT {id}` (CanUpdateServiceRequests), `PATCH batch-outcome` (CanUpdateServiceRequests — up to 25 SRs per call), `DELETE {id}` (CanDeleteServiceRequests). Location filtering applied server-side via `ClaimsService.HasAccessToLocation()`.

> **Phase 2 — Request Additional Information:** MVP: Managers contact the customer via phone/email outside the system using `CustomerSnapshotEmbedded` contact details. Phase 2: Add `POST api/dealerships/{id}/service-requests/{srId}/follow-ups` with `INotificationOrchestrator.SendFollowUpRequestAsync` (routes to email and/or SMS based on customer preference).

**AttachmentsController** — Route: `api/dealerships/{dealershipId}/service-requests/{serviceRequestId}/attachments`. Actions: `POST` (CanUploadAttachments — authenticated upload for dealer staff/technicians), `GET {attachmentId}` (CanReadAttachments), `DELETE {attachmentId}` (CanDeleteAttachments).

**DealershipsController** — Route: `api/dealerships`. Actions: `GET` (CanReadDealerships), `GET {id}` (CanReadDealerships), `PUT {id}` (CanUpdateDealerships).

**LocationsController** — Route: `api/locations`. Actions: `GET` (CanReadLocations), `GET {id}` (CanReadLocations), `POST` (CanCreateLocations), `PUT {id}` (CanUpdateLocations), `GET {id}/qr-code` (CanReadLocations).

**TenantsController** — Route: `api/tenants`. Actions: `POST config`, `GET config`, `PUT config`, `GET access-gate` (all CanManageTenantConfig).

**LookupsController** — Route: `api/lookups`. Action: `GET {lookupSetId}` (CanReadLookups).

**AnalyticsController** — Route: `api/dealerships/{dealershipId}/analytics`. Action: `GET service-requests/summary` (CanReadAnalytics). Query parameters: `?from={date}&to={date}&locationId={locId}`.

---

## 9. Middleware Pipeline

| Order | Component | Registration Pattern | Description |
|---|---|---|---|
| 1 | Dev-only endpoints | `MapOpenApi()`, `UseSwaggerUI()` | Development environment only |
| 2 | HTTPS redirection | `UseHttpsRedirection()` | Production only |
| 3 | CORS | `UseCors("AllowBlazorClient")` | Allows Blazor WASM client origins |
| 4 | Rate limiting | `UseRateLimiter()` | Protects public intake + status endpoints |
| 5 | ExceptionHandlingMiddleware | `IMiddleware`, singleton | Catches all unhandled exceptions, returns structured ProblemDetails |
| 6 | Authentication & Authorization | `UseAuthentication()` + `UseAuthorization()` | Auth0 JWT validation + policy checks |
| 7 | TenantAccessGateMiddleware | `RequestDelegate`, scoped injection | Checks `TenantConfig.AccessGate` and trial expiry |
| 8 | Map controllers | `MapControllers()` | Terminal |

> **Cosmos SDK connection mode:** `ConnectionMode.Gateway` enables server-side gateway caching for stable point reads (`slugLookup`, `tenantConfigs`, `lookupSets`), reducing effective RU consumption without application-layer cache code. `TenantAccessGateMiddleware` reads `TenantConfig` on every authenticated request — gateway caching makes this effectively free after the first read per tenant per cache window.

### CORS Origins Per Environment

| Environment | Origins |
|---|---|
| Development | `https://localhost:7xxx` |
| Staging | `https://staging.dashboard.rvserviceflow.com`, `https://staging.app.rvserviceflow.com` |
| Production | `https://dashboard.rvserviceflow.com`, `https://app.rvserviceflow.com` |

The intake form (`app.rvserviceflow.com`) and dealer dashboard (`dashboard.rvserviceflow.com`) are different subdomains and must both be listed.

### TenantAccessGateMiddleware Scale Considerations

The middleware relies on Cosmos Gateway cache (typically 5-minute window). At >1,000 tenants with diversified request patterns, gateway cache hit rate drops and Cosmos RU costs become measurable. **Phase 2 mitigation:** Introduce `TenantConfigCache` (IMemoryCache, scoped per deploy, 5-minute TTL) to eliminate the Cosmos dependency at scale.

---

## 10. Azure Blob Storage Structure

```
rvs-attachments/
  └── {tenantId}/
      └── {locationId}/
          └── {serviceRequestId}/
              ├── att_001_slide_issue.jpg
              ├── att_002_pump_video.mp4
              └── att_003_vin_plate.jpg
```

- **Upload:** Streaming via API (MVP). **Must implement SAS pre-signed upload** for media files to avoid blocking API worker threads on 25 MB video uploads (see Section 20.2).
- **Access:** Time-limited SAS URIs generated on demand.
- **Retention:** Configurable per-tenant in `TenantConfig`. Implement lifecycle management: Hot (0–90 days), Cool (91–365 days), Archive/delete (365+ days per tenant plan).
- **Path includes `locationId`** for storage organization and future per-location retention policies.
- **Container ACL:** Explicitly set `PublicAccess = BlobContainerPublicAccessType.None` on container creation in `BlobRepository.cs`. Add an IaC check or startup assertion that validates the container ACL.

---

## 11. Complete API Route Summary

| Method | Route | Auth | Policy | Purpose |
|---|---|---|---|---|
| `GET` | `api/intake/{locationSlug}?token={t}` | Anonymous | — | Intake config + optional prefill via magic link |
| `POST` | `api/intake/{locationSlug}/service-requests` | Anonymous | — | Submit request → full intake orchestration |
| `POST` | `api/intake/{locationSlug}/diagnostic-questions` | Anonymous | — | AI-generated diagnostic questions |
| `POST` | `api/intake/{locationSlug}/service-requests/{id}/attachments` | Anonymous | — | Upload photo/video |
| `GET` | `api/status/{token}` | Anonymous | — | Customer status page via magic link |
| `GET` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanReadServiceRequests | Request detail |
| `POST` | `api/dealerships/{id}/service-requests/search` | Bearer | CanSearchServiceRequests | Search/filter requests |
| `PUT` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanUpdateServiceRequests | Update request |
| `PATCH` | `api/dealerships/{id}/service-requests/batch-outcome` | Bearer | CanUpdateServiceRequests | Batch apply repair outcome (max 25) |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanDeleteServiceRequests | Delete request |
| `GET` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | CanReadAttachments | Get attachment SAS URL |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | CanDeleteAttachments | Delete attachment |
| `POST` | `api/dealerships/{id}/service-requests/{srId}/attachments` | Bearer | CanUploadAttachments | Upload attachment (authenticated) |
| `GET` | `api/dealerships` | Bearer | CanReadDealerships | List dealerships for tenant |
| `GET` | `api/dealerships/{id}` | Bearer | CanReadDealerships | Dealership detail |
| `PUT` | `api/dealerships/{id}` | Bearer | CanUpdateDealerships | Update dealership |
| `GET` | `api/locations` | Bearer | CanReadLocations | List locations (filtered by user's access) |
| `GET` | `api/locations/{id}` | Bearer | CanReadLocations | Location detail |
| `POST` | `api/locations` | Bearer | CanCreateLocations | Create location |
| `PUT` | `api/locations/{id}` | Bearer | CanUpdateLocations | Update location |
| `GET` | `api/locations/{id}/qr-code` | Bearer | CanReadLocations | Generate intake QR code |
| `GET` | `api/dealerships/{id}/analytics/service-requests/summary` | Bearer | CanReadAnalytics | Request analytics |
| `POST` | `api/tenants/config` | Bearer | CanManageTenantConfig | Bootstrap tenant config |
| `GET` | `api/tenants/config` | Bearer | CanManageTenantConfig | Get tenant config |
| `PUT` | `api/tenants/config` | Bearer | CanManageTenantConfig | Update tenant config |
| `GET` | `api/tenants/access-gate` | Bearer | CanManageTenantConfig | Access gate check |
| `GET` | `api/lookups/{lookupSetId}` | Bearer | CanReadLookups | Lookup values |
| `POST` | `api/billing/stripe/webhook` | Anonymous | — | Stripe webhook (signature-verified) |
| `GET` | `api/tenants/billing/usage` | Bearer | CanManageTenantConfig | Current period usage |

---

## 12. RU Cost Analysis

| Operation | Cosmos Calls | Estimated RU |
|---|---|---|
| **Slug resolution (gateway cold)** | 1 point read (`slugLookup`) | ~1 RU |
| **Slug resolution (gateway cached)** | 1 point read (served from gateway cache) | ~0 RU |
| **TenantConfig read (gateway cached)** | 1 point read (served from gateway cache) | ~0 RU |
| **New customer intake (first visit, first corporation)** | 1 slug + 1 identity miss + 1 identity write + 1 profile write + 1 SR write + 1 ledger write + 1 identity update + 1 profile update | ~11.8 RU |
| **Returning customer intake (same corporation)** | 1 slug + 1 identity hit + 1 profile hit + 1 AssetId check + 1 SR write + 1 ledger write + 2 updates | ~10.8 RU |
| **Returning customer, new corporation** | 1 slug + 1 identity hit + 1 profile miss + 1 profile write + 1 SR write + 1 ledger write + 2 updates | ~11.8 RU |
| **Magic-link status page** | 1 point read (token → partition via email-hash prefix) + N point reads (linked SRs) | ~1 + N RU |
| **Dealer dashboard: view request** | 1 point read (SR — snapshot embedded) | ~1 RU |
| **Dealer dashboard: search requests** | 1 single-partition query (with locationId filter) | ~3 RU |
| **Asset service history (10A query)** | 1 single-partition read (assetLedger, /assetId) | ~1 RU |

### 12.1 Analytics Response DTO

`ServiceRequestAnalyticsResponseDto` — Returned by `GET api/dealerships/{id}/analytics/service-requests/summary`.

| Field | Type | Description |
|---|---|---|
| `TotalRequests` | `int` | Total service requests in the queried period |
| `RequestsByStatus` | `Dictionary<string, int>` | Count per status value |
| `RequestsByCategory` | `Dictionary<string, int>` | Count per issue category |
| `RequestsByLocation` | `Dictionary<string, int>` | Count per location |
| `TopFailureModes` | `List<AnalyticsRankItem>` | Top N failure modes with occurrence count |
| `TopRepairActions` | `List<AnalyticsRankItem>` | Top N repair actions with occurrence count |
| `AverageRepairTimeHours` | `decimal?` | Mean labor hours across completed jobs |
| `TopPartsUsed` | `List<AnalyticsRankItem>` | Top N parts with replacement count |
| `AverageDaysToComplete` | `decimal?` | Mean elapsed days from created to closed |

> **Scaling note:** For MVP volumes (<200 jobs/month), a single-partition aggregate query is acceptable (~5–10 RU). Phase 2 change feed → Azure Tables aggregation (Section 15.3) accelerates this.

---

## 13. Magic Link Security

| Concern | Mitigation |
|---|---|
| **Token format** | `base64url(SHA256(email)[0..8]):random_bytes` — email-hash prefix enables partition-key derivation (single-partition point read) |
| **Token guessing** | 32-byte cryptographic random (256-bit entropy), URL-safe Base64. The 8-byte email-hash prefix is non-secret metadata. |
| **Token expiry** | 90-day default, configurable per tenant |
| **Token rotation** | Generated once per account; reused on subsequent intakes; regenerated only when absent or expired |
| **Token storage** | Stored as-is (not hashed). The email-hash prefix is a routing optimization, not a secret. Token security relies on the 256-bit random portion. |
| **Rate limiting** | `api/status/{token}` limited to 10 req/min per IP |
| **PII exposure** | Status page returns first name + asset summaries only — no full email, no phone, no other customers' data |
| **Cross-dealer visibility** | Customer sees their own requests across all corporations — intentional for customer convenience |

---

## 14. Key Architectural Decisions Summary

| Decision | Rationale |
|---|---|
| **Tenant = Corporation, not Location** | Enables cross-location analytics, shared customer profiles, single Auth0 Org for Blue Compass |
| **`locationId` as filter, not partition key** | Cross-location queries stay single-partition. Avoids fan-out for corporate dashboards. |
| **Shadow profiles (no customer sign-up)** | Zero friction intake. Customers never see a registration screen. |
| **Three identity containers** | Each access pattern needs a different partition key. One doc can't serve all three. |
| **Append-only asset ledger** | Data moat — proprietary, accumulating, non-replicable. Powers Section 10A intelligence. |
| **`AssetId` as `{AssetType}:{Identifier}` compound key** | Globally unique across asset types; clean Cosmos partition key; works across industries without schema changes. |
| **`CustomerSnapshotEmbedded` denormalized in SR** | Dashboard reads never join to customerProfiles. ~1 RU per view. |
| **Magic link on global identity, not profile** | Status page shows requests across ALL corporations for the customer. |
| **Email-hash prefix in magic-link token** | Avoids cross-partition query on `globalCustomerAccts`. Token format lets `ValidateMagicLinkAsync` derive the partition key. |
| **`IntakeFormConfigEmbedded` on Location, not Dealership** | Each physical site can have different intake settings. |
| **`regionTag` on Location** | Enables regional manager scoping without complex hierarchy. |
| **Intake URL uses `locationSlug`, not `dealershipSlug`** | Each physical location has its own QR code / intake URL. Slug resolves to both `tenantId` and `locationId` via O(1) point read. |
| **Blob path includes `locationId`** | Storage organization mirrors data model. Enables future per-location retention policies. |
| **`Dealership.IsMultiLocation` flag** | UI can adapt (show location picker vs. skip). No code branching in the API layer. |
| **`ConnectionMode.Gateway` for Cosmos SDK** | Enables server-side gateway caching for stable point reads. Zero additional cost. |
| **`slugLookup` as a dedicated container** | Decouples slug resolution from `locations` partition scheme. O(1) point reads. Gateway-cached. |
| **AI-generated diagnostic questions, not static templates** | Eliminates `DiagnosticQuestionTemplate` entity, template CRUD API, admin UI. Cost: ~$0.0002/intake. |
| **Accept eventual consistency for AssetLedger during intake** | Ledger write failure doesn't block the customer. Change feed enrichment in Phase 5–6 recovers any gaps. |
| **Slug rename: WriteNew → UpdateLocation → DeleteOld** | Eliminates the unreachability window. Degrades safely on failure at any step. |

---

## 15. Change Feed Strategy

Cosmos DB Change Feed is not used in MVP, but several patterns create natural future consumers.

### 15.1 Overview

| Consumer | Source Container | Target | Phase | Priority |
|---|---|---|---|---|
| Asset ledger enrichment | `serviceRequests` | `assetLedger` | Phase 5–6 | **Required** |
| Analytics counter aggregation | `serviceRequests` | Azure Tables | Phase 2 | High |
| Snapshot staleness repair | `serviceRequests` | `serviceRequests` | Phase 2+ | Low |

### 15.2 Asset Ledger Enrichment (Required — Phase 5–6)

`AssetLedgerEntry` is written at intake with only fields available then. Section 10A fields (`FailureMode`, `RepairAction`, `PartsUsed`, `LaborHours`) are populated by the technician when the SR is completed. A change feed consumer watches `serviceRequests` for `status = Completed` and patches the matching `assetLedger` document.

**Why change feed, not synchronous write:** The SR completion path is latency-sensitive. Ledger enrichment is analytics pipeline work — eventual consistency is acceptable.

### 15.3 Analytics Counter Aggregation (Phase 2)

A change feed consumer on `serviceRequests` maintains pre-aggregated counters in Azure Table Storage. Counters partitioned by `tenantId` + `locationId`, updated on every status transition. Dashboard reads hit Azure Tables for counts instead of Cosmos.

### 15.4 CustomerSnapshot Staleness (Phase 2+, Low Priority)

The snapshot is point-in-time by design. For open SRs, staleness is a minor UX issue. A change feed consumer on `customerProfiles` could fan out patch updates to open SRs in the same tenant partition, scoped to `status IN ('New', 'InProgress')`.

**MVP stance:** Accept stale snapshots. Only warranted if customer contact updates during open SRs become a reported pain point.

---

## 16. Azure OpenAI Integration

### 16.1 Purpose

Azure OpenAI powers three features:

1. **Dynamic diagnostic question generation** — Given an issue category, optional initial description, and asset info, generates 2–4 targeted follow-up questions.
2. **Enhanced auto-categorization and technician summary** — Uses diagnostic responses alongside the free-text description.
3. **Transcript cleanup** — Cleans speech-to-text transcripts for the issue description field (add `CleanTranscriptAsync` to `ICategorizationService`; fallback: return raw transcript unchanged if AI is unavailable).

### 16.2 Interface

`ICategorizationService` is the sole integration point:

| Method | Purpose | Called When |
|---|---|---|
| `CategorizeAsync(issueDescription, issueCategory?, assetInfo?, diagnosticResponses?)` | Auto-categorize + generate technician summary | Step 4 of intake orchestration |
| `GenerateDiagnosticQuestionsAsync(issueCategory, initialDescription?, assetInfo?, aiContext?)` | Generate follow-up questions | `POST api/intake/{locationSlug}/diagnostic-questions` |
| `CleanTranscriptAsync(rawTranscript)` | Clean speech-to-text output | During issue description entry (Phase 0.5 of intake) |

### 16.3 Implementation

**Primary: `AzureOpenAiCategorizationService`** — Lives in `RVS.API/Integrations/`. Calls Azure OpenAI (GPT-4o-mini) with structured system prompt. Uses JSON mode for structured output. The `aiContext` parameter (from `IntakeFormConfigEmbedded`) appends per-dealer customization to the system prompt.

**Fallback: Rule-based** — If Azure OpenAI is unavailable, falls back to keyword-based categorization and hardcoded questions per category. Intake never blocks on an external service.

### 16.4 DTOs

**Request — `DiagnosticQuestionsRequestDto`:**

| Field | Type | Description |
|---|---|---|
| `IssueCategory` | `string` | Selected category |
| `InitialDescription` | `string?` | Optional free-text typed so far |
| `Asset` | `AssetInfoDto?` | Optional vehicle info for context |

**Response — `DiagnosticQuestionsResponseDto`:**

| Field | Type | Description |
|---|---|---|
| `Questions` | `List<DiagnosticQuestionDto>` | 2–4 generated questions |
| `SmartSuggestion` | `string?` | Optional AI insight |

**`DiagnosticQuestionDto`:** `QuestionText` (`string`), `Options` (`List<string>`), `AllowFreeText` (`bool`), `HelpText` (`string?`).

**`DiagnosticResponseDto`** (in `ServiceRequestCreateRequestDto`): `QuestionText` (`string`), `SelectedOptions` (`List<string>`), `FreeTextResponse` (`string?`).

### 16.5 Cost & Performance

| Model | Input Tokens | Output Tokens | Cost per Intake | Latency |
|---|---|---|---|---|
| **GPT-4o-mini** (recommended) | ~300 | ~400 | ~$0.0002 | 500ms–1s |
| GPT-4o (future) | ~300 | ~400 | ~$0.003 | 1–2s |

At 1,000 intakes/month: ~$0.20/month. Negligible relative to Cosmos DB costs.

### 16.6 Two-Phase Intake Flow

**Phase 1 — Diagnostic Questions (mid-wizard):**

```
POST api/intake/{locationSlug}/diagnostic-questions
Body: { issueCategory, initialDescription?, asset? }
→ Returns: DiagnosticQuestionsResponseDto
```

**Phase 2 — Submission (existing flow, enhanced):**

```
POST api/intake/{locationSlug}/service-requests
Body: { asset, issueCategory, issueDescription, diagnosticResponses[], customer }
→ Returns: 201 Created with SR details
```

Phase 1 is optional — if it fails or times out, the customer proceeds with free-text description only.

### 16.7 Configuration

```json
{
  "AzureOpenAi": {
    "Endpoint": "https://{resource}.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini",
    "MaxTokens": 500,
    "TimeoutSeconds": 5
  }
}
```

Authentication uses `DefaultAzureCredential` (consistent with all other Azure services). **⚠️ Endpoint must move to Azure Key Vault.**

---

## 17. Technician Mobile App — API Readiness

**Front-end format:** MAUI Blazor Hybrid (iOS + Android). Offline-first with native barcode/QR scanning, camera, and voice notes via MAUI Essentials. Offline queue uses SQLite; syncs via sequential `PUT` calls on reconnect. Employer-provisioned install.

### 17.1 Gap Summary

| Priority | Gap | Resolution | Status |
|---|---|---|---|
| 🔴 Critical | No authenticated attachment upload for dealer staff | Added `POST` to `AttachmentsController` with `CanUploadAttachments` | ✅ Resolved |
| 🔴 Critical | `dealer:technician` missing `service-requests:search` | Added to role → permission matrix | ✅ Resolved |
| 🟡 Important | No `assignedTechnicianId` search filter | Added to `ServiceRequestSearchRequestDto` | ✅ Resolved |
| 🟡 Important | No `assignedBayId` search filter | Added to `ServiceRequestSearchRequestDto` | ✅ Resolved |
| 🟡 Important | No `assetId` search filter (VIN scan → job lookup) | Added to `ServiceRequestSearchRequestDto` | ✅ Resolved |
| 🟡 Important | Seed `LookupSet` data for `failureModes`, `repairActions` | Seed data requirement documented | ✅ Resolved |
| 🟢 Nice-to-have | Voice notes (audio file support) | Added `.m4a`, `.wav` to defaults | ✅ Resolved |
| 🟢 Nice-to-have | Batch update endpoint for offline sync | Deferred — sequential `PUT` acceptable for MVP | 🔵 Deferred |
| 🔵 Future | Labor time prediction API | Phase 5–6 via `AssetLedger` data | 🔵 Deferred |

### 17.2 Required LookupSet Seed Data

| Category | Example Items | Used By |
|---|---|---|
| `failureModes` | Hydraulic pump failure, Slide motor failure, Electrical fault, Fluid leak, Seal degradation, Bearing failure, Control board malfunction | Failure Mode picker |
| `repairActions` | Replace pump, Replace motor, Repair wiring, Adjust mechanism, Replace seal, Lubricate, Reflash firmware | Repair Action picker |
| `componentTypes` | Hydraulic System, Electrical System, Slide Mechanism, HVAC, Plumbing, Refrigeration, Leveling System, Awning, Generator | Component Type picker |

### 17.3 Offline Sync Strategy (Deferred)

1. Queue failed `PUT` requests locally (SQLite).
2. On connectivity restore, replay queued requests sequentially.
3. Use `updatedAtUtc` for optimistic concurrency — surface conflict to user if server's timestamp is newer.

---

## 18. Service Manager Desktop App — API Readiness

**Front-end format:** Blazor WebAssembly (Standalone). Long polling (configurable interval, default 5 min) for near-real-time updates. SignalR deferred to vNEXT. Deployed to Azure Static Web Apps.

### 18.1 Gap Summary

| Priority | Gap | Resolution | Status |
|---|---|---|---|
| 🔴 Critical | Status model only has 4 values; Service Board needs 7+1 | Expanded to 8 status values with transition validation | ✅ Resolved |
| 🔴 Critical | No batch outcome endpoint | Added `PATCH batch-outcome` | ✅ Resolved |
| 🟡 Important | `Priority` field missing | Added to `ServiceRequest` entity and DTOs | ✅ Resolved |
| 🟡 Important | No `HasOutcome` filter | Added to `ServiceRequestSearchRequestDto` | ✅ Resolved |
| 🟡 Important | No scheduled date search filters | Added `ScheduledAfterUtc`/`ScheduledBeforeUtc` to search DTO | ✅ Resolved |
| 🟡 Important | Analytics DTO scope too narrow | Expanded with failure modes, repair times, parts trends | ✅ Resolved |
| 🟢 Phase 2 | No "request additional info" flow | MVP: external contact. Phase 2: follow-ups endpoint | 🔵 Deferred |

### 18.2 Expanded Status Values and Transition Validation

| Status Value | Service Board Column | Notes |
|---|---|---|
| `"New"` | New Requests | Submitted, not yet triaged |
| `"Scheduled"` | Scheduled | Has `ScheduledDateUtc` set |
| `"InDiagnosis"` | In Diagnosis | Technician is diagnosing |
| `"WaitingParts"` | Waiting Parts | Blocked on parts availability |
| `"InRepair"` | In Repair | Repair actively in progress |
| `"Completed"` | Completed (Outcome Needed) | Repair done; outcome may be missing |
| `"Closed"` | Closed | Outcome recorded; job fully closed |
| `"Cancelled"` | — | Cancelled at any stage |

**Allowed transitions** (enforced by `StatusTransitions.cs` in `RVS.Domain/Validation/`):

| From | Allowed Next States |
|---|---|
| `New` | `Scheduled`, `InDiagnosis`, `Cancelled` |
| `Scheduled` | `InDiagnosis`, `WaitingParts`, `Cancelled` |
| `InDiagnosis` | `WaitingParts`, `InRepair`, `Cancelled` |
| `WaitingParts` | `InDiagnosis`, `InRepair`, `Cancelled` |
| `InRepair` | `WaitingParts`, `Completed`, `Cancelled` |
| `Completed` | `Closed`, `InRepair` (reopen) |
| `Closed` | *(terminal)* |
| `Cancelled` | *(terminal)* |

### 18.3 Batch Outcome Endpoint

`PATCH api/dealerships/{id}/service-requests/batch-outcome` — Applies a shared repair outcome to up to 25 SRs.

**Request — `ServiceRequestBatchOutcomeRequestDto`:**

| Field | Type | Description |
|---|---|---|
| `ServiceRequestIds` | `List<string>` | Required. Max 25. |
| `FailureMode` | `string?` | Failure mode to apply |
| `RepairAction` | `string?` | Repair action to apply |
| `PartsUsed` | `List<string>?` | Parts list to apply |
| `LaborHours` | `decimal?` | Labor hours to apply |

**Response — `ServiceRequestBatchOutcomeResponseDto`:**

| Field | Type | Description |
|---|---|---|
| `Succeeded` | `List<string>` | IDs of successfully updated SRs |
| `Failed` | `List<BatchOutcomeFailureDto>` | Items that failed, with reason |

---

## 19. Billing & Metering Architecture

Full specification in **`RVS_Billing_Metering_Architecture.md`**.

### 19.1 Plan Tiers

| Tier | Price | Service Requests / Month | Locations | SFTP |
|---|---|---|---|---|
| **Starter** | $199/mo | 500 | 1 | No |
| **Pro** | $349/mo | 2,000 | 5 | Yes |
| **Enterprise** | $499/mo | Unlimited | Unlimited | Yes |

Enterprise tenants bypass all SR and location count enforcement. All tiers include the intake portal, dealer dashboard, and asset ledger.

### 19.2 TenantConfig — BillingConfig Embedment

```json
{
  "billingConfig": {
    "planTier": "Pro",
    "stripeCustomerId": "cus_abc123",
    "stripeSubscriptionId": "sub_xyz789",
    "billingPeriodStartDay": 1,
    "currentPeriodSrCount": 347,
    "currentPeriodStart": "2026-04-01T00:00:00Z",
    "trialEndsAtUtc": null,
    "maxMonthlyServiceRequests": 2000,
    "maxLocations": 5,
    "isSftpEnabled": true,
    "attachmentRetentionDays": 365
  }
}
```

**Counter strategy:** Each SR creation issues a Cosmos atomic patch `IncrementAsync("/billingConfig/currentPeriodSrCount", 1)` (~1 RU, O(1)). A daily Azure Function (timer trigger at 00:01 UTC on `billingPeriodStartDay`) resets the counter. No change feed or aggregation job needed.

### 19.3 Billable and Observable Metrics

**Primary billable metric:** `sr_created_count` — incremented on each SR creation, partitioned by `tenantId` and billing period.

**Secondary observable metrics (non-billable):**

| Metric | Purpose |
|---|---|
| `ai_categorization_calls` | Azure OpenAI cost attribution |
| `ai_categorization_fallback_rate` | AI health monitoring |
| `attachment_storage_bytes` | Blob lifecycle policy enforcement |
| `api_request_count` | Noisy neighbor detection |
| `sftp_export_success` / `sftp_export_failure` | SLA monitoring |
| `magic_link_sends` | Notification cost attribution |
| `cosmos_ru_consumed` | Internal cost allocation |

All metrics emitted as Application Insights `CustomEvent` or `CustomMetric` with dimensions: `tenantId`, `locationId`, `operationType`, `planTier`, `stampId`.

### 19.4 Enforcement Points

**SR limit — `IntakeOrchestrationService.SubmitIntakeAsync` (before SR write):**

- If `PlanTier != Enterprise` AND `currentPeriodSrCount >= maxMonthlyServiceRequests` → HTTP 402
- At 80% of limit, emit `PlanLimitWarning` custom event → dashboard warning banner

**Location limit — `LocationService.CreateAsync`:**

- If `PlanTier != Enterprise` AND `active_location_count >= maxLocations` → HTTP 402

**SFTP feature gate — `SftpExportService`:** Check `BillingConfig.IsSftpEnabled` before scheduling export.

**Trial expiry — `TenantAccessGateMiddleware`:** When `trialEndsAtUtc` is non-null and past, and `stripeSubscriptionId` is null → HTTP 402. Plan limit enforcement remains in the service layer (not middleware) to avoid per-request Cosmos reads.

### 19.5 Stripe Integration

| Stripe Object | RVS Mapping |
|---|---|
| **Customer** | One per `tenantId`; created at tenant provisioning |
| **Product** | One per tier (`RVS Starter`, `RVS Pro`, `RVS Enterprise`) |
| **Price** | One per Product (monthly recurring) |
| **Subscription** | One per Customer |
| **Webhook** | `POST /api/billing/stripe/webhook` |

**Webhook events handled:**

| Event | Action |
|---|---|
| `invoice.payment_succeeded` | Reset `currentPeriodStart`; log payment |
| `invoice.payment_failed` | Alert platform admin; 3-day grace period before disabling tenant |
| `customer.subscription.updated` | Update `planTier`, limits, `isSftpEnabled` in `TenantConfig` |
| `customer.subscription.deleted` | Set `AccessGate.LoginsEnabled = false` |
| `customer.subscription.trial_will_end` | Emit App Insights event; trigger in-app notification |

All webhook payloads validated using `Stripe-Signature` header. Stripe webhook secret stored in Azure Key Vault.

**Interface contract:**

```csharp
public interface IStripeService
{
    Task<string> CreateCustomerAsync(string tenantId, string tenantName, string adminEmail, CancellationToken ct = default);
    Task<string> CreateSubscriptionAsync(string stripeCustomerId, string planTier, int trialDays, CancellationToken ct = default);
    Task UpdateSubscriptionPlanAsync(string stripeSubscriptionId, string newPlanTier, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);
}
```

### 19.6 Dealer Dashboard — Billing Usage Visibility

`GET /api/tenants/billing/usage` response:

```json
{
  "planTier": "Pro",
  "currentPeriodSrCount": 1680,
  "maxMonthlyServiceRequests": 2000,
  "usagePercent": 84,
  "currentPeriodStart": "2026-04-01T00:00:00Z",
  "trialEndsAtUtc": null,
  "isTrialActive": false,
  "locationCount": 3,
  "maxLocations": 5
}
```

| Condition | Banner |
|---|---|
| `usagePercent >= 80 && < 100` | Warning — upgrade prompt |
| `usagePercent >= 100` | Error — intake paused until next reset |
| Trial ends within 7 days | Info — add payment method |
| Trial expired | Error — redirect to upgrade page |

### 19.7 Free Trial Design

- Default: 30 days from provisioning, no credit card required.
- SR counting and Starter limits apply during the trial.
- Trial-to-paid: Stripe activation clears `trialEndsAtUtc` immediately.
- Platform admin can manually extend `trialEndsAtUtc` (sales concession lever).

---

## 20. Architecture Gaps Not Yet Resolved

This section collects design gaps identified across assessment reviews that have no implementation section yet.

### 20.1 SFTP / DMS Export — No Implementation Design

FR-013 is marked High priority. Open questions:

- **Hosted Service vs. Azure Function?** A `BackgroundService` inside `RVS.API` puts periodic I/O on the API process. An Azure Function (timer trigger) is isolated and independently scalable but adds deployment complexity.
- **Per-tenant scheduling:** Global time (e.g., 2 AM for all tenants) or tenant-configurable? `TenantConfig` doesn't have `SftpScheduleExpression`.
- **Failure handling:** Retry count before alert? Failed export retried next day or discarded?
- **CSV streaming vs. batching:** For 500+ SRs in the export window, streaming pagination matters for memory.
- **Key Vault reference pattern:** `TenantConfig` entity must be updated to use `privateKeySecretUri` (not raw key).

### 20.2 SAS Pre-Signed Upload for Media Files

The MVP routes all 25 MB uploads through the API, blocking worker threads for 3–8 seconds on mobile LTE. At 20 concurrent intakes with video, App Service single-instance becomes the chokepoint.

**Required flow:**

1. Client calls `POST .../attachments/upload-url` → API returns a SAS URI with 10-minute write expiry scoped to `{tenantId}/{serviceRequestId}/{attachmentId}`
2. Client uploads directly to Blob Storage (no API thread held)
3. Client calls `POST .../attachments/confirm` with attachment metadata

~50 lines of additional code. Eliminates the upload bottleneck entirely.

### 20.3 VIN Decode + Camera Scan — No Resiliency Design

The NHTSA vPIC API is the only hard external dependency not behind a fallback abstraction. Open questions:

- What happens if vPIC is down at intake time? (Form blocked? VIN pre-population skipped?)
- Is vPIC called client-side by Blazor or server-side by the API?
- Is there an `IVinDecodeService` interface with a stub fallback?
- VIN validation rule: NHTSA check digit (North American only) vs. length-only check for EU/international?
- `BarcodeDetector` API has ~70% browser support. The `zxing-js` fallback is mentioned but not specified.

### 20.4 Speech-to-Text + AI Clean-Up — No Endpoint or Flow Design

Section 16 now specifies `CleanTranscriptAsync` on `ICategorizationService`. Remaining open questions:

- Is the raw transcript sent to the API for AI cleanup, or does this happen in-browser?
- Token cost for transcript cleanup is not included in the Section 16.5 cost model.
- This is a "Phase 0.5" API call that happens during issue description entry — before the two-phase flow.

### 20.5 Application Insights / Telemetry Architecture

P0 item with no implementation design. Open questions:

- Where is the `TelemetryInitializer` registered? What standard dimensions does it stamp?
- Which service requests emit `CustomEvent` vs. just `Dependency` traces?
- Alert policy for Cosmos `429 / RU throttling`?
- Structured log schema (correlation IDs, operation types)?

### 20.6 Azure Infrastructure Architecture

No Azure resource topology exists in the ASOT set. Undocumented decisions:

- App Service Plan tier (B1? P1v3? Scale-up triggers?)
- Single-region vs. multi-region Cosmos DB account
- Key Vault access model (RBAC — Access Policies are deprecated)
- Storage account redundancy tier (LRS vs. ZRS vs. GRS)
- Static Web App tier for Blazor WASM (Standard needed for Auth0 custom auth)
- Deployment slot configuration (staging → production swap)
- Environment separation model (subscription per environment vs. resource group per environment)

### 20.7 Blazor WASM Frontend Architecture

No frontend architecture document exists. Missing:

- Component hierarchy (pages, shared components, feature folders)
- State management strategy (Fluxor? Cascading? Service-based binding?)
- Auth0 PKCE flow in Blazor WASM (`Auth0.OidcClient` vs. MSAL)
- Anonymous intake route vs. authenticated dashboard: how does a single Blazor app serve both?
- VIN camera scan: `BarcodeDetector` browser API from Blazor WASM requires JavaScript interop — no `IJSRuntime` wrapper designed
- Offline queue for technician mobile: IndexedDB/SQLite package decision unmade

### 20.8 Regional Manager `regionTag` Lifecycle

- **Assignment:** Who sets `regionTag` on a user's `app_metadata`? Tenant provisioning flow only covers owner onboarding.
- **Consistency:** If a Location's `regionTag` changes, do existing regional managers lose access until their `app_metadata` is updated?
- **Token staleness:** `app_metadata` is embedded at login. Mid-session tag changes not reflected until token renewal (1 hour).
- **Empty regionTag:** A `dealer:regional-manager` with no `regionTag` passes `GetRegionTag()` returning null — enforcement unclear.

### 20.9 Section 10A Audit Trail

Technicians update `ServiceEvent` fields via `service-requests:update-service-event`. The only audit stamp is `ServiceRequest.UpdatedByUserId` / `UpdatedAtUtc` — which records the *last* update but not *who changed what and when*.

For warranty claims or dealer disputes, field-level change attribution may be necessary. **Decision needed:** Is a `ServiceEventChangeLog` embedded list needed, does the `AssetLedger` enrichment serve as a sufficient audit trail substitute, or does the business requirement not need field-level history?

### 20.10 `AllKnownAssetIds` Cap of 200 Items

`GlobalCustomerAcct.AllKnownAssetIds` is capped at 200 items (most-recent first), overflow recoverable from `AssetLedger`. Undocumented:

- When item 201 is added, is the oldest silently dropped?
- Is the 200 limit validated in the service layer or documentation-only?
- Fleet owners (rental company, livery service) could plausibly hit this limit.

### 20.11 `GlobalCustomerAcct.LinkedProfiles` Growing List

`LinkedProfiles` is an embedded list with no documented size cap. A customer who submits at 50 different dealerships would have 50 embedded profile references. Unlike `AllKnownAssetIds`, no cap or mitigation is defined. Apply a similar cap-and-recover approach.

### 20.12 Notification Service — No Dead Letter Handling

`INotificationOrchestrator` dispatches email via `INotificationService` (ACS Email) and SMS via `ISmsNotificationService` (ACS SMS) as fire-and-forget. If ACS is down, the customer never receives their magic-link notification. No retry, no audit trail.

**MVP mitigation:** Log failures to Application Insights with tenant ID, SR ID, and customer email (hashed). Add retry with exponential backoff using `Microsoft.Extensions.Http.Resilience`. ACS delivery status events via Event Grid provide delivery confirmation for both email and SMS. Phase 2: Azure Service Bus for durable notification queuing.

---

## 21. SaaS-Specific Architecture Notes

### Deployment Stamp Foundation (Deferred — Correct for MVP)

Single-stamp architecture is appropriate for MVP. Two stubs to set up now:

1. **Stamp identifier in config** — Add `"StampId": "stamp-01"` to `appsettings.json`.
2. **No hard-coded single-tenant assumptions** — Already clean. Preserve this discipline.

### `GlobalCustomerAcct` Cross-Tenant Data Access Security

`GlobalCustomerAcct` crosses tenant boundaries by design. Validation requirements:

- Customer status page endpoint must **never** return tenant-specific data except what belongs to the requesting customer (email-hash verified).
- `LinkedProfiles` list exposes `tenantId`, `DealershipName` — ensure these don't leak beyond the safe `CustomerServiceRequestSummaryDto`.

### Auth0 Cost Cliff at Commercialization

Migration trigger: *"Migrate to Auth0 Organizations when tenant #6 signs or when a prospective tenant requires enterprise SSO."* The backend is already migration-neutral.

### Per-Tenant Rate Limiting (Noisy Neighbor Prevention)

Authenticated dealer API endpoints need per-tenant throughput controls. A Blue Compass automation script running batch search queries could consume disproportionate Cosmos RU.

**Recommendation:** Add per-`tenantId` sliding-window rate limiter on authenticated endpoints. Starter: 100 req/min. Pro: 300 req/min. Enterprise: 1,000 req/min. Returns `429` with `Retry-After`. Also functions as a tier-upgrade sales lever.

---

## 22. Document Health Issues

### `RVS_Auth0_roles&perms.md` (V1) — Archive or Delete

The V1 roles document is materially inconsistent with `RVS_Auth0_Identity_Version2.md` (6 roles vs. 8 roles, missing `attachments:upload` permission, no location scoping). Anyone reading the ASOT set reads incompatible permission matrices.

### `RVS_Context.md` — Stale First-Generation Document

Predates the multi-location tenancy model. No mention of Blue Compass problem, `dealer:corporate-admin`/`dealer:regional-manager` roles, `AssetLedger` data moat, Auth0 identity strategy, or nine-container Cosmos design. Either rewrite or replace with a short "What is RVS" summary linking to the PRD.

### Stale Cross-References in Core Architecture

Core Architecture cross-references `RVS_Auth0_Identity.md` (eight places) — the actual file is `RVS_Auth0_Identity_Version2.md`. Every link is broken.

### Auth0 V2 Date Stamp

`RVS_Auth0_Identity_Version2.md` is dated March 10 — the V3 architecture resolved several permission changes (Section 17.1). The Auth0 V2 document needs its date bumped and a reconciled permission matrix.

---

## 23. Unified Critical Gaps Table

All critical gaps identified across the Core Architecture, SaaS WAF Assessment, and ASOT Gap Analysis, deduplicated and organized by priority.

| # | Priority | Gap | WAF Pillar | Impact | Required Action | Status |
|---|---|---|---|---|---|---|
| 1 | 🔴 P0 | No Infrastructure-as-Code (Bicep/Terraform) | Ops | Manual env setup, infra drift, no disaster recovery | Create Bicep templates for all Azure resources; GitHub Actions CI/CD pipeline | **Open** |
| 2 | 🔴 P0 | No CI/CD Pipeline (GitHub Actions) | Ops | Manual deployments, no safe release path, no auto-provisioning | Build `build → test → deploy-staging → (approval) → deploy-prod` | **Open** |
| 3 | 🔴 P0 | No Application Insights (zero telemetry) | Reliability/Ops | Cannot measure SLAs, detect outages, or troubleshoot | Wire App Insights with tenant-tagged dimensions, RU tracking, OpenAI latency, error rates | **Open** |
| 4 | 🔴 P0 | SFTP private keys stored in Cosmos DB | Security | Fails SOC 2 audit, key exposed if DB compromised | Move to Azure Key Vault; store only `privateKeySecretUri` in `TenantConfig` | **Open** |
| 5 | 🔴 P0 | No Azure Key Vault for *any* secrets | Security | Auth0 client secret, OpenAI key, Stripe key all in `appsettings.json` | Add `AddAzureKeyVault` to `Program.cs`; App Service managed identity + `Key Vault Secrets User` RBAC | **Open** |
| 6 | 🔴 P0 | Billing/metering design not implemented | Cost/Business | Cannot charge customers; violates SaaS WAF cost/revenue principle | Section 19 design complete — implementation pending | **Design Complete** |
| 7 | 🔴 P0 | `RVS_Auth0_roles&perms.md` (V1) conflicts with V2 | Ops/Doc | Incompatible permission matrices confuse onboarding developers | Delete or archive V1 document | **Open** |
| 8 | 🔴 P0 | `RVS_Context.md` is stale, misrepresents current design | Ops/Doc | Outdated assumptions for developers and investors | Rewrite or retire | **Open** |
| 9 | 🟡 P1 | No Azure Front Door + WAF | Security/Perf | Anonymous intake endpoints unprotected from injection, DDoS, bots; no CDN | Place Azure Front Door Standard in front of API (~$35/month) | **Open** |
| 10 | 🟡 P1 | No SAS pre-signed upload for media | Performance | 25 MB uploads block API worker threads; App Service becomes chokepoint | Add `GenerateUploadSasUriAsync` → client uploads directly to Blob | **Open** |
| 11 | 🟡 P1 | Per-tenant rate limiting missing | Reliability | Noisy neighbor — one tenant's batch queries degrade all tenants | Add per-`tenantId` sliding-window rate limiter on authenticated endpoints | **Open** |
| 12 | 🟡 P1 | No health check endpoints | Reliability | App Service can't detect unhealthy API (broken Cosmos connection) | Add `/health`, `/health/live`, `/health/ready` with Cosmos connectivity check | **Open** |
| 13 | 🟡 P1 | No SFTP/DMS export design section | Feature | FR-013 high priority with no implementation design | Add dedicated architecture section (hosted service vs. function, scheduling, failure handling) | **Open** |
| 14 | 🟡 P1 | VIN decode + camera scan — no resiliency | Feature | Only hard external dependency without fallback abstraction | Add `IVinDecodeService` with stub fallback; document failure behavior | **Open** |
| 15 | 🟡 P1 | Speech-to-text AI cleanup — no endpoint design | Feature | FR-005 high priority, `CleanTranscriptAsync` interface spec'd but no flow/endpoint | Document endpoint, in-browser vs. API, cost model | **Open** |
| 16 | 🟡 P1 | `CustomerName` search filter unindexed | Performance | Full-partition scan on name search in `serviceRequests` | Add composite index or document as intentional deferral | **Open** |
| 17 | 🟡 P1 | Intake orchestration race condition undocumented | Reliability | Partial state if ledger write fails after SR write | Explicitly accept eventual consistency (see Section 6 decision) | **Resolved in this doc** |
| 18 | 🟡 P1 | `GlobalCustomerAcct.LinkedProfiles` — no size cap | Data | Unbounded embedded list growth for frequent customers | Define cap-and-recover strategy matching `AllKnownAssetIds` | **Open** |
| 19 | 🟡 P1 | Slug rename sequencing has unreachability window | Reliability | Location unreachable during rename | Document corrected order: WriteNew → UpdateLocation → DeleteOld | **Resolved in this doc** |
| 20 | 🟡 P1 | No Blazor frontend architecture document | Ops/Doc | Zero frontend ASOT; no component model, state management, or auth flow | Create frontend architecture ASOT doc | **Open** |
| 21 | 🟡 P1 | Core Architecture cross-references broken (`RVS_Auth0_Identity.md`) | Doc | All 8 links point to non-existent file | Fix to `RVS_Auth0_Identity_Version2.md` | **Open** |
| 22 | 🟡 P1 | Auth0 V2 date stale; post-V3 permission changes not reflected | Doc | Permission matrix may not include Section 17.1 additions | Sync date and reconcile permissions | **Open** |
| 23 | 🟡 P2 | No Blazor WASM CDN (5–15 MB download from App Service) | Performance | Poor UX on mobile customer devices | Deploy `Blazor.Intake` + `Blazor.Manager` to Azure Static Web Apps | **Open** |
| 24 | 🟡 P2 | CORS origins per environment not defined | Security | Potential misconfiguration during deployment | Document `AllowBlazorClient` policy origins per env | **Resolved in this doc** |
| 25 | 🟡 P2 | Blob Storage container ACL not explicitly enforced | Security | Accidental public access exposes customer photos/videos | Explicitly set `PublicAccess = None`; IaC assertion | **Open** |
| 26 | 🟡 P2 | No Blob Storage lifecycle management | Cost | Storage costs grow linearly (photos/videos retained indefinitely) | Define per-tier retention; implement Hot→Cool→Archive lifecycle | **Open** |
| 27 | 🟡 P2 | Regional manager `regionTag` lifecycle undefined | Auth | Assignment, consistency, staleness, enforcement unclear | Document full lifecycle | **Open** |
| 28 | 🟡 P2 | Section 10A has no field-level audit trail | Data | Technician repair changes lose history on overwrite | Decide if `ServiceEventChangeLog` or AssetLedger enrichment suffices | **Open** |
| 29 | 🟢 P3 | Application Insights architecture needs design section | Ops | P0 item acknowledged but not designed | `TelemetryInitializer`, alert policies, log schema | **Open** |
| 30 | 🟢 P3 | `AllKnownAssetIds` cap enforcement undocumented | Data | Fleet owners could hit 200-item limit; behavior undefined | Validate/document enforcement in service layer | **Open** |
| 31 | 🟢 P3 | `TenantAccessGateMiddleware` scale limit undocumented | Performance | Cosmos RU costs from middleware at >1,000 tenants | Add IMemoryCache mitigation note (Phase 2) | **Open** |
| 32 | 🟢 P3 | Notification service — no dead letter handling | Reliability | Failed email sends lost silently | Log failures to App Insights; add retry; Phase 2: Service Bus | **Open** |
| 33 | 🟢 P3 | Structured tenant context missing from logs | Ops | Diagnosing per-tenant issues requires guessing | `ILogger.BeginScope()` enricher with `TenantId`/`LocationId` | **Open** |
| 34 | 🟢 P3 | No deployment stamp foundation stubs | Future | Missing `StampId` config makes future multi-stamp migration harder | Add `"StampId": "stamp-01"` to `appsettings.json` | **Open** |

---

## Reference Documents

- **RVS_Auth0_Identity_Version2.md** — Auth0 setup, RBAC roles, JWT claims, permissions matrix
- **RVS_Billing_Metering_Architecture.md** — Full billing/metering specification
- **RVS_PRD.md** — Product Requirements Document
- `.github/copilot-instructions.md` — ASP.NET Core, C# 14 coding patterns

---

*Consolidated from: `RVS_Core_Architecture_Version3.1.md`, `RVS_SaaS_Architecture_Assessment.md`, `RVS_Cloud_Arch_Assessment.md` — March 18, 2026*
