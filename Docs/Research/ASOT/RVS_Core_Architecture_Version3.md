

# RV Service Flow (RVS) — Core Backend Architecture

**As-of-Thread (ASOT) — March 10, 2026**

This document captures the domain model, multi-location tenancy, data layer, orchestration flows, service layer, middleware pipeline, API surface, and storage design for RVS. For Auth0 identity, RBAC roles/permissions, ClaimsService, and authorization policies, see the companion document **RVS_Auth0_Identity.md**.

---

## 1. Solution Structure

```
RVS.slnx
├── RVS.API/                           # ASP.NET Core API (.NET 10, C# 14)
│   ├── Controllers/
│   │   ├── IntakeController.cs        # [AllowAnonymous] — customer-facing intake
│   │   ├── CustomerStatusController.cs # [AllowAnonymous] — magic-link status page
│   │   ├── ServiceRequestsController.cs # [Authorize] — dealer dashboard CRUD
│   │   ├── AttachmentsController.cs    # [Authorize] — photo/video SAS URLs
│   │   ├── DealershipsController.cs    # [Authorize] — dealership/corporation management
│   │   ├── LocationsController.cs     # [Authorize] — physical location management
│   │   ├── TenantsController.cs        # [Authorize] — tenant config, access gate
│   │   ├── LookupsController.cs        # [Authorize] — issue categories, component types
│   │   └── AnalyticsController.cs      # [Authorize] — basic request analytics
│   ├── Services/
│   │   ├── ServiceRequestService.cs
│   │   ├── CustomerProfileService.cs   # Shadow profile resolve-or-create + magic link
│   │   ├── GlobalCustomerAcctService.cs # Cross-dealer identity federation
│   │   ├── AssetLedgerService.cs       # Append-only asset event log
│   │   ├── AttachmentService.cs
│   │   ├── DealershipService.cs
│   │   ├── LocationService.cs         # Physical location CRUD + slug resolution
│   │   ├── TenantService.cs
│   │   ├── LookupService.cs
│   │   ├── CategorizationService.cs    # Rule-based MVP; AI-ready interface
│   │   ├── NotificationService.cs
│   │   └── ClaimsService.cs           # Auth0 JWT claims (see RVS_Auth0_Identity.md)
│   ├── Mappers/
│   │   ├── ServiceRequestMapper.cs
│   │   ├── CustomerProfileMapper.cs
│   │   ├── GlobalCustomerAcctMapper.cs
│   │   ├── AssetLedgerMapper.cs
│   │   ├── DealershipMapper.cs
│   │   ├── LocationMapper.cs
│   │   └── LookupMapper.cs
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs  # IMiddleware, singleton
│   │   └── TenantAccessGateMiddleware.cs   # RequestDelegate, scoped injection
│   ├── Integrations/                   # Future: DMS webhooks, AI clients
│   ├── Properties/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── appsettings.Production.json
│
├── RVS.Domain/                        # Zero infra dependencies
│   ├── Entities/
│   │   ├── EntityBase.cs
│   │   ├── ServiceRequest.cs
│   │   ├── CustomerSnapshotEmbedded.cs        # Embedded in ServiceRequest
│   │   ├── AssetInfoEmbedded.cs             # Embedded in ServiceRequest
│   │   ├── ServiceRequestAttachmentEmbedded.cs # Embedded in ServiceRequest
│   │   ├── ServiceEventEmbedded.cs            # Embedded in ServiceRequest (10A fields)
│   │   ├── CustomerProfile.cs         # Tenant-scoped shadow record
│   │   ├── AssetsOwnedEmbedded.cs      # Embedded in CustomerProfile
│   │   ├── AssetsOwnedStatus.cs
│   │   ├── GlobalCustomerAcct.cs      # Cross-dealer global identity
│   │   ├── LinkedProfileReferenceEmbedded.cs  # Embedded in GlobalCustomerAcct
│   │   ├── AssetLedgerEntry.cs        # Append-only asset service event
│   │   ├── Dealership.cs             # Corporation / dealer group
│   │   ├── Location.cs               # Physical service location
│   │   ├── AddressEmbedded.cs                # Embedded in Location
│   │   ├── IntakeFormConfigEmbedded.cs       # Embedded in Location
│   │   ├── TenantConfig.cs
│   │   ├── TenantAccessGateEmbedded.cs        # Embedded in TenantConfig
│   │   └── LookupSet.cs
│   ├── DTOs/
│   │   ├── ServiceRequestCreateRequestDto.cs
│   │   ├── ServiceRequestUpdateRequestDto.cs
│   │   ├── ServiceRequestSearchRequestDto.cs
│   │   ├── ServiceRequestDetailResponseDto.cs
│   │   ├── ServiceRequestSummaryResponseDto.cs
│   │   ├── AttachmentUploadRequestDto.cs
│   │   ├── AttachmentResponseDto.cs
│   │   ├── AssetInfoDto.cs
│   │   ├── CustomerInfoDto.cs
│   │   ├── CustomerStatusResponseDto.cs
│   │   ├── CustomerServiceRequestSummaryDto.cs
│   │   ├── IntakeConfigResponseDto.cs
│   │   ├── IntakePrefillDto.cs
│   │   ├── AssetPrefillDto.cs
│   │   ├── DealershipDetailResponseDto.cs
│   │   ├── DealershipSummaryResponseDto.cs
│   │   ├── DealershipUpdateRequestDto.cs
│   │   ├── LocationDetailResponseDto.cs
│   │   ├── LocationSummaryResponseDto.cs
│   │   ├── LocationCreateRequestDto.cs
│   │   ├── LocationUpdateRequestDto.cs
│   │   ├── TenantConfigCreateRequestDto.cs
│   │   ├── TenantConfigUpdateRequestDto.cs
│   │   ├── TenantConfigResponseDto.cs
│   │   ├── TenantAccessGateDto.cs
│   │   ├── LookupSetDto.cs
│   │   ├── LookupItemDto.cs
│   │   └── ServiceRequestAnalyticsResponseDto.cs
│   ├── Interfaces/
│   │   ├── IServiceRequestRepository.cs
│   │   ├── IServiceRequestService.cs
│   │   ├── ICustomerProfileRepository.cs
│   │   ├── ICustomerProfileService.cs
│   │   ├── IGlobalCustomerAcctRepository.cs
│   │   ├── IGlobalCustomerAcctService.cs
│   │   ├── IAssetLedgerRepository.cs
│   │   ├── IAssetLedgerService.cs
│   │   ├── IAttachmentService.cs
│   │   ├── IBlobStorageRepository.cs
│   │   ├── IDealershipRepository.cs
│   │   ├── IDealershipService.cs
│   │   ├── ILocationRepository.cs
│   │   ├── ILocationService.cs
│   │   ├── ITenantRepository.cs
│   │   ├── ITenantService.cs
│   │   ├── ILookupRepository.cs
│   │   ├── ILookupService.cs
│   │   ├── ICategorizationService.cs  # Rule-based MVP; AI future
│   │   ├── INotificationService.cs
│   │   └── IUserContextAccessor.cs
│   ├── Validation/
│   └── Shared/
│       └── PagedResult.cs
│
├── RVS.Infra.AzCosmosRepository/      # Cosmos DB repository implementations
├── RVS.Infra.AzBlobRepository/        # Azure Blob Storage
├── RVS.Infra.AzTablesRepository/      # Azure Table Storage (analytics counters cache)
├── RVS.Infra.AzCredentials/           # DefaultAzureCredential shared config
├── RVS.Data.Cosmos.Seed/              # Seed data for dev/test
│
├── .github/
│   ├── copilot-instructions.md
│   └── instructions/
│       ├── csharp.instructions.md
│       ├── aspnet-rest-apis.instructions.md
│       ├── blazor.instructions.md
│       └── markdowninstructions.md
│
└── Docs/
    └── Research/
        ├── RVS_Core_Architecture.md       # This document
        └── RVS_Auth0_Identity.md          # Auth0, RBAC, ClaimsService
```

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

