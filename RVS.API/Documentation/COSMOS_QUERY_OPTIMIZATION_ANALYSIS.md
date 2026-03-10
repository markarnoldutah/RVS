# Cosmos DB Query Optimization Analysis

## Executive Summary

Analysis of cross-partition queries in the BF codebase to identify optimization opportunities for reducing RU costs.

**Key Finding:** ? **One cross-partition query identified that CAN be optimized into point reads**

---

## Cross-Partition Query Inventory

### ? **Cannot Be Optimized (Legitimate Use Cases)**

These queries MUST be cross-partition by their nature and should remain as-is:

| Repository | Method | Query Type | Partitions Scanned | Why Cross-Partition? |
|-----------|--------|-----------|-------------------|---------------------|
| `CosmosPayerRepository` | `SearchAsync()` | Cross-partition | 2 (GLOBAL + tenantId) | **Intentional**: Returns GLOBAL payers + tenant-specific payers. This is the core feature. |
| `CosmosPatientRepository` | `SearchAsync()` | Single-partition | 1 (tenantId) | ? **Already optimized** with `PartitionKey = tenantId` |
| `CosmosConfigRepository` | `GetPayerConfigsAsync()` | Single-partition | 1 (tenantId) | ? **Already optimized** with `PartitionKey = tenantId` |

---

## ? Optimization Opportunity #1: GetPayerConfigAsync()

### Current Implementation (Query)
**File:** `BF.Infra.AzCosmosRepository\Repositories\CosmosConfigRepository.cs`  
**Method:** `GetPayerConfigAsync(string tenantId, string payerId)`

```csharp
// CURRENT: Using query with MaxItemCount = 1
var sql = "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.type = 'payerConfig' AND c.payerId = @payerId";
var queryDef = new QueryDefinition(sql)
    .WithParameter("@tenantId", tenantId)
    .WithParameter("@payerId", payerId);

var options = new QueryRequestOptions
{
    PartitionKey = new PartitionKey(tenantId),
    MaxItemCount = 1  // ?? Still a query, not a point read
};

var iterator = _payersContainer.GetItemQueryIterator<PayerConfig>(queryDef, requestOptions: options);
```

**Current RU Cost:** ~2.5-3 RU (single-partition query with filter)

### ? Recommended Optimization (Point Read)

```csharp
public async Task<PayerConfig?> GetPayerConfigAsync(string tenantId, string payerId)
{
    if (string.IsNullOrWhiteSpace(tenantId))
        throw new ArgumentException("tenantId is required.", nameof(tenantId));
    if (string.IsNullOrWhiteSpace(payerId))
        throw new ArgumentException("payerId is required.", nameof(payerId));

    // Generate the PayerConfig document ID
    // Based on seed data pattern: "ten_001_payer_vsp_001"
    var payerConfigId = $"{tenantId}_payer_{payerId.Replace("payer_", "")}";

    try
    {
        var response = await _payersContainer.ReadItemAsync<PayerConfig>(
            id: payerConfigId,
            partitionKey: new PartitionKey(tenantId));

        return response.Resource;
    }
    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        return null;
    }
}
```

**Optimized RU Cost:** ~1 RU (point read)

**Savings:** ~1.5-2 RU per call (60-67% reduction)

---

## Why This Optimization Works

### ID Pattern Analysis

Looking at the seed data in `BF.Data.Cosmos.Seed\Program.cs`:

```csharp
new PayerConfig
{
    Id = "ten_001_payer_vsp_001",  // ? Predictable pattern!
    TenantId = "ten_001",
    Type = "payerConfig",
    PayerId = "payer_vsp_001",
    // ...
}
```

**Pattern:** `{tenantId}_payer_{payerId.Replace("payer_", "")}`

Example mappings:
- tenantId=`ten_001`, payerId=`payer_vsp_001` ? ID=`ten_001_payer_vsp_001`
- tenantId=`ten_002`, payerId=`payer_bcbs_001` ? ID=`ten_002_payer_bcbs_001`

### Partition Key Strategy

