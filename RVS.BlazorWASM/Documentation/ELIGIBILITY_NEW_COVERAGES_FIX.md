# Eligibility Checks for New Patients/Coverages - FIXED

## ? **Problem**

When adding a new patient with new coverages during check-in, eligibility checks were not being activated.

### **Symptoms:**
- ? New patient created successfully
- ? New coverages created successfully
- ? New encounter created successfully
- ? Eligibility checks NOT run (skipped with warning)

### **Error in API Logs:**
```
Coverage enrollment new-coverage-0 not found - skipping eligibility check.
```

---

## ?? **Root Cause Analysis**

### **The Problem Flow**

**1. Client sends check-in request with new coverage:**
```json
{
  "patientId": null,  // New patient
  "coverageEnrollments": [
    {
      "coverageEnrollmentId": null,  // New coverage
      "payerId": "payer_vsp_001",
      "memberId": "VSP123456"
    }
  ],
  "eligibilityChecks": [
    {
      "coverageEnrollmentId": "new-coverage-0"  // ? Placeholder, not real ID
    }
  ]
}
```

**2. API creates coverage with generated ID:**
```csharp
// In Phase 2, coverage is created with a new ID
var coverage = new CoverageEnrollmentEmbedded
{
    CoverageEnrollmentId = Guid.NewGuid().ToString(),  // e.g., "abc-123-def"
    PayerId = "payer_vsp_001",
    MemberId = "VSP123456"
};
```

**3. API tries to find coverage for eligibility check:**
```csharp
// In Phase 6, looking for "new-coverage-0" but actual ID is "abc-123-def"
var coverage = patient.CoverageEnrollments?
    .FirstOrDefault(c => c.CoverageEnrollmentId == "new-coverage-0");  // ? NOT FOUND!
```

---

## ? **Solution**

### **1. Added `CoverageIndex` to `EligibilityCheckRunDto`**

**BF.Domain\DTOs\PatientCheckInRequestDto.cs:**
```csharp
public sealed record EligibilityCheckRunDto
{
    [Required]
    public string CoverageEnrollmentId { get; init; } = default!;

    /// <summary>
    /// Index of the coverage in the CoverageEnrollments array.
    /// Required when running eligibility for new coverages that don't have an ID yet.
    /// The API will correlate this index to the saved coverage's ID after persistence.
    /// </summary>
    public int? CoverageIndex { get; init; }  // ? NEW PROPERTY
    
    // ... other properties
}
```

### **2. Updated Client to Include `CoverageIndex`**

**BF.BlazorWASM\Features\CheckIn\CheckInState.cs:**
```csharp
public PatientCheckInRequestDto BuildRequest()
{
    // ...
    
    for (int i = 0; i < CoverageEnrollments.Count; i++)
    {
        var coverage = CoverageEnrollments[i];
        var shouldCheck = CoverageEnrollmentsToCheck[i];
        
        if (shouldCheck && !string.IsNullOrWhiteSpace(coverage.PayerId) && !string.IsNullOrWhiteSpace(coverage.MemberId))
        {
            eligibilityChecks.Add(new EligibilityCheckRunDto
            {
                CoverageEnrollmentId = coverage.CoverageEnrollmentId ?? $"new-coverage-{i}",
                CoverageIndex = string.IsNullOrEmpty(coverage.CoverageEnrollmentId) ? i : null  // ? SET INDEX FOR NEW
            });
        }
    }
    
    // ...
}
```

### **3. Updated API to Use `CoverageIndex` for Correlation**

**BF.API\Services\PatientCheckInService.cs:**
```csharp
// In Phase 6: Run Eligibility Checks
foreach (var eligibilityRequest in request.EligibilityChecks)
{
    CoverageEnrollmentEmbedded? coverage = null;

    // First, try to find by CoverageEnrollmentId (for existing coverages)
    if (!string.IsNullOrWhiteSpace(eligibilityRequest.CoverageEnrollmentId) 
        && !eligibilityRequest.CoverageEnrollmentId.StartsWith("new-coverage-"))
    {
        coverage = patient.CoverageEnrollments?
            .FirstOrDefault(c => c.CoverageEnrollmentId == eligibilityRequest.CoverageEnrollmentId);
    }
    
    // If not found by ID and CoverageIndex is provided, use the index
    if (coverage is null && eligibilityRequest.CoverageIndex.HasValue)
    {
        var index = eligibilityRequest.CoverageIndex.Value;
        if (index >= 0 && index < coverageResults.Count)
        {
            var covResult = coverageResults[index];
            coverage = patient.CoverageEnrollments?
                .FirstOrDefault(c => c.CoverageEnrollmentId == covResult.CoverageEnrollmentId);
        }
    }

    if (coverage is null)
    {
        warnings.Add($"Coverage enrollment not found - skipping eligibility check.");
        continue;
    }
    
    // Run eligibility check with the ACTUAL coverage ID
    var checkRequest = new EligibilityCheckRequestDto
    {
        CoverageEnrollmentId = coverage.CoverageEnrollmentId,  // ? Real ID!
        // ...
    };
    
    await _eligibilityCheckService.RunWithPatientAsync(patient, encounter.Id, checkRequest, ...);
}
```

