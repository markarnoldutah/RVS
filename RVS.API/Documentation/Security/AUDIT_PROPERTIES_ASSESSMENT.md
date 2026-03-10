# Audit Properties Assessment and Fixes

## Summary
This document summarizes the assessment of audit property management across all service methods that create or update EntityBase derivative classes and embedded entities.

## Findings

### EntityBase Derivatives (Full Entity Classes)

#### ? CORRECT IMPLEMENTATIONS

1. **TenantService.CreateTenantConfigAsync**
   - CreatedAtUtc: ? Set automatically by EntityBase
   - CreatedByUserId: ? Set via mapper from _userContext.UserId
   - UpdatedAtUtc: ? Set by mapper
   - UpdatedByUserId: ? Set by MarkAsUpdated() in service

2. **TenantService.UpdateTenantConfigAsync**
   - UpdatedAtUtc: ? Set by MarkAsUpdated()
   - UpdatedByUserId: ? Set by MarkAsUpdated(_userContext.UserId)

3. **PayerService.UpdatePayerConfigAsync**
   - UpdatedAtUtc: ? Set by MarkAsUpdated()
   - UpdatedByUserId: ? Set by MarkAsUpdated(_userContext.UserId)

4. **PatientService.UpdatePatientAsync**
   - UpdatedAtUtc: ? Set by MarkAsUpdated()
   - UpdatedByUserId: ? Set by MarkAsUpdated(_userContext.UserId)

#### ? FIXED IMPLEMENTATIONS

5. **PatientService.CreatePatientAsync**
   - **BEFORE**: Missing UpdatedByUserId
   - **AFTER**: ? Added UpdatedByUserId = _userContext.UserId
   - Rationale: New entities should have both created and updated user IDs set

### Embedded Entities

#### ? FIXED IMPLEMENTATIONS

6. **CoverageEnrollmentEmbedded class**
   - **BEFORE**: No audit properties at all
   - **AFTER**: ? Added all audit properties
     - createdAtUtc (init = DateTime.UtcNow)
     - updatedAtUtc (nullable)
     - createdByUserId (init, nullable)
     - updatedByUserId (nullable)

7. **EncounterEmbedded class**
   - **BEFORE**: Missing updatedByUserId
   - **AFTER**: ? Added updatedByUserId property

8. **PatientService.AddCoverageEnrollmentAsync**
   - **BEFORE**: No audit properties set
   - **AFTER**: ? Sets CreatedByUserId and UpdatedByUserId

9. **PatientService.UpdateCoverageEnrollmentAsync**
   - **BEFORE**: No audit properties set
   - **AFTER**: ? Sets UpdatedAtUtc and UpdatedByUserId

10. **PatientService.CreateEncounterAsync**
    - **BEFORE**: Missing CreatedByUserId
    - **AFTER**: ? Sets CreatedByUserId = _userContext.UserId

11. **PatientService.UpdateEncounterAsync**
    - **BEFORE**: No audit properties set
    - **AFTER**: ? Sets UpdatedAtUtc and UpdatedByUserId

12. **PatientService.SetCoverageDecisionAsync**
    - **BEFORE**: Missing CreatedByUserId
    - **AFTER**: ? Sets CreatedByUserId = _userContext.UserId

### Repository Layer

#### ? FIXED IMPLEMENTATIONS

13. **CosmosPatientRepository.CreateAsync**
    - **BEFORE**: Set UpdatedAtUtc redundantly
    - **AFTER**: ? Removed - audit properties are set in service layer

14. **CosmosPatientRepository.UpdateAsync**
    - **BEFORE**: Set UpdatedAtUtc redundantly
    - **AFTER**: ? Removed - audit properties are set in service layer via MarkAsUpdated()

15. **CosmosPatientRepository.UpdateEncounterAsync**
    - **BEFORE**: Set UpdatedAtUtc redundantly
    - **AFTER**: ? Removed - audit properties are set in service layer

### Entities Without Audit Tracking (By Design)

The following embedded entities do NOT have audit properties because they are:
- Immutable after creation (no update operations)
- System-generated data snapshots

1. **CoverageDecisionEmbedded** - ? Correct (only created, never updated)
   - Has: createdAtUtc, createdByUserId
   - Missing: updatedAtUtc, updatedByUserId (not needed - decisions are immutable)