PayerConfig documents are stored in the `payers` container with:
- **Partition Key:** `/tenantId`
- **Document ID:** `{tenantId}_payer_{cleanedPayerId}`

This makes it a perfect candidate for point reads!

---

## Implementation Plan

### Step 1: Update CosmosConfigRepository

**File:** `BF.Infra.AzCosmosRepository\Repositories\CosmosConfigRepository.cs`

Replace the query-based implementation with the point read version shown above.

### Step 2: Add Helper Method (Optional)

If the ID pattern might change in the future, encapsulate it:

```csharp
private static string GetPayerConfigId(string tenantId, string payerId)
{
    // Remove "payer_" prefix if present
    var cleanPayerId = payerId.StartsWith("payer_") 
        ? payerId.Substring(6) 
        : payerId;
    
    return $"{tenantId}_payer_{cleanPayerId}";
}

public async Task<PayerConfig?> GetPayerConfigAsync(string tenantId, string payerId)
{
    // ...validation...
    
    var payerConfigId = GetPayerConfigId(tenantId, payerId);
    
    try
    {
        var response = await _payersContainer.ReadItemAsync<PayerConfig>(
            id: payerConfigId,
            partitionKey: new PartitionKey(tenantId));
        return response.Resource;
    }
    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        return null;
    }
}
```

### Step 3: Update SavePayerConfigAsync (Consistency)

Ensure the save method uses the same ID pattern:

```csharp
public async Task SavePayerConfigAsync(string tenantId, PayerConfig payerConfig)
{
    // ...validation...
    
    // Ensure ID follows the pattern
    var expectedId = GetPayerConfigId(tenantId, payerConfig.PayerId);
    if (string.IsNullOrWhiteSpace(payerConfig.Id))
    {
        payerConfig.Id = expectedId;
    }
    else if (payerConfig.Id != expectedId)
    {
        throw new ArgumentException(
            $"PayerConfig ID '{payerConfig.Id}' does not match expected pattern '{expectedId}'.", 
            nameof(payerConfig));
    }
    
    // ...rest of save logic...
}
```

### Step 4: Update Tests

Update any tests that rely on the query behavior:

```csharp
[Test]
public async Task GetPayerConfigAsync_ExistingConfig_ReturnsConfig()
{
    // Arrange
    var tenantId = "ten_001";
    var payerId = "payer_vsp_001";
    var expectedId = "ten_001_payer_vsp_001";  // ? Verify ID pattern
    
    // Act
    var result = await _repo.GetPayerConfigAsync(tenantId, payerId);
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal(expectedId, result.Id);
}
```

---

## Performance Impact Analysis

### Scenario: Get PayerConfig for Tenant

**Current Approach (Query):**
```
Request: GET /api/config/{tenantId}/payers/{payerId}
?
Query: SELECT * FROM c WHERE c.tenantId = @tenantId AND c.payerId = @payerId
?
RU Cost: ~2.5-3 RU
```

**Optimized Approach (Point Read):**
```
Request: GET /api/config/{tenantId}/payers/{payerId}
?
Point Read: ReadItemAsync(id: "ten_001_payer_vsp_001", pk: "ten_001")
?
RU Cost: ~1 RU
```

### At Scale

Assuming 1,000 PayerConfig reads per day:

| Metric | Current (Query) | Optimized (Point Read) | Savings |
|--------|----------------|----------------------|---------|
| RU per call | 2.5 RU | 1 RU | 1.5 RU |
| Daily RUs | 2,500 RU | 1,000 RU | 1,500 RU |
| Monthly RUs (30 days) | 75,000 RU | 30,000 RU | 45,000 RU |
| **Cost Savings** (at $0.008/10K RU) | - | - | **~$0.036/month** |

For higher volumes (10,000 calls/day):
- **Monthly Savings:** ~$0.36/month
- **Annual Savings:** ~$4.32/year

*Note: Actual savings depend on your RU provisioning model (manual vs. autoscale) and request patterns.*

---

## Additional Optimization Opportunities

