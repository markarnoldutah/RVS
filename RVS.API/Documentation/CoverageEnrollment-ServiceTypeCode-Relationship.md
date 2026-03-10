# Coverage Enrollment and Service Type Code Relationship

## Overview

**Coverage Enrollment** and **Service Type Codes** are **loosely coupled** concepts that work together in the eligibility check workflow. They serve different but complementary purposes:

- **Coverage Enrollment** = "Which insurance plan are we checking?"
- **Service Type Code** = "What services do we want to verify coverage for?"

---

## What is Coverage Enrollment?

A `CoverageEnrollmentEmbedded` represents a **patient's enrollment in a specific insurance plan**. It's embedded within the `Patient` document in Cosmos DB.

### Structure

```csharp
// Embedded in Patient.CoverageEnrollments[]
public class CoverageEnrollmentEmbedded
{
    public string CoverageEnrollmentId { get; init; }  // Unique ID
    public string PayerId { get; init; }               // Insurance company (e.g., VSP, BCBS)
    public string PlanType { get; init; }              // "Vision", "Medical", "Dental"
    public string MemberId { get; init; }              // Patient's member ID
    public string? GroupNumber { get; init; }          // Group/plan number
    public DateOnly? EffectiveDate { get; init; }      // Coverage start date
    public DateOnly? TerminationDate { get; init; }    // Coverage end date
    public bool IsEnabled { get; init; }               // Active/inactive flag
    public byte? CobPriorityHint { get; init; }        // 1=primary, 2=secondary
    // ... additional fields
}
```

### Key Characteristics

| Property | Purpose | Example |
|----------|---------|---------|
| `PayerId` | Identifies the insurance company | `"payer_vsp_001"`, `"payer_bcbs_001"` |
| `PlanType` | Categorizes the insurance type | `"Vision"`, `"Medical"`, `"Dental"` |
| `MemberId` | Patient's ID with that payer | `"VSP87654321"`, `"BCBS-XYZ123456789"` |
| `CobPriorityHint` | Coordination of benefits order | `1` (primary), `2` (secondary) |

### Example: Patient with Multiple Coverage Enrollments

```csharp
var patient = new Patient
{
    FirstName = "Emily",
    LastName = "Rodriguez",
    CoverageEnrollments = 
    [
        // Vision insurance (primary for vision services)
        new CoverageEnrollmentEmbedded
        {
            CoverageEnrollmentId = "cov_001_vision",
            PayerId = "payer_vsp_001",
            PlanType = "Vision",
            MemberId = "VSP87654321",
            GroupNumber = "GRP-TECH-2024",
            CobPriorityHint = 1
        },
        
        // Medical insurance (primary for medical services)
        new CoverageEnrollmentEmbedded
        {
            CoverageEnrollmentId = "cov_001_medical",
            PayerId = "payer_bcbs_001",
            PlanType = "Medical",
            MemberId = "BCBS-XYZ123456789",
            GroupNumber = "TECHSOL-GRP-001",
            CobPriorityHint = 2  // Secondary for vision, primary for medical
        }
    ]
};
```

---

## What is Service Type Code?

A **Service Type Code** is a standardized **X12 EDI code** (from HIPAA 270/271 transactions) that specifies **which healthcare services** you want to check eligibility for.

### Common Codes

| Code | Service Type | Typical Use Case |
|------|--------------|------------------|
| **30** | Health Benefit Plan Coverage | General medical services (default) |
| **AL** | Vision (Optometry) | Eye exams, vision care |
| **AM** | Frames | Eyeglass frames |
| **AO** | Lenses | Eyeglass lenses |
| **AN** | Routine Exam | Routine eye examination |
| **BR** | Eye Care (Ophthalmology) | Medical ophthalmology services |
| **35** | Dental Care | General dental services |
| **98** | Professional (Physician) Visit - Office | Doctor office visits |
| **47** | Hospital | Hospital services |
| **88** | Pharmacy | Prescription drugs |

### Source

