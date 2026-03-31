Now I have all the context needed. Let me compile the complete, updated architecture document.

---

# RV Service Flow (RVS) — Complete Backend Architecture

**As-of-Thread (ASOT) — March 9, 2026**

This document captures every architectural decision made during the design conversation, incorporating the shadow profile strategy, VIN ownership lifecycle, three-container identity model, data moat strategy, and all MF-inherited patterns.

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
│   │   ├── DealershipsController.cs    # [Authorize] — dealership management
│   │   ├── TenantsController.cs        # [Authorize] — tenant config, access gate
│   │   ├── LookupsController.cs        # [Authorize] — issue categories, component types
│   │   └── AnalyticsController.cs      # [Authorize] — basic request analytics
│   ├── Services/
│   │   ├── ServiceRequestService.cs
│   │   ├── CustomerProfileService.cs   # Shadow profile resolve-or-create + magic link
│   │   ├── CustomerIdentityService.cs  # Cross-dealer identity federation
│   │   ├── VehicleLedgerService.cs     # Append-only VIN event log
│   │   ├── AttachmentService.cs
│   │   ├── DealershipService.cs
│   │   ├── TenantService.cs
│   │   ├── LookupService.cs
│   │   ├── CategorizationService.cs    # Rule-based MVP; AI-ready interface
│   │   ├── NotificationService.cs
│   │   └── ClaimsService.cs
│   ├── Mappers/
│   │   ├── ServiceRequestMapper.cs
│   │   ├── CustomerProfileMapper.cs
│   │   ├── CustomerIdentityMapper.cs
│   │   ├── VehicleLedgerMapper.cs
│   │   ├── DealershipMapper.cs
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
│   │   ├── CustomerSnapshot.cs        # Embedded in ServiceRequest
│   │   ├── VehicleInfo.cs             # Embedded in ServiceRequest
│   │   ├── ServiceRequestAttachment.cs # Embedded in ServiceRequest
│   │   ├── ServiceEvent.cs            # Embedded in ServiceRequest (10A fields)
│   │   ├── CustomerProfile.cs         # Tenant-scoped shadow record
│   │   ├── VehicleInteraction.cs      # Embedded in CustomerProfile
│   │   ├── VehicleInteractionStatus.cs
│   │   ├── CustomerIdentity.cs        # Cross-dealer global identity
│   │   ├── LinkedProfileReference.cs  # Embedded in CustomerIdentity
│   │   ├── VehicleLedgerEntry.cs      # Append-only VIN service event
│   │   ├── Dealership.cs
│   │   ├── TenantConfig.cs
│   │   ├── TenantAccessGate.cs        # Embedded in TenantConfig
│   │   └── LookupSet.cs
│   ├── DTOs/
│   │   ├── ServiceRequestCreateRequestDto.cs
│   │   ├── ServiceRequestUpdateRequestDto.cs
│   │   ├── ServiceRequestSearchRequestDto.cs
│   │   ├── ServiceRequestDetailResponseDto.cs
│   │   ├── ServiceRequestSummaryResponseDto.cs
│   │   ├── AttachmentUploadRequestDto.cs
│   │   ├── AttachmentResponseDto.cs
│   │   ├── VehicleInfoDto.cs
│   │   ├── CustomerInfoDto.cs
│   │   ├── CustomerStatusResponseDto.cs
│   │   ├── CustomerServiceRequestSummaryDto.cs
│   │   ├── IntakeConfigResponseDto.cs
│   │   ├── IntakePrefillDto.cs
│   │   ├── VehiclePrefillDto.cs
│   │   ├── DealershipDetailResponseDto.cs
│   │   ├── DealershipSummaryResponseDto.cs
│   │   ├── DealershipUpdateRequestDto.cs
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
│   │   ├── ICustomerIdentityRepository.cs
│   │   ├── ICustomerIdentityService.cs
│   │   ├── IVehicleLedgerRepository.cs
│   │   ├── IVehicleLedgerService.cs
│   │   ├── IAttachmentService.cs
│   │   ├── IBlobStorageRepository.cs
│   │   ├── IDealershipRepository.cs
│   │   ├── IDealershipService.cs
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
    └── Architecture/
        └── RVS_Backend_Architecture_ASOT.md  # This document
```

---

## 2. Domain Entities

All entities inherit `EntityBase` following the [MF `EntityBase` pattern](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.Domain/Entities/EntityBase.cs):

- `Type` (abstract, `init`-only discriminator)
- `Id` (auto GUID, `init`-only)
- `TenantId` (virtual, `init`-only)
- `Name` (virtual)
- `IsEnabled` (soft-enable/disable)
- `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
- `MarkAsUpdated(userId)` stamps update fields
- `[JsonProperty("camelCase")]` on all properties

### 2.1 ServiceRequest (Aggregate Root)

