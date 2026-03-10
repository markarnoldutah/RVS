# Complete Embedded Documents CRUD Pattern - Summary

## Pattern Overview

This document summarizes the recommended patterns for managing embedded documents in Azure Cosmos DB

## Core Principles

### 1. Read-Through-Parent
Always read embedded entities through the parent document.

```csharp
// ? Single read operation
var patient = await _patientRepo.GetByIdAsync(tenantId, patientId);
var coverage = patient.CoverageEnrollments
    .FirstOrDefault(c => c.CoverageEnrollmentId == id);
```

### 2. Choose the Right Write Pattern

| Scenario | Pattern | Why |
|----------|---------|-----|
| **Add to array** | Replace or Patch Add | Patch preferred for large docs |
| **Update 1-2 fields** | **Patch Replace** ? | 60% RU savings |
| **Update many fields** | Replace | Simpler code |
| **Multi-region** | **Patch** ? | Path-level conflict resolution |
| **Single object update** | **Patch Replace** ? | Most efficient |

### 3. Atomic Operations
All embedded document operations are atomic with their parent.

## Implementation Patterns

### Pattern A: Replace Entire Document (Simpler)

**When to Use:**
- Small documents (<100KB)
- Updating multiple fields
- Simpler code preferred over performance

**Implementation:**
```csharp
public async Task<TEntity> UpdateEmbeddedAsync(
    string parentId,
    string embeddedId,
    Action<TEntity> updateAction)
{
    // 1. Read parent
    var parent = await GetByIdAsync(parentId);
    
    // 2. Find and modify embedded entity
    var entity = parent.EmbeddedList.Find(e => e.Id == embeddedId);
    updateAction(entity);
    
    // 3. Replace entire parent
    parent.UpdatedAtUtc = DateTime.UtcNow;
    await _container.ReplaceItemAsync(parent, parentId, new PartitionKey(tenantId));
    
    return entity;
}
```

**Benefits:**
- ? Simple, straightforward code
- ? Easy to understand and maintain
- ? Atomic with parent

**Drawbacks:**
- ?? Higher RU costs (entire document sent)
- ?? Document-level conflict resolution (LWW)
- ?? More network bandwidth

### Pattern B: Patch API (Recommended)

**When to Use:**
- Large documents (>100KB)
- Updating few fields
- Multi-region deployments
- Performance-critical operations

**Implementation:**
```csharp
// Single Object Update
public async Task<CoverageDecision> SetCoverageDecisionAsync(...)
{
    var patchOps = new List<PatchOperation>
    {
        PatchOperation.Replace("/coverageDecision", newDecision)
    };
    
    var response = await _container.PatchItemAsync<TParent>(
        parentId, new PartitionKey(tenantId), patchOps);
    
    return response.Resource.CoverageDecision;
}

// Array Element Add
public async Task<TEmbedded> AddToArrayAsync(...)
{
    var patchOps = new List<PatchOperation>
    {
        PatchOperation.Add("/embeddedArray/-", newEntity)  // "-" appends
    };
    
    var response = await _container.PatchItemAsync<TParent>(
        parentId, new PartitionKey(tenantId), patchOps);
    
    return response.Resource.EmbeddedArray.Last();
}

// Array Element Update
public async Task<TEmbedded> UpdateArrayElementAsync(...)
{
    // Read to find index
    var parent = await GetByIdAsync(parentId);
    var index = parent.EmbeddedArray.FindIndex(e => e.Id == embeddedId);
    
    // Apply updates locally
    var entity = parent.EmbeddedArray[index];
    updateAction(entity);
    
    // Patch specific fields
    var patchOps = new List<PatchOperation>
    {
        PatchOperation.Replace($"/embeddedArray/{index}/field1", entity.Field1),
        PatchOperation.Replace($"/embeddedArray/{index}/field2", entity.Field2)
    };
    
    var response = await _container.PatchItemAsync<TParent>(
        parentId, new PartitionKey(tenantId), patchOps);
    
    return response.Resource.EmbeddedArray[index];
}
```

**Benefits:**
- ? **60% lower RU costs**
- ? Path-level conflict resolution (multi-region)
- ? Reduced network bandwidth
- ? Better performance

**Drawbacks:**
- ?? Slightly more complex code
- ?? Need to find array index first
- ?? Requires .NET SDK 3.23.0+

### Pattern C: Hybrid Approach (Best of Both)

