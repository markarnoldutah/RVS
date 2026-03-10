# Optimal Patient Check-In Workflow for Front-Desk Staff

## Executive Summary

This document defines the **realistic, optimized workflow** for front-desk staff performing patient check-in. Based on analysis of the existing API design (particularly `PatientCheckInController`) and the documented eligibility/COB decision patterns, the workflow is designed as a **two-phase process** that balances operational efficiency with real-world decision-making requirements.

---

## Patient Discovery: Integrated Search UX

### The Problem

Front-desk staff will **never** know the `patientId` at the start of check-in. They need to:

1. Search for the patient by name/DOB
2. Handle disambiguation when multiple matches exist
3. Seamlessly transition to creating a new patient if no match found
4. Pre-populate the check-in form with existing patient data

### Recommended UX: Inline Search → Select/Create → Check-In Form

The patient discovery flow should be **integrated into the check-in form** rather than a separate page, enabling a smooth single-screen experience.

---

### UX Flow Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│  PATIENT CHECK-IN                                                    │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  📋 FIND OR CREATE PATIENT                                           │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Last Name:  [Smith________]  First Name: [John_________]      │  │
│  │  DOB:        [03/15/1985___]  [🔍 Search]                      │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  🔍 SEARCH RESULTS (2 matches found)                           │  │  
│  │  ┌──────────────────────────────────────────────────────────┐  │  │
│  │  │ ○ John Smith | DOB: 03/15/1985 | VSP: VSP123456789       │  │  │
│  │  │   Phone: (555) 123-4567                                  │  │  │
│  │  ├──────────────────────────────────────────────────────────┤  │  │
│  │  │ ○ John A. Smith | DOB: 03/15/1985 | EyeMed: EYE987654    │  │  │
│  │  │   Phone: (555) 987-6543                                  │  │  │
│  │  └──────────────────────────────────────────────────────────┘  │  │
│  │                                                                │  │
│  │  [Select Patient]  or  [➕ Create New Patient]                 │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ────────────────────────────────────────────────────────────────    │
│  ↓ After selection, form expands with patient data pre-filled ↓      │
│  ────────────────────────────────────────────────────────────────    │
│                                                                      │
│  ✓ PATIENT: John Smith (ID: patient-123)                             │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Demographics  |  Coverage  |  Encounter  |  Eligibility       │  │
│  │  ──────────────────────────────────────────────────────────    │  │
│  │  First Name: [John_______]  Last Name: [Smith_______]          │  │
│  │  DOB: [03/15/1985]  Phone: [(555) 123-4567]                    │  │
│  │  Email: [john@email.com________________________]               │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

---

### Search Behavior Details

#### Trigger Conditions
- **Auto-search**: Triggered when user has entered at least:
  - Last name (2+ chars) AND first name (2+ chars), OR
  - Last name (2+ chars) AND DOB
- **Manual search**: User clicks Search button
- **Debounce**: 300ms delay after typing stops before auto-search fires

#### Search Results States

| State | UI Behavior |
|-------|-------------|
| **No matches** | Show "No patients found" + prominent "Create New Patient" button |
| **Single match** | Auto-select and expand form (with "Not this patient?" link) |
| **Multiple matches (2-5)** | Show selection list with radio buttons |
| **Many matches (6+)** | Show first 5 + "Refine search" prompt |

#### Result Display Fields
Each search result card shows:
- Full name
- Date of birth
- Phone number (for quick verbal verification)
- Primary payer + member ID (helps identify the right John Smith)

---

### UX States: Progressive Disclosure

#### State 1: Initial (Search Mode)
```
┌──────────────────────────────────────────────────────────────────────┐
│  📋 FIND OR CREATE PATIENT                                           │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Last Name:  [____________]  First Name: [______________]      │  │
│  │  DOB:        [____________]  [🔍 Search]                       │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  💡 Enter patient name and date of birth to search                   │
│                                                                      │
│  [➕ New Walk-In Patient (skip search)]                              │  
└──────────────────────────────────────────────────────────────────────┘
```