```csharp name=RVS.Domain/Entities/ServiceRequest.cs
public class ServiceRequest : EntityBase
{
    public override string Type { get; init; } = "serviceRequest";

    [JsonProperty("status")]
    public string Status { get; set; } = "New";

    // ── Customer linkage ──

    /// <summary>
    /// FK to tenant-scoped CustomerProfile.
    /// </summary>
    [JsonProperty("customerProfileId")]
    public string CustomerProfileId { get; set; } = string.Empty;

    /// <summary>
    /// Point-in-time snapshot of customer info.
    /// Denormalized so dealer dashboard never joins to customer-profiles.
    /// </summary>
    [JsonProperty("customer")]
    public CustomerSnapshot Customer { get; set; } = new();

    // ── Vehicle ──

    [JsonProperty("vehicle")]
    public VehicleInfo Vehicle { get; set; } = new();

    // ── Issue ──

    [JsonProperty("issueDescription")]
    public string IssueDescription { get; set; } = string.Empty;

    [JsonProperty("issueCategory")]
    public string? IssueCategory { get; set; }

    [JsonProperty("technicianSummary")]
    public string? TechnicianSummary { get; set; }

    // ── Attachments ──

    [JsonProperty("attachments")]
    public List<ServiceRequestAttachment> Attachments { get; set; } = [];

    // ── Structured service event (Section 10A) ──

    [JsonProperty("serviceEvent")]
    public ServiceEvent ServiceEvent { get; set; } = new();

    // ── Future phase fields (nullable, present from day one) ──

    [JsonProperty("scheduledDateUtc")]
    public DateTime? ScheduledDateUtc { get; set; }

    [JsonProperty("assignedBayId")]
    public string? AssignedBayId { get; set; }

    [JsonProperty("assignedTechnicianId")]
    public string? AssignedTechnicianId { get; set; }

    [JsonProperty("requiredSkills")]
    public List<string> RequiredSkills { get; set; } = [];
}
```

### 2.2 Embedded Sub-Entities (within ServiceRequest)

```csharp name=RVS.Domain/Entities/CustomerSnapshot.cs
/// <summary>
/// Point-in-time snapshot of customer info embedded in ServiceRequest.
/// Denormalized so the dealer dashboard never needs to join to CustomerProfile.
/// </summary>
public class CustomerSnapshot
{
    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// True if this customer had prior service requests at this dealership.
    /// </summary>
    [JsonProperty("isReturningCustomer")]
    public bool IsReturningCustomer { get; set; }

    /// <summary>
    /// Prior request count at this dealership (0 for first-time).
    /// </summary>
    [JsonProperty("priorRequestCount")]
    public int PriorRequestCount { get; set; }
}
```

```csharp name=RVS.Domain/Entities/VehicleInfo.cs
public class VehicleInfo
{
    [JsonProperty("vin")]
    public string Vin { get; set; } = string.Empty;

    [JsonProperty("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }
}
```

```csharp name=RVS.Domain/Entities/ServiceRequestAttachment.cs
public class ServiceRequestAttachment
{
    [JsonProperty("attachmentId")]
    public string AttachmentId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("blobUri")]
    public string BlobUri { get; set; } = string.Empty;

    [JsonProperty("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonProperty("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonProperty("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
```

```csharp name=RVS.Domain/Entities/ServiceEvent.cs
/// <summary>
/// Structured service event data per Section 10A.
/// Fields populated progressively across phases.
/// MVP captures issueCategory and componentType only.
/// </summary>
public class ServiceEvent
{
    [JsonProperty("componentType")]
    public string? ComponentType { get; set; }

    [JsonProperty("failureMode")]
    public string? FailureMode { get; set; }

    [JsonProperty("repairAction")]
    public string? RepairAction { get; set; }

    [JsonProperty("partsUsed")]
    public List<string> PartsUsed { get; set; } = [];

    [JsonProperty("laborHours")]
    public decimal? LaborHours { get; set; }

    [JsonProperty("serviceDateUtc")]
    public DateTime? ServiceDateUtc { get; set; }
}
```

### 2.3 CustomerProfile (Tenant-Scoped Shadow Record)

```csharp name=RVS.Domain/Entities/CustomerProfile.cs
/// <summary>
/// Shadow profile — created automatically on first intake submission at a dealership.
/// One per customer per dealership (tenant-scoped).
/// The customer never sees a "Sign Up" screen.
/// </summary>
public class CustomerProfile : EntityBase
{
    public override string Type { get; init; } = "customerProfile";

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("normalizedEmail")]
    public string NormalizedEmail { get; set; } = string.Empty;

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// FK to the global CustomerIdentity record.
    /// All profiles for the same email point to the same identity.
    /// </summary>
    [JsonProperty("customerIdentityId")]
    public string CustomerIdentityId { get; set; } = string.Empty;

    /// <summary>
    /// Tracks the full lifecycle of each customer ↔ VIN relationship.
    /// Replaces a flat KnownVins list to handle ownership transfers.
    /// </summary>
    [JsonProperty("vehicleInteractions")]
    public List<VehicleInteraction> VehicleInteractions { get; set; } = [];

    [JsonProperty("serviceRequestIds")]
    public List<string> ServiceRequestIds { get; set; } = [];

    [JsonProperty("totalRequestCount")]
    public int TotalRequestCount { get; set; }

    // ── Convenience helpers (not persisted) ──

    /// <summary>
    /// Returns only VINs with Active status — used for intake prefill.
    /// </summary>
    public List<string> GetActiveVins() =>
        VehicleInteractions
            .Where(v => v.Status == VehicleInteractionStatus.Active)
            .Select(v => v.Vin)
            .ToList();

    /// <summary>
    /// Returns the active interaction for a VIN, or null.
    /// </summary>
    public VehicleInteraction? GetActiveInteraction(string vin) =>
        VehicleInteractions.FirstOrDefault(
            v => v.Vin == vin && v.Status == VehicleInteractionStatus.Active);
}
```

### 2.4 VehicleInteraction (Embedded in CustomerProfile)

