# Payers Container Schema Migration - Complete

## Summary of Changes

This migration moves `PayerConfig` storage from the `tenants` container to the `payers` container and changes the `payers` container partition key from `/id` to `/tenantId`.

**Status:** ? **COMPLETE** - Supports both GLOBAL and tenant-specific payers

## Container Structure Changes

### Before Migration

```
Tenants Container (PK: /tenantId)
??? Tenant entities (type: "tenant")
??? TenantConfig entities (type: "tenantConfig")
??? PayerConfig entities (type: "payerConfig")  ? MOVED

Payers Container (PK: /id)  ? CHANGED PK
??? Payer entities (type: "payer")
```

### After Migration (Complete)

```
Tenants Container (PK: /tenantId)
??? Tenant entities (type: "tenant")
??? TenantConfig entities (type: "tenantConfig")

Payers Container (PK: /tenantId)  ? NEW PK
??? Payer entities (type: "payer") 
?   ??? GLOBAL partition ? Shared payers (VSP, BCBS, Medicare, EyeMed)
?   ??? Tenant-specific partitions ? State-specific payers (e.g., CA Medicaid for ten_001)
??? PayerConfig entities (type: "payerConfig") ? MOVED HERE (per-tenant)
```

### Future Enhancement (Post-MVP)

```
Payers Container (PK: /tenantId)
??? Payer entities with tenantId = "GLOBAL" (VSP, BCBS, Medicare, EyeMed)
??? Payer entities with tenantId = "ten_001" (CA Medicaid) ? Tenant-specific
??? Payer entities with tenantId = "ten_002" (NY Medicaid) ? Tenant-specific
??? PayerConfig entities (per-tenant)
```

## Data Model Changes

### GLOBAL Payers (Shared)
- `tenantId = "GLOBAL"`
- Shared across all tenants
- Examples: VSP, BCBS, EyeMed, Medicare

### Tenant-Specific Payers
- `tenantId = {specific tenant ID}`
- Available only to that tenant
- Examples: CA Medicaid (ten_001), NY Medicaid (ten_002)

**Query Behavior:**
- When querying payers for a tenant, both GLOBAL and tenant-specific payers are returned
- Tenant-specific payers take precedence if same `payerId` exists in both partitions

### PayerConfig (Per-Tenant)
- Stored in payers container
- Partition key = `tenantId` (each tenant has its own configs)
- Links both GLOBAL and tenant-specific payers to specific tenants
- Provides tenant-specific settings (display name, sort order, COB role, enabled/disabled)

## Files Changed
### 1. `BF.Data.Cosmos.Seed\Program.cs`
- ? Changed payers container partition key from `/id` to `/tenantId`
- ? All GLOBAL payers set `TenantId = "GLOBAL"`
- ? Tenant-specific payer included (CA Medicaid for ten_001)
- ? Updated payer seeding to use `tenantId ?? "GLOBAL"` as partition key
- ? Moved PayerConfig seeding from tenants container to payers container
- ? Updated container creation comments
- ? Added Medicaid PayerConfig for ten_001

### 2. `BF.Infra.AzCosmosRepository\Repositories\CosmosPayerRepository.cs`
- ? **Updated `SearchAsync()` to query both GLOBAL and tenant-specific payers**
  - Cross-partition query: `WHERE (c.tenantId = 'GLOBAL' OR c.tenantId = @tenantId)`
  - Returns all payers available to the tenant
- ? Added type discriminator filter (`c.type = 'payer'`)
- ? **Updated `GetByIdAsync()` with tenant-specific fallback logic**
  - Checks tenant partition first
  - Falls back to GLOBAL partition if not found
  - Returns null if not in either partition

### 3. `BF.Infra.AzCosmosRepository\Repositories\CosmosConfigRepository.cs`
- ? Added `_payersContainer` field
- ? Updated constructor to accept both `tenantsContainerId` and `payersContainerId`
- ? Updated `GetPayerConfigsAsync()` to query from payers container
- ? Updated `GetPayerConfigAsync()` to query from payers container
- ? Updated `SavePayerConfigAsync()` to save to payers container

### 4. `BF.API\Program.cs`
- ? Updated `IConfigRepository` registration to pass both container names:
  ```csharp
  return new CosmosConfigRepository(client, databaseId, "tenants", "payers");
  ```

## Query Pattern Changes

### Querying Payers (Complete Implementation)

**Cross-partition query for both GLOBAL and tenant-specific payers:**
```csharp
// Queries both GLOBAL and tenant-specific payers
var sql = "SELECT * FROM c WHERE c.type = 'payer' AND (c.tenantId = 'GLOBAL' OR c.tenantId = @tenantId)";
var iterator = container.GetItemQueryIterator<Payer>(sql);
```

**Point read with fallback:**
```csharp
// Try tenant-specific first
try {
    return await container.ReadItemAsync<Payer>(payerId, new PartitionKey(tenantId));
} catch (NotFound) {
    // Fall back to GLOBAL
    return await container.ReadItemAsync<Payer>(payerId, new PartitionKey("GLOBAL"));
}
```

### Querying PayerConfigs

**Single-partition query for tenant's configs:**
```csharp
// Query from payers container
var configs = await payersContainer.GetItemQueryIterator<PayerConfig>(
    "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.type = 'payerConfig'",
    new PartitionKey(tenantId)
);
```

## Benefits

### ? Flexible Multi-Tenancy
- **GLOBAL payers**: Shared across all tenants (VSP, Medicare, BCBS, etc.)
- **Tenant-specific payers**: State or region-specific (CA Medicaid, NY Medicaid)
- Single query returns all payers available to a tenant

### ? Logical Co-location
- `Payer` and `PayerConfig` entities stored together in same container
- Related data physically co-located

### ? Efficient Queries
- Cross-partition query hits only 2 partitions max (GLOBAL + tenantId)
- Point reads check tenant partition first (fast path for overrides)
- PayerConfig queries remain single-partition

### ? Extensibility
- Easy to add new GLOBAL payers (all tenants get access)
- Easy to add tenant-specific payers (only that tenant gets access)
- No schema changes needed to add either type

### ? Pattern Consistency
- Matches Lookups container pattern (GLOBAL + tenant-specific)
- Follows Azure Cosmos DB best practices
- Partition key strategy scales well

## Testing Checklist

**MVP Scope:**
- [ ] Seed script creates payers container with `/tenantId` partition key
- [ ] All payers are created with `tenantId = "GLOBAL"`
- [ ] PayerConfigs are stored in payers container (partitioned by tenantId)
- [ ] `SearchAsync()` queries only GLOBAL partition
- [ ] `GetByIdAsync()` reads from GLOBAL partition only
- [ ] `GetPayerConfigsAsync()` retrieves configs from payers container
- [ ] API endpoints work correctly:
  - [ ] `GET /api/payers?tenantId=ten_001` returns GLOBAL payers
  - [ ] `GET /api/payers/{payerId}?tenantId=ten_001` finds GLOBAL payer
  - [ ] `GET /api/config/{tenantId}/payers` returns tenant's payer configs

**Future Enhancement Checklist (when adding tenant-specific payers):**
- [ ] Uncomment tenant-specific payer in BuildPayers()
- [ ] Update SearchAsync() to include `OR c.tenantId = @tenantId`  
- [ ] Update GetByIdAsync() with tenant partition fallback logic
- [ ] Test that tenant-specific payers appear for correct tenant only
- [ ] Test that GLOBAL payers still appear for all tenants