#### State 2: Searching
```
┌──────────────────────────────────────────────────────────────────────┐
│  🔍 Searching for "Smith, John (03/15/1985)"...                      │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  ⏳ Loading...                                                 │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

#### State 3: No Results
```
┌──────────────────────────────────────────────────────────────────────┐
│  🔍 SEARCH RESULTS                                                   │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  ⚠️ No patients found matching "Smith, John (03/15/1985)"      │  │
│  │                                                                │  │
│  │  [➕ Create New Patient with This Info]                        │  │
│  │                                                                │  │
│  │  Or try:                                                       │  │
│  │  • Check spelling of name                                      │  │
│  │  • Verify date of birth                                        │  │
│  │  • Search by member ID instead                                 │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

#### State 4: Patient Selected → Form Expands
```
┌──────────────────────────────────────────────────────────────────────┐
│  ✅ PATIENT SELECTED                                                 │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  John Smith | DOB: 03/15/1985 | ID: patient-123                │  │
│  │  [✏️ Change Patient]                                           │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ────────────────────────────────────────────────────────────────    │
│                                                                      │
│  📋 DEMOGRAPHICS (verify/update)                                     │ 
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  First Name: [John_______]  Last Name: [Smith_______]          │  │
│  │  DOB: [03/15/1985]  Phone: [(555) 123-4567]                    │  │
│  │  Email: [john@email.com________________________]               │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  💳 COVERAGE ENROLLMENTS                                             │  
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Existing coverages loaded from patient record...              │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

---

### API Calls for Patient Discovery

| Action | API Call | When Called |
|--------|----------|-------------|
| Search patients | `POST /api/practices/{practiceId}/patients/search` | On search trigger (auto or manual) |
| Load full patient | `GET /api/practices/{practiceId}/patients/{patientId}` | When patient is selected |

**Note**: The `PatientCheckInRequestDto` accepts `patientId` from the selected patient. The full patient load retrieves existing coverage enrollments to pre-populate the form.

---

### Blazor Component Structure for Patient Discovery

```
CheckInPage.razor
│
├── PatientDiscoverySection.razor
│   │
│   ├── PatientSearchForm.razor
│   │   ├── Inputs: LastName, FirstName, DOB, MemberId (optional)
│   │   ├── Emits: OnSearch(PatientSearchRequestDto)
│   │   └── Emits: OnCreateNew()
│   │
│   ├── PatientSearchResults.razor
│   │   ├── Displays: List<PatientSearchResultResponseDto>
│   │   ├── Emits: OnPatientSelected(patientId)
│   │   └── Emits: OnCreateNewFromSearch(demographics)
│   │
│   └── SelectedPatientBanner.razor
│       ├── Shows: Selected patient summary
│       └── Emits: OnChangePatient()
│
├── PatientDemographicsSection.razor (shown after selection/creation)
├── CoverageEnrollmentsSection.razor
├── EncounterDetailsSection.razor
├── EligibilityCheckSelectionSection.razor
│
└── [Submit Button] → POST /check-in
```

---

### Component State Machine

```csharp
public enum PatientDiscoveryState
{
    /// <summary>Initial state - search form visible, no results</summary>
    Searching,
    
    /// <summary>API call in progress</summary>
    Loading,
    
    /// <summary>Results displayed, awaiting selection</summary>
    ResultsDisplayed,
    
    /// <summary>No results found, create option prominent</summary>
    NoResults,
    
    /// <summary>Patient selected, full form visible</summary>
    PatientSelected,
    
    /// <summary>Creating new patient (no search performed)</summary>
    CreatingNew
}
```

---

### Data Flow: Search → Select → Pre-populate

```
1. Staff enters: "Smith", "John", "03/15/1985"
   │
   ▼
2. POST /patients/search
   Request:  { lastName: "Smith", firstName: "John", dateOfBirth: "1985-03-15" }
   Response: { items: [{ patientId: "patient-123", ... }, { patientId: "patient-456", ... }] }
   │
   ▼
3. Staff selects "patient-123"
   │
   ▼
4. GET /patients/patient-123
   Response: Full PatientDetailResponseDto including:
   - Demographics (firstName, lastName, dob, phone, email)
   - CoverageEnrollments[] (all existing coverages)
   - Recent encounters (optional, for context)
   │
   ▼
5. Form pre-populates:
   - PatientId = "patient-123"
   - Demographics section filled (editable for updates)
   - Coverage enrollments loaded with existing data
   - Encounter section ready for new encounter
   │
   ▼
6. Staff reviews, updates if needed, clicks "Check In"
   │
   ▼