### **4. Updated UI to Validate Required Fields**

**BF.BlazorWASM\Features\CheckIn\EligibilityCheckSelectionSection.razor:**
```razor
var hasRequiredFields = !string.IsNullOrEmpty(coverage.PayerId) && !string.IsNullOrEmpty(coverage.MemberId);

<FluentCheckbox Value="@isChecked"
                Disabled="@(!hasRequiredFields)"  // ? Can't check if missing required fields
                ValueChanged="@((bool v) => ToggleEligibilityCheck(index, v))" />

@if (!hasRequiredFields)
{
    <FluentLabel Color="Color.Error">
        Payer and Member ID required for eligibility check
    </FluentLabel>
}
```

---

## ?? **Flow Diagram - After Fix**

```
New Patient Check-In Flow
==========================

1. User adds new coverage
   CoverageEnrollments: [
     { coverageEnrollmentId: null, payerId: "VSP", memberId: "123" }  // Index 0
   ]
   
2. UI marks for eligibility check
   CoverageEnrollmentsToCheck: [true]  // Index 0 = true

3. BuildRequest() creates:
   {
     eligibilityChecks: [
       { 
         coverageEnrollmentId: "new-coverage-0",  // Placeholder
         coverageIndex: 0                          // ? Index for correlation!
       }
     ]
   }

4. API Phase 2 creates coverage:
   CoverageEnrollmentId = "abc-123-def" (generated)
   coverageResults[0] = { coverageEnrollmentId: "abc-123-def", wasCreated: true }

5. API Phase 6 runs eligibility:
   - Sees coverageIndex: 0
   - Looks up coverageResults[0].coverageEnrollmentId = "abc-123-def"
   - Finds coverage with ID "abc-123-def" ?
   - Runs eligibility check with actual ID ?

6. Result:
   - Eligibility check runs successfully
   - Linked to the new coverage
   - Polling starts for results
```

---

## ?? **Testing**

### **Test Case: New Patient with New Coverage**

1. Navigate to Check-In page
2. Click "Create New Patient"
3. Enter patient demographics
4. Click "Add Coverage"
5. Select a Payer and enter Member ID
6. Verify the coverage shows in Eligibility Checks section with checkbox enabled
7. Ensure the coverage is checked (default)
8. Click "Check In & Verify Eligibility"
9. **Expected:**
   - ? Patient created
   - ? Coverage created
   - ? Encounter created
   - ? Eligibility check initiated (status: InProgress or Pending)
   - ? Polling starts for eligibility results

### **Test Case: Incomplete Coverage**

1. Add a new coverage
2. Leave Member ID empty
3. **Expected:**
   - ? Checkbox disabled
   - ? Warning message: "Payer and Member ID required for eligibility check"
   - ? Can still submit (but eligibility check won't run for this coverage)

---

## ?? **Files Changed**

### **BF.Domain\DTOs\PatientCheckInRequestDto.cs**
- Added `CoverageIndex` property to `EligibilityCheckRunDto`

### **BF.BlazorWASM\Features\CheckIn\CheckInState.cs**
- Updated `BuildRequest()` to set `CoverageIndex` for new coverages
- Added validation for PayerId and MemberId before including eligibility check

### **BF.BlazorWASM\Features\CheckIn\EligibilityCheckSelectionSection.razor**
- Added validation for required fields (PayerId, MemberId)
- Disabled checkbox if required fields missing
- Added "New" badge for new coverages
- Added synchronization of `CoverageEnrollmentsToCheck` list

### **BF.API\Services\PatientCheckInService.cs**
- Updated Phase 6 to correlate new coverages using `CoverageIndex`
- Added validation for required eligibility check fields
- Uses actual `CoverageEnrollmentId` (not placeholder) for eligibility check

---

## ? **Status**

**Status:** ? FIXED

**Root Cause:** New coverages were created with generated IDs, but eligibility checks referenced placeholder IDs that didn't exist.

**Solution:** Added `CoverageIndex` property to correlate new coverages with their eligibility checks by position in the array.

**Impact:** New patients with new coverages can now have eligibility checks run during check-in.