## 3. Domain Entities

All entities inherit `EntityBase` following the [MF `EntityBase` pattern](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.Domain/Entities/EntityBase.cs):

- `Type` — abstract, `init`-only discriminator
- `Id` — auto GUID, `init`-only
- `TenantId` — virtual, `init`-only
- `Name` — virtual
- `IsEnabled` — soft-enable/disable
- `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
- `MarkAsUpdated(userId)` stamps update fields
- `[JsonProperty("camelCase")]` on all properties

### 3.1 ServiceRequest (Aggregate Root)

The central entity. Partitioned by `/tenantId`. Each service request belongs to exactly one Location within the tenant.

| Field | Type | Description |
|---|---|---|
| `Type` | `string` | `"serviceRequest"` (discriminator) |
| `TenantId` | `string` | Corporation partition key (inherited from `EntityBase`) |
| `LocationId` | `string` | FK to the physical `Location` where this SR was submitted |
| `Status` | `string` | `"New"`, `"InProgress"`, `"Completed"`, `"Cancelled"` |
| `CustomerProfileId` | `string` | FK to tenant-scoped `CustomerProfile` |
| `Customer` | `CustomerSnapshotEmbedded` | Point-in-time denormalized snapshot (no joins on dashboard reads) |
| `Asset` | `AssetInfoEmbedded` | Identifier, manufacturer, model, year |
| `IssueDescription` | `string` | Free-text customer description |
| `IssueCategory` | `string?` | Auto-categorized (rule-based MVP, AI future) |
| `TechnicianSummary` | `string?` | Generated summary for tech |
| `Attachments` | `List<ServiceRequestAttachmentEmbedded>` | Embedded photo/video references |
| `ServiceEvent` | `ServiceEventEmbedded` | Section 10A structured repair data |
| `ScheduledDateUtc` | `DateTime?` | Future: scheduling |
| `AssignedBayId` | `string?` | Future: bay assignment |
| `AssignedTechnicianId` | `string?` | Future: tech assignment |
| `RequiredSkills` | `List<string>` | Future: skill matching |

### 3.2 Embedded Sub-Entities (within ServiceRequest)

**CustomerSnapshotEmbedded** — Point-in-time copy of customer info. Fields: `FirstName`, `LastName`, `Email`, `Phone`, `IsReturningCustomer`, `PriorRequestCount`. Denormalized so the dealer dashboard never joins to `customerProfiles`.

**AssetInfoEmbedded** — Fields: `AssetId`, `Manufacturer`, `Model`, `Year`.

**ServiceRequestAttachmentEmbedded** — Fields: `AttachmentId` (auto GUID), `BlobUri`, `FileName`, `ContentType`, `SizeBytes`, `CreatedAtUtc`.

**ServiceEventEmbedded** — Section 10A structured data. Fields: `ComponentType`, `FailureMode`, `RepairAction`, `PartsUsed` (list), `LaborHours`, `ServiceDateUtc`. Populated progressively across phases; MVP captures `IssueCategory` and `ComponentType` only.

### 3.3 CustomerProfile (Tenant-Scoped Shadow Record)

Shadow profile — created automatically on first intake submission at a corporation. One per customer per tenant (not per location). The customer never sees a "Sign Up" screen. Partitioned by `/tenantId`.

| Field | Type | Description |
|---|---|---|
| `Type` | `string` | `"customerProfile"` |
| `TenantId` | `string` | Corporation partition key |
| `Email` | `string` | Customer email (normalized on input) |
| `FirstName` / `LastName` / `Phone` | `string` | Contact info, updated on each intake |
| `GlobalCustomerAcctId` | `string` | FK to global `GlobalCustomerAcct` |
| `AssetsOwned` | `List<AssetsOwnedEmbedded>` | Full lifecycle of each customer ↔ asset relationship |
| `TotalRequestCount` | `int` | Running count |

Convenience helpers (not persisted): `GetActiveAssetIds()` returns asset identifiers with Active status. `GetOwnership(assetId)` returns the active ownership record for a specific asset.

### 3.4 AssetsOwnedEmbedded (Embedded in CustomerProfile)

Records a customer's relationship to a specific asset over time. Handles ownership transfers: when a different customer submits for the same asset, the previous owner's record is set to Inactive.

| Field | Type | Description |
|---|---|---|
| `AssetId` | `string` | Compound asset key — format: `{AssetType}:{Identifier}` (e.g. `RV:1ABC234567`) |
| `Manufacturer` / `Model` / `Year` | `string?` / `int?` | Asset details |
| `Status` | `string` | `"Active"` or `"Inactive"` (string constants, not enum) |
| `FirstSeenAtUtc` / `LastSeenAtUtc` | `DateTime` | Lifecycle timestamps |
| `RequestCount` | `int` | Number of SRs for this asset |
| `DeactivatedAtUtc` | `DateTime?` | When ownership was transferred |
| `DeactivationReason` | `string?` | e.g. "Asset claimed by a different customer" |

### 3.5 GlobalCustomerAcct (Cross-Dealer Global Record)

Global customer identity — one record per real human (by email). Cross-tenant. Links all corporation-scoped profiles. Partitioned by `/email` for O(1) intake resolution.

| Field | Type | Description |
|---|---|---|
| `Type` | `string` | `"globalCustomerAcct"` |
| `Email` | `string` | Partition key (normalized on input) |
| `FirstName` / `LastName` / `Phone` | `string` | Latest contact info |
| `LinkedProfiles` | `List<LinkedProfileReferenceEmbedded>` | Pointers to all tenant-scoped profiles |
| `AllKnownAssetIds` | `List<string>` | Every AssetId ever associated across all dealers (format: `{AssetType}:{Identifier}`). Capped at 200 items (most-recent first); older history is recoverable from `AssetLedgerEntry` by querying on `GlobalCustomerAcctId`. |
| `MagicLinkToken` | `string?` | Global magic-link token for status page. Format: `base64url(SHA256(email)[0..8]):random_bytes` — the email-hash prefix encodes the partition key so token lookup is a single-partition point read (no cross-partition query). |
| `MagicLinkExpiresAtUtc` | `DateTime?` | Token expiry |
| `Auth0UserId` | `string?` | Phase 2+: linked Auth0 account (null during MVP) |

**LinkedProfileReferenceEmbedded** — Lightweight pointer from global identity to a tenant-scoped profile. Fields: `TenantId`, `ProfileId`, `DealershipName`, `LocationId`, `LocationName`, `FirstSeenAtUtc`, `RequestCount`.

### 3.6 AssetLedgerEntry (Append-Only Asset Event Log — Data Moat)

Append-only service event record keyed by asset. One entry per service request, written at intake time. This is the **data moat**: proprietary, accumulating, non-replicable data that powers Section 10A service intelligence. Partitioned by `/assetId`.

**AssetId format:** `{AssetType}:{Identifier}` — a normalized compound key that is globally unique, works across industries, and preserves VIN/HIN/serial semantics.

| Asset Type | Example AssetId |
|---|---|
| RV | `RV:1ABC234567` |
| Boat | `Boat:HIN123456789` |
| Excavator | `Excavator:CAT320GX987654` |
| Tractor | `Tractor:JD8R34012345` |

| Field | Type | Description |
|---|---|---|
| `AssetId` | `string` | Partition key — format: `{AssetType}:{Identifier}` (e.g. `RV:1ABC234567`) |
| `AssetType` | `string` | Asset category (e.g. `"RV"`, `"Boat"`, `"Excavator"`) |
| `TenantId` | `string` | Which corporation |
| `DealershipName` | `string` | Corporation display name |
| `LocationId` / `LocationName` | `string` | Which physical location |
| `ServiceRequestId` | `string` | FK back to the SR |
| `GlobalCustomerAcctId` | `string` | Which customer (global) |
| `Manufacturer` / `Model` / `Year` | `string?` / `int?` | Asset details |
| `IssueCategory` / `IssueDescription` | `string?` | What was reported |
| `FailureMode` / `RepairAction` / `PartsUsed` / `LaborHours` | various | Section 10A fields, populated progressively |
| `Status` | `string` | SR status at time of write |
| `SubmittedAtUtc` / `ServiceDateUtc` | `DateTime` / `DateTime?` | When submitted / when serviced |

**TTL:** No TTL — entries are retained indefinitely by design. Partition size monitored per-asset. For assets with extreme service histories, the 20 GB logical partition limit is not a concern (each entry is ~1 KB, so you'd need ~20 million entries per asset).

### 3.7 Dealership (Corporation / Dealer Group)

Represents the corporation or dealer group — the Auth0 Organization boundary and Cosmos partition key. Follows the MF [Practice](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.Domain/Entities/Practice.cs) pattern.

| Field | Type | Description |
|---|---|---|
| `Type` | `string` | `"dealership"` |
| `Slug` | `string` | URL-safe identifier (e.g. `"blue-compass-rv"`) |
| `CorporateName` | `string` | Display name (e.g. `"Blue Compass RV"`) |
| `LogoUrl` | `string?` | Corporate logo |
| `IsMultiLocation` | `bool` | `true` for Blue Compass, `false` for single-location independents |

### 3.8 Location (Physical Service Location)

A physical service site within a dealership group. Single-location dealers have exactly one. Multi-location groups have many. Partitioned by `/tenantId` — all locations for a corporation are co-located.

| Field | Type | Description |
|---|---|---|
| `Type` | `string` | `"location"` |
| `TenantId` | `string` | Parent corporation |
| `Slug` | `string` | Globally unique intake URL slug (e.g. `"blue-compass-salt-lake"`) |
| `DisplayName` | `string` | e.g. `"Blue Compass RV - Salt Lake City"` |
| `Address` | `AddressEmbedded` | Street, City, State, Zip |
| `ServiceEmail` / `Phone` | `string?` | Location contact info |
| `LogoUrl` | `string?` | Location-specific logo (overrides corporate if set) |
| `IntakeConfig` | `IntakeFormConfigEmbedded` | Accepted file types, max file size |
| `RegionTag` | `string?` | e.g. `"west"`, `"southeast"` — for regional manager scoping |

**AddressEmbedded** — Fields: `Street`, `City`, `State`, `Zip`.

**IntakeFormConfigEmbedded** — Fields: `AcceptedFileTypes` (default: `.jpg`, `.png`, `.mp4`), `MaxFileSizeMb` (default: 25).

### 3.9 TenantConfig, LookupSet

Follow MF patterns for [TenantConfig](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.Domain/Entities/TenantConfig.cs) and [LookupSet](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.Domain/Entities/LookupSet.cs). `TenantConfig` embeds `TenantAccessGateEmbedded` for onboarding flow control. No changes from MF baseline.

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

**`serviceRequests`** — Included paths: `/tenantId/?`, `/locationId/?`, `/status/?`, `/customerProfileId/?`, `/createdAtUtc/?`, `/issueCategory/?`. Composite indexes: `[tenantId ASC, locationId ASC, createdAtUtc DESC]`, `[tenantId ASC, locationId ASC, status ASC, createdAtUtc DESC]`.

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
  "attachments": [
    {
      "attachmentId": "att_001",
      "blobUri": "https://rvsstorage.blob.core.windows.net/...",
      "fileName": "slide_issue.jpg",
      "contentType": "image/jpeg",
      "sizeBytes": 245000,
      "createdAtUtc": "2026-03-08T10:00:00Z"
    }
  ],
  "serviceEvent": {
    "componentType": "Hydraulic System",
    "failureMode": null,
    "repairAction": null,
    "partsUsed": [],
    "laborHours": null,
    "serviceDateUtc": null
  },
  "scheduledDateUtc": null,
  "assignedBayId": null,
  "assignedTechnicianId": null,
  "requiredSkills": [],
  "createdAtUtc": "2026-03-08T10:00:00Z",
  "createdByUserId": null,
  "updatedAtUtc": "2026-03-08T10:00:00Z",
  "updatedByUserId": null
}
```