These codes originate from the **ANSI X12 270/271 EDI transaction set** for Healthcare Eligibility Benefit Inquiry and Response, standardized by CMS (Centers for Medicare & Medicaid Services).

---

## How They Work Together

### The Relationship Model

```
???????????????????????????????????????????????????????
? Patient                                             ?
? ??????????????????????????????????????????????????? ?
? ? Coverage Enrollment 1 (Vision - VSP)            ? ?
? ? - PayerId: payer_vsp_001                        ? ?
? ? - MemberId: VSP87654321                         ? ?
? ??????????????????????????????????????????????????? ?
? ??????????????????????????????????????????????????? ?
? ? Coverage Enrollment 2 (Medical - BCBS)          ? ?
? ? - PayerId: payer_bcbs_001                       ? ?
? ? - MemberId: BCBS-XYZ123456789                   ? ?
? ??????????????????????????????????????????????????? ?
???????????????????????????????????????????????????????
                      ?
                      ? Check eligibility
                      ?
        ???????????????????????????????
        ? Eligibility Check Request   ?
        ???????????????????????????????
        ? CoverageEnrollmentId: cov_001_vision  ?
        ? ServiceTypeCodes: ["AL", "AM", "AO"]  ?
        ???????????????????????????????
                      ?
                      ?
            Query: "Check VSP Vision plan
            for Vision (AL), Frames (AM), Lenses (AO)"
```

### Data Flow in Check-In Service

```csharp
// From PatientCheckInService.cs, PHASE 6
foreach (var eligibilityRequest in request.EligibilityChecks)
{
    // STEP 1: Find the coverage enrollment (which insurance plan?)
    var coverage = patient.CoverageEnrollments?
        .FirstOrDefault(c => c.CoverageEnrollmentId == eligibilityRequest.CoverageEnrollmentId);
    
    if (coverage is null)
    {
        warnings.Add($"Coverage enrollment {eligibilityRequest.CoverageEnrollmentId} not found");
        continue;
    }

    // STEP 2: Build eligibility check request with service type codes
    var checkRequest = new EligibilityCheckRequestDto
    {
        CoverageEnrollmentId = eligibilityRequest.CoverageEnrollmentId,  // Which plan
        ServiceTypeCodes = eligibilityRequest.ServiceTypeCodes,          // What services
        OverrideDateOfService = eligibilityRequest.OverrideDateOfService,
        ForceRefresh = eligibilityRequest.ForceRefresh
    };

    // STEP 3: Run eligibility check against that specific plan
    var result = await _eligibilityCheckService.RunWithPatientAsync(
        patient,
        encounter.Id,
        checkRequest,
        cancellationToken,
        timeout);
}
```

---

## Real-World Example: Routine Vision Exam

### Scenario

**Patient:** Emily Rodriguez  
**Insurance:** VSP Vision (primary for vision) + BCBS Medical (primary for medical)  
**Visit Type:** Routine vision exam  
**Services Needed:** Vision exam, frames, lenses

### Check-In Request

```json
POST /api/practices/prac_001/patients/pat_001/check-in

{
  "patientId": "pat_001",
  "encounter": {
    "visitType": "RoutineVision",
    "visitDate": "2025-01-20T10:00:00Z",
    "locationId": "loc_001"
  },
  "coverageDecision": {
    "primaryCoverageEnrollmentId": "cov_001_vision",
    "cobReason": "RoutineVision_UseVisionPlanPrimary"
  },
  "eligibilityChecks": [
    {
      "coverageEnrollmentId": "cov_001_vision",  // ? Which plan to check
      "serviceTypeCodes": ["AL", "AM", "AO"],    // ? What services to verify
      "forceRefresh": false
    }
  ]
}
```

### What Happens Step-by-Step

#### 1. Coverage Enrollment Selected
```
Coverage: cov_001_vision
Payer: VSP (Vision Service Plan)
Member ID: VSP87654321
Plan Type: Vision
```

#### 2. Service Type Codes Specified
```
AL ? Vision (Optometry) - Eye exams and vision care
AM ? Frames - Eyeglass frames  
AO ? Lenses - Eyeglass lenses
```

