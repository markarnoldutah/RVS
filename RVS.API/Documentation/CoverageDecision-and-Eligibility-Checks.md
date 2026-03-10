# Coverage Decision and Eligibility Checks

## Overview

This document explains the relationship between **Coverage Decision** (Coordination of Benefits - COB) and **Eligibility Checks** in the BF.API system, including their purposes, data structures, and how they work together in the patient check-in workflow.

---

## Table of Contents

1. [What is Coverage Decision?](#what-is-coverage-decision)
2. [What are Eligibility Checks?](#what-are-eligibility-checks)
3. [Data Model Structure](#data-model-structure)
4. [Key Differences](#key-differences)
5. [How They Work Together](#how-they-work-together)
6. [Workflow Example](#workflow-example)
7. [API Integration](#api-integration)
8. [Best Practices](#best-practices)

---

## What is Coverage Decision?

**Coverage Decision** (also called **Coordination of Benefits - COB**) is a **business decision** that determines which insurance coverage to use for billing when a patient has multiple insurance plans.

### Purpose

- Determines **billing order** (which insurance gets billed first, second, etc.)
- Captures the **reason** for the decision
- Tracks whether the decision was made **automatically or manually**
- Provides **audit trail** for coverage determination

### Data Structure

```csharp
public class CoverageDecisionEmbedded
{
    public string EncounterCoverageDecisionId { get; init; }
    
    // Which coverage to bill first
    public required string PrimaryCoverageEnrollmentId { get; init; }
    
    // Which coverage to bill second (optional)
    public string? SecondaryCoverageEnrollmentId { get; set; }
    
    // Why was this decision made?
    public required string CobReason { get; init; }
    
    // Was it automatic ("AUTO") or manual ("USER")?
    public string? CobDeterminationSource { get; set; }
    
    // Did a user override the automatic decision?
    public bool OverriddenByUser { get; set; }
    
    // Notes about the override
    public string? OverrideNote { get; set; }
    
    // Audit fields
    public DateTime CreatedAtUtc { get; init; }
    public string? CreatedByUserId { get; init; }
}
```

### Storage Location

- **One per encounter** - Stored as `encounter.CoverageDecision`
- Embedded within the `EncounterEmbedded` entity
- Part of the Patient aggregate root

---

## What are Eligibility Checks?

**Eligibility Checks** are **technical verifications** that contact the insurance payer (via Availity) to determine what benefits are available under a specific coverage enrollment.

### Purpose

- **Verify active coverage** - Is the insurance active for the date of service?
- **Retrieve benefit details** - What services are covered? What are the copays, deductibles, etc.?
- **Get plan information** - Plan name, group number, effective dates
- **Obtain coverage lines** - Specific benefit breakdowns by service type

### Data Structure

```csharp
public class EligibilityCheckEmbedded
{
    public string EligibilityCheckId { get; init; }
    
    // Which coverage enrollment was checked
    public required string CoverageEnrollmentId { get; init; }
    
    // Which payer was contacted
    public required string PayerId { get; init; }
    
    // When is the service?
    public DateTime DateOfService { get; set; }
    
    // Status: "Pending", "InProgress", "Complete", "Failed", "Canceled"
    public string Status { get; set; }
    
    // Availity's coverage ID for polling
    public string? AvailityCoverageId { get; set; }
    
    // Payer response codes
    public string? RawStatusCode { get; set; }
    public string? RawStatusDescription { get; set; }
    
    // When to poll again (for async checks)
    public DateTime? NextPollAfterUtc { get; set; }
    
    // Snapshots from payer response
    public string? PlanNameSnapshot { get; set; }
    public DateTime? EffectiveDateSnapshot { get; set; }
    public DateTime? TerminationDateSnapshot { get; set; }
    
    // Error information (if failed)
    public string? ErrorMessage { get; set; }
    public List<string>? ValidationMessages { get; set; }
    
    // Benefit details
    public List<CoverageLineEmbedded> CoverageLines { get; set; }
    
    // Audit fields
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
```

### Storage Location

- **Multiple per encounter** - Stored as `encounter.EligibilityChecks[]`
- Array/collection within the `EncounterEmbedded` entity
- One check per coverage enrollment (can run checks for primary, secondary, etc.)

---

## Data Model Structure

```
Patient (Aggregate Root)
?
??? CoverageEnrollments[] (Insurance cards on file)
?   ??? CoverageEnrollmentId: "cov_001_vsp"
?   ??? PayerId: "VSP"
?   ??? MemberId: "VSP123456"
?   ??? CobPriorityHint: 1
?   ??? ...
?
??? Encounters[] (Patient visits)
    ?
    ??? EncounterId: "enc_001"
    ??? VisitDate: "2024-01-15"
    ??? VisitType: "Routine Vision Exam"
    ?
    ??? CoverageDecision (ONE per encounter - BUSINESS DECISION)
    ?   ??? PrimaryCoverageEnrollmentId: "cov_001_vsp"
    ?   ??? SecondaryCoverageEnrollmentId: "cov_002_aetna"
    ?   ??? CobReason: "Dual coverage through employer and spouse"
    ?   ??? CobDeterminationSource: "AUTO"
    ?
    ??? EligibilityChecks[] (MULTIPLE per encounter - BENEFIT VERIFICATIONS)
        ??? Check #1:
        ?   ??? EligibilityCheckId: "elig_001"
        ?   ??? CoverageEnrollmentId: "cov_001_vsp"
        ?   ??? Status: "Complete"
        ?   ??? PlanNameSnapshot: "VSP Choice Plus"
        ?   ??? CoverageLines[] (Exam: $10 copay, Frames: $150 allowance, etc.)
        ?
        ??? Check #2:
            ??? EligibilityCheckId: "elig_002"
            ??? CoverageEnrollmentId: "cov_002_aetna"
            ??? Status: "Complete"
            ??? PlanNameSnapshot: "Aetna Vision Preferred"
            ??? CoverageLines[] (Exam: $0 copay, Frames: $50 allowance, etc.)
```

---

## Key Differences

| Aspect | Coverage Decision | Eligibility Check |
|--------|------------------|-------------------|
| **Purpose** | Determines billing order | Verifies benefits availability |
| **Type** | Business decision | Technical verification |
| **Quantity** | One per encounter | Multiple per encounter (one per coverage) |
| **Timing** | Set during check-in or updated later | Run during check-in or as needed |
| **Source** | Internal system/user decision | External payer system (Availity) |
| **Can Change** | Yes, can be overridden by staff | No, historical snapshot |
| **Data** | Coverage IDs + reason | Detailed benefit information |
| **Required** | Optional (can bill without formal COB) | Optional (but recommended) |

---

## How They Work Together

### The Relationship

```
???????????????????????????????????????????????????????????????
?                    PATIENT CHECK-IN FLOW                    ?
???????????????????????????????????????????????????????????????
                              ?
                              ?
                    ???????????????????
                    ? Patient arrives ?
                    ? with 2 insurance?
                    ? cards (VSP +    ?
                    ? Aetna Vision)   ?
                    ???????????????????
                              ?
              ?????????????????????????????????
              ?                               ?
    ????????????????????          ????????????????????
    ? COVERAGE DECISION?          ?ELIGIBILITY CHECKS?
    ?    (COB)         ?          ?                  ?
    ? "Which to bill?" ?          ? "What benefits?" ?
    ????????????????????          ????????????????????
              ?                               ?
              ?                               ?
    Primary: VSP                    Check VSP: $10 copay
    Secondary: Aetna                         $150 frames
                                   
                                    Check Aetna: $0 copay
                                                 $50 frames
              ?                               ?
              ?????????????????????????????????
                              ?
                    ???????????????????
                    ? BILLING PROCESS ?
                    ???????????????????
                    ? 1. Bill VSP first  ?
                    ?    (primary)       ?
                    ? 2. Use VSP benefits?
                    ?    ($10 copay)     ?
                    ? 3. Bill Aetna for  ?
                    ?    remaining amount?
                    ???????????????????
```

### Sequential Logic

1. **Step 1: Capture Insurance Information**
   - Patient provides VSP card (employer plan)
   - Patient provides Aetna card (spouse's plan)
   - System creates `CoverageEnrollment` records

2. **Step 2: Determine Billing Order (Coverage Decision)**
   - System or user decides: VSP is primary, Aetna is secondary
   - Decision stored in `encounter.CoverageDecision`
   - Reason captured: "Dual coverage through employer and spouse"

3. **Step 3: Verify Benefits (Eligibility Checks)**
   - System runs eligibility check against VSP
   - System runs eligibility check against Aetna
   - Each check retrieves specific benefit details
   - Results stored in `encounter.EligibilityChecks[]`

4. **Step 4: Billing Uses Both**
   - Billing department sees VSP is primary (from Coverage Decision)
   - Billing department knows VSP covers exam with $10 copay (from Eligibility Check)
   - Billing department knows Aetna is secondary (from Coverage Decision)
   - Billing department knows Aetna benefit limits (from Eligibility Check)

---

## Workflow Example

### Scenario: Sarah's Routine Eye Exam

**Patient:** Sarah Johnson  
**Visit Date:** January 15, 2024  
**Visit Type:** Routine Vision Exam  
**Insurance:** 2 vision plans

#### 1. Patient Check-In Request

```json
{
  "patientId": "pat_sarah_001",
  
  "coverageEnrollments": [
    {
      "payerId": "VSP",
      "planType": "Vision",
      "memberId": "VSP123456",
      "groupNumber": "GRP001",
      "relationshipToSubscriber": "Self",
      "isEmployerPlan": true,
      "cobPriorityHint": 1
    },
    {
      "payerId": "Aetna",
      "planType": "Vision", 
      "memberId": "AET987654",
      "groupNumber": "GRP002",
      "relationshipToSubscriber": "Spouse",
      "subscriberFirstName": "John",
      "subscriberLastName": "Johnson",
      "isEmployerPlan": true,
      "cobPriorityHint": 2
    }
  ],
  
  "encounter": {
    "locationId": "loc_001",
    "visitDate": "2024-01-15T10:00:00Z",
    "visitType": "Routine Vision Exam"
  },
  
  "coverageDecision": {
    "primaryCoverageEnrollmentId": "cov_sarah_vsp",
    "secondaryCoverageEnrollmentId": "cov_sarah_aetna",
    "cobReason": "Dual coverage through employer and spouse",
    "overriddenByUser": false
  },
  
  "eligibilityChecks": [
    {
      "coverageEnrollmentId": "cov_sarah_vsp",
      "serviceTypeCodes": ["30"],
      "forceRefresh": true
    },
    {
      "coverageEnrollmentId": "cov_sarah_aetna",
      "serviceTypeCodes": ["30"],
      "forceRefresh": true
    }
  ]
}
```

#### 2. System Processing

**Phase 1: Create/Update Coverage Enrollments**
- Adds VSP coverage to patient record
- Adds Aetna coverage to patient record

**Phase 2: Create Encounter**
- Creates new encounter for the visit

**Phase 3: Set Coverage Decision**
- Stores COB: VSP primary, Aetna secondary
- Reason: "Dual coverage through employer and spouse"
- Source: AUTO

**Phase 4: Run Eligibility Checks**
- Sends VSP eligibility request to Availity
- Sends Aetna eligibility request to Availity
- Stores results in encounter

#### 3. Eligibility Check Results

**VSP Response:**
```json
{
  "eligibilityCheckId": "elig_sarah_vsp_001",
  "status": "Complete",
  "planNameSnapshot": "VSP Choice Plus",
  "effectiveDateSnapshot": "2024-01-01",
  "coverageLines": [
    {
      "serviceTypeCode": "30",
      "coverageDescription": "Routine Eye Exam",
      "copayAmount": 10.00,
      "networkIndicator": "IN"
    },
    {
      "serviceTypeCode": "30",
      "coverageDescription": "Frame Allowance",
      "allowanceAmount": 150.00,
      "networkIndicator": "IN"
    },
    {
      "serviceTypeCode": "30",
      "coverageDescription": "Single Vision Lenses",
      "copayAmount": 0.00,
      "networkIndicator": "IN"
    }
  ]
}
```

**Aetna Response:**
```json
{
  "eligibilityCheckId": "elig_sarah_aetna_001",
  "status": "Complete",
  "planNameSnapshot": "Aetna Vision Preferred",
  "effectiveDateSnapshot": "2023-06-01",
  "coverageLines": [
    {
      "serviceTypeCode": "30",
      "coverageDescription": "Routine Eye Exam",
      "copayAmount": 0.00,
      "networkIndicator": "IN"
    },
    {
      "serviceTypeCode": "30",
      "coverageDescription": "Frame Allowance",
      "allowanceAmount": 50.00,
      "networkIndicator": "IN"
    }
  ]
}
```

#### 4. Billing Process Uses Both

**Coverage Decision tells billing team:**
- ? Bill VSP first (primary)
- ? Bill Aetna second (secondary)

**Eligibility Checks tell billing team:**
- ? VSP will cover exam with $10 copay
- ? VSP provides $150 frame allowance
- ? Aetna covers exam with $0 copay (but VSP already primary)
- ? Aetna provides $50 additional frame allowance

**Outcome:**
- Patient pays $10 copay for exam (VSP)
- Patient can use up to $150 from VSP for frames
- Patient can use up to $50 from Aetna for frames (additional)
- Total frame allowance: $200 ($150 VSP + $50 Aetna)

---

## API Integration

### Coverage Decision is NOT Part of Eligibility Check Request

**Important:** Coverage Decision and Eligibility Checks are **separate operations** with different DTOs.

#### Eligibility Check Request DTO

```csharp
public sealed record EligibilityCheckRequestDto
{
    // Only specifies WHICH coverage to check
    [Required]
    public string CoverageEnrollmentId { get; init; } = default!;
    
    [DataType(DataType.Date)]
    public DateTime? OverrideDateOfService { get; init; }
    
    // Service types to check (e.g., "30" for vision)
    public List<string>? ServiceTypeCodes { get; init; }
    
    // Force new check even if recent one exists
    public bool ForceRefresh { get; init; }
}
```

**Note:** No Coverage Decision fields! Eligibility checks are **independent** of COB decisions.

#### Standalone Eligibility Check Endpoint

```http
POST /api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks/run

{
  "coverageEnrollmentId": "cov_001_vsp",
  "serviceTypeCodes": ["30"],
  "forceRefresh": true
}
```

#### Standalone Coverage Decision Endpoint

```http
PUT /api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/coverage-decision

{
  "primaryCoverageEnrollmentId": "cov_001_vsp",
  "secondaryCoverageEnrollmentId": "cov_002_aetna",
  "cobReason": "Dual coverage through employer and spouse",
  "overriddenByUser": false
}
```

### Patient Check-In Workflow (Convenience API)

The check-in endpoint allows setting **both** in a single call for convenience:

```http
POST /api/practices/{practiceId}/check-in

{
  "patientId": "pat_001",
  
  "coverageEnrollments": [...],
  
  "encounter": {...},
  
  "coverageDecision": {
    "primaryCoverageEnrollmentId": "cov_001_vsp",
    "secondaryCoverageEnrollmentId": "cov_002_aetna",
    "cobReason": "Dual coverage"
  },
  
  "eligibilityChecks": [
    {
      "coverageEnrollmentId": "cov_001_vsp",
      "serviceTypeCodes": ["30"]
    },
    {
      "coverageEnrollmentId": "cov_002_aetna",
      "serviceTypeCodes": ["30"]
    }
  ]
}
```

**Processing Order:**
1. Upsert patient demographics
2. Add/update coverage enrollments
3. Create/update encounter
4. **Set coverage decision** (COB determination)
5. **Run eligibility checks** (benefit verification)

---

## Best Practices

### When to Set Coverage Decision

? **DO set during check-in when:**
- Patient has multiple active coverages
- Staff needs to establish billing order
- COB rules are clear (e.g., employer plan is always primary)

? **DON'T require it when:**
- Patient has only one coverage
- Coverage determination is still being researched
- Waiting for coordination with patient

### When to Run Eligibility Checks

? **DO run eligibility checks:**
- During patient check-in
- Before service delivery
- When coverage information changes
- Periodically to refresh benefit information (with `forceRefresh: true`)

? **DON'T run excessively:**
- Multiple times per day for same coverage (unless using `forceRefresh: false` to leverage cache)
- When recent check (< 24 hours) exists and `forceRefresh: false` - the system will automatically return cached results
- For terminated/inactive coverage enrollments

### Using ForceRefresh

The `forceRefresh` parameter controls eligibility check caching:

**`forceRefresh: false` (default)**
- System checks for recent eligibility results (< 24 hours old) for the same coverage and date of service
- If found, returns the cached result **without calling Availity**
- **Benefits:**
  - Saves Availity API costs
  - Improves response time
  - Avoids payer rate limits
  - Reduces network traffic

**`forceRefresh: true`**
- Always calls Availity, even if recent results exist
- Creates a new eligibility check record
- **Use when:**
  - Coverage information may have changed
  - Testing/troubleshooting
  - Patient requests verification
  - More than 24 hours since last check

**Example:**
```json
{
  "coverageEnrollmentId": "cov_001_vsp",
  "serviceTypeCodes": ["30"],
  "forceRefresh": false  // Use cached result if available
}
```

### Handling Multiple Coverages

**Best Practice Workflow:**

1. **Capture all coverage cards** during check-in
2. **Run eligibility checks** for all active coverages
3. **Review eligibility results** before setting COB
4. **Set coverage decision** based on:
   - Eligibility check results (which plans are active?)
   - Benefit comparison (which plan has better benefits?)
   - COB rules (employer plans typically primary)
   - Patient preference (if applicable)
5. **Allow manual override** by staff when needed

### Audit Trail

Always capture:
- **CobDeterminationSource**: "AUTO" or "USER"
- **OverriddenByUser**: true if staff manually changed
- **OverrideNote**: Explanation of why override was needed
- **CobReason**: Why this coverage order was chosen

### Error Handling

**If Eligibility Check Fails:**
- ? Don't block check-in
- ? Allow staff to proceed with manual entry
- ? Mark eligibility as "Failed" with error message
- ? Allow retry later

**If Coverage Decision is Unclear:**
- ? Don't guess or set arbitrary order
- ? Leave `CoverageDecision` null until determined
- ? Set it later when information is available
- ? Document in `CobNotes` why delayed

---

## Common Misconceptions

### ? Misconception 1: "Coverage Decision determines which eligibility check to run"

**Reality:** Eligibility checks can be run for **any coverage enrollment**, regardless of whether a Coverage Decision has been set. They are independent operations.

### ? Misconception 2: "You must set Coverage Decision before running eligibility checks"

**Reality:** You can run eligibility checks **before** deciding on COB. In fact, eligibility results often **inform** the Coverage Decision.

### ? Misconception 3: "Coverage Decision is required to bill"

**Reality:** Coverage Decision is **helpful but optional**. Many practices bill using coverage priority hints or manual selection without a formal COB record.

### ? Misconception 4: "Eligibility check creates the Coverage Decision"

**Reality:** Eligibility checks **verify benefits**; they don't make COB decisions. COB is a separate business logic determination.

### ? Misconception 5: "One Coverage Decision applies to all encounters"

**Reality:** Coverage Decision is **per encounter**. A patient might have different primary coverage for different visits (e.g., vision vs medical).

---

## Summary

| **Coverage Decision (COB)** | **Eligibility Check** |
|----------------------------|----------------------|
| **Business decision** about billing order | **Technical verification** of benefits |
| Answers: "Which coverage do we bill first?" | Answers: "What benefits are available?" |
| Set by system or user | Retrieved from payer (Availity) |
| One per encounter | Multiple per encounter |
| Can be changed/overridden | Historical snapshot |
| Optional | Optional but recommended |
| Stored in `encounter.CoverageDecision` | Stored in `encounter.EligibilityChecks[]` |

**They are separate but complementary:**
- Coverage Decision determines **billing workflow**
- Eligibility Checks provide **benefit information**
- Both are needed for accurate, efficient billing
- Both can be set during check-in for convenience
- Neither is required to proceed with patient care

---

## Related Documentation

- [Patient Check-In Workflow](./Patient-CheckIn-Workflow.md) *(if exists)*
- [Eligibility Check Polling Pattern](./Eligibility-Check-Polling.md) *(if exists)*
- [Coordination of Benefits (COB) Rules](./COB-Rules.md) *(if exists)*
- [Availity Integration](./Availity-Integration.md) *(if exists)*

---

**Document Version:** 1.0  
**Last Updated:** 2024-01-15  
**Author:** BF.API Development Team