**Location:**

```json
{
  "id": "loc_blue_compass_slc",
  "type": "location",
  "tenantId": "org_blue_compass_rv",
  "slug": "blue-compass-salt-lake",
  "displayName": "Blue Compass RV - Salt Lake City",
  "address": {
    "street": "1234 RV Parkway",
    "city": "Salt Lake City",
    "state": "UT",
    "zip": "84101"
  },
  "serviceEmail": "service-slc@bluecompassrv.com",
  "phone": "801-555-0100",
  "logoUrl": "https://rvsstorage.blob.core.windows.net/logos/bc-slc.png",
  "intakeConfig": {
    "acceptedFileTypes": [".jpg", ".png", ".mp4"],
    "maxFileSizeMb": 25
  },
  "regionTag": "west",
  "isEnabled": true,
  "createdAtUtc": "2026-01-15T08:00:00Z"
}
```

**Slug Lookup:**

```json
{
  "id": "blue-compass-salt-lake",
  "slug": "blue-compass-salt-lake",
  "tenantId": "org_blue_compass_rv",
  "locationId": "loc_blue_compass_slc",
  "isEnabled": true,
  "updatedAtUtc": "2026-01-15T08:00:00Z"
}
```

---

## 5. Domain Interfaces

### 5.1 Service Request