#### 3. Eligibility Check Sent to Availity

```
POST https://api.availity.com/v1/coverages
Content-Type: application/x-www-form-urlencoded

payerId=VSP
memberId=VSP87654321
serviceType[]=AL
serviceType[]=AM
serviceType[]=AO
asOfDate=2025-01-20
```

#### 4. Availity Response

```json
{
  "coverageId": "AVAIL-12345",
  "statusCode": "4",
  "status": "Complete",
  "plans": [{
    "planName": "VSP Choice Network",
    "benefits": [
      {
        "name": "AL",  // Vision exam
        "amounts": {
          "coPay": {
            "inNetwork": [{ "amount": "10.00" }]
          }
        }
      },
      {
        "name": "AM",  // Frames
        "amounts": {
          "allowances": {
            "inNetwork": [{ "amount": "200.00" }]
          }
        }
      },
      {
        "name": "AO",  // Lenses
        "amounts": {
          "coPay": {
            "inNetwork": [{ "amount": "25.00" }]
          }
        }
      }
    ]
  }]
}
```

#### 5. Result Stored in Encounter

```csharp
encounter.EligibilityChecks.Add(new EligibilityCheckEmbedded
{
    CoverageEnrollmentId = "cov_001_vision",  // Which plan was checked
    PayerId = "payer_vsp_001",
    Status = "Succeeded",
    PlanNameSnapshot = "VSP Choice Network",
    CoverageLines = 
    [
        new CoverageLineEmbedded 
        { 
            ServiceTypeCode = "AL", 
            CoverageDescription = "Vision - Exam",
            CopayAmount = 10m,
            NetworkIndicator = "IN"
        },
        new CoverageLineEmbedded 
        { 
            ServiceTypeCode = "AM", 
            CoverageDescription = "Vision - Frames",
            AllowanceAmount = 200m,
            NetworkIndicator = "IN"
        },
        new CoverageLineEmbedded 
        { 
            ServiceTypeCode = "AO", 
            CoverageDescription = "Vision - Lenses",
            CopayAmount = 25m,
            NetworkIndicator = "IN"
        }
    ]
});
```

---

## Why This Separation Matters

### 1. Plan Type vs. Service Type

The `PlanType` on a coverage enrollment is a **hint**, not a restriction. You can query any service type code against any plan.

| Coverage Enrollment `PlanType` | Typical Service Type Codes | Cross-Over Scenarios |
|--------------------------------|---------------------------|----------------------|
| **"Vision"** | `AL`, `AM`, `AO`, `AN`, `CP` | Can check `30` (medical) for vision plans that cover medical eye conditions |
| **"Medical"** | `30`, `98`, `BR`, `2` | Can check `AL` (vision) for medical plans with vision riders |
| **"Dental"** | `23`, `35`, `38`, `40`, `41` | Usually doesn't cross over |

### 2. Flexibility Example: Medical Eye Visit with Vision Insurance

Some vision plans cover **medical eye conditions** (e.g., infections, injuries). You might check:

```json
{
  "coverageEnrollmentId": "cov_001_vision",  // Vision plan
  "serviceTypeCodes": ["30", "BR", "98"]     // Medical service codes!
}
```

This checks if the **VSP Vision plan** covers:
- `30` ? General health benefit plan coverage
- `BR` ? Eye care (ophthalmology)
- `98` ? Professional physician visit

### 3. Multiple Checks for One Plan

You can run **multiple separate eligibility checks** against the **same coverage enrollment** with **different service code combinations**:

```json
{
  "eligibilityChecks": [
    {
      "coverageEnrollmentId": "cov_001_vision",
      "serviceTypeCodes": ["AL"]  // Check routine vision exam only
    },
    {
      "coverageEnrollmentId": "cov_001_vision",
      "serviceTypeCodes": ["AM", "AO"]  // Check eyewear benefits separately
    },
    {
      "coverageEnrollmentId": "cov_001_vision",
      "serviceTypeCodes": ["30", "BR"]  // Check medical eye coverage
    }
  ]
}
```