7. POST /check-in with patientId + any updates
```

---

### Keyboard Navigation & Accessibility

| Key | Action |
|-----|--------|
| `Tab` | Move between search fields |
| `Enter` | Trigger search (when in search form) |
| `↓/↑` | Navigate search results |
| `Enter` | Select highlighted result |
| `Escape` | Clear results / return to search |

---

### Edge Cases

#### Case A: Staff Knows Patient Exists but Typos Name
- Show "Did you mean?" suggestions if fuzzy match available
- Prominent "Search by Member ID" alternative

#### Case B: Patient Changed Name (Marriage, etc.)
- Search finds no match
- Staff creates "new" patient
- Later: Admin merges duplicate records (out of scope for check-in)

#### Case C: Multiple Family Members with Same Name
- DOB is critical differentiator
- Show phone number for verbal confirmation
- Consider showing last visit date for established patients

#### Case D: Slow Network
- Show skeleton loaders during search
- Allow cancellation of in-progress search
- Cache recent search results in session storage

---

## Key Insight: Why Two Phases?

The existing documentation (`checkin flow - coverage decision.md`) correctly identifies that **coverage decisions should typically be deferred until after eligibility results are available**. Staff cannot confidently set COB order without knowing:

1. Which coverages are currently active
2. Which plans provide better benefits for this visit type
3. Whether any coverage has terminated or has limitations

This leads to a **two-phase workflow** with a single combined API call in Phase 1, followed by a focused update in Phase 2.

---

## Realistic Scenario: Front-Desk Check-In

### Context
- **Staff member**: Sarah (front-desk coordinator at ABC Optometry)
- **Patient**: John Smith, returning patient with dual coverage (VSP + Aetna)
- **Visit type**: Routine Vision exam
- **Time constraint**: 2-3 minutes per patient check-in

---

## Phase 1: Combined Check-In Form + Eligibility Verification

### Staff Experience (Single Form UI)

Sarah opens the Check-In screen and sees a unified form with these sections:

```
┌──────────────────────────────────────────────────────────────────────┐
│  PATIENT CHECK-IN                                                    │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  📋 PATIENT SEARCH / DEMOGRAPHICS                                    │ 
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  🔍 Search: [John Smith     ] [DOB: 1985-03-15]  [Search]      │  │
│  │                                                                │  │
│  │  ✓ Patient Found: John Smith (DOB: 03/15/1985)                 │  │
│  │    Phone: (555) 123-4567 | Email: john@email.com               │  │
│  │    [Edit Demographics]                                         │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  📅 ENCOUNTER DETAILS                                                │  
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Visit Type: [Routine Vision ▼]                                │  │
│  │  Location:   [Main Office ▼]                                   │  │
│  │  Visit Date: [Today - 10:30 AM]                                │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  💳 COVERAGE ENROLLMENTS (verify/update as needed)                   │  
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  ☑ VSP Choice Plan          Member ID: VSP123456789           │  │
│  │    Group: ABC Corp | Priority Hint: 1 (Vision Primary)         │  │
│  │                                                                │  │
│  │  ☑ Aetna PPO Medical        Member ID: AET987654321           │  │
│  │    Group: XYZ Inc | Priority Hint: 2 (Medical Secondary)       │  │
│  │                                                                │  │
│  │  [+ Add Coverage]                                              │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ✅ ELIGIBILITY CHECKS (select coverages to verify)                  │ │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  ☑ Run eligibility for: VSP Choice Plan                       │  │
│  │  ☑ Run eligibility for: Aetna PPO Medical                     │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ⚠️ Coverage Decision will be set after eligibility results          │
│                                                                      │
│           [ Check In & Verify Eligibility ]                          │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

### Actions Performed
1. **Patient lookup** (via search) → finds existing patient
2. **Demographics verification** → updates if phone/email changed
3. **Coverage review** → confirms or updates member IDs, adds new coverage if needed
4. **Encounter creation** → sets visit type, location, date
5. **Eligibility selection** → checks boxes for which coverages to verify
6. **Submit** → single button triggers all operations

### API Call: Single Combined Request

```
POST /api/practices/{practiceId}/check-in
```