**`IServiceRequestRepository`** — `GetByIdAsync(tenantId, serviceRequestId)`, `SearchAsync(tenantId, searchDto)`, `GetByProfileIdAsync(tenantId, profileId, limit)`, `CreateAsync`, `UpdateAsync`, `DeleteAsync(tenantId, serviceRequestId)`, `GetCountByStatusAsync(tenantId, status?, locationId?)`.

**`IServiceRequestService`** — `CreateServiceRequestAsync(tenantId, locationId, createDto)`, `GetServiceRequestAsync(tenantId, serviceRequestId)`, `SearchServiceRequestsAsync(tenantId, searchDto)`, `GetByProfileAsync(tenantId, profileId, limit)`, `UpdateServiceRequestAsync(tenantId, serviceRequestId, updateDto)`, `UpdateStatusAsync(tenantId, serviceRequestId, newStatus)`, `DeleteServiceRequestAsync(tenantId, serviceRequestId)`.

### 5.2 Customer Profile

**`ICustomerProfileRepository`** — `GetByIdAsync(tenantId, profileId)`, `GetByIdentityIdAsync(tenantId, globalCustomerAcctId)`, `GetByActiveAssetIdAsync(tenantId, assetId)`, `CreateAsync`, `UpdateAsync`.

**`ICustomerProfileService`** — `ResolveOrCreateProfileAsync(tenantId, identity, assetId?, assetInfo?)`. Resolves existing or creates shadow record. Handles asset ownership transfer detection within the tenant.