**Use case:** Check routine vision and medical eye benefits separately to see which COB applies.

---

## Default Behavior

From `BF.API\Integrations\Availity\AvailityEligibilityClient.cs`:

```csharp
// Lines 221-235
if (request.ServiceTypeCodes is { Count: > 0 })
{
    foreach (var stc in request.ServiceTypeCodes)
    {
        fields.Add(new("serviceType[]", stc));
    }
}
else
{
    // ? DEFAULT: If no service type codes specified, use "30"
    fields.Add(new("serviceType[]", "30"));
}
```

**If you don't specify `serviceTypeCodes`, the system defaults to code "30" (General Health Benefit Plan Coverage).**

---

## Interaction Summary

### Coverage Enrollment (Persistent)

| Aspect | Details |
|--------|---------|
| **Storage** | Embedded in `Patient` document in Cosmos DB |
| **Scope** | Patient-level (a patient can have multiple) |
| **Lifecycle** | Long-lived (active for months/years) |
| **Purpose** | Identifies which insurance plans a patient has |
| **Example** | VSP Vision, BCBS Medical, Medicare Part B |

### Service Type Code (Transient)

| Aspect | Details |
|--------|---------|
| **Storage** | Request parameter only (not persisted on coverage) |
| **Scope** | Check-level (each eligibility check specifies codes) |
| **Lifecycle** | Ephemeral (exists only during the request) |
| **Purpose** | Specifies which services to verify coverage for |
| **Example** | `["AL", "AM", "AO"]` for vision exam + eyewear |

### The Query Pattern

```
Coverage Enrollment (WHO) + Service Type Codes (WHAT) = Eligibility Check (RESULT)
```

**Example:**

```
VSP Vision Plan (WHO)
  + Vision Exam/Frames/Lenses (WHAT - codes AL/AM/AO)
  = Coverage details with copays and allowances (RESULT)
```

---

## Code Mapping: Plan Type to Recommended Service Codes

| Plan Type | Recommended Service Type Codes | Description |
|-----------|-------------------------------|-------------|
| **Vision** | `AL`, `AN`, `AM`, `AO`, `CP` | Vision exams, routine exams, frames, lenses, eyewear |
| **Medical** | `30`, `98`, `BR`, `2`, `4`, `5` | General health, office visits, eye care, surgical, diagnostics |
| **Dental** | `23`, `24`, `25`, `35`, `38`, `40`, `41` | Diagnostic, periodontics, restorative, dental care, orthodontics, oral surgery, preventive |
| **Medicare** | `30`, `33`, `98`, `BR` | Health coverage, chiropractic, office visits, eye care |

---

## Key Takeaways

1. **Coverage Enrollment** = Insurance plan identification (VSP, BCBS, etc.)
2. **Service Type Code** = Specific benefit categories to check (vision exam, frames, etc.)
3. **Relationship** = Loosely coupled; you specify which plan and which services per check
4. **Flexibility** = Can query any service code against any plan (not restricted by `PlanType`)
5. **Default** = If no service codes specified, defaults to `"30"` (general medical)
6. **Storage** = Coverage enrollments are persistent; service type codes are request parameters

### Mental Model

Think of it like a restaurant:

- **Coverage Enrollment** = Your membership card (Costco, Sam's Club)
- **Service Type Codes** = Which departments you want to shop in (groceries, electronics, pharmacy)
- **Eligibility Check** = Asking "What can I buy in these departments with this membership?"

You can have multiple memberships (dual coverage) and check different departments (service types) with each one!

---

## Related Documentation

- **X12 Service Type Codes:** See `BF.Data.Cosmos.Seed\Program.cs` (BuildLookups) for complete list
- **Eligibility Check Service:** `BF.API\Services\EligibilityCheckService.cs`
- **Patient Check-In Service:** `BF.API\Services\PatientCheckInService.cs`
- **Availity Integration:** `BF.API\Integrations\Availity\AvailityEligibilityClient.cs`

---

**Created:** January 2025  
**Last Updated:** January 2025  
**Author:** Development Team  
**Project:** Benefetch API