**Recommendation:**
- Use **Replace** for adding to arrays (simpler)
- Use **Patch** for updating existing elements (more efficient)
- Use **Patch** for single object updates (most efficient)

```csharp
public class CosmosEncounterRepository
{
    // ? Add: Use Replace (simpler, one-time operation)
    public async Task<EligibilityCheck> AddCheckAsync(...)
    {
        var encounter = await GetByIdAsync(encounterId);
        encounter.EligibilityChecks.Add(newCheck);
        await _container.ReplaceItemAsync(encounter, ...);
        return newCheck;
    }
    
    // ? Update: Use Patch (frequent operation, better perf)
    public async Task<EligibilityCheck> UpdateCheckAsync(...)
    {
        var encounter = await GetByIdAsync(encounterId);
        var index = encounter.EligibilityChecks.FindIndex(...);
        var check = encounter.EligibilityChecks[index];
        updateAction(check);
        
        var patchOps = new List<PatchOperation>
        {
            PatchOperation.Replace($"/eligibilityChecks/{index}/status", check.Status),
            PatchOperation.Replace($"/eligibilityChecks/{index}/completedAtUtc", check.CompletedAtUtc)
        };
        
        var response = await _container.PatchItemAsync<Encounter>(..., patchOps);
        return response.Resource.EligibilityChecks[index];
    }
}
```

## Common Patterns by Entity Type

### Patient ? CoverageEnrollment (1-to-Few)

**Characteristics:**
- Typically 1-3 enrollments
- Updates infrequent
- Accessed with patient data

**Recommended Pattern:** Replace (simpler)
- Documents stay small
- Performance difference minimal
- Code clarity preferred

### Encounter ? EligibilityCheck (1-to-Many)

**Characteristics:**
- Can grow to 10+ checks
- Updates frequent (after API calls)
- Large nested data (coverage lines, payloads)

**Recommended Pattern:** Hybrid
- Add: Replace (one-time)
- Update: **Patch** (frequent, better RU savings)
- Large documents benefit from Patch efficiency

### Encounter ? CoverageDecision (1-to-One)

**Characteristics:**
- Single embedded object
- Occasionally updated
- Small object

**Recommended Pattern:** **Patch** (best efficiency)
- Single PatchOperation.Replace()
- Minimal RU cost
- Simple implementation

## Implementation Checklist

- [ ] Define clear entity relationship (1-to-1, 1-to-few, 1-to-many)
- [ ] Estimate document sizes and growth patterns
- [ ] Choose write pattern (Replace vs Patch vs Hybrid)
- [ ] Implement repository methods
- [ ] Add repository interface methods
- [ ] Update service layer
- [ ] Update DI registrations
- [ ] Add integration tests
- [ ] Monitor RU consumption
- [ ] Document pattern choice and rationale

## Performance Monitoring

```csharp
// Log RU consumption for analysis
var response = await _container.PatchItemAsync<T>(...);
_logger.LogInformation(
    "Patch operation consumed {RequestCharge} RUs for {Operation}",
    response.RequestCharge,
    nameof(UpdateEligibilityCheckAsync));
```

## Anti-Patterns to Avoid

? **Don't** use separate containers for embedded entities  
? **Don't** use LINQ queries to filter embedded collections  
? **Don't** forget to set timestamps (CreatedAtUtc, UpdatedAtUtc)  
? **Don't** ignore document size limits (2MB max)  
? **Don't** skip optimistic concurrency (ETags) in production  
? **Don't** patch entire embedded objects when only updating 1-2 fields  

## Decision Tree

```
Is the embedded entity...

?? A single object (1-to-1)?
?  ?? ? Use Patch Replace
?
?? A small array (1-3 items)?
?  ?? Adding new items?
?  ?  ?? ? Use Replace (simpler)
?  ?? Updating existing?
?     ?? ? Use Patch if multi-region, else Replace
?
?? A larger array (>3 items) or growing unbounded?
   ?? Adding new items?
   ?  ?? ? Use Patch Add
   ?? Updating existing?
      ?? ? Use Patch Replace (fields)
```

## References

- [Azure Cosmos DB Partial Document Update](https://learn.microsoft.com/en-us/azure/cosmos-db/partial-document-update)
- [Cosmos DB Data Modeling](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/modeling-data)
- [Patch API Getting Started](https://learn.microsoft.com/en-us/azure/cosmos-db/partial-document-update-getting-started)