### 5.3 Global Customer Acct

**`IGlobalCustomerAcctRepository`** — `GetByEmailAsync(email)`, `GetByMagicLinkTokenAsync(emailPrefix, token)`, `CreateAsync`, `UpdateAsync`. Note: `GetByMagicLinkTokenAsync` accepts the email-hash prefix (extracted from the token) to derive the partition key, enabling a single-partition query instead of a cross-partition scan.

**`IGlobalCustomerAcctService`** — `ResolveOrCreateIdentityAsync(email, firstName, lastName, phone?)`, `ValidateMagicLinkAsync(token)` (parses the email-hash prefix from the token to derive the partition key, then single-partition lookup + expiry check), `RotateMagicLinkTokenAsync(globalCustomerAcctId, email)` (generates a new token with format `base64url(SHA256(email)[0..8]):random_bytes`).

### 5.4 Asset Ledger

**`IAssetLedgerRepository`** — `AppendAsync(entry)`, `GetByAssetIdAsync(assetId, limit)`, `UpdateEntryAsync(entry)`.

**`IAssetLedgerService`** — `RecordServiceEventAsync(request, dealershipName, locationId, locationName, globalCustomerAcctId)`, `GetAssetHistoryAsync(assetId, limit)`. Write-only in MVP; read in Phase 5-6.