### GetTenantConfigAsync - Already Optimized! ?

**File:** `BF.Infra.AzCosmosRepository\Repositories\CosmosConfigRepository.cs`

```csharp
public async Task<TenantConfig?> GetTenantConfigAsync(string tenantId)
{
    try
    {
        var resp = await _tenantsContainer.ReadItemAsync<TenantConfig>(
            id: $"{tenantId}_config",  // ? Predictable ID pattern
            partitionKey: new PartitionKey(tenantId));
        return resp.Resource;
    }
    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        return null;
    }
}
```

**This is already a point read!** No optimization needed.

---

## Queries That SHOULD Remain Cross-Partition

### CosmosPayerRepository.SearchAsync() - Intentional Design

```csharp
// Query both GLOBAL and tenant-specific payers
var sql = "SELECT * FROM c WHERE c.type = 'payer' AND (c.tenantId = 'GLOBAL' OR c.tenantId = @tenantId)";
```

**Why this is correct:**
- ? Returns GLOBAL payers (shared across all tenants)
- ? Returns tenant-specific payers (e.g., CA Medicaid for ten_001)
- ? This is the core multi-tenancy feature
- ? Only scans 2 partitions max (GLOBAL + specific tenant)

**RU Cost:** ~3-7 RU (acceptable for this use case)

**Should NOT be converted to point reads** because:
1. Unknown number of payers per tenant
2. Need to return combined GLOBAL + tenant payers
3. Filtering by `planType` and `search` term required

---

## Summary of Recommendations

### ? **Implement This Optimization**

| Repository | Method | Change | RU Savings | Effort |
|-----------|--------|--------|-----------|--------|
| `CosmosConfigRepository` | `GetPayerConfigAsync()` | Query ? Point Read | ~60% | Low |

### ? **Already Optimized - No Action Needed**

| Repository | Method | Status |
|-----------|--------|--------|
| `CosmosConfigRepository` | `GetTenantConfigAsync()` | ? Point read |
| `CosmosPatientRepository` | `SearchAsync()` | ? Single-partition |
| `CosmosPatientRepository` | `GetByIdAsync()` | ? Point read |
| `CosmosPayerRepository` | `GetByIdAsync()` | ? Point read with fallback |

### ? **Should NOT Optimize**

| Repository | Method | Reason |
|-----------|--------|--------|
| `CosmosPayerRepository` | `SearchAsync()` | Intentional cross-partition design (GLOBAL + tenant) |

---

## Implementation Checklist

- [ ] Update `CosmosConfigRepository.GetPayerConfigAsync()` to use point read
- [ ] Add helper method `GetPayerConfigId()` for ID generation
- [ ] Update `SavePayerConfigAsync()` to ensure ID consistency
- [ ] Update unit tests to verify point read behavior
- [ ] Run integration tests
- [ ] Verify RU cost reduction in Azure portal metrics
- [ ] Update documentation

---

## Monitoring After Implementation

### Before vs. After Metrics

Monitor these in Azure Portal ? Metrics:

| Metric | Location | Expected Change |
|--------|----------|-----------------|
| Total Request Units | Cosmos DB Account | ?? Decrease by ~1.5 RU per GetPayerConfig call |
| Normalized RU Consumption | Containers ? payers | ?? Decrease for point reads |
| 2xx Success | Containers ? payers | ?? No change (still 200 OK) |

---

## Conclusion

**One optimization opportunity identified:**

? **CosmosConfigRepository.GetPayerConfigAsync()** can be converted from a query to a point read, saving ~60% RUs per call.

**All other queries are either:**
- ? Already optimized (point reads or single-partition queries)
- ? Intentionally cross-partition by design (GLOBAL payer feature)

The recommended optimization is **low effort** and provides **measurable RU savings** with no functional changes.

---

**Analysis Date:** 2025-01-20  
**Reviewed Files:**
- `CosmosPayerRepository.cs`
- `CosmosConfigRepository.cs`
- `CosmosPatientRepository.cs`
- `BF.Data.Cosmos.Seed\Program.cs`
