

# RV Service Flow (RVS) — Core Backend Architecture

**Authoritative Source of Truth (ASOT) — March 18, 2026**

This document captures the domain model, multi-location tenancy, data layer, orchestration flows, service layer, middleware pipeline, API surface, and storage design for RVS. For Auth0 identity, RBAC roles/permissions, ClaimsService, and authorization policies, see the companion document **RVS_Auth0_Identity_Version2.md**.

---

## Executive Summary

RVS is a B2B SaaS platform for RV dealership service management. The backend is built on ASP.NET Core (.NET 10), Azure Cosmos DB (NoSQL), Blazor WASM frontend, and Auth0 identity.

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
6. Update linkages (customer request count, magic-link rotation)
7. Send confirmation email

**Magic-Link Design:** Customer sees all their SRs across corporations via `api/status/{token}`. Token embeds email-hash prefix enabling O(1) partition-key derivation (no cross-partition query).

---

## 1. Solution Structure and Layering

**Technology Stack:** ASP.NET Core (.NET 10, C# 14), Blazor WASM frontend, Azure Cosmos DB (SQL API), Azure Blob Storage, Auth0 identity.

**Layered Architecture:**
- **RVS.API** — ASP.NET Core REST API; request handlers, service layer, middleware pipeline
- **RVS.Domain** — Zero infrastructure dependencies; entities, DTOs, interfaces, validation rules
- **RVS.Infra.*** — Azure service implementations (Cosmos repositories, Blob Storage, Table Storage, credential management)
- **RVS.Data.Cosmos.Seed** — Development seed data
- **RVS.BlazorWASM** — Customer intake portal + dealer dashboard frontend

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

7. **TenantConfig** — Tenant settings, access gate, SFTP export config. **⚠️ SECURITY GAP — See Assessment doc: SFTP private keys must move to Key Vault.**

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
| `globalCustomerAccts` | `/email` | `globalCustomerAcct` | Manual 400 | Cross-dealer identity federation |
| `assetLedger` | `/assetId` | `assetLedgerEntry` | Autoscale 400–1000 | Section 10A data moat |
| `dealerships` | `/tenantId` | `dealership` | Manual 400 | Corporation profiles |
| `locations` | `/tenantId` | `location` | Autoscale 400–1000 | Physical service locations |
| `tenantConfigs` | `/tenantId` | `tenantConfig` | Manual 400 | Tenant settings, access gate |
| `lookupSets` | `/category` | `lookupSet` | Manual 400 | Issue categories, component types |
| `slugLookup` | `/slug` | `slugLookup` | Autoscale 400–1,000 | Slug → tenantId + locationId point read |

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

**`customerProfiles`** — Included paths: `/tenantId/?`, `/email/?`, `/globalCustomerAcctId/?`, `/assetsOwned/[]/assetId/?`, `/assetsOwned/[]/status/?`. Composite index: `[tenantId ASC, email ASC]`. Unique key: `[/tenantId, /email]`.

**`locations`** — Included paths: `/tenantId/?`, `/slug/?`, `/regionTag/?`.

**`slugLookup`** — No secondary indexes required. All reads are point reads by `/slug` (partition key = document id). Index policy: excluded paths `/*`, included paths `/_etag/?` only.

### 4.5 Cosmos DB Document Examples

**Service Request:**

```json
{
  "id": "sr_abc123",
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
  ],
  ``` 

  ## 4. Data Layer: Cosmos DB

  ### 4.1 Container Design

  | Container | Partition Key | RU Mode | Access Pattern |
  |---|---|---|---|
  | `serviceRequests` | `/tenantId` | Autoscale 400–4000 | Single-tenant SRs, location filtering |
  | `customerProfiles` | `/tenantId` | Autoscale 400–1000 | Tenant-scoped profiles by identity |
  | `globalCustomerAccts` | `/email` | Autoscale 400–1000 | Cross-dealer identity lookup |
  | `assetLedger` | `/assetId` | Autoscale 400–1000 | Section 10A asset history (append-only) |
  | `dealerships` | `/tenantId` | Manual 400 | Corp metadata (low cardinality) |
  | `locations` | `/tenantId` | Autoscale 400–1000 | Location CRUD + regional filtering |
  | `tenantConfigs` | `/tenantId` | Autoscale 400–1000 | Tenant settings read on every auth request (gateway-cached) |
  | `lookupSets` | `/category` | Manual 400 | Reference data (gateway-cached) |
  | `slugLookup` | `/slug` | Autoscale 400–1000 | O(1) intake URL resolution (gateway-cached) |

  ### 4.2 Multi-Tenancy & Blue Compass Problem

  **Tenant Mapping:**
  - **Tenant = Corporation/Dealership (Auth0 Organization boundary)**
  - **Partition key = `tenantId`** — all locations for a corporation share one partition
  - **`locationId` is a filter, not a partition boundary** — enables cross-location analytics within a single partition

  This design solves the "Blue Compass problem": Blue Compass operates 100+ RV service locations. The alternative (partition by location) would scatter related data across 100 partitions, making corporate analytics queries fan out and become expensive. By partitioning by corporation, we keep corporate admin queries single-partition and cheap (~3–5 RU), while technicians at specific locations use location-scoped filters within the partition.

  ### 4.3 Indexing & Query Patterns

  **Key Indexes:**
  - **`serviceRequests`**: Composite indexes for `[tenantId ASC, locationId ASC, createdAtUtc DESC]`, `[tenantId ASC, status ASC]`, `[tenantId ASC, assignedTechnicianId ASC]` to support dashboard searches
  - **`customerProfiles`**: Unique constraint on `[tenantId, email]` to prevent duplicates
  - **`globalCustomerAccts`**: Primary index on `/email` (partition key); no secondary indexes needed
  - **`assetLedger`**: Primary index on `/assetId` (partition key); enables append-only reads by asset

  **RU Cost Estimates (per operation):**
  - Slug resolution (point read, gateway-cached): ~1 RU cold / ~0 RU cached
  - New customer intake (7 Cosmos operations): ~11.8 RU cold / ~10.8 RU with gateway caching
  - Returning customer intake: ~10.8 RU
  - Dealer dashboard search (single-partition query): ~3 RU
  - Asset history query: ~1 RU (single-partition point read)

  ### 4.4 Autoscale vs. Manual RU

  ⚠️ **ASSESSMENT GAP:** The assessment recommends autoscaling **all** variable-load containers. Currently, `dealerships`, `tenantConfigs`, and `lookupSets` are Manual 400. These should be **Autoscale 400–1000** to:
  - Reduce per-container monthly cost from ~$25 to ~$5.84 (billed at 10% minimum)
  - Handle intake bursts without throttling
  - For 9 containers, total floor drops from ~$225 to ~$52/month

  ### 4.5 Cosmos Gateway-Mode Caching

  **`ConnectionMode.Gateway`** is enabled on `CosmosClient`. Server-side caching on stable containers (`slugLookup`, `tenantConfigs`, `lookupSets`) provides:
  - Zero additional application-layer cache code
  - Free after the first read per cache window (usually 5–30 minutes)
  - No staleness concerns — cached entries invalidated on write
  - Consistent behavior across all SDK replicas and deployments

  For a multi-location corporation, the first technician at Location A reads the `tenantConfig` (1 RU + cache), and the next technician at Location B in the same minute hits the cached response (0 RU).

  See source code documentation (`RVS.Infra.AzCosmosRepository/`) for detailed indexing policies. DTO structure and field definitions are in `RVS.Domain/DTOs/`.

┌──────────────────────────────────────────────────┐
│ STEP 1: Resolve GlobalCustomerAcct               │
│                                                  │
│ Container: globalCustomerAccts                   │
│ Query: point read by email partition             │
│ Cost: ~1 RU                                      │
│                                                  │
│ Found? → use existing identity                   │
│ Not found? → create new                          │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 2: Resolve Tenant-Scoped CustomerProfile    │
│                                                  │
│ Container: customerProfiles                      │
│ Query: WHERE tenantId = @t                       │
│   AND globalCustomerAcctId = @identityId         │
│ Cost: ~2.8 RU (single-partition indexed query)   │
│                                                  │
│ NOTE: Profile is per-corporation, not per-location│
│ Blue Compass SLC and Denver share the same       │
│ CustomerProfile for John Doe.                    │
│                                                  │
│ Found? → update contact info, handle AssetId     │
│ Not found? → create new shadow profile           │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 2a: AssetId Ownership Resolution            │
│                                                  │
│ If AssetId on THIS profile → update lastSeen     │
│ If AssetId on DIFFERENT profile at same          │
│   corporation → deactivate old, activate on this │
│ If AssetId is brand new → create Active ownership │
│ Cost: ~3 RU (single-partition array filter)      │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 3: Create ServiceRequest                    │
│                                                  │
│ Container: serviceRequests                       │
│ Stamp tenantId + locationId from Step 0          │
│ Embed CustomerSnapshotEmbedded (denormalized)    │
│ Auto-categorize issue (Azure OpenAI)             │
│ Generate technician summary (AI-enhanced with    │
│   diagnostic responses if present)               │
│ Cost: ~1 RU                                      │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 4: Append AssetLedgerEntry (Data Moat)      │
│                                                  │
│ Container: assetLedger                           │
│ Partition: /assetId                              │
│ Includes: locationId + locationName              │
│ Write-only in MVP (nothing reads it yet)         │
│ Cost: ~1 RU                                      │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 5: Update Linkages                          │
│                                                  │
│ CustomerProfile: add SR ID, increment count      │
│ GlobalCustomerAcct: add assetId, add linked profile│
│   reference (with locationId + locationName),    │
│   rotate magic-link token                        │
│   (format: base64url(emailHash):random_bytes)    │
│ Cost: ~2 RU                                      │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 6: Send Confirmation Email                  │
│                                                  │
│ Includes magic-link URL:                         │
│   rvs.app/status/{magicLinkToken}                │
│ Fire-and-forget (async)                          │
└──────────────────────────────────────────────────┘

Total Cosmos cost per intake: ~11.8 RU (cold, gateway miss)
                              ~10.8 RU (gateway-cached slug)

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
- **`ICategorizationService`** — Issue auto-categorization (AI-powered with rule-based fallback) and technician summary generation.
- **`ILocationService`** — Location CRUD with slug-lookup table synchronization.
---

## 6. Intake Orchestration Flow

**Six-step sequence when a customer submits a service request:**

1. **Slug Resolution** — O(1) lookup in `slugLookup` container (gateway-cached) → returns `tenantId` + `locationId` (~1 RU cold, ~0 RU cached)
2. **Global Identity Resolution** — Point read in `globalCustomerAccts` by email. Create if first visit. (~1 RU)
3. **Profile Resolution & Asset Ownership** — Query `customerProfiles` by tenant + identity. Handle asset transfer logic (deactivate old owner, activate current). (~2–3 RU)
4. **ServiceRequest Creation** — Create SR in `serviceRequests` container. Embed customer snapshot. Call `ICategorizationService` for issue categorization. (~1 RU)
5. **Data Moat (Append-Only Ledger)** — Write `AssetLedgerEntry` to `assetLedger` container (partitioned by assetId). (~1 RU)
6. **Linkage Updates** — Increment customer request count, rotate magic-link token, add linked profile reference to global identity. (~2 RU)
7. **Notification** — Send confirmation email with magic-link (fire-and-forget, no Cosmos cost)

**Total RU cost per intake: ~10.8–11.8 RU** (accounting for gateway caching and cold misses)

For detailed implementation logic, see `ServiceRequestService.CreateServiceRequestAsync` and related service classes.
---

## 7. Service Layer

All services are `sealed`, inject repository interfaces + `IUserContextAccessor`, guard clauses first, return domain entities. Follows [MF patterns](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Middleware/ExceptionHandlingMiddleware.cs).

### 7.1 ServiceRequestService (Primary Orchestrator)

Implements the 7-step intake flow from Section 6. Injects: `IServiceRequestRepository`, `IGlobalCustomerAcctService`, `ICustomerProfileService`, `IAssetLedgerService`, `IDealershipService`, `ILocationService`, `ICategorizationService`, `INotificationService`, `IUserContextAccessor`.

**`CreateServiceRequestAsync(tenantId, locationId, request)`** executes Steps 1–6 sequentially:

1. Calls `IGlobalCustomerAcctService.ResolveOrCreateIdentityAsync` with customer email/name/phone from the request DTO.
2. Calls `ICustomerProfileService.ResolveOrCreateProfileAsync` with the resolved identity, asset identifier, and asset info. This handles shadow profile creation and asset ownership transfer.
3. Builds the `ServiceRequest` entity. Stamps `tenantId` and `locationId`. Embeds a `CustomerSnapshotEmbedded` denormalized from the profile (firstName, lastName, email, phone, isReturningCustomer, priorRequestCount). Embeds `DiagnosticResponses` from the request DTO (captured during the AI-guided wizard step). Calls `ICategorizationService.CategorizeAsync` for auto-categorization and technician summary — the categorization service uses diagnostic responses (if present) to produce higher-quality results.
4. Calls `IAssetLedgerService.RecordServiceEventAsync` to append the data moat entry with locationId and locationName.
5. Updates linkages: increments `TotalRequestCount`, rotates the magic-link token on the global identity. Token format: `base64url(SHA256(email)[0..8]):random_bytes` — embeds the email hash so token lookup derives the partition key without a cross-partition query. (Service requests for a customer are retrieved via query: `WHERE tenantId = @t AND customerProfileId = @p` on the `serviceRequests` container — a cheap single-partition read (~3 RU) that avoids unbounded list growth on the profile document.)
6. Fires `INotificationService.SendIntakeConfirmationAsync` with the magic-link token (fire-and-forget).

### 7.2 CustomerProfileService (Shadow Profile + Asset Ownership)

Implements `ResolveOrCreateProfileAsync`. Two phases:

**Phase 1 — Profile Resolution:**
- Find by `globalCustomerAcctId` within tenant partition.
- If not found → create new shadow profile with all customer fields, no assets owned, zero request count.
- If found → update contact info (firstName, lastName, phone) from the latest submission.

**Phase 2 — Asset Ownership Resolution (three branches):**
- **Same customer, same asset** → update `LastSeenAtUtc`, increment `RequestCount` on the existing Active ownership.
- **Different customer at same corporation owns this asset** → deactivate the previous owner's `AssetsOwnedEmbedded` (set status to Inactive, stamp `DeactivatedAtUtc` and reason). Then create or reactivate on the current profile.
- **Brand new asset (not seen before at this corporation)** → create new Active `AssetsOwnedEmbedded` with `FirstSeenAtUtc = now`, `RequestCount = 1`.

Also handles **reactivation** — if the current customer previously had an Inactive ownership for this asset (sold the RV, bought it back), the existing ownership is reactivated rather than creating a duplicate.

### 7.3 LocationService

Implements physical location CRUD for the dealer-facing API. `UpsertSlugLookupAsync(slug, tenantId, locationId)` is called within `CreateLocationAsync` and `UpdateLocationAsync`. On slug rename via `UpdateLocationAsync`, the old slug entry is deleted and the new one is written atomically before the `Location` document is updated — ensuring the lookup table is never stale.

## 7. Service Layer Design

Services follow MF patterns: sealed classes, dependency injection for repositories/interfaces, guard clauses first, return domain entities (not DTOs). See `.github/copilot-instructions.md` for detailed service layer conventions.

**Key orchestration responsibilities** (delegated to specific service classes):
- **`ServiceRequestService`** — Primary orchestrator. Implements the intake flow (Section 6), handles status transitions using `StatusTransitions` validation rules, executes batch outcome operations.
- **`CustomerProfileService`** — Shadow profile resolution/creation, asset ownership tracking and transfer detection.
- **`GlobalCustomerAcctService`** — Cross-dealer identity federation, magic-link token generation and validation.
- **`AssetLedgerService`** — Append-only event recording for Section 10A data accumulation.
- **`LocationService`** — Location CRUD with slug-lookup table synchronization (critical for intake URL consistency).
- **`ICategorizationService`** — Issue categorization (Azure OpenAI with rule-based fallback) and technician summary generation.

All services perform tenant isolation checks via `ClaimsService` before returning data. Authorization policies are enforced at the controller level and validated again in service layer (defense-in-depth).
---

## 8. Controllers

Following the [RVS copilot-instructions.md](https://github.com/markarnoldutah/RVS/blob/f1e1bc1a099c242cace68f515f6bdb8db9ea2418/.github/copilot-instructions.md) and [MF `ClaimsService` pattern](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Services/ClaimsService.cs). Authorization policies referenced below are defined in **RVS_Auth0_Identity_Version2.md Section 10**.

### 8.1 Customer-Facing (Unauthenticated)

**IntakeController** — Route: `api/intake/{locationSlug}`. `[AllowAnonymous]`. Resolves location slug → `tenantId` + `locationId`. Endpoints: `GET` (intake config + optional prefill via `?token=`), `POST diagnostic-questions` (AI-generated follow-up questions based on category + initial description), `POST service-requests` (submit), `POST service-requests/{id}/attachments` (upload).

**CustomerStatusController** — Route: `api/status`. `[AllowAnonymous]`. Endpoint: `GET {token}` parses the email-hash prefix from the token to derive the partition key, validates the magic link via single-partition point read, returns requests across all dealerships/locations.

### 8.2 Dealer-Facing (Authenticated)

**ServiceRequestsController** — Route: `api/dealerships/{dealershipId}/service-requests`. Actions: `GET {id}` (CanReadServiceRequests), `POST search` (CanSearchServiceRequests), `PUT {id}` (CanUpdateServiceRequests), `PATCH batch-outcome` (CanUpdateServiceRequests — applies a shared repair outcome to up to 25 service requests in one call; delegates to `IServiceRequestService.BatchApplyOutcomeAsync`), `DELETE {id}` (CanDeleteServiceRequests). Location filtering applied server-side via `ClaimsService.HasAccessToLocation()`.

> **Phase 2 — Request Additional Information (GAP 5 — MVP Deferral)**
>
> **MVP approach:** Managers record questions internally via the existing `PUT {id}` update and contact the customer via phone/email outside the system. Customer contact details are available in the embedded `CustomerSnapshotEmbedded` (`Email`, `Phone`).
>
> **Phase 2 implementation:** Add `INotificationService.SendFollowUpRequestAsync(serviceRequestId, message)` and a new endpoint `POST api/dealerships/{id}/service-requests/{srId}/follow-ups`. This will send the customer a structured follow-up email with a link to a response form on their magic-link status page.

**AttachmentsController** — Route: `api/dealerships/{dealershipId}/service-requests/{serviceRequestId}/attachments`. Actions: `POST` (CanUploadAttachments — authenticated upload for dealer staff/technicians), `GET {attachmentId}` (CanReadAttachments), `DELETE {attachmentId}` (CanDeleteAttachments).

**DealershipsController** — Route: `api/dealerships`. Actions: `GET` (CanReadDealerships), `GET {id}` (CanReadDealerships), `PUT {id}` (CanUpdateDealerships).

**LocationsController** — Route: `api/locations`. Actions: `GET` (CanReadLocations, filtered by user's `locationIds`), `GET {id}` (CanReadLocations), `POST` (CanCreateLocations), `PUT {id}` (CanUpdateLocations), `GET {id}/qr-code` (CanReadLocations).

**TenantsController** — Route: `api/tenants`. Actions: `POST config`, `GET config`, `PUT config`, `GET access-gate` (all CanManageTenantConfig).

**LookupsController** — Route: `api/lookups`. Action: `GET {lookupSetId}` (CanReadLookups).

**AnalyticsController** — Route: `api/dealerships/{dealershipId}/analytics`. Action: `GET service-requests/summary` (CanReadAnalytics).

- **Query parameters:** `?from={date}&to={date}&locationId={locId}` (all optional)
- **Response:** `ServiceRequestAnalyticsResponseDto` — covers all analytics dimensions needed by the Service Manager Desktop, including request counts by status/category/location, top failure modes, top repair actions, average repair time, top parts used, and average days to completion. See Section 12.1 for the full response DTO field definitions.
- **Performance:** For MVP volumes (<200 jobs/month), a single-partition aggregate query is acceptable (~5–10 RU). Accelerate the Phase 2 change feed → Azure Tables aggregation (Section 15.3) if analytics query cost becomes measurable at higher volume.

## 8. API Controllers

**Customer-Facing (Unauthenticated):**
- **IntakeController** — `api/intake/{locationSlug}` — GET (intake form config + prefill), POST diagnostic-questions (AI questions), POST service-requests (submit), POST attachments (upload)
- **CustomerStatusController** — `api/status/{token}` — GET (cross-dealer service request summary via magic link)

**Dealer-Facing (Authenticated):**
- **ServiceRequestsController** — `api/dealerships/{dealershipId}/service-requests` — CRUD operations, search/filter, batch-outcome endpoint (up to 25 at once)
- **AttachmentsController** — `api/.../attachments` — Upload (authenticated), retrieve SAS URLs, delete
- **DealershipsController**, **LocationsController**, **TenantsController**, **LookupsController**, **AnalyticsController** — Standard CRUD and reporting operations

All authenticated endpoints enforce authorization policies defined in `RVS_Auth0_Identity_Version2.md`. Location-based access control is enforced server-side via `ClaimsService.HasAccessToLocation()`. See Section 11 for the complete API route summary.

⚠️ **SECURITY GAP:** Unauthenticated intake and attachment endpoints (`POST api/intake/{locationSlug}/service-requests` and file uploads) acceptlarge file uploads. See Assessment doc: **Azure Front Door WAF** is required to mitigate injection, traversal, and DDoS attacks on these high-risk surfaces.
---

## 9. Middleware Pipeline

Following the [RVS copilot-instructions.md pipeline order](https://github.com/markarnoldutah/RVS/blob/f1e1bc1a099c242cace68f515f6bdb8db9ea2418/.github/copilot-instructions.md) and [MF `ExceptionHandlingMiddleware`](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Middleware/ExceptionHandlingMiddleware.cs):

| Order | Component | Registration Pattern | Description |
|---|---|---|---|
| 1 | Dev-only endpoints | `MapOpenApi()`, `UseSwaggerUI()` | Development environment only |
| 2 | HTTPS redirection | `UseHttpsRedirection()` | Production only |
| 3 | CORS | `UseCors("AllowBlazorClient")` | Allows Blazor WASM client origin |
| 4 | Rate limiting | `UseRateLimiter()` | Protects public intake + status endpoints |
| 5 | ExceptionHandlingMiddleware | `IMiddleware`, singleton | Catches all unhandled exceptions, returns structured ProblemDetails |
| 6 | Authentication & Authorization | `UseAuthentication()` + `UseAuthorization()` | Auth0 JWT validation + policy checks |
| 7 | TenantAccessGateMiddleware | `RequestDelegate`, scoped injection | Checks `TenantConfig.AccessGate` to verify tenant is active/configured |
| 8 | Map controllers | `MapControllers()` | Terminal |

> **Cosmos SDK connection mode:** `ConnectionMode.Gateway` is configured globally on `CosmosClient`. Gateway mode enables server-side result caching for point reads on stable-key containers (`slugLookup`, `tenantConfigs`, `lookupSets`), reducing effective RU consumption on repeated reads without application-layer cache code. `TenantAccessGateMiddleware` reads `TenantConfig` on every authenticated request — gateway caching makes this effectively free after the first read per tenant per cache window.

## 9. Middleware Pipeline

Middleware registration order (from `Program.cs`):
1. Dev-only endpoints (Swagger/OpenAPI)
2. HTTPS redirection (prod only)
3. CORS (`AllowBlazorClient`)
4. **Rate limiting** (protects public intake endpoints)
5. **ExceptionHandlingMiddleware** (singleton, returns ProblemDetails)
6. Authentication & Authorization (Auth0 JWT validation + policy checks)
7. **TenantAccessGateMiddleware** (scoped, checks `TenantConfig.AccessGate` for tenant active status)
8. Controller mapping

**Cosmos Gateway-Mode Caching:** `ConnectionMode.Gateway` enables server-side caching for `slugLookup`, `tenantConfigs`, `lookupSets` — repeated reads hit cache (0 RU). Especially valuable for `TenantAccessGateMiddleware` reads on every authenticated request.

⚠️ **ASSESSMENT GAPS:**
- No Application Insights telemetry — **P0 before paying customers** (see Assessment doc for required instrumentation)
- No health check endpoints — **add `/health` endpoint** to detect Cosmos/Blob connectivity issues
- Per-tenant rate limiting missing on dealer API — **implement per-`tenantId` sliding-window limits** to prevent noisy neighbor issues
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

- **Upload:** Streaming via API (MVP). Future: SAS URI direct upload for large videos.
- **Access:** Time-limited SAS URIs generated on demand.
- **Retention:** Configurable per-tenant in `TenantConfig`.
- **Path includes `locationId`** for storage organization and future per-location retention policies.

## 10. Blob Storage & File Upload

**Structure:** `rvs-attachments/{tenantId}/{locationId}/{serviceRequestId}/` — tenant/location scoping enables future per-location retention policies.

**Access:** Time-limited SAS URIs generated on demand. Container access set to `BlobContainerPublicAccessType.None` to prevent accidental public exposure.

**Upload Strategy:** MVP routes uploads through API (streaming). Phase 2+: SAS pre-signed direct upload from client to Blob Storage (eliminates API thread blocking on large video files, see Assessment doc).

⚠️ **ASSESSMENT GAP:** No SAS pre-signed direct upload. Large video uploads (25 MB) currently block API worker threads. Recommend **Phase 1 Sprint N: implement SAS pre-signed URLs** (~50 lines, high-impact performance improvement).
---

## 11. Complete API Route Summary

| Method | Route | Auth | Policy | Purpose |
|---|---|---|---|---|
| `GET` | `api/intake/{locationSlug}?token={t}` | Anonymous | — | Intake config + optional prefill via magic link |
| `POST` | `api/intake/{locationSlug}/service-requests` | Anonymous | — | Submit request → full intake orchestration |
| `POST` | `api/intake/{locationSlug}/diagnostic-questions` | Anonymous | — | AI-generated diagnostic questions for selected category |
| `POST` | `api/intake/{locationSlug}/service-requests/{id}/attachments` | Anonymous | — | Upload photo/video |
| `GET` | `api/status/{token}` | Anonymous | — | Customer status page via magic link (cross-dealer) |
| `GET` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanReadServiceRequests | Request detail |
| `POST` | `api/dealerships/{id}/service-requests/search` | Bearer | CanSearchServiceRequests | Search/filter requests |
| `PUT` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanUpdateServiceRequests | Update request |
| `PATCH` | `api/dealerships/{id}/service-requests/batch-outcome` | Bearer | CanUpdateServiceRequests | Batch apply repair outcome to multiple service requests (max 25) |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanDeleteServiceRequests | Delete request |
| `GET` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | CanReadAttachments | Get attachment SAS URL |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | CanDeleteAttachments | Delete attachment |
| `POST` | `api/dealerships/{id}/service-requests/{srId}/attachments` | Bearer | CanUploadAttachments | Upload attachment (authenticated — technician/staff photo capture) |
| `GET` | `api/dealerships` | Bearer | CanReadDealerships | List dealerships for tenant |
| `GET` | `api/dealerships/{id}` | Bearer | CanReadDealerships | Dealership detail |
| `PUT` | `api/dealerships/{id}` | Bearer | CanUpdateDealerships | Update dealership |
| `GET` | `api/locations` | Bearer | CanReadLocations | List locations (filtered by user's access) |
| `GET` | `api/locations/{id}` | Bearer | CanReadLocations | Location detail |
| `POST` | `api/locations` | Bearer | CanCreateLocations | Create location |
| `PUT` | `api/locations/{id}` | Bearer | CanUpdateLocations | Update location |
| `GET` | `api/locations/{id}/qr-code` | Bearer | CanReadLocations | Generate intake QR code |
| `GET` | `api/dealerships/{id}/analytics/service-requests/summary?from={date}&to={date}&locationId={locId}` | Bearer | CanReadAnalytics | Request analytics (see Section 12.1 for response DTO) |
| `POST` | `api/tenants/config` | Bearer | CanManageTenantConfig | Bootstrap tenant config |
| `GET` | `api/tenants/config` | Bearer | CanManageTenantConfig | Get tenant config |
| `PUT` | `api/tenants/config` | Bearer | CanManageTenantConfig | Update tenant config |
| `GET` | `api/tenants/access-gate` | Bearer | CanManageTenantConfig | Access gate check |
| `GET` | `api/lookups/{lookupSetId}` | Bearer | CanReadLookups | Lookup values |

## 11. API Reference

**Public intake endpoints** — `api/intake/{locationSlug}` (GET config, POST submit, POST diagnostic-questions, POST attachments)
**Status page** — `api/status/{token}` (GET cross-dealer service requests)
**Service requests** — `api/dealerships/{id}/service-requests` (CRUD, search, batch-outcome)
**Attachments** — `api/.../{srId}/attachments` (upload, retrieve, delete)
**Dealerships, Locations, Tenants, Analytics** — Standard CRUD routes with policy-based access control

See `.github/copilot-instructions.md` for route naming conventions and `Controllers/` source code for method signatures. Detailed API reference documentation should live in `Docs/API/` (not yet created).
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
| **With gateway-cached slug** | Subtract ~1 RU from above | ~10.8 / ~9.8 / ~10.8 RU |
| **Magic-link status page** | 1 point read (token → partition via email-hash prefix) + N point reads (linked SRs) | ~1 + N RU |
| **Dealer dashboard: view request** | 1 point read (SR — snapshot embedded) | ~1 RU |
| **Dealer dashboard: search requests** | 1 single-partition query (with locationId filter) | ~3 RU |
| **Asset service history (10A query)** | 1 single-partition read (assetLedger, /assetId) | **~1 RU** |

---

## 12.1 Analytics Response DTO

`ServiceRequestAnalyticsResponseDto` — Returned by `GET api/dealerships/{id}/analytics/service-requests/summary`. Supports optional time range and location filtering via query parameters (`?from={date}&to={date}&locationId={locId}`).

| Field | Type | Description |
|---|---|---|
| `TotalRequests` | `int` | Total service requests in the queried period |
| `RequestsByStatus` | `Dictionary<string, int>` | Count per status value (e.g. `"New"`, `"InRepair"`) |
| `RequestsByCategory` | `Dictionary<string, int>` | Count per issue category |
| `RequestsByLocation` | `Dictionary<string, int>` | Count per location (relevant for multi-location groups) |
| `TopFailureModes` | `List<AnalyticsRankItem>` | Top N failure modes with occurrence count |
| `TopRepairActions` | `List<AnalyticsRankItem>` | Top N repair actions with occurrence count |
| `AverageRepairTimeHours` | `decimal?` | Mean labor hours across completed jobs with `serviceEvent.laborHours` populated |
| `TopPartsUsed` | `List<AnalyticsRankItem>` | Top N parts with total replacement count |
| `AverageDaysToComplete` | `decimal?` | Mean elapsed days from `createdAtUtc` to `Closed` status across completed jobs |

`AnalyticsRankItem` — `Name` (`string`), `Count` (`int`).

> **Scaling note:** For MVP volumes (<200 jobs/month), a single-partition aggregate query is acceptable (~5–10 RU). If analytics queries become expensive at higher volume, accelerate the Phase 2 change feed → Azure Tables aggregation pattern (Section 15.3).

## 12. Cost & Performance Characteristics

**RU Consumption (Cosmos DB):**
- Slug resolution: ~1 RU (cold) / ~0 RU (gateway-cached)
- Intake submission: ~10.8–11.8 RU (6–7 Cosmos operations)
- Dealer dashboard search: ~3 RU (single-partition query)
- Asset history query: ~1 RU (point read)

**Performance Optimization:** Gateway-mode caching on stable containers (`slugLookup`, `tenantConfigs`, `lookupSets`) provides free RU after first read. For MVP volumes (<200 jobs/month), on-demand Cosmos queries are acceptable. Phase 2: offload analytics to Azure Tables (change feed consumer, see Section 15).

**Scaling:** All containers use autoscale except manual 400 RU on `dealerships`, `tenantConfigs`, `lookupSets`. Assessment recommends autoscaling all three to handle intake bursts (saves ~$170/month total floor cost).
---

## 13. Magic Link Security

| Concern | Mitigation |
|---|---|
| **Token format** | `base64url(SHA256(email)[0..8]):random_bytes` — email-hash prefix embedded in token enables partition-key derivation on lookup (single-partition point read, no cross-partition query) |
| **Token guessing** | Random portion is 32-byte cryptographic random (256-bit entropy), URL-safe Base64. The 8-byte email-hash prefix is non-secret metadata; security relies entirely on the random portion. |
| **Token expiry** | 30-day default, configurable per tenant |
| **Token rotation** | New token on every intake submission; previous invalidated |
| **Rate limiting** | `api/status/{token}` limited to 10 req/min per IP |
| **PII exposure** | Status page returns first name + asset summaries only — no full email, no phone, no other customers' data |
| **Cross-dealer visibility** | Customer sees their own requests across all corporations — intentional for customer convenience. No other customer's data is exposed. |

## 13. Security & Magic Link Design

**Magic-Link Pattern:**
- Token format: `base64url(SHA256(email)[0..8]):random_bytes` — email-hash prefix enables O(1) partition-key derivation (no cross-partition query needed)
- 32-byte random portion (256-bit entropy, URL-safe Base64)
- 30-day expiry, rotated on each intake
- Status page view limited to 10 req/min/IP
- Cross-dealer visibility intentional (customer sees all their SRs across corporations)

⚠️ **ASSESSMENT GAPS — CRITICAL SECURITY ISSUES:**
1. **SFTP private keys in Cosmos DB** — Must move to Azure Key Vault. See Assessment: CRITICAL — SFTP security gap.
2. **No Azure Front Door WAF** — Unauthenticated intake/upload endpoints are unprotected. See Assessment: CRITICAL — WAF missing.
3. **No Key Vault for any secrets** — Auth0 client secret, Azure OpenAI API key should not be in `appsettings.json`. See Assessment: HIGH — Key Vault integration.
4. **Blob container public access not hardened** — Container should be explicitly set to `BlobContainerPublicAccessType.None`. See Assessment: HIGH — add startup assertion.
---

## 14. Key Architectural Decisions Summary

| Decision | Rationale |
|---|---|
| **Tenant = Corporation, not Location** | Enables cross-location analytics, shared customer profiles, single Auth0 Org for Blue Compass |
| **`locationId` as filter, not partition key** | Cross-location queries stay single-partition. Avoids fan-out for corporate dashboards. |
| **Shadow profiles (no customer sign-up)** | Zero friction intake. Customers never see a registration screen. |
| **Three identity containers** | Each access pattern needs a different partition key. One doc can't serve all three. |
| **Append-only asset ledger** | Data moat — proprietary, accumulating, non-replicable. Powers Section 10A intelligence. |
| **`AssetId` as `{AssetType}:{Identifier}` compound key** | Globally unique across asset types; clean Cosmos partition key; preserves VIN/HIN/serial semantics; works across industries without schema changes. |
| **`CustomerSnapshotEmbedded` denormalized in SR** | Dashboard reads never join to customerProfiles. ~1 RU per view. |
| **Magic link on global identity, not profile** | Status page shows requests across ALL corporations for the customer. |
| **Email-hash prefix in magic-link token** | Avoids cross-partition query on `globalCustomerAccts` (partitioned by `/email`). Token format `base64url(emailHash):random_bytes` lets `ValidateMagicLinkAsync` derive the partition key from the token itself → single-partition point read (~1 RU). |
| **`IntakeFormConfigEmbedded` on Location, not Dealership** | Each physical site can have different intake settings (e.g., different file size limits). |
| **`regionTag` on Location** | Enables regional manager scoping without complex hierarchy. |
| **Intake URL uses `locationSlug`, not `dealershipSlug`** | Each physical location has its own QR code / intake URL. The slug resolves to both `tenantId` and `locationId` via a point read on the `slugLookup` container — O(1), gateway-cacheable, replica-consistent, no application-layer cache required. |
| **Blob path includes `locationId`** | Storage organization mirrors data model. Enables future per-location retention policies. |
| **`Dealership.IsMultiLocation` flag** | UI can adapt (show location picker vs. skip). No code branching in the API layer. |
| **`ConnectionMode.Gateway` for Cosmos SDK** | Enables server-side gateway caching for stable point reads (`slugLookup`, `tenantConfigs`, `lookupSets`). Zero additional cost. Eliminates application-layer caching for these access patterns. Consistent across all replicas and through deploys. |
| **`slugLookup` as a dedicated container** | Decouples slug resolution from the `locations` container partition scheme. Enables O(1) point reads partitioned by `/slug`. Gateway-cached on repeated reads. Autoscale floor ~$2.30/month. Invalidation is write-through on `CreateLocationAsync` / `UpdateLocationAsync`. |
| **Azure OpenAI for diagnostic questions, not static templates** | Dynamic AI-generated questions adapt to category, description, and asset context. Eliminates need for per-category question template CRUD, admin UI, and ongoing curation. Cost: ~$0.0002/intake with GPT-4o-mini. Rule-based fallback if AI is unavailable. |

## 14. Key Architectural Decisions

**Multi-tenancy:** Tenant = Corporation, not Location. Enables Blue Compass (100+ locations) to have one partition, one Auth0 Org, cheap cross-location analytics. `locationId` is a filter, not a partition boundary.

**Identity Separation:** Three containers (serviceRequests/tenantId, globalCustomerAccts/email, assetLedger/assetId) serve three different access patterns — each needs its own partition key for O(1) efficiency.

**No Customer Sign-Up:** Shadow profiles created automatically on first intake. Zero friction.

**Denormalization:** `CustomerSnapshotEmbedded` embedded in ServiceRequest eliminates joins on dashboard reads (~1 RU/view vs. ~5–10 RU with join).

**Data Moat:** Append-only `AssetLedgerEntry` indexed by `assetId` accumulates proprietary service intelligence (Section 10A). Write-only in MVP; read-enabled Phase 5–6 when 10A analytics materialize.

**Smart Token Design:** Magic-link token embeds email-hash prefix (`base64url(SHA256(email)[0..8]):random_bytes`), enabling single-partition token lookup without cross-partition scan.

**Slug Lookup Table:** Dedicated O(1) container (gateway-cached) for intake URL resolution. Decouples from `locations` partition scheme, enables QR-code-per-location scalability.

**Azure OpenAI Fallback:** Dynamic AI-generated diagnostic questions with rule-based fallback. No template CRUD Admin UI. Cost: ~$0.0002/intake (negligible).

⚠️ **ASSESSMENT GAPS** — Architectural issues requiring resolution:
- No IaC (Bicep/Terraform) for infrastructure → manual setup, drift, disaster recovery risk. See Assessment: CRITICAL P0 item.
- No CI/CD pipeline (GitHub Actions) → cannot automate deployment or blue-green updates. See Assessment: CRITICAL P0 item.
- No billing/metering infrastructure → cannot charge customers or enforce plan tiers. See Assessment: HIGH P1 item.
- No Application Insights → no observability, cannot meet SaaS uptime commitments. See Assessment: CRITICAL P0 item.
- Per-tenant rate limiting missing → vulnerable to noisy neighbor. See Assessment: HIGH P1 item.
---

## 15. Change Feed Strategy

Cosmos DB Change Feed is not used in MVP, but several architectural patterns create natural future consumers. This section documents where change feed fits and when each consumer is warranted.

### 15.1 Overview

| Consumer | Source Container | Target | Phase | Priority |
|---|---|---|---|---|
| Asset ledger enrichment | `serviceRequests` | `assetLedger` | Phase 5–6 | **Required** |
| Analytics counter aggregation | `serviceRequests` | Azure Tables | Phase 2 | High |
| Snapshot staleness repair | `serviceRequests` | `serviceRequests` | Phase 2+ | Low |

### 15.2 Asset Ledger Enrichment (Required — Phase 5–6)

**Problem:** `AssetLedgerEntry` is written at intake time with only the fields available then (`IssueCategory`, `IssueDescription`, basic asset info). Section 10A fields — `FailureMode`, `RepairAction`, `PartsUsed`, `LaborHours`, `ServiceDateUtc` — are populated by the technician when the `ServiceRequest` is updated to `Completed`.

**Pattern:** A change feed consumer watches `serviceRequests` for documents where `status = Completed` and `serviceEvent` fields are populated. It issues a patch update to the matching `assetLedger` document (keyed by `serviceRequestId` within the `/assetId` partition).

**Why change feed, not synchronous write:** The SR completion path is latency-sensitive (technician UI). The ledger enrichment is analytics pipeline work — eventual consistency (seconds to minutes) is acceptable. Decoupling also means a ledger write failure doesn't roll back the SR completion.

**MVP stance:** The `AssetLedgerEntry` is intentionally written with null Section 10A fields at intake. The stale state is acceptable until Phase 5–6 when the 10A query surface is built out.

### 15.3 Analytics Counter Aggregation (Phase 2)

**Problem:** Dealer dashboards will need aggregate counts (open requests by location, requests by status, daily volume). These are expensive to compute on-demand from Cosmos.

**Pattern:** A change feed consumer on `serviceRequests` maintains pre-aggregated counters in `RVS.Infra.AzTablesRepository` (Azure Table Storage). Counters are partitioned by `tenantId` + `locationId` and updated on every status transition. Dashboard reads hit Azure Tables for counts (~0 RU Cosmos cost) and Cosmos only for document-level detail.

**MVP stance:** Aggregate queries run against Cosmos directly (single-partition, acceptable at low volume). Azure Tables aggregation is deferred until query cost or latency becomes measurable.

### 15.4 CustomerSnapshot Staleness (Phase 2+, Low Priority)

**Problem:** `CustomerSnapshotEmbedded` is denormalized into every `ServiceRequest` at intake time. If a customer later updates their contact info (phone, email) in `customerProfiles`, open SRs reflect stale data.

**Design intent:** The snapshot is point-in-time by design — it records who the customer was at intake, not who they are today. This is correct for completed/historical SRs. For open SRs, staleness is a minor UX issue (advisor sees old phone number).

**Pattern if needed:** A change feed consumer on `customerProfiles` fans out patch updates to open `serviceRequests` in the same tenant partition. Scope is limited to `status IN ('New', 'InProgress')` to avoid touching historical records.

**MVP stance:** Accept stale snapshots. The advisor can always look up the current profile. This consumer is only warranted if customer contact updates during open SRs become a reported pain point.

## 15. Change Feed Strategy (Phase 2+)

Not used in MVP. Planned consumers for future phases:

1. **Asset Ledger Enrichment** (Phase 5–6, required) — Watch `serviceRequests` for status=Completed. Patch `assetLedger` with Section 10A fields (FailureMode, RepairAction, etc.). Eventual consistency acceptable (seconds-to-minutes latency).

2. **Analytics Aggregation** (Phase 2, high) — Watch `serviceRequests`, maintain pre-aggregated counters in Azure Tables (partitioned by tenantId+locationId). Reduces analytics query cost from ~5–10 RU to ~0 RU Cosmos.

3. **Snapshot Staleness Repair** (Phase 2+, low) — Watch `customerProfiles`, fan out updates to open `serviceRequests` if customer contact info changes. Minor UX benefit only; accept stale snapshots in MVP.

*** End Patch
---

## 16. Azure OpenAI Integration

### 16.1 Purpose

Azure OpenAI powers two features in the intake flow:

1. **Dynamic diagnostic question generation** — Given an issue category, optional initial description, and asset info, generates 2–4 targeted follow-up questions with checkbox options. Replaces static per-category question templates.
2. **Enhanced auto-categorization and technician summary** — Uses structured diagnostic responses (if present) alongside the free-text description to produce more accurate issue categorization and richer technician summaries.

### 16.2 Interface

`ICategorizationService` is the sole integration point. Two methods:

| Method | Purpose | Called When |
|---|---|---|
| `CategorizeAsync(issueDescription, issueCategory?, assetInfo?, diagnosticResponses?)` | Auto-categorize + generate technician summary | Step 3 of intake orchestration (submission) |
| `GenerateDiagnosticQuestionsAsync(issueCategory, initialDescription?, assetInfo?, aiContext?)` | Generate follow-up questions for the intake wizard | `POST api/intake/{locationSlug}/diagnostic-questions` |

### 16.3 Implementation

**Primary: `AzureOpenAiCategorizationService`** — Lives in `RVS.API/Integrations/`. Calls Azure OpenAI (GPT-4o-mini) with a structured system prompt containing RV service domain knowledge. Uses JSON mode for structured output. The `aiContext` parameter (from `IntakeFormConfigEmbedded`) is appended to the system prompt when present, allowing per-dealer customization without a template admin UI.

**Fallback: Rule-based** — If Azure OpenAI is unavailable (timeout, quota, outage), falls back to keyword-based categorization (existing rule-based logic) and returns a minimal set of hardcoded questions per category. The fallback ensures intake never blocks on an external service.

### 16.4 DTOs

**Request — `DiagnosticQuestionsRequestDto`:**

| Field | Type | Description |
|---|---|---|
| `IssueCategory` | `string` | Selected category (e.g. "Slide System") |
| `InitialDescription` | `string?` | Optional free-text the customer has typed so far |
| `Asset` | `AssetInfoDto?` | Optional vehicle info for context |

**Response — `DiagnosticQuestionsResponseDto`:**

| Field | Type | Description |
|---|---|---|
| `Questions` | `List<DiagnosticQuestionDto>` | 2–4 generated questions |
| `SmartSuggestion` | `string?` | Optional AI insight (e.g. "This is commonly caused by a hydraulic pump issue. Please upload a photo of the hydraulic pump area if possible.") |

**`DiagnosticQuestionDto`:**

| Field | Type | Description |
|---|---|---|
| `QuestionText` | `string` | The question to display |
| `Options` | `List<string>` | Checkbox/radio options |
| `AllowFreeText` | `bool` | Whether to show a free-text input alongside options |
| `HelpText` | `string?` | Optional guidance (e.g. "Upload a photo of...") |

**`DiagnosticResponseDto`** (in `ServiceRequestCreateRequestDto`):

| Field | Type | Description |
|---|---|---|
| `QuestionText` | `string` | The question that was asked |
| `SelectedOptions` | `List<string>` | Options the customer selected |
| `FreeTextResponse` | `string?` | Optional free-text answer |

### 16.5 Cost & Performance

| Model | Input Tokens | Output Tokens | Cost per Intake | Latency |
|---|---|---|---|---|
| **GPT-4o-mini** (recommended) | ~300 | ~400 | ~$0.0002 | 500ms–1s |
| GPT-4o (future, if quality demands) | ~300 | ~400 | ~$0.003 | 1–2s |

At 1,000 intakes/month: ~$0.20/month. At 100,000 intakes/month: ~$20/month. Negligible relative to Cosmos DB costs.

Latency is acceptable because the diagnostic questions call happens during a wizard step transition — the customer just tapped "Next" after selecting a category. A brief loading indicator ("Generating diagnostic questions...") is expected.

### 16.6 Two-Phase Intake Flow

The intake wizard becomes a two-phase API interaction:

**Phase 1 — Diagnostic Questions (mid-wizard):**
```
POST api/intake/{locationSlug}/diagnostic-questions
Body: { issueCategory, initialDescription?, asset? }
→ Returns: DiagnosticQuestionsResponseDto (2–4 questions + optional smart suggestion)
```

**Phase 2 — Submission (existing flow, enhanced):**
```
POST api/intake/{locationSlug}/service-requests
Body: { asset, issueCategory, issueDescription, diagnosticResponses[], customer }
→ Returns: 201 Created with SR details
```

Phase 1 is optional — if it fails or times out, the customer proceeds with free-text description only. The submission in Phase 2 works with or without `diagnosticResponses`.

### 16.7 Configuration

Azure OpenAI settings in `appsettings.json`:

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

Authentication uses `DefaultAzureCredential` (consistent with Cosmos DB, Blob Storage, and all other Azure services in the stack via `RVS.Infra.AzCredentials`).

### 16.8 Architectural Decision

| Decision | Rationale |
|---|---|
| **AI-generated questions, not static templates** | Eliminates `DiagnosticQuestionTemplate` entity, template CRUD API, admin UI, and per-dealer curation. AI adapts to any category including "Other". Cost is negligible (~$0.0002/intake). |
| **Single `ICategorizationService` interface** | Both question generation and categorization share the same domain knowledge and external dependency. One interface, one DI registration, one fallback strategy. |
| **Fallback to rule-based on AI failure** | Intake must never block on an external service. The fallback produces acceptable (if less rich) results. |
| **`aiContext` on `IntakeFormConfigEmbedded`** | Dealers can customize AI behavior without a template admin UI. Simple string field, appended to system prompt. E.g. "We specialize in Grand Design and Keystone brands." |
| **`diagnosticResponses` embedded in `ServiceRequest`** | Stored alongside the SR for full audit trail. Used by the categorization step for better results. Queryable via Cosmos indexing for future analytics on symptom patterns. |

## 16. Azure OpenAI Integration

**Purpose:** Two features powered by Azure OpenAI (GPT-4o-mini):
1. Dynamic diagnostic question generation (replaces static templates)
2. Issue auto-categorization + technician summary enhancement

**Interface:** `ICategorizationService` with two methods — `CategorizeAsync` and `GenerateDiagnosticQuestionsAsync`. Rule-based fallback if AI unavailable (intake never blocks on external service).

**Configuration:** `appsettings.json` contains endpoint, deployment name, token limits, timeout. Uses `DefaultAzureCredential` for authentication (consistent with Cosmos, Blob Storage).

**Cost:** ~$0.0002 per intake with GPT-4o-mini. Negligible (~$0.20/month at 1,000 intakes/month).

**Per-Tenant Customization:** `IntakeFormConfigEmbedded.AiContext` allows dealers to append custom context to system prompt (e.g., "We specialize in Grand Design and Keystone brands") — no template admin UI needed.

### 16a. Diagnostic Response Capture (Two-Phase Intake Flow)

**Phase 1** — `POST api/intake/{locationSlug}/diagnostic-questions` returns AI-generated 2–4 questions with checkbox options
**Phase 2** — `POST api/intake/{locationSlug}/service-requests` submits SR with `diagnosticResponses[]` array embedded

Responses are stored in `ServiceRequest` and used by categorization service to produce higher-quality results.
---

## 17. Technician Mobile App — API Readiness

This section documents the API surface gaps identified from the technician mobile app feature requirements (see `Docs/Research/FrontEnd/RVS_Features_Tech_Mobile.md`) and the resolutions adopted in this architecture.

### 17.1 Gap Summary

| Priority | Gap | Resolution | Status |
|---|---|---|---|
| 🔴 Critical | No authenticated attachment upload for dealer staff | Added `POST` to `AttachmentsController` with `CanUploadAttachments` policy (Section 8.2, Section 11) | ✅ Resolved in this version |
| 🔴 Critical | `dealer:technician` missing `service-requests:search` permission | Added to role → permission matrix (see `RVS_Auth0_Identity` update) | ✅ Resolved in this version |
| 🟡 Important | No `assignedTechnicianId` search filter (My Jobs Queue) | Added to `ServiceRequestSearchRequestDto` (Section 5.1) | ✅ Resolved in this version |
| 🟡 Important | No `assignedBayId` search filter (Bay-Based Access) | Added to `ServiceRequestSearchRequestDto` (Section 5.1) | ✅ Resolved in this version |
| 🟡 Important | No `assetId` search filter (VIN scan → job lookup) | Added to `ServiceRequestSearchRequestDto` (Section 5.1) | ✅ Resolved in this version |
| 🟡 Important | Seed `LookupSet` data for `failureModes`, `repairActions` | Documented below as seed data requirement | ✅ Resolved in this version |
| 🟢 Nice-to-have | Voice notes (audio file support) | Added `.m4a`, `.wav` to `IntakeFormConfigEmbedded.AcceptedFileTypes` defaults (Section 3.8) | ✅ Resolved in this version |
| 🟢 Nice-to-have | Batch update endpoint for offline sync | Deferred — sequential `PUT` calls acceptable for MVP | 🔵 Deferred |
| 🔵 Future | Labor time prediction API | Phase 5–6 feature powered by `AssetLedger` data | 🔵 Deferred |

### 17.2 Required LookupSet Seed Data

The technician mobile app's repair outcome entry screen (failure mode selection, repair action selection) requires the following `LookupSet` categories to be seeded in `RVS.Data.Cosmos.Seed`:

| Category | Example Items | Used By |
|---|---|---|
| `failureModes` | Hydraulic pump failure, Slide motor failure, Electrical fault, Fluid leak, Seal degradation, Bearing failure, Control board malfunction | Failure Mode picker |
| `repairActions` | Replace pump, Replace motor, Repair wiring, Adjust mechanism, Replace seal, Lubricate, Reflash firmware | Repair Action picker |
| `componentTypes` | Hydraulic System, Electrical System, Slide Mechanism, HVAC, Plumbing, Refrigeration, Leveling System, Awning, Generator | Component Type picker |

These categories use the existing `LookupSet` entity and `LookupsController` (`GET api/lookups/{lookupSetId}`). The `dealer:technician` role already has `lookups:read` permission. No new API endpoints or permissions are needed — only seed data.

### 17.3 Offline Sync Strategy (Deferred)

For MVP, the technician mobile app handles offline mode client-side:

1. Queue failed `PUT` requests locally (IndexedDB or SQLite).
2. On connectivity restore, replay queued requests sequentially via `PUT api/dealerships/{id}/service-requests/{srId}`.
3. Use `updatedAtUtc` for optimistic concurrency — if the server's `updatedAtUtc` is newer than the queued request's baseline, surface a conflict to the user.

A dedicated `POST api/dealerships/{id}/service-requests/batch-update` endpoint may be added in a future phase if sequential replay proves insufficient at scale.

### 17.4 Labor Time Prediction (Deferred — Phase 5–6)

When the `AssetLedger` accumulates sufficient Section 10A data, a prediction endpoint can be added:

```
GET api/predictions/labor?issueCategory={cat}&componentType={type}&manufacturer={mfg}
```

For MVP, the mobile app uses static suggested labor times from the `LookupSet` data or hardcoded client-side values.

## 17. Technician Mobile App

**Resolved gaps (this version):**
- Authenticated attachment upload (photo/video capture in field)
- VIN scan → job lookup filter (`assetId` in search)
- "My Jobs" queue filter (`assignedTechnicianId` in search)
- Bay-based job assignment filter (`assignedBayId`)
- Voice note support (`.m4a`, `.wav` file types)

**Required seed data:** `LookupSet` categories for `failureModes`, `repairActions`, `componentTypes` (used by repair outcome picker). See `RVS.Data.Cosmos.Seed/` project.

**Deferred to Phase 2:**
- Batch update endpoint (offline sync currently uses sequential PUT calls)
- Labor time prediction API (requires sufficient AssetLedger data accumulation)
- Optimistic offline queue (MVP: client-side JSON queue, replay on reconnect)
---

## 18. Service Manager Desktop App — API Readiness

This section documents the API surface gaps identified from the Service Manager Desktop feature requirements (see `Docs/Research/FrontEnd/RVS_Features_Mngr_Desktop.md`) and the resolutions adopted in this architecture version.

### 18.1 Gap Summary

| Priority | Gap | Resolution | Status |
|---|---|---|---|
| 🔴 Critical | Status model only has 4 values; Service Board needs 7+1 | Expanded to 8 status values with transition validation (`StatusTransitions.cs`) — Section 3.1 | ✅ Resolved in this version |
| 🔴 Critical | No batch outcome endpoint for Batch Outcome Entry (M6) | Added `PATCH api/dealerships/{id}/service-requests/batch-outcome` — Sections 8.2, 11 | ✅ Resolved in this version |
| 🟡 Important | `Priority` field missing for Work Assignment (M3) | Added `Priority` (`string?`) to `ServiceRequest` entity and all relevant DTOs — Sections 3.1, 4.4, 4.5, 5.1 | ✅ Resolved in this version |
| 🟡 Important | No `HasOutcome` filter for Outcome Compliance Monitoring (M5) | Added `HasOutcome` (`bool?`) to `ServiceRequestSearchRequestDto` — Section 5.1 | ✅ Resolved in this version |
| 🟡 Important | No scheduled date search filters (M2, M3) | Added `ScheduledAfterUtc`/`ScheduledBeforeUtc` to `ServiceRequestSearchRequestDto`; added `/scheduledDateUtc/?` to indexing policy — Sections 4.4, 5.1 | ✅ Resolved in this version |
| 🟡 Important | Analytics response DTO scope too narrow (M7) | Expanded `ServiceRequestAnalyticsResponseDto` with failure modes, repair times, parts trends; added time range query params — Sections 8.2, 11, 12.1 | ✅ Resolved in this version |
| 🟢 Phase 2 | No "request additional info" flow for triage (M2) | MVP: managers contact customers externally via `CustomerSnapshotEmbedded` contact details. Phase 2: `POST .../follow-ups` endpoint + `INotificationService.SendFollowUpRequestAsync` — Section 8.2 | 🔵 Deferred |

### 18.2 Expanded Status Values and Transition Validation

The `ServiceRequest.Status` field has been expanded from 4 to 8 values to align with the Service Manager Desktop Service Board columns:

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
| `Completed` | `Closed`, `InRepair` (reopen for additional work) |
| `Closed` | *(terminal — no transitions)* |
| `Cancelled` | *(terminal — no transitions)* |

`UpdateStatusAsync` in `IServiceRequestService` validates the requested transition using this class and throws `ArgumentException` (mapped to HTTP 400) if the transition is not allowed.

### 18.3 Batch Outcome Endpoint

`PATCH api/dealerships/{id}/service-requests/batch-outcome` applies a shared repair outcome to up to 25 service requests in one call.

**Validation:**
- Requests with more than 25 IDs in `ServiceRequestIds` return HTTP 400 immediately.
- Each SR is validated to exist within the tenant and to have a status of `"Completed"` (the expected state for outcome entry).
- SRs that fail validation (not found, wrong status, wrong tenant) are collected into the `Failed` list with an explanatory reason rather than aborting the entire batch.

**Operation:** For each valid SR, the service applies the provided `ServiceEvent` fields (`FailureMode`, `RepairAction`, `PartsUsed`, `LaborHours`) and returns a result summary with separate `Succeeded` and `Failed` lists.

**Request — `ServiceRequestBatchOutcomeRequestDto`:**

| Field | Type | Description |
|---|---|---|
| `ServiceRequestIds` | `List<string>` | Required. IDs of SRs to update. Max 25. |
| `FailureMode` | `string?` | Failure mode to apply to all |
| `RepairAction` | `string?` | Repair action to apply to all |
| `PartsUsed` | `List<string>?` | Parts list to apply to all |
| `LaborHours` | `decimal?` | Labor hours to apply to all |

**Response — `ServiceRequestBatchOutcomeResponseDto`:**

| Field | Type | Description |
|---|---|---|
| `Succeeded` | `List<string>` | IDs of successfully updated service requests |
| `Failed` | `List<BatchOutcomeFailureDto>` | Items that failed, with reason |

`BatchOutcomeFailureDto` — `ServiceRequestId` (`string`), `Reason` (`string`).

---

---

## ⚠️ CRITICAL GAPS — Assessment Action Items

The following issues are identified in `RVS_SaaS_Architecture_Assessment.md` and `RVS_Cloud_Arch_Assessment.md` and require resolution before commercialization. Organized by urgency:

### 🔴 CRITICAL — P0 (Non-negotiable before paying customers)

1. **No Infrastructure-as-Code (Bicep/Terraform)**
  - Impact: Manual environment setup, infra drift, disaster recovery risk
  - Action: Create Bicep templates for App Service, Cosmos DB (9 containers), Blob Storage, Key Vault, Application Insights, Static Web Apps
  - See: Assessment "Operational Excellence" section, Cloud Arch Assessment

2. **No CI/CD Pipeline (GitHub Actions)**
  - Impact: Manual deployment, cannot safely release changes, tenant onboarding cannot auto-provision
  - Action: Build GitHub Actions workflow with `build → test → deploy-staging → (manual approval) → deploy-prod`
  - See: Assessment "Operational Excellence" section

3. **Application Insights Missing (Zero Telemetry)**
  - Impact: Cannot measure SLAs, cannot detect outages until customer complains, cannot troubleshoot issues
  - Action: Wire Application Insights with tenant-tagged custom dimensions (TenantId, LocationId), Cosmos RU tracking, Azure OpenAI latency, error rates by tenant
  - See: Assessment "Reliability" section

4. **SFTP Private Keys Stored in Cosmos DB**
  - Impact: Critical security violation, fails SOC 2 audit, private key exposed if DB compromised
  - Action: Move SFTP credentials to Azure Key Vault. Store only `privateKeySecretUri` in `TenantConfig`.
  - See: Assessment "Security" section

5. **Billing/Metering Infrastructure Missing**
  - Impact: Cannot charge customers, violates SaaS WAF principle "Understand your cost/revenue relationship"
  - Action: Design lightweight metering layer: `PlanTier` field in `TenantConfig`, monthly SR count tracking (change feed or scheduled job), Stripe Billing integration, overage enforcement in `TenantAccessGateMiddleware`
  - See: Assessment "Cost Optimization" section, Cloud Arch Assessment

### 🟡 HIGH — P1 (Must have before MVP launch to production)

1. **No Azure Front Door + WAF**
  - Impact: Unauthenticated intake endpoints unprotected from injection, DDoS, bot attacks
  - Action: Place Azure Front Door Standard in front of API, enable OWASP Core Rule Set
  - Cost: ~$35/month at MVP scale
  - See: Assessment "Security" section

2. **No Azure Key Vault for Secrets**
  - Impact: Auth0 client secret, Azure OpenAI API key, Stripe API key all in `appsettings.json` or environment variables (expo sure risk, no rotation)
  - Action: Add `AddAzureKeyVault` to Program.cs, grant App Service managed identity `Key Vault Secrets User` RBAC
  - See: Assessment "Security" section

3. **No SAS Pre-Signed Direct Upload for Large Files**
  - Impact: 25 MB video uploads block API worker threads (3–8 seconds on mobile LTE), causes request starvation
  - Action: Add `GenerateUploadSasUriAsync` endpoint. Client uploads directly to Blob Storage, calls confirm endpoint afterward.
  - See: Assessment "Performance Efficiency" section

4. **Per-Tenant Rate Limiting Missing**
  - Impact: "Noisy neighbor" — one tenant's batch queries can consume all Cosmos RU, degrading service for other tenants
  - Action: Add per-`tenantId` sliding-window rate limiter (e.g., 300 req/min per tenant) on authenticated endpoints
  - See: Assessment "Operational Excellence", Cloud Arch Assessment

5. **No Health Check Endpoints**
  - Impact: App Service cannot detect unhealthy API (Cosmos connection broken → 503 responses but health = OK)
  - Action: Add `/health`, `/health/live`, `/health/ready` with Cosmos connectivity check
  - See: Assessment "Reliability" section

### 🟠 MEDIUM — P1 (Phase 1 Sprint N)

1. **Autoscale Missing on Variable-Load Containers**
  - Current: `dealerships`, `tenantConfigs`, `lookupSets` on Manual 400 RU
  - Impact: Risk throttling (429) during intake bursts, higher cost ($225/month floor vs. $52/month for autoscale)
  - Action: Change to Autoscale 400–1000 RU for all three
  - See: Assessment "Cost Optimization" section

2. **No CDN for Blazor WASM**
  - Impact: 5–15 MB runtime + app download from single App Service instance, poor UX on mobile
  - Action: Deploy Blazor WASM to Azure Static Web Apps (Free tier) or front with Azure Front Door CDN
  - See: Assessment "Performance Efficiency" section

3. **No Blob Storage Lifecycle Management**
  - Impact: Storage costs grow linearly with service request volume (photos/videos retained indefinitely)
  - Action: Define per-tier retention policies in `TenantConfig`, implement Blob lifecycle management (Hot→Cool→Archive)
  - See: Assessment "Cost Optimization" section

### 📋 Reference Documents

- **RVS_SaaS_Architecture_Assessment.md** — Complete security, reliability, performance, cost, operational analysis
- **RVS_Cloud_Arch_Assessment.md** — Document health issues, architecture gaps, incomplete designs, scope conflicts
- **RVS_Auth0_Identity_Version2.md** — Companion doc: Auth0 setup, RBAC roles, JWT claims, permissions matrix
- `.github/copilot-instructions.md` — ASP.NET Core, C# 14 coding patterns