### 5.5 Location

**`ILocationRepository`** — `GetByIdAsync(tenantId, locationId)`, `GetByTenantIdAsync(tenantId)`, `CreateAsync`, `UpdateAsync`.

**`ILocationService`** — `GetByIdAsync(tenantId, locationId)`, `GetByTenantIdAsync(tenantId)`, `CreateLocationAsync(tenantId, createDto)`, `UpdateLocationAsync(tenantId, locationId, updateDto)`.

**`ISlugLookupRepository`** — `GetBySlugAsync(slug)`, `UpsertAsync(slug, tenantId, locationId)`, `DeleteAsync(slug)`.

### 5.6 Attachments & Blob Storage

**`IAttachmentService`** — `UploadAttachmentAsync(tenantId, locationId, serviceRequestId, uploadDto, fileStream)`, `GetAttachmentStreamAsync(tenantId, serviceRequestId, attachmentId)`, `DeleteAttachmentAsync(tenantId, serviceRequestId, attachmentId)`, `GenerateSasUriAsync(tenantId, serviceRequestId, attachmentId, expiry)`.

**`IBlobStorageRepository`** — `UploadAsync(containerPath, fileName, content, contentType)`, `DownloadAsync(blobUri)`, `DeleteAsync(blobUri)`, `GenerateSasUriAsync(blobUri, expiry)`.