```csharp name=RVS.Domain/Entities/VehicleInteraction.cs
/// <summary>
/// Records a customer's relationship to a specific VIN over time.
/// Handles ownership transfers: when a different customer submits for
/// a VIN, the previous owner's interaction is set to Inactive.
/// </summary>
public class VehicleInteraction
{
    [JsonProperty("vin")]
    public string Vin { get; set; } = string.Empty;

    [JsonProperty("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Active = customer currently associated with this VIN.
    /// Inactive = customer no longer associated (sold, traded, ownership transfer).
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; } = VehicleInteractionStatus.Active;

    [JsonProperty("firstSeenAtUtc")]
    public DateTime FirstSeenAtUtc { get; set; }

    [JsonProperty("lastSeenAtUtc")]
    public DateTime LastSeenAtUtc { get; set; }

    [JsonProperty("requestCount")]
    public int RequestCount { get; set; }

    [JsonProperty("deactivatedAtUtc")]
    public DateTime? DeactivatedAtUtc { get; set; }

    [JsonProperty("deactivationReason")]
    public string? DeactivationReason { get; set; }
}
```

```csharp name=RVS.Domain/Entities/VehicleInteractionStatus.cs
/// <summary>
/// String constants (not enum) for Cosmos DB serialization simplicity.
/// </summary>
public static class VehicleInteractionStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}
```

### 2.5 CustomerIdentity (Cross-Dealer Global Record)

```csharp name=RVS.Domain/Entities/CustomerIdentity.cs
/// <summary>
/// Global customer identity — one record per real human (by email).
/// Cross-tenant. Links all dealership-scoped profiles.
/// Partitioned by normalizedEmail for O(1) intake resolution.
/// </summary>
public class CustomerIdentity : EntityBase
{
    public override string Type { get; init; } = "customerIdentity";

    [JsonProperty("normalizedEmail")]
    public string NormalizedEmail { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// All dealership-scoped profiles linked to this identity.
    /// Enables "show me all my service history across all dealerships."
    /// </summary>
    [JsonProperty("linkedProfiles")]
    public List<LinkedProfileReference> LinkedProfiles { get; set; } = [];

    /// <summary>
    /// All VINs ever associated with this person across all dealerships.
    /// </summary>
    [JsonProperty("allKnownVins")]
    public List<string> AllKnownVins { get; set; } = [];

    /// <summary>
    /// Global magic-link token — resolves to the identity (not a single profile).
    /// Status page shows requests across all dealerships.
    /// </summary>
    [JsonProperty("magicLinkToken")]
    public string? MagicLinkToken { get; set; }

    [JsonProperty("magicLinkExpiresAtUtc")]
    public DateTime? MagicLinkExpiresAtUtc { get; set; }

    /// <summary>
    /// Phase 2+: Auth0 user ID when customer creates an account.
    /// Null during MVP.
    /// </summary>
    [JsonProperty("auth0UserId")]
    public string? Auth0UserId { get; set; }
}
```

```csharp name=RVS.Domain/Entities/LinkedProfileReference.cs
/// <summary>
/// Lightweight pointer from a global identity to a tenant-scoped profile.
/// </summary>
public class LinkedProfileReference
{
    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonProperty("dealershipName")]
    public string DealershipName { get; set; } = string.Empty;

    [JsonProperty("firstSeenAtUtc")]
    public DateTime FirstSeenAtUtc { get; set; }

    [JsonProperty("requestCount")]
    public int RequestCount { get; set; }
}
```

### 2.6 VehicleLedgerEntry (Append-Only VIN Event Log — Data Moat)

```csharp name=RVS.Domain/Entities/VehicleLedgerEntry.cs
/// <summary>
/// Append-only service event record linked to a VIN.
/// One entry per service request, written at intake time.
/// This is the data moat: proprietary, accumulating, non-replicable data
/// that powers Section 10A service intelligence.
/// 
/// Partitioned by /vin for O(1) "complete service history for this unit" queries.
/// </summary>
public class VehicleLedgerEntry
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("vin")]
    public string Vin { get; set; } = string.Empty;

    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("dealershipName")]
    public string DealershipName { get; set; } = string.Empty;

    [JsonProperty("serviceRequestId")]
    public string ServiceRequestId { get; set; } = string.Empty;

    [JsonProperty("customerIdentityId")]
    public string CustomerIdentityId { get; set; } = string.Empty;

    [JsonProperty("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }

    [JsonProperty("issueCategory")]
    public string? IssueCategory { get; set; }

    [JsonProperty("issueDescription")]
    public string? IssueDescription { get; set; }

    // ── Section 10A fields — populated progressively ──

    [JsonProperty("failureMode")]
    public string? FailureMode { get; set; }

    [JsonProperty("repairAction")]
    public string? RepairAction { get; set; }

    [JsonProperty("partsUsed")]
    public List<string> PartsUsed { get; set; } = [];

    [JsonProperty("laborHours")]
    public decimal? LaborHours { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = "New";

    [JsonProperty("submittedAtUtc")]
    public DateTime SubmittedAtUtc { get; set; }

    [JsonProperty("serviceDateUtc")]
    public DateTime? ServiceDateUtc { get; set; }
}
```

### 2.7 Dealership, TenantConfig, LookupSet

These follow the MF patterns for [Practice](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.Domain/Entities/Practice.cs), [TenantConfig](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.Domain/Entities/TenantConfig.cs), and [LookupSet](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.Domain/Entities/LookupSet.cs) respectively.

```csharp name=RVS.Domain/Entities/Dealership.cs
public class Dealership : EntityBase
{
    public override string Type { get; init; } = "dealership";

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;  // e.g. "camping-world-salt-lake"

    [JsonProperty("logoUrl")]
    public string? LogoUrl { get; set; }

    [JsonProperty("serviceEmail")]
    public string? ServiceEmail { get; set; }

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    [JsonProperty("intakeConfig")]
    public IntakeFormConfig IntakeConfig { get; set; } = new();
}

public class IntakeFormConfig
{
    [JsonProperty("acceptedFileTypes")]
    public List<string> AcceptedFileTypes { get; set; } = [".jpg", ".png", ".mp4"];

    [JsonProperty("maxFileSizeMb")]
    public int MaxFileSizeMb { get; set; } = 25;
}
```