```json
{
  "patientId": "patient-john-smith-123",
  "patient": {
    "firstName": "John",
    "lastName": "Smith",
    "dateOfBirth": "1985-03-15",
    "phone": "(555) 123-4567",
    "email": "john.smith@newemail.com"
  },
  "coverageEnrollments": [
    {
      "coverageEnrollmentId": "cov-vsp-existing-456",
      "payerId": "vsp-choice",
      "planType": "Vision",
      "memberId": "VSP123456789",
      "groupNumber": "ABC-CORP-001",
      "cobPriorityHint": 1
    },
    {
      "coverageEnrollmentId": "cov-aetna-existing-789",
      "payerId": "aetna-ppo",
      "planType": "Medical",
      "memberId": "AET987654321",
      "groupNumber": "XYZ-INC-002",
      "cobPriorityHint": 2
    }
  ],
  "encounter": {
    "locationId": "loc-main-office",
    "visitType": "RoutineVision",
    "visitDate": "2025-01-15T10:30:00Z"
  },
  "coverageDecision": null,
  "eligibilityChecks": [
    { "coverageEnrollmentId": "cov-vsp-existing-456" },
    { "coverageEnrollmentId": "cov-aetna-existing-789" }
  ]
}
```

### Response Includes Eligibility Results

```json
{
  "patientId": "patient-john-smith-123",
  "encounterId": "enc-20250115-001",
  "patient": {
    "patientId": "patient-john-smith-123",
    "firstName": "John",
    "lastName": "Smith",
    "wasCreated": false
  },
  "encounter": {
    "encounterId": "enc-20250115-001",
    "visitType": "RoutineVision",
    "status": "scheduled",
    "wasCreated": true
  },
  "coverageEnrollments": [
    { "coverageEnrollmentId": "cov-vsp-existing-456", "wasCreated": false },
    { "coverageEnrollmentId": "cov-aetna-existing-789", "wasCreated": false }
  ],
  "coverageDecision": null,
  "eligibilityChecks": [
    {
      "eligibilityCheckId": "elig-001",
      "coverageEnrollmentId": "cov-vsp-existing-456",
      "payerId": "vsp-choice",
      "status": "Complete",
      "planName": "VSP Choice Plan - Employee",
      "effectiveDate": "2024-01-01",
      "coverageLines": [
        { "serviceType": "Frame", "allowance": 150.00, "frequency": "24 months" },
        { "serviceType": "Lens", "copay": 25.00, "frequency": "12 months" }
      ]
    },
    {
      "eligibilityCheckId": "elig-002",
      "coverageEnrollmentId": "cov-aetna-existing-789",
      "payerId": "aetna-ppo",
      "status": "Complete",
      "planName": "Aetna PPO Gold",
      "effectiveDate": "2023-07-01",
      "coverageLines": [
        { "serviceType": "Medical Exam", "copay": 40.00 }
      ]
    }
  ],
  "allEligibilityChecksSucceeded": true,
  "warnings": []
}
```

### RU Cost: ~4-6 RU (vs. ~15-20 RU for separate calls)

---

## Phase 2: Review Results & Set Coverage Decision

### Staff Experience (Results Review + COB Decision)

After the initial check-in completes, Sarah sees the eligibility results summary:

```
┌──────────────────────────────────────────────────────────────────────┐
│  CHECK-IN RESULTS - John Smith                                       │
│  Encounter: #ENC-20250115-001 | Routine Vision | Today 10:30 AM      │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  📋 ELIGIBILITY VERIFICATION RESULTS                                 │  
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  ✅ VSP Choice Plan - ACTIVE                                   │  │
│  │     Plan: VSP Choice Plan - Employee                            │  │
│  │     Effective: 01/01/2024                                       │  │
│  │     ┌────────────────────────────────────────────────────────┐  │  │
│  │     │ Frame Allowance: $150 (every 24 months) - ELIGIBLE     │  │  │
│  │     │ Lens Copay: $25 (every 12 months) - ELIGIBLE           │  │  │
│  │     └────────────────────────────────────────────────────────┘  │  │
│  │                                                                 │  │
│  │  ✅ Aetna PPO Medical - ACTIVE                                  │  │
│  │     Plan: Aetna PPO Gold                                        │  │
│  │     Effective: 07/01/2023                                       │  │
│  │     ┌────────────────────────────────────────────────────────┐  │  │
│  │     │ Medical Exam Copay: $40                                │  │  │
│  │     └────────────────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────── ─┘  │
│                                                                       │
│  🎯 COVERAGE DECISION (COB)                                           │  
│  ┌────────────────────────────────────────────────────────────────┐   │
│  │  💡 Recommended: VSP Primary → Aetna Secondary                 │   │
│  │     Reason: Routine Vision visit uses vision plan first        │   │
│  │                                                                 │  │
│  │  Primary Coverage:   [VSP Choice Plan ▼]                        │  │
│  │  Secondary Coverage: [Aetna PPO Medical ▼]                      │  │
│  │  COB Reason:         [RoutineVision_VisionThenMedical ▼]        │  │
│  │                                                                 │  │
│  │  ☐ Override recommended decision                               │  │
│  │    Override Note: [_______________________________]             │  │
│  └─────────────────────────────────────────────────────────────── ─┘  │
│                                                                       │
│           [ Confirm Coverage Decision ]    [ Skip for Now ]           │
│                                                                       │
└────────────────────────────────────────────────────────────────────── ┘
```