### 5.7 Dealership, Tenant, Lookup

Follow standard MF CRUD patterns. `IDealershipRepository` / `IDealershipService`, `ITenantRepository` / `ITenantService`, `ILookupRepository` / `ILookupService`.

---

## 6. Core Orchestration Flow: Intake Submission

The most important flow in the system — the complete sequence when a customer submits a service request:

```
Customer submits intake form at location slug "blue-compass-salt-lake"
                    │
                    ▼
┌──────────────────────────────────────────────────┐
│ STEP 0: Resolve Location by Slug                 │
│                                                  │
│ Container: slugLookup                            │
│ Query: point read by slug (partition key = slug) │
│ Returns: tenantId + locationId                   │
│ Cost: ~1 RU cold / ~0 RU gateway-cached          │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
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
│ Auto-categorize issue (rule-based MVP)           │
│ Generate technician summary                      │
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
```

---

## 7. Service Layer

All services are `sealed`, inject repository interfaces + `IUserContextAccessor`, guard clauses first, return domain entities. Follows [MF patterns](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Middleware/ExceptionHandlingMiddleware.cs).

### 7.1 ServiceRequestService (Primary Orchestrator)

Implements the 7-step intake flow from Section 6. Injects: `IServiceRequestRepository`, `IGlobalCustomerAcctService`, `ICustomerProfileService`, `IAssetLedgerService`, `IDealershipService`, `ILocationService`, `ICategorizationService`, `INotificationService`, `IUserContextAccessor`.

**`CreateServiceRequestAsync(tenantId, locationId, request)`** executes Steps 1–6 sequentially:

1. Calls `IGlobalCustomerAcctService.ResolveOrCreateIdentityAsync` with customer email/name/phone from the request DTO.
2. Calls `ICustomerProfileService.ResolveOrCreateProfileAsync` with the resolved identity, asset identifier, and asset info. This handles shadow profile creation and asset ownership transfer.
3. Builds the `ServiceRequest` entity. Stamps `tenantId` and `locationId`. Embeds a `CustomerSnapshotEmbedded` denormalized from the profile (firstName, lastName, email, phone, isReturningCustomer, priorRequestCount). Calls `ICategorizationService.CategorizeAsync` for auto-categorization and technician summary.
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