---

## 3. Cosmos DB Container Design

### 3.1 Container Summary

| Container | Partition Key | Documents | RU Mode | Purpose |
|---|---|---|---|---|
| `service-requests` | `/tenantId` | `ServiceRequest` (aggregate root) | Autoscale 400–4000 | Core service request data |
| `customer-profiles` | `/tenantId` | `CustomerProfile` (shadow records) | Autoscale 400–1000 | Tenant-scoped customer view |
| `customer-identities` | `/normalizedEmail` | `CustomerIdentity` (global) | Manual 400 | Cross-dealer identity federation |
| `vehicle-ledger` | `/vin` | `VehicleLedgerEntry` (append-only) | Autoscale 400–1000 | Section 10A data moat |
| `dealerships` | `/tenantId` | `Dealership` | Autoscale 400–1000 | Dealership profiles |
| `config` | `/tenantId` | `TenantConfig` | Manual 400 | Tenant settings, access gate |
| `lookups` | `/category` | `LookupSet` | Manual 400 | Issue categories, component types |

### 3.2 Why Three Identity Containers?

One document cannot serve three different access patterns:

| Identity | Partition Key | Optimized For |
|---|---|---|
| **Tenant-scoped customer** (Dealer A's view of John) | `/tenantId` | Dealer dashboard, VIN ownership transfer, search |
| **Global customer** (John across all dealerships) | `/normalizedEmail` | Intake email resolution (~1 RU point read), cross-dealer status |
| **Global vehicle** (VIN 1ABC across all owners and dealers) | `/vin` | Unit service history (~1 RU), Section 10A analytics |

### 3.3 Indexing Policy: `customer-profiles`

```json name=customer-profiles-indexing-policy.json
{
  "indexingPolicy": {
    "includedPaths": [
      { "path": "/tenantId/?" },
      { "path": "/normalizedEmail/?" },
      { "path": "/customerIdentityId/?" },
      { "path": "/vehicleInteractions/[]/vin/?" },
      { "path": "/vehicleInteractions/[]/status/?" }
    ],
    "compositeIndexes": [
      [
        { "path": "/tenantId", "order": "ascending" },
        { "path": "/normalizedEmail", "order": "ascending" }
      ]
    ]
  },
  "uniqueKeyPolicy": {
    "uniqueKeys": [
      { "paths": ["/tenantId", "/normalizedEmail"] }
    ]
  }
}
```

### 3.4 Cosmos DB Document Examples

```json name=service-request-document.json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "type": "serviceRequest",
  "tenantId": "dealer_saltlake",
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
  "vehicle": {
    "vin": "1ABC234567",
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

---

## 4. Domain Interfaces

### 4.1 Service Request

```csharp name=RVS.Domain/Interfaces/IServiceRequestRepository.cs
public interface IServiceRequestRepository
{
    Task<ServiceRequest?> GetByIdAsync(string tenantId, string serviceRequestId, CancellationToken ct = default);
    Task<PagedResult<ServiceRequest>> SearchAsync(string tenantId, ServiceRequestSearchRequestDto request, CancellationToken ct = default);
    Task<List<ServiceRequest>> GetByProfileIdAsync(string tenantId, string customerProfileId, int limit, CancellationToken ct = default);
    Task CreateAsync(ServiceRequest entity, CancellationToken ct = default);
    Task UpdateAsync(ServiceRequest entity, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, string serviceRequestId, CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(string tenantId, string? status = null, CancellationToken ct = default);
}
```

```csharp name=RVS.Domain/Interfaces/IServiceRequestService.cs
public interface IServiceRequestService
{
    Task<ServiceRequest> CreateServiceRequestAsync(string tenantId, ServiceRequestCreateRequestDto request);
    Task<ServiceRequest> GetServiceRequestAsync(string tenantId, string serviceRequestId);
    Task<PagedResult<ServiceRequest>> SearchServiceRequestsAsync(string tenantId, ServiceRequestSearchRequestDto request);
    Task<List<ServiceRequest>> GetByProfileAsync(string tenantId, string customerProfileId, int limit);
    Task<ServiceRequest> UpdateServiceRequestAsync(string tenantId, string serviceRequestId, ServiceRequestUpdateRequestDto request);
    Task UpdateStatusAsync(string tenantId, string serviceRequestId, string newStatus);
    Task DeleteServiceRequestAsync(string tenantId, string serviceRequestId);
}
```

### 4.2 Customer Profile (Tenant-Scoped Shadow)

```csharp name=RVS.Domain/Interfaces/ICustomerProfileRepository.cs
public interface ICustomerProfileRepository
{
    Task<CustomerProfile?> GetByIdAsync(string tenantId, string profileId, CancellationToken ct = default);
    Task<CustomerProfile?> GetByIdentityIdAsync(string tenantId, string customerIdentityId, CancellationToken ct = default);
    Task<CustomerProfile?> GetByActiveVinAsync(string tenantId, string vin, CancellationToken ct = default);
    Task CreateAsync(CustomerProfile profile, CancellationToken ct = default);
    Task UpdateAsync(CustomerProfile profile, CancellationToken ct = default);
}
```

```csharp name=RVS.Domain/Interfaces/ICustomerProfileService.cs
public interface ICustomerProfileService
{
    /// <summary>
    /// Resolves an existing tenant-scoped profile or creates a new shadow record.
    /// Also handles VIN ownership transfer detection within the tenant.
    /// </summary>
    Task<CustomerProfile> ResolveOrCreateProfileAsync(
        string tenantId, CustomerIdentity identity, string? vin, VehicleInfo? vehicleInfo);
}
```

### 4.3 Customer Identity (Cross-Dealer Global)

```csharp name=RVS.Domain/Interfaces/ICustomerIdentityRepository.cs
public interface ICustomerIdentityRepository
{
    Task<CustomerIdentity?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<CustomerIdentity?> GetByMagicLinkTokenAsync(string token, CancellationToken ct = default);
    Task CreateAsync(CustomerIdentity identity, CancellationToken ct = default);
    Task UpdateAsync(CustomerIdentity identity, CancellationToken ct = default);
}
```

```csharp name=RVS.Domain/Interfaces/ICustomerIdentityService.cs
public interface ICustomerIdentityService
{
    /// <summary>
    /// Resolves a global identity by email or creates a new one.
    /// </summary>
    Task<CustomerIdentity> ResolveOrCreateIdentityAsync(
        string email, string firstName, string lastName, string? phone);

    /// <summary>
    /// Validates a magic-link token and returns the global identity.
    /// </summary>
    Task<CustomerIdentity> ValidateMagicLinkAsync(string token);

    /// <summary>
    /// Rotates the magic-link token on the global identity.
    /// </summary>
    Task<string> RotateMagicLinkTokenAsync(string identityId);
}
```

### 4.4 Vehicle Ledger (Data Moat)

```csharp name=RVS.Domain/Interfaces/IVehicleLedgerRepository.cs
public interface IVehicleLedgerRepository
{
    Task AppendAsync(VehicleLedgerEntry entry, CancellationToken ct = default);
    Task<List<VehicleLedgerEntry>> GetByVinAsync(string vin, int limit = 50, CancellationToken ct = default);
    Task UpdateEntryAsync(VehicleLedgerEntry entry, CancellationToken ct = default);
}
```

```csharp name=RVS.Domain/Interfaces/IVehicleLedgerService.cs
public interface IVehicleLedgerService
{
    /// <summary>
    /// Appends a ledger entry when a service request is created.
    /// Write-only in MVP; read capabilities added in Phase 5-6.
    /// </summary>
    Task RecordServiceEventAsync(ServiceRequest request, string dealershipName, string customerIdentityId);

    /// <summary>
    /// Returns full service history for a VIN across all dealerships.
    /// ~1 RU point read into VIN partition.
    /// </summary>
    Task<List<VehicleLedgerEntry>> GetVinHistoryAsync(string vin, int limit = 50);
}
```

### 4.5 Attachments & Blob Storage

```csharp name=RVS.Domain/Interfaces/IAttachmentService.cs
public interface IAttachmentService
{
    Task<AttachmentResponseDto> UploadAttachmentAsync(string tenantId, string serviceRequestId,
        AttachmentUploadRequestDto request, Stream fileStream, CancellationToken ct = default);
    Task<Stream> GetAttachmentStreamAsync(string tenantId, string serviceRequestId,
        string attachmentId, CancellationToken ct = default);
    Task DeleteAttachmentAsync(string tenantId, string serviceRequestId,
        string attachmentId, CancellationToken ct = default);
    Task<string> GenerateSasUriAsync(string tenantId, string serviceRequestId,
        string attachmentId, TimeSpan expiry, CancellationToken ct = default);
}
```

```csharp name=RVS.Domain/Interfaces/IBlobStorageRepository.cs
public interface IBlobStorageRepository
{
    Task<string> UploadAsync(string containerPath, string fileName, Stream content,
        string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string blobUri, CancellationToken ct = default);
    Task DeleteAsync(string blobUri, CancellationToken ct = default);
    Task<string> GenerateSasUriAsync(string blobUri, TimeSpan expiry, CancellationToken ct = default);
}
```

---

## 5. Core Orchestration Flow: Intake Submission

This is the most important flow in the system — the complete sequence when a customer submits a service request:

```
Customer submits intake form at Dealership B
        │
        ▼
┌──────────────────────────────────────────────────┐
│ STEP 1: Resolve Global CustomerIdentity           │
│                                                    │
│ Container: customer-identities                    │
│ Query: point read by normalizedEmail partition    │
│ Cost: ~1 RU                                       │
│                                                    │
│ Found? → use existing identity                    │
│ Not found? → create new                           │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 2: Resolve Tenant-Scoped CustomerProfile     │
│                                                    │
│ Container: customer-profiles                      │
│ Query: WHERE tenantId = @t                        │
│   AND customerIdentityId = @identityId            │
│ Cost: ~2.8 RU (single-partition indexed query)    │
│                                                    │
│ Found? → update contact info, handle VIN          │
│ Not found? → create new shadow profile            │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 2a: VIN Ownership Resolution                 │
│                                                    │
│ If VIN is active on THIS profile → update lastSeen│
│ If VIN is active on DIFFERENT profile at same      │
│   dealership → deactivate old, activate on this   │
│ If VIN is brand new → create Active interaction   │
│ Cost: ~3 RU (single-partition array filter)       │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 3: Create ServiceRequest                     │
│                                                    │
│ Container: service-requests                       │
│ Embed CustomerSnapshot (denormalized)             │
│ Auto-categorize issue (rule-based MVP)            │
│ Generate technician summary                       │
│ Cost: ~1 RU                                       │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 4: Append VehicleLedgerEntry (Data Moat)     │
│                                                    │
│ Container: vehicle-ledger                         │
│ Partition: /vin                                   │
│ Write-only in MVP (nothing reads it yet)          │
│ Cost: ~1 RU                                       │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 5: Update Linkages                           │
│                                                    │
│ CustomerProfile: add SR ID, increment count       │
│ CustomerIdentity: add VIN, add dealership link,   │
│   rotate magic-link token                         │
│ Cost: ~2 RU                                       │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│ STEP 6: Send Confirmation Email                   │
│                                                    │
│ Includes magic-link URL:                          │
│   rvs.app/status/{magicLinkToken}                 │
│ Fire-and-forget (async)                           │
└──────────────────────────────────────────────────┘

Total Cosmos cost per intake: ~10.8 RU
```

---

## 6. Controllers

Following the [RVS copilot-instructions.md](https://github.com/markarnoldutah/RVS/blob/f1e1bc1a099c242cace68f515f6bdb8db9ea2418/.github/copilot-instructions.md) and [MF `ClaimsService` pattern](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Services/ClaimsService.cs).

### 6.1 Customer-Facing (Unauthenticated)

```csharp name=RVS.API/Controllers/IntakeController.cs
[ApiController]
[Route("api/intake/{dealershipSlug}")]
[AllowAnonymous]
public class IntakeController : ControllerBase
{
    private readonly IServiceRequestService _serviceRequestService;
    private readonly IAttachmentService _attachmentService;
    private readonly IDealershipService _dealershipService;
    private readonly ICustomerIdentityService _identityService;

    // GET                                       → GetIntakeConfig (+ optional prefill via ?token=)
    // POST  service-requests                    → SubmitServiceRequest
    // POST  service-requests/{id}/attachments   → UploadAttachment
}
```

```csharp name=RVS.API/Controllers/CustomerStatusController.cs
[ApiController]
[Route("api/status")]
[AllowAnonymous]
public class CustomerStatusController : ControllerBase
{
    private readonly ICustomerIdentityService _identityService;
    private readonly IServiceRequestService _serviceRequestService;

    // GET {token}  → GetStatus (validates magic link, returns requests across all dealerships)
}
```

### 6.2 Dealer-Facing (Authenticated)

```csharp name=RVS.API/Controllers/ServiceRequestsController.cs
[ApiController]
[Route("api/dealerships/{dealershipId}/service-requests")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _serviceRequestService;
    private readonly ClaimsService _claimsService;

    // GET    {id}           → GetServiceRequest
    // POST   search         → SearchServiceRequests
    // PUT    {id}           → UpdateServiceRequest
    // DELETE {id}           → DeleteServiceRequest
}
```

```csharp name=RVS.API/Controllers/AttachmentsController.cs
[ApiController]
[Route("api/dealerships/{dealershipId}/service-requests/{serviceRequestId}/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    // GET    {attachmentId}  → GetAttachment (SAS URL)
    // DELETE {attachmentId}  → DeleteAttachment
}
```

```csharp name=RVS.API/Controllers/DealershipsController.cs
[ApiController]
[Route("api/dealerships")]
[Authorize]
public class DealershipsController : ControllerBase
{
    // GET              → GetDealerships
    // GET  {id}        → GetDealership
    // PUT  {id}        → UpdateDealership
    // GET  {id}/qr-code → GenerateQrCode
}
```

```csharp name=RVS.API/Controllers/TenantsController.cs
[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    // POST  config       → CreateTenantConfig
    // GET   config       → GetTenantConfig
    // PUT   config       → UpdateTenantConfig
    // GET   access-gate  → GetAccessGate
}
```

```csharp name=RVS.API/Controllers/LookupsController.cs
[ApiController]
[Route("api/lookups")]
[Authorize]
public class LookupsController : ControllerBase
{
    // GET {lookupSetId}  → GetLookupSet
}
```

```csharp name=RVS.API/Controllers/AnalyticsController.cs
[ApiController]
[Route("api/dealerships/{dealershipId}/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    // GET service-requests/summary  → GetServiceRequestAnalytics
}
```

---

## 7. Service Layer

All services are `sealed`, inject repository interfaces + `IUserContextAccessor`, guard clauses first, return domain entities. Follows [MF patterns](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Middleware/ExceptionHandlingMiddleware.cs).

### 7.1 `ServiceRequestService` (Primary Orchestrator)

```csharp name=RVS.API/Services/ServiceRequestService.cs
public sealed class ServiceRequestService : IServiceRequestService
{
    private readonly IServiceRequestRepository _repository;
    private readonly ICustomerIdentityService _identityService;
    private readonly ICustomerProfileService _profileService;
    private readonly IVehicleLedgerService _ledgerService;
    private readonly IDealershipService _dealershipService;
    private readonly ICategorizationService _categorizationService;
    private readonly INotificationService _notificationService;
    private readonly IUserContextAccessor _userContext;

    public async Task<ServiceRequest> CreateServiceRequestAsync(
        string tenantId, ServiceRequestCreateRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        // Step 1: Resolve global identity
        var identity = await _identityService.ResolveOrCreateIdentityAsync(
            request.Customer.Email, request.Customer.FirstName,
            request.Customer.LastName, request.Customer.Phone);

        // Step 2: Resolve tenant-scoped profile (handles VIN ownership)
        var profile = await _profileService.ResolveOrCreateProfileAsync(
            tenantId, identity, request.Vehicle.Vin,
            request.Vehicle.ToVehicleInfo());

        // Step 3: Build ServiceRequest
        var entity = request.ToEntity(tenantId, _userContext.UserId);
        entity.CustomerProfileId = profile.Id;
        entity.Customer = new CustomerSnapshot
        {
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Email = profile.Email,
            Phone = profile.Phone,
            IsReturningCustomer = profile.TotalRequestCount > 0,
            PriorRequestCount = profile.TotalRequestCount
        };

        // Step 3a: Auto-categorize
        var category = await _categorizationService.CategorizeAsync(
            request.IssueDescription, request.Vehicle);
        entity.IssueCategory = category.Category;
        entity.TechnicianSummary = category.TechnicianSummary;

        await _repository.CreateAsync(entity);

        // Step 4: Append vehicle ledger (data moat — write-only)
        var dealership = await _dealershipService.GetByTenantIdAsync(tenantId);
        await _ledgerService.RecordServiceEventAsync(
            entity, dealership.Name, identity.Id);

        // Step 5: Update linkages
        profile.ServiceRequestIds.Add(entity.Id);
        profile.TotalRequestCount++;
        profile.MarkAsUpdated(null);

        var magicLinkToken = await _identityService.RotateMagicLinkTokenAsync(identity.Id);

        // Step 6: Send confirmation with magic link
        _ = _notificationService.SendIntakeConfirmationAsync(entity, magicLinkToken);

        return entity;
    }
}
```

### 7.2 `CustomerProfileService` (Shadow Profile + VIN Ownership)

```csharp name=RVS.API/Services/CustomerProfileService.cs
public sealed class CustomerProfileService : ICustomerProfileService
{
    private readonly ICustomerProfileRepository _profileRepository;

    public async Task<CustomerProfile> ResolveOrCreateProfileAsync(
        string tenantId, CustomerIdentity identity, string? vin, VehicleInfo? vehicleInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(identity);

        var now = DateTime.UtcNow;

        // Find existing profile for this identity at this dealership
        var profile = await _profileRepository.GetByIdentityIdAsync(tenantId, identity.Id);

        if (profile is null)
        {
            profile = new CustomerProfile
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Email = identity.Email,
                NormalizedEmail = identity.NormalizedEmail,
                FirstName = identity.FirstName,
                LastName = identity.LastName,
                Phone = identity.Phone,
                CustomerIdentityId = identity.Id,
                VehicleInteractions = [],
                ServiceRequestIds = [],
                TotalRequestCount = 0,
                CreatedAtUtc = now,
                CreatedByUserId = null
            };
            await _profileRepository.CreateAsync(profile);
        }
        else
        {
            profile.FirstName = identity.FirstName;
            profile.LastName = identity.LastName;
            profile.Phone = identity.Phone ?? profile.Phone;
            profile.MarkAsUpdated(null);
        }

        // Handle VIN ownership lifecycle
        if (!string.IsNullOrWhiteSpace(vin))
        {
            await ResolveVinOwnershipAsync(tenantId, profile, vin, vehicleInfo, now);
        }

        await _profileRepository.UpdateAsync(profile);
        return profile;
    }

    private async Task ResolveVinOwnershipAsync(
        string tenantId, CustomerProfile currentProfile,
        string vin, VehicleInfo? vehicleInfo, DateTime now)
    {
        var existingOnThisProfile = currentProfile.GetActiveInteraction(vin);

        if (existingOnThisProfile is not null)
        {
            // Same customer, same VIN — update timestamps
            existingOnThisProfile.LastSeenAtUtc = now;
            existingOnThisProfile.RequestCount++;
            return;
        }

        // Check if another profile at this dealership "owns" this VIN
        var previousOwnerProfile = await _profileRepository.GetByActiveVinAsync(tenantId, vin);

        if (previousOwnerProfile is not null && previousOwnerProfile.Id != currentProfile.Id)
        {
            // Ownership transfer: deactivate on previous owner
            var previousInteraction = previousOwnerProfile.GetActiveInteraction(vin);
            if (previousInteraction is not null)
            {
                previousInteraction.Status = VehicleInteractionStatus.Inactive;
                previousInteraction.DeactivatedAtUtc = now;
                previousInteraction.DeactivationReason = "VIN submitted by a different customer";
                previousOwnerProfile.MarkAsUpdated(null);
                await _profileRepository.UpdateAsync(previousOwnerProfile);
            }
        }

        // Check for reactivation (customer sold RV, bought it back)
        var reactivate = currentProfile.VehicleInteractions
            .FirstOrDefault(v => v.Vin == vin && v.Status == VehicleInteractionStatus.Inactive);

        if (reactivate is not null)
        {
            reactivate.Status = VehicleInteractionStatus.Active;
            reactivate.LastSeenAtUtc = now;
            reactivate.RequestCount++;
            reactivate.DeactivatedAtUtc = null;
            reactivate.DeactivationReason = null;
        }
        else
        {
            currentProfile.VehicleInteractions.Add(new VehicleInteraction
            {
                Vin = vin,
                Manufacturer = vehicleInfo?.Manufacturer,
                Model = vehicleInfo?.Model,
                Year = vehicleInfo?.Year,
                Status = VehicleInteractionStatus.Active,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                RequestCount = 1
            });
        }
    }
}
```

---

## 8. Middleware Pipeline

Following the [RVS copilot-instructions.md pipeline order](https://github.com/markarnoldutah/RVS/blob/f1e1bc1a099c242cace68f515f6bdb8db9ea2418/.github/copilot-instructions.md) and [MF `ExceptionHandlingMiddleware`](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Middleware/ExceptionHandlingMiddleware.cs):

```csharp name=RVS.API/Program.cs (pipeline)
// 1. Dev-only endpoints
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI();
}

// 2. HTTPS redirection (production only)
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// 3. CORS
app.UseCors("AllowBlazorClient");

// 4. Rate limiting (protects public intake + status endpoints)
app.UseRateLimiter();

// 5. ExceptionHandlingMiddleware (IMiddleware, singleton)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 6. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 7. Tenant access gate
app.UseMiddleware<TenantAccessGateMiddleware>();

// 8. Map controllers
app.MapControllers();
```

---

## 9. Authentication & Authorization

| Actor | Auth Method | Profile Model | Status Access |
|---|---|---|---|
| **Customer (intake form)** | `[AllowAnonymous]` + rate limiting | Shadow `CustomerProfile` auto-created | None at submission time |
| **Customer (status page)** | `[AllowAnonymous]` + magic-link token | Existing `CustomerIdentity` | `GET /api/status/{token}` — cross-dealer |
| **Customer (future Phase 2+)** | Auth0 OIDC (optional upgrade) | `CustomerIdentity.Auth0UserId` linked | Full account access |
| **Dealer staff** | Auth0 JWT Bearer | Auth0 user + tenant claims | Full dashboard access |
| **System/API-to-API (future)** | Auth0 M2M Client Credentials | N/A | DMS integration |

```csharp name=RVS.API/Services/ClaimsService.cs
public sealed class ClaimsService
{
    public const string TenantIdClaimType = "http://rvserviceflow.com/tenantId";
    public const string RoleClaimType = "http://rvserviceflow.com/roles";

    private readonly IHttpContextAccessor _httpContextAccessor;

    private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("User is not available in the current context.");

    public string GetTenantIdOrThrow()
    {
        var tenantId = User.FindFirst(TenantIdClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new UnauthorizedAccessException("Tenant identifier is missing.");
        return tenantId;
    }

    public string? GetRole() => User.FindFirst(RoleClaimType)?.Value;
}
```

---

## 10. Azure Blob Storage Structure

```
rvs-attachments/
  └── {tenantId}/
      └── {serviceRequestId}/
          ├── att_001_slide_issue.jpg
          ├── att_002_pump_video.mp4
          └── att_003_vin_plate.jpg
```

- **Upload:** Streaming via API (MVP). Future: SAS URI direct upload for large videos.
- **Access:** Time-limited SAS URIs generated on demand.
- **Retention:** Configurable per-tenant in `TenantConfig`.

---

## 11. Complete API Route Summary

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `GET` | `api/intake/{slug}?token={t}` | Anonymous | Intake config + optional prefill via magic link |
| `POST` | `api/intake/{slug}/service-requests` | Anonymous | Submit request → resolves identity/profile/VIN/ledger |
| `POST` | `api/intake/{slug}/service-requests/{id}/attachments` | Anonymous | Upload photo/video |
| `GET` | `api/status/{token}` | Anonymous | Customer status page via magic link (cross-dealer) |
| `POST` | `api/dealerships/{id}/service-requests/search` | Bearer | Search/filter requests |
| `GET` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | Request detail + attachments |
| `PUT` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | Update request (status, notes, service event) |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | Delete request |
| `GET` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | Get attachment (SAS URL) |
| `GET` | `api/dealerships` | Bearer | List dealerships for tenant |
| `GET` | `api/dealerships/{id}` | Bearer | Dealership detail |
| `PUT` | `api/dealerships/{id}` | Bearer | Update dealership |
| `GET` | `api/dealerships/{id}/qr-code` | Bearer | Generate QR code |
| `GET` | `api/dealerships/{id}/analytics/service-requests/summary` | Bearer | Basic request analytics |
| `POST` | `api/tenants/config` | Bearer | Bootstrap tenant config |
| `GET` | `api/tenants/config` | Bearer | Get tenant config |
| `PUT` | `api/tenants/config` | Bearer | Update tenant config |
| `GET` | `api/tenants/access-gate` | Bearer | Access gate check |
| `GET` | `api/lookups/{lookupSetId}` | Bearer | Lookup values |

---

## 12. RU Cost Analysis

| Operation | Cosmos Calls | Estimated RU |
|---|---|---|
| **New customer intake (first visit, first dealership)** | 1 identity miss + 1 identity write + 1 profile write + 1 SR write + 1 ledger write + 1 identity update + 1 profile update | ~10.8 RU |
| **Returning customer intake (same dealership)** | 1 identity hit + 1 profile hit + 1 VIN check + 1 SR write + 1 ledger write + 2 updates | ~9.8 RU |
| **Returning customer, new dealership** | 1 identity hit + 1 profile miss + 1 profile write + 1 SR write + 1 ledger write + 2 updates | ~10.8 RU |
| **Magic-link status page** | 1 identity query (token) + N point reads (linked service requests) | ~1 + N RU |
| **Dealer dashboard: view request** | 1 point read (SR — customer snapshot embedded) | ~1 RU |
| **Dealer dashboard: search requests** | 1 single-partition query | ~3 RU |
| **VIN service history (10A query)** | 1 single-partition read (vehicle-ledger, /vin) | **~1 RU** |

---

## 13. Magic Link Security

| Concern | Mitigation |
|---|---|
| **Token guessing** | 32-byte cryptographic random (256-bit entropy), URL-safe Base64 |
| **Token expiry** | 30-day default, configurable per tenant |
| **Token rotation** | New token on every intake submission; previous invalidated |
| **Rate limiting** | `api/status/{token}` limited to 10 req/min per IP |
| **PII exposure** | Status page returns first name + vehicle summaries only — no