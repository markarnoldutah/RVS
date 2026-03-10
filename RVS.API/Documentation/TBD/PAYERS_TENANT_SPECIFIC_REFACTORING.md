# Payers Repository Refactoring - Support for Tenant-Specific Payers

## Overview
This refactoring extends the Payers repository to support both **GLOBAL payers** (shared across all tenants) and **tenant-specific payers** (available only to specific tenants).

## Changes Made

### 1. CosmosPayerRepository.cs

#### SearchAsync Method
**Before (MVP - GLOBAL only):**
```csharp
var sql = "SELECT * FROM c WHERE c.type = 'payer' AND c.tenantId = 'GLOBAL'";
var options = new QueryRequestOptions
{
    PartitionKey = new PartitionKey("GLOBAL")
};
```

**After (Full Support):**
```csharp
var sql = "SELECT * FROM c WHERE c.type = 'payer' AND (c.tenantId = 'GLOBAL' OR c.tenantId = @tenantId)";
var queryDef = new QueryDefinition(sql).WithParameter("@tenantId", tenantId);
// Cross-partition query - no partition key restriction
var iterator = _container.GetItemQueryIterator<Payer>(queryDef);
```

**Benefits:**
- ? Returns both GLOBAL and tenant-specific payers in single query
- ? Cross-partition query hits max 2 partitions (GLOBAL + tenantId)
- ? Maintains backward compatibility (still returns GLOBAL payers)

#### GetByIdAsync Method
**Before (MVP - GLOBAL only):**
```csharp
var resp = await _container.ReadItemAsync<Payer>(
    id: payerId,
    partitionKey: new PartitionKey("GLOBAL"));
```

**After (Full Support with Fallback):**
```csharp
// Try tenant-specific payer first
try
{
    var resp = await _container.ReadItemAsync<Payer>(
        id: payerId,
        partitionKey: new PartitionKey(tenantId));
    return resp.Resource;
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    // Fall back to GLOBAL payer
    try
    {
        var resp = await _container.ReadItemAsync<Payer>(
            id: payerId,
            partitionKey: new PartitionKey("GLOBAL"));
        return resp.Resource;
    }
    catch (CosmosException globalEx) when (globalEx.StatusCode == HttpStatusCode.NotFound)
    {
        return null;
    }
}
```

**Benefits:**
- ? Tenant-specific payers can override GLOBAL payers (checked first)
- ? Falls back to GLOBAL if no tenant-specific override exists
- ? Efficient point reads (no query, just 1-2 partition reads)
- ? Graceful error handling

### 2. Program.cs (Seed Script)

#### Uncommented Tenant-Specific Payer
```csharp
new Payer
{
    Id = "payer_medicaid_ca_001",
    Type = "payer",
    TenantId = "ten_001",  // Tenant-specific for California tenant
    Name = "California Medicaid (Medi-Cal)",
    PlanType = "Medical",
    AvailityPayerCode = "MEDICAID_CA",
    X12PayerId = "68069",
    IsMedicare = false,
    IsMedicaid = true,
    CreatedAtUtc = new DateTime(2024, 2, 15, 9, 0, 0, DateTimeKind.Utc),
    UpdatedAtUtc = new DateTime(2024, 12, 10, 11, 0, 0, DateTimeKind.Utc)
}
```

#### Added PayerConfig for Tenant-Specific Payer
```csharp
new PayerConfig
{
    Id = "ten_001_payer_medicaid_ca_001",
    TenantId = "ten_001",
    Type = "payerConfig",
    PayerId = "payer_medicaid_ca_001",
    PracticeId = null,
    IsEnabled = true,
    SortOrder = 40,
    DisplayName = "CA Medicaid (Medi-Cal)",
    CobDefaultRole = "PrimaryMedical",
    CreatedAtUtc = new DateTime(2025, 1, 20, 9, 18, 0, DateTimeKind.Utc),
    UpdatedAtUtc = new DateTime(2025, 1, 20, 9, 18, 0, DateTimeKind.Utc)
}
```

## Data Model

### Payers Container (PK: /tenantId)

```
GLOBAL Partition
??? payer_vsp_001 (VSP - Vision)
??? payer_bcbs_001 (BCBS - Medical)
??? payer_eyemed_001 (EyeMed - Vision)
??? payer_medicare_001 (Medicare - Medical)

ten_001 Partition
??? payer_medicaid_ca_001 (CA Medicaid - Medical) ? Tenant-specific
??? PayerConfig: ten_001_payer_vsp_001
??? PayerConfig: ten_001_payer_eyemed_001
??? PayerConfig: ten_001_payer_bcbs_001
??? PayerConfig: ten_001_payer_medicaid_ca_001

ten_002 Partition
??? PayerConfig: ten_002_payer_vsp_001
??? PayerConfig: ten_002_payer_bcbs_001

ten_003 Partition
??? PayerConfig: ten_003_payer_medicare_001
??? PayerConfig: ten_003_payer_bcbs_001
```

## Use Cases

### Use Case 1: National Payers (GLOBAL)
All tenants need access to major national payers like VSP, BCBS, Medicare.

**Solution:** Store in GLOBAL partition
- All tenants get access automatically
- Single source of truth
- Easy to update centrally

### Use Case 2: State-Specific Payers
California tenants need Medi-Cal, New York tenants need NY Medicaid.

**Solution:** Store in tenant-specific partition
- Only relevant tenants see state-specific payers
- Can have different configurations per state
- Doesn't clutter other tenants' payer lists

### Use Case 3: Tenant Override
A large tenant negotiates custom rates with BCBS and needs a tenant-specific entry.

**Solution:** Create tenant-specific payer with same ID
- Tenant-specific version checked first
- Falls back to GLOBAL if not found
- Other tenants still use GLOBAL version

## Query Examples

### Example 1: Get All Payers for Tenant
```csharp
var payers = await payerRepository.SearchAsync("ten_001", null, null);
```

**Returns:**
- payer_vsp_001 (GLOBAL)
- payer_bcbs_001 (GLOBAL)
- payer_eyemed_001 (GLOBAL)
- payer_medicare_001 (GLOBAL)
- payer_medicaid_ca_001 (ten_001 specific) ? Only for ten_001!

### Example 2: Get Vision Payers Only
```csharp
var visionPayers = await payerRepository.SearchAsync("ten_001", "Vision", null);
```

**Returns:**
- payer_vsp_001 (GLOBAL)
- payer_eyemed_001 (GLOBAL)

### Example 3: Search by Name
```csharp
var medicarePayers = await payerRepository.SearchAsync("ten_001", null, "medicare");
```

**Returns:**
- payer_medicare_001 (GLOBAL)

### Example 4: Get Specific Payer
```csharp
// For ten_001: Returns tenant-specific Medicaid
var medicaid = await payerRepository.GetByIdAsync("ten_001", "payer_medicaid_ca_001");

// For ten_002: Returns null (not found in either partition)
var medicaid = await payerRepository.GetByIdAsync("ten_002", "payer_medicaid_ca_001");
```

## Performance Considerations

### SearchAsync (Cross-Partition Query)
- **Before:** 1 partition read (GLOBAL only)
- **After:** 2 partition reads max (GLOBAL + tenantId)
- **Impact:** Minimal - only 2 partitions scanned
- **RU Cost:** Slightly higher but still efficient

### GetByIdAsync (Point Reads)
- **Before:** 1 point read (GLOBAL)
- **After:** 1-2 point reads (tenant first, then GLOBAL fallback)
- **Impact:** 
  - Best case: 1 read (tenant-specific exists)
  - Typical case: 2 reads (not found in tenant, read from GLOBAL)
- **RU Cost:** Point reads are cheap (~1 RU each)

## Testing Strategy

### Unit Tests Needed
1. ? SearchAsync returns GLOBAL payers for all tenants
2. ? SearchAsync returns tenant-specific payers only for that tenant
3. ? SearchAsync combines both GLOBAL and tenant-specific
4. ? GetByIdAsync finds tenant-specific payer
5. ? GetByIdAsync falls back to GLOBAL payer
6. ? GetByIdAsync returns null if not found in either
7. ? Filtering by planType works for both types
8. ? Text search works for both types

### Integration Tests Needed
1. ? Seed script creates both GLOBAL and tenant-specific payers
2. ? API returns correct payers for each tenant
3. ? PayerConfigs link correctly to both payer types
4. ? Cross-tenant isolation maintained

### Postman Tests to Update
1. Update "Get All Payers (ten_001)" to expect 5 payers (4 GLOBAL + 1 tenant-specific)
2. Update "Get All Payers (ten_002)" to expect 4 payers (4 GLOBAL only)
3. Add test for tenant-specific payer (CA Medicaid)
4. Add test to verify ten_002 cannot see CA Medicaid

## Migration Path

### For Existing Deployments

**Step 1: Update Code**
- ? Deploy updated `CosmosPayerRepository` with new query logic
- ? Code is backward compatible (still works with GLOBAL-only data)

**Step 2: Add Tenant-Specific Payers (Optional)**
- Run seed script OR manually add tenant-specific payers
- Each tenant can have 0 or more tenant-specific payers

**Step 3: Add PayerConfigs for Tenant-Specific Payers**
- Run seed script OR use API to add configs
- Links tenant-specific payers to tenants

**No Breaking Changes:**
- Existing GLOBAL payers continue to work
- Existing PayerConfigs continue to work
- New functionality is additive

## Benefits Summary

? **Flexibility**: Support both shared and tenant-specific payers  
? **Performance**: Efficient queries (max 2 partitions)  
? **Scalability**: Easy to add new payers of either type  
? **Isolation**: Tenant-specific payers only visible to that tenant  
? **Override Capability**: Tenants can override GLOBAL payers  
? **Backward Compatible**: Existing GLOBAL payers work unchanged  
? **Cost Effective**: Minimal RU overhead for added functionality  

## Next Steps

1. ? Code changes complete
2. ? Seed data updated
3. ? Documentation updated
4. [ ] Run seed script to populate test data
5. [ ] Run Postman tests to validate
6. [ ] Update unit tests
7. [ ] Deploy to dev environment
8. [ ] Validate with real-world scenarios

---

**Refactoring Date:** 2025-01-20  
**Status:** ? Complete  
**Backward Compatible:** Yes  
**Breaking Changes:** None