---

## 8. Controllers

Following the [RVS copilot-instructions.md](https://github.com/markarnoldutah/RVS/blob/f1e1bc1a099c242cace68f515f6bdb8db9ea2418/.github/copilot-instructions.md) and [MF `ClaimsService` pattern](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Services/ClaimsService.cs). Authorization policies referenced below are defined in **RVS_Auth0_Identity.md Section 10**.

### 8.1 Customer-Facing (Unauthenticated)

**IntakeController** — Route: `api/intake/{locationSlug}`. `[AllowAnonymous]`. Resolves location slug → `tenantId` + `locationId`. Endpoints: `GET` (intake config + optional prefill via `?token=`), `POST service-requests` (submit), `POST service-requests/{id}/attachments` (upload).

**CustomerStatusController** — Route: `api/status`. `[AllowAnonymous]`. Endpoint: `GET {token}` parses the email-hash prefix from the token to derive the partition key, validates the magic link via single-partition point read, returns requests across all dealerships/locations.

### 8.2 Dealer-Facing (Authenticated)

**ServiceRequestsController** — Route: `api/dealerships/{dealershipId}/service-requests`. Actions: `GET {id}` (CanReadServiceRequests), `POST search` (CanSearchServiceRequests), `PUT {id}` (CanUpdateServiceRequests), `DELETE {id}` (CanDeleteServiceRequests). Location filtering applied server-side via `ClaimsService.HasAccessToLocation()`.

**AttachmentsController** — Route: `api/dealerships/{dealershipId}/service-requests/{serviceRequestId}/attachments`. Actions: `GET {attachmentId}` (CanReadAttachments), `DELETE {attachmentId}` (CanDeleteAttachments).

**DealershipsController** — Route: `api/dealerships`. Actions: `GET` (CanReadDealerships), `GET {id}` (CanReadDealerships), `PUT {id}` (CanUpdateDealerships).

**LocationsController** — Route: `api/locations`. Actions: `GET` (CanReadLocations, filtered by user's `locationIds`), `GET {id}` (CanReadLocations), `POST` (CanCreateLocations), `PUT {id}` (CanUpdateLocations), `GET {id}/qr-code` (CanReadLocations).

**TenantsController** — Route: `api/tenants`. Actions: `POST config`, `GET config`, `PUT config`, `GET access-gate` (all CanManageTenantConfig).

**LookupsController** — Route: `api/lookups`. Action: `GET {lookupSetId}` (CanReadLookups).

**AnalyticsController** — Route: `api/dealerships/{dealershipId}/analytics`. Action: `GET service-requests/summary` (CanReadAnalytics).

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

---

## 11. Complete API Route Summary

| Method | Route | Auth | Policy | Purpose |
|---|---|---|---|---|
| `GET` | `api/intake/{locationSlug}?token={t}` | Anonymous | — | Intake config + optional prefill via magic link |
| `POST` | `api/intake/{locationSlug}/service-requests` | Anonymous | — | Submit request → full intake orchestration |
| `POST` | `api/intake/{locationSlug}/service-requests/{id}/attachments` | Anonymous | — | Upload photo/video |
| `GET` | `api/status/{token}` | Anonymous | — | Customer status page via magic link (cross-dealer) |
| `GET` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanReadServiceRequests | Request detail |
| `POST` | `api/dealerships/{id}/service-requests/search` | Bearer | CanSearchServiceRequests | Search/filter requests |
| `PUT` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanUpdateServiceRequests | Update request |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | CanDeleteServiceRequests | Delete request |
| `GET` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | CanReadAttachments | Get attachment SAS URL |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | CanDeleteAttachments | Delete attachment |
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