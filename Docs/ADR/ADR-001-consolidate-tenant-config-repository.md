# ADR-001: Consolidate Duplicate Tenant Config Repositories

- **Status:** Accepted
- **Date:** 2025-07-11
- **Applies To:** `RVS.API`, `RVS.Domain`, `RVS.Infra.AzCosmosRepository`

---

## 1. Context

The codebase contained **two parallel stacks** for `TenantConfig` CRUD, each pointing at a
**different Cosmos DB container**:

| Layer | Legacy stack (removed) | Canonical stack (kept) |
|---|---|---|
| Repository interface | `IConfigRepository` | `ITenantConfigRepository` |
| Repository impl | `CosmosConfigRepository` | `CosmosTenantConfigRepository` |
| Cosmos container | `"tenants"` | `"tenant-configs"` |
| Service interface | `ITenantService` | `ITenantConfigService` |
| Service impl | `TenantService` | `TenantConfigService` |
| Consumer | `TenantAccessGateMiddleware` | `TenantsController` |

---

## 2. Problem

### 2a. The `"tenants"` container does not exist

`RVS.Data.Cosmos.Seed` creates exactly **9 containers**:

| Container | Partition key | Notes |
|---|---|---|
| `service-requests` | `/tenantId` | |
| `customer-profiles` | `/tenantId` | |
| `global-customer-accounts` | `/email` | |
| `asset-ledger` | `/assetId` | |
| `dealerships` | `/tenantId` | Also holds `Tenant` docs via type discriminator |
| `locations` | `/tenantId` | |
| `slug-lookups` | `/slug` | |
| `tenant-configs` | `/tenantId` | ← The only tenant-config container |
| `lookup-sets` | `/category` | |

**`"tenants"` is never created by the Seed.** `Tenant` entities are seeded into the `"dealerships"` container using a type discriminator. The `"tenants"` container passed to `CosmosConfigRepository` was a phantom.

### 2b. Cascading 404 on every authenticated request

```
GET /api/locations
  → TenantAccessGateMiddleware
    → TenantService.GetAccessGateAsync("ten_bluecompass")
      → TenantService.GetTenantConfigAsync("ten_bluecompass")
        → CosmosConfigRepository.GetTenantConfigAsync("ten_bluecompass")
          → Cosmos ReadItemAsync on "tenants" container → null
        ← throws KeyNotFoundException("Tenant config not found.")
      ← ExceptionHandlingMiddleware catches KeyNotFoundException → 404
```

### 2c. Duplicate interfaces with identical contracts

Both `ITenantService` / `ITenantConfigService` declared the same four methods over the same
`TenantConfig` entity. Both `IConfigRepository` / `ITenantConfigRepository` performed the same
CRUD with only minor method-name differences (`GetTenantConfigAsync` vs `GetAsync`).

---

## 3. Decision

Eliminate the legacy stack entirely. All consumers use the canonical types.

### Chosen canonical stack

| Layer | Type | Backed by |
|---|---|---|
| Repository interface | `ITenantConfigRepository` | — |
| Repository impl | `CosmosTenantConfigRepository` | `"tenant-configs"` container, PK `/tenantId` |
| Service interface | `ITenantConfigService` | — |
| Service impl | `TenantConfigService` (sealed) | `ITenantConfigRepository` |

### Document ID convention (unchanged)

`{tenantId}_config` — e.g. `ten_bluecompass_config`

---

## 4. Changes Made

### Files modified

| File | Change |
|---|---|
| `RVS.API/Middleware/TenantAccessGateMiddleWare.cs` | `InvokeAsync` parameter changed from `ITenantService` → `ITenantConfigService` |
| `RVS.API/Program.cs` | Removed `IConfigRepository` + `ITenantService` DI registrations |
| `Tests/.../TenantAccessGateMiddlewareTests.cs` | Mock changed from `ITenantService` → `ITenantConfigService` |
| `Tests/.../TenantConfigServiceTests.cs` | Added `GetAccessGateAsync_WhenConfigNotFound_ShouldReturnDefaultWithLoginsEnabled` test |

### Files cleared (content replaced with tombstone comment)

> These files are candidates for physical deletion from the repository via `git rm`.

| File | Reason |
|---|---|
| `RVS.Domain/Interfaces/IConfigRepository.cs` | Duplicate of `ITenantConfigRepository` |
| `RVS.Domain/Interfaces/ITenantService.cs` | Duplicate of `ITenantConfigService` |
| `RVS.Infra.AzCosmosRepository/Repositories/CosmosConfigRepository.cs` | Pointed at phantom `"tenants"` container |
| `RVS.API/Services/TenantService.cs` | Duplicate of `TenantConfigService` |

### Also fixed (prior session)

`TenantService.GetAccessGateAsync` and `TenantConfigService.GetAccessGateAsync` both had a
latent bug: they delegated to `GetTenantConfigAsync` (which throws `KeyNotFoundException`)
rather than the repository directly, making the "safe default" comment dead code. Both were
fixed to call the repository and use null-coalescing:

```csharp
// Before — throws if config doc is missing
var cfg = await GetTenantConfigAsync(tenantId, cancellationToken);
return cfg.AccessGate ?? new TenantAccessGateEmbedded { LoginsEnabled = true };

// After — returns safe default for missing config doc
var cfg = await _repository.GetAsync(tenantId, cancellationToken);
return cfg?.AccessGate ?? new TenantAccessGateEmbedded { LoginsEnabled = true };
```

---

## 5. Seed file clarification

`RVS.Data.Cosmos.Seed/Program.cs` does **not** use DI, repositories, or service classes.
It seeds all data directly via `CosmosClient.UpsertItemAsync`. No changes were required.

Key seed lines confirming the canonical container:

```csharp
await SeedItemsAsync(containers["tenant-configs"], tenantConfigs, tc => new PartitionKey(tc.TenantId), "tenant-configs");

// Tenants go into the dealerships container (type discriminator), NOT a "tenants" container
await SeedItemsAsync(containers["dealerships"], tenants, t => new PartitionKey(t.TenantId), "tenants (in dealerships container)");
```

---

## 6. Physical deletion (to do)

The four cleared files should be physically removed from source control:

```bash
git rm RVS.Domain/Interfaces/IConfigRepository.cs
git rm RVS.Domain/Interfaces/ITenantService.cs
git rm "RVS.Infra.AzCosmosRepository/Repositories/CosmosConfigRepository.cs"
git rm RVS.API/Services/TenantService.cs
git commit -m "chore: remove legacy IConfigRepository / ITenantService stack (ADR-001)"
```

---

## 7. Optional — delete phantom container from Azure

If the `"tenants"` container was inadvertently created in any environment (it should be empty):

```bash
az cosmosdb sql container delete \
  --account-name <account-name> \
  --database-name rvsdb \
  --name tenants \
  --resource-group <resource-group>
```

---

## 8. Consequences

| | |
|---|---|
| **No API surface change** | All HTTP endpoints and DTOs unchanged |
| **No Cosmos schema change** | `"tenant-configs"` container, partition key, and doc ID format unchanged |
| **~150 lines of dead code removed** | Four files cleared |
| **Single source of truth** | One repository interface, one service interface, one container |
| **Seed alignment** | DI registrations now match exactly what the Seed creates |