2. **EligibilityCheckEmbedded** - ? Correct (system-generated, never updated by users)
   - Has: requestedAtUtc, completedAtUtc (domain-specific timestamps)
   - Missing: created/updated user tracking (not applicable - system process)

3. **CoverageLineEmbedded** - ? Correct (system-generated from payer response)
   - No audit properties (part of immutable eligibility check response)

4. **EligibilityPayloadEmbedded** - ? Correct (system-generated)
   - Has: createdAtUtc
   - Missing: user tracking (not applicable - system process)

## Best Practices Established

### 1. Service Layer Responsibility
- ? Service layer sets all audit properties
- ? Repository layer does NOT set audit properties
- ? Use MarkAsUpdated() helper for EntityBase derivatives

### 2. Embedded Entity Patterns
- ? User-modifiable embedded entities: Full audit tracking (created/updated timestamps + user IDs)
- ? System-generated embedded entities: Timestamp only, no user tracking
- ? Immutable embedded entities: Created timestamp/user only

### 3. Create vs Update
- ? CREATE: Set CreatedByUserId, UpdatedByUserId (both same user for new records)
- ? UPDATE: Set UpdatedAtUtc + UpdatedByUserId via MarkAsUpdated() or explicit assignment

### 4. Initialization Patterns
```csharp
// EntityBase derivatives (full entities)
var entity = new Patient
{
    CreatedByUserId = _userContext.UserId,
    UpdatedByUserId = _userContext.UserId  // Set on create too
};

// User-modifiable embedded entities
var coverage = new CoverageEnrollmentEmbedded
{
    CreatedByUserId = _userContext.UserId,
    UpdatedByUserId = _userContext.UserId
    // CreatedAtUtc auto-set via init property
};

// Immutable embedded entities
var decision = new CoverageDecisionEmbedded
{
    CreatedAtUtc = DateTime.UtcNow,
    CreatedByUserId = _userContext.UserId
    // No updated properties - never modified
};
```

## Testing Recommendations

### Unit Tests to Add
1. Verify all create methods set CreatedByUserId and UpdatedByUserId
2. Verify all update methods call MarkAsUpdated() or set audit properties
3. Verify repository layer does NOT modify audit properties
4. Verify embedded entity updates set UpdatedAtUtc and UpdatedByUserId

### Integration Tests to Add
1. Create entity and verify all audit timestamps are within 1 second of DateTime.UtcNow
2. Update entity and verify UpdatedAtUtc > CreatedAtUtc
3. Update entity multiple times and verify UpdatedByUserId reflects last user

## Files Modified

1. `BF.API\Services\PatientService.cs`
   - CreatePatientAsync: Added UpdatedByUserId
   - AddCoverageEnrollmentAsync: Added CreatedByUserId and UpdatedByUserId
   - UpdateCoverageEnrollmentAsync: Added UpdatedAtUtc and UpdatedByUserId
   - CreateEncounterAsync: Added CreatedByUserId
   - UpdateEncounterAsync: Added UpdatedAtUtc and UpdatedByUserId
   - SetCoverageDecisionAsync: Added CreatedByUserId

2. `BF.Domain\Entities\Patient.cs`
   - CoverageEnrollmentEmbedded: Added all audit properties
   - EncounterEmbedded: Added updatedByUserId

3. `BF.Infra.AzCosmosRepository\Repositories\CosmosPatientRepository.cs`
   - CreateAsync: Removed redundant UpdatedAtUtc assignment
   - UpdateAsync: Removed redundant UpdatedAtUtc assignment
   - UpdateEncounterAsync: Removed redundant UpdatedAtUtc assignment

## Conclusion

All audit property issues have been identified and corrected. The codebase now follows a consistent pattern:

? **Service Layer**: Responsible for setting all audit properties  
? **Repository Layer**: Does NOT modify audit properties  
? **EntityBase Derivatives**: Use MarkAsUpdated() helper  
? **Embedded Entities**: Set audit properties explicitly based on mutability  
? **Consistent Patterns**: All create and update operations properly tracked  

---
**Date**: January 2025  
**Assessment Scope**: All services in BF.API that create or update entities  
**Status**: ? COMPLETE