### Actions Performed
1. **Review eligibility results** → both coverages are active
2. **Accept recommended COB** → system suggests VSP primary based on visit type
3. **Confirm decision** → saves coverage decision to encounter

### API Call: Focused Coverage Decision Update

```
PUT /api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/coverage-decision
```

```json
{
  "primaryCoverageEnrollmentId": "cov-vsp-existing-456",
  "secondaryCoverageEnrollmentId": "cov-aetna-existing-789",
  "cobReason": "RoutineVision_VisionThenMedical",
  "overriddenByUser": false,
  "overrideNote": null
}
```

### RU Cost: ~2-3 RU (single document update)

---

## Workflow Summary: API Calls Required

| Phase | User Action | API Call(s) | RU Cost |
|-------|-------------|-------------|---------|
| **Pre-Check-In** | Search for patient | `POST /patients/search` | ~2 RU |
| **Pre-Check-In** | Load patient details | `GET /patients/{patientId}` | ~1 RU |
| **Phase 1** | Submit check-in form | `POST /check-in` (combined) | ~4-6 RU |
| **Phase 2** | Confirm coverage decision | `PUT /encounters/{id}/coverage-decision` | ~2-3 RU |
| **Total** | - | **4 API calls** | **~9-12 RU** |

### Comparison to Individual Calls Approach

| Step | Individual API Approach | RU Cost |
|------|------------------------|---------|
| 1 | `POST /patients/search` | ~2 RU |
| 2 | `GET /patients/{id}` | ~1 RU |
| 3 | `PUT /patients/{id}` (demographics) | ~2 RU |
| 4 | `PUT /patients/{id}/coverages/{id}` (×2) | ~4 RU |
| 5 | `POST /encounters` | ~2 RU |
| 6 | `POST /eligibility-checks` (×2) | ~6 RU |
| 7 | `PUT /coverage-decision` | ~2 RU |
| **Total** | **8+ API calls** | **~19+ RU** |

**Savings: ~50% reduction in API calls and RU consumption**

---

## Edge Case Workflows

### Scenario A: New Patient (Walk-In)

**Phase 1 Request** (no `patientId`, full demographics required):

```json
{
  "patientId": null,
  "patient": {
    "firstName": "Jane",
    "lastName": "Doe",
    "dateOfBirth": "1990-06-20",
    "phone": "(555) 987-6543",
    "email": "jane.doe@email.com"
  },
  "coverageEnrollments": [
    {
      "coverageEnrollmentId": null,
      "payerId": "eyemed-standard",
      "planType": "Vision",
      "memberId": "EYE456789012",
      "groupNumber": "NEW-EMPLOYER"
    }
  ],
  "encounter": {
    "locationId": "loc-main-office",
    "visitType": "RoutineVision",
    "visitDate": "2025-01-15T14:00:00Z"
  },
  "eligibilityChecks": []
}
```

**Note**: No eligibility checks on first visit (coverage was just entered, IDs need verification). Staff runs eligibility manually after confirming member ID is correct.

**Follow-up API Call** (after verifying member ID):
```
POST /api/practices/{practiceId}/encounters/{encounterId}/eligibility-checks
```

---

### Scenario B: Single Coverage (No COB Needed)

If patient has only one active coverage, the UI can auto-set coverage decision in Phase 1:

```json
{
  "patientId": "patient-single-coverage",
  "coverageEnrollments": [...],
  "encounter": {...},
  "coverageDecision": {
    "primaryCoverageEnrollmentId": "cov-only-coverage-123",
    "secondaryCoverageEnrollmentId": null,
    "cobReason": "SingleCoverage"
  },
  "eligibilityChecks": [...]
}
```

**Result**: Phase 2 is skipped entirely—single API call completes full check-in.

---

### Scenario C: Eligibility Check Fails

If one coverage returns "Inactive" or "Error":

```
┌──────────────────────────────────────────────────────────────────────┐
│  📋 ELIGIBILITY VERIFICATION RESULTS                                 │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  ✅ VSP Choice Plan - ACTIVE                                   │  │
│  │                                                                │  │
│  │  ❌ Aetna PPO Medical - INACTIVE                               │  │
│  │     Error: Coverage terminated 12/31/2024                      │  │
│  │     ⚠️ Patient may have new coverage                           │  │
│  │     [Update Coverage Info] [Contact Patient]                   │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  🎯 COVERAGE DECISION (COB)                                          │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  ⚠️ Only one active coverage - setting as primary              │  │
│  │                                                                │  │
│  │  Primary Coverage:   [VSP Choice Plan ▼]                       │  │
│  │  Secondary Coverage: [None ▼]                                  │  │
│  │  COB Reason:         [SingleActiveCoverage ▼]                  │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

**Workflow**: Staff can still proceed with single coverage, or update the inactive coverage info and re-run eligibility.

---

## Blazor WASM UI Component Structure

```
CheckInPage.razor
├── PatientDiscoverySection.razor
│   ├── PatientSearchForm.razor
│   │   └── Uses: PatientSearchRequestDto → POST /patients/search
│   ├── PatientSearchResults.razor
│   │   └── Emits: PatientSelected event with PatientId
│   └── SelectedPatientBanner.razor
│       └── Shows selected patient, allows change
│
├── PatientDemographicsSection.razor
│   └── Bound to: PatientCheckInDemographicsDto
│
├── CoverageEnrollmentsSection.razor
│   └── Bound to: List<CoverageEnrollmentUpsertDto>
│
├── EncounterDetailsSection.razor
│   └── Bound to: EncounterCheckInDto
│
├── EligibilityCheckSelectionSection.razor
│   └── Bound to: List<EligibilityCheckRunDto>
│
├── [Submit Button] → POST /check-in (PatientCheckInRequestDto)
│
└── CheckInResultsPage.razor (shown after Phase 1)
    ├── EligibilityResultsSummary.razor
    │   └── Displays: List<EligibilityCheckResultDto>
    │
    ├── CoverageDecisionSection.razor
    │   ├── Shows: Recommended decision based on eligibility + visit type
    │   └── Bound to: CoverageDecisionCheckInDto
    │
    └── [Confirm Decision] → PUT /encounters/{id}/coverage-decision
```

---

## Summary of API Call Strategy

| Workflow Step | Combined vs. Separate | Rationale |
|---------------|----------------------|-----------|
| Patient search | **Separate** (pre-form) | Required before form can be populated |
| Patient details load | **Separate** (pre-form) | Needed to populate coverages in form |
| Patient demographics upsert | **Combined** | Part of single check-in transaction |
| Coverage enrollment upsert(s) | **Combined** | Atomic with patient update |
| Encounter creation | **Combined** | Atomic with patient update |
| Eligibility checks | **Combined** | Run with pre-loaded patient (RU optimized) |
| Coverage decision | **Separate** (Phase 2) | Requires human review of eligibility results |

---

## Conclusion

The optimal workflow uses:

1. **One patient search call** (required to identify patient)
2. **One patient details call** (required to load existing coverages)
3. **One combined check-in call** (Phase 1: patient + coverages + encounter + eligibility)
4. **One coverage decision call** (Phase 2: after staff reviews eligibility results)

This design:
- ✅ Reduces RU consumption by ~50%
- ✅ Minimizes network round-trips (4 calls vs. 8+)
- ✅ Aligns with real-world staff decision-making (COB after eligibility)
- ✅ Supports edge cases (new patients, single coverage, failed checks)
- ✅ Maintains audit trail (eligibility checks appended, not overwritten)
- ✅ Provides intuitive patient discovery UX integrated into check-in flow
