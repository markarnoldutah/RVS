# BF.Domain Entity Sample Documents

Comprehensive sample JSON documents for all BF.Domain entities with fully populated embedded documents.

---

## 1. Tenant Samples

### Tenant 1: Large Multi-Location Practice Group
```json
{
  "id": "ten_001",
  "tenantId": "ten_001",
  "type": "tenant",
  "name": "VisionCare Partners Network",
  "billingEmail": "billing@visioncarepartners.com",
  "status": "Active",
  "plan": "Enterprise",
  "notes": "Multi-state optometry group with 50+ locations. Premium clearinghouse integration enabled.",
  "createdAtUtc": "2024-01-15T08:00:00Z",
  "updatedAtUtc": "2025-01-10T14:30:00Z"
}
```

### Tenant 2: Mid-Size Practice
```json
{
  "id": "ten_002",
  "tenantId": "ten_002",
  "type": "tenant",
  "name": "Acme Eye Group",
  "billingEmail": "admin@acmeeye.com",
  "status": "Active",
  "plan": "Professional",
  "notes": "Growing practice with 5 locations. Early adopter of COB automation.",
  "createdAtUtc": "2024-03-20T10:15:00Z",
  "updatedAtUtc": "2025-01-12T09:00:00Z"
}
```

### Tenant 3: Small Independent Practice
```json
{
  "id": "ten_003",
  "tenantId": "ten_003",
  "type": "tenant",
  "name": "Dr. Smith Family Optometry",
  "billingEmail": "drsmith@familyeye.net",
  "status": "Active",
  "plan": "Starter",
  "notes": "Single-location family practice. Basic eligibility checking only.",
  "createdAtUtc": "2024-06-01T12:00:00Z",
  "updatedAtUtc": "2025-01-05T16:45:00Z"
}
```

### Tenant 4: Trial Account
```json
{
  "id": "ten_004",
  "tenantId": "ten_004",
  "type": "tenant",
  "name": "ClearSight Vision Center",
  "billingEmail": "trial@clearsightvision.com",
  "status": "Trial",
  "plan": "Trial",
  "notes": "30-day trial started. Evaluating for 3-location rollout.",
  "createdAtUtc": "2025-01-01T09:00:00Z",
  "updatedAtUtc": "2025-01-15T11:20:00Z"
}
```

### Tenant 5: Suspended Account
```json
{
  "id": "ten_005",
  "tenantId": "ten_005",
  "type": "tenant",
  "name": "Budget Vision LLC",
  "billingEmail": "accounting@budgetvision.com",
  "status": "Suspended",
  "plan": "Starter",
  "notes": "Payment issue - suspended 2024-12-15. Pending resolution.",
  "createdAtUtc": "2024-08-10T14:00:00Z",
  "updatedAtUtc": "2024-12-15T10:00:00Z"
}
```

---

## 2. Payer Samples

### Payer 1: VSP (National Vision Plan)
```json
{
  "id": "payer_vsp_001",
  "payerId": "payer_vsp_001",
  "type": "payer",
  "tenantId": null,
  "name": "VSP (Vision Service Plan)",
  "supportedPlanTypes": ["Vision"],
  "availityPayerCode": "VSP",
  "x12PayerId": "12345",
  "isMedicare": false,
  "isMedicaid": false,
  "createdAtUtc": "2024-01-01T00:00:00Z",
  "updatedAtUtc": "2024-11-15T10:30:00Z"
}
```

### Payer 2: Blue Cross Blue Shield (Medical)
```json
{
  "id": "payer_bcbs_001",
  "payerId": "payer_bcbs_001",
  "type": "payer",
  "tenantId": null,
  "name": "Blue Cross Blue Shield - National",
  "supportedPlanTypes": ["Medical"],
  "availityPayerCode": "BCBS",
  "x12PayerId": "00590",
  "isMedicare": false,
  "isMedicaid": false,
  "createdAtUtc": "2024-01-01T00:00:00Z",
  "updatedAtUtc": "2024-12-01T08:00:00Z"
}
```

### Payer 3: EyeMed (Vision)
```json
{
  "id": "payer_eyemed_001",
  "payerId": "payer_eyemed_001",
  "type": "payer",
  "tenantId": null,
  "name": "EyeMed Vision Care",
  "supportedPlanTypes": ["Vision"],
  "availityPayerCode": "EYEMED",
  "x12PayerId": "13551",
  "isMedicare": false,
  "isMedicaid": false,
  "createdAtUtc": "2024-01-01T00:00:00Z",
  "updatedAtUtc": "2024-10-20T14:15:00Z"
}
```

### Payer 4: Medicare
```json
{
  "id": "payer_medicare_001",
  "payerId": "payer_medicare_001",
  "type": "payer",
  "tenantId": null,
  "name": "Medicare Part B",
  "supportedPlanTypes": ["Medical"],
  "availityPayerCode": "MEDICARE",
  "x12PayerId": "00901",
  "isMedicare": true,
  "isMedicaid": false,
  "createdAtUtc": "2024-01-01T00:00:00Z",
  "updatedAtUtc": "2024-09-30T12:00:00Z"
}
```

### Payer 5: State Medicaid (Tenant-Specific)
```json
{
  "id": "payer_medicaid_ca_001",
  "payerId": "payer_medicaid_ca_001",
  "type": "payer",
  "tenantId": "ten_001",
  "name": "California Medicaid (Medi-Cal)",
  "supportedPlanTypes": ["Medical"],
  "availityPayerCode": "MEDICAID_CA",
  "x12PayerId": "68069",
  "isMedicare": false,
  "isMedicaid": true,
  "createdAtUtc": "2024-02-15T09:00:00Z",
  "updatedAtUtc": "2024-12-10T11:00:00Z"
}
```

---

## 3. Practice Samples

### Practice 1: Multi-Location Downtown Practice
```json
{
  "id": "prac_001",
  "practiceId": "prac_001",
  "type": "practice",
  "tenantId": "ten_001",
  "name": "VisionCare Partners - Downtown District",
  "externalRef": "LEGACY-PRAC-12345",
  "isActive": true,
  "phone": "555-100-2000",
  "email": "downtown@visioncarepartners.com",
  "locations": [
    {
      "locationId": "loc_001",
      "name": "Main Street Clinic",
      "address1": "123 Main Street",
      "address2": "Suite 400",
      "city": "Los Angeles",
      "state": "CA",
      "postalCode": "90001",
      "phone": "555-100-2001",
      "isActive": true
    },
    {
      "locationId": "loc_002",
      "name": "Financial District Office",
      "address1": "789 Corporate Plaza",
      "address2": "Floor 12",
      "city": "Los Angeles",
      "state": "CA",
      "postalCode": "90071",
      "phone": "555-100-2002",
      "isActive": true
    },
    {
      "locationId": "loc_003",
      "name": "Union Station Express Care",
      "address1": "800 N Alameda Street",
      "address2": null,
      "city": "Los Angeles",
      "state": "CA",
      "postalCode": "90012",
      "phone": "555-100-2003",
      "isActive": true
    }
  ],
  "createdAtUtc": "2024-01-20T08:00:00Z",
  "updatedAtUtc": "2025-01-10T15:30:00Z"
}
```

### Practice 2: Suburban Practice with Specialty Services
```json
{
  "id": "prac_002",
  "practiceId": "prac_002",
  "type": "practice",
  "tenantId": "ten_002",
  "name": "Acme Eye - Pasadena Specialty Center",
  "externalRef": "ACME-PAS-001",
  "isActive": true,
  "phone": "555-200-3000",
  "email": "pasadena@acmeeye.com",
  "locations": [
    {
      "locationId": "loc_101",
      "name": "Pasadena Eye Center",
      "address1": "456 Colorado Boulevard",
      "address2": null,
      "city": "Pasadena",
      "state": "CA",
      "postalCode": "91101",
      "phone": "555-200-3001",
      "isActive": true
    },
    {
      "locationId": "loc_102",
      "name": "Pediatric Vision Clinic",
      "address1": "789 Lake Avenue",
      "address2": "Building B",
      "city": "Pasadena",
      "state": "CA",
      "postalCode": "91101",
      "phone": "555-200-3002",
      "isActive": true
    }
  ],
  "createdAtUtc": "2024-03-25T10:00:00Z",
  "updatedAtUtc": "2025-01-08T12:15:00Z"
}
```

### Practice 3: Single-Location Family Practice
```json
{
  "id": "prac_003",
  "practiceId": "prac_003",
  "type": "practice",
  "tenantId": "ten_003",
  "name": "Dr. Smith Family Optometry",
  "externalRef": null,
  "isActive": true,
  "phone": "555-300-4000",
  "email": "office@familyeye.net",
  "locations": [
    {
      "locationId": "loc_201",
      "name": "Smith Family Eye Care",
      "address1": "1010 Maple Drive",
      "address2": null,
      "city": "Glendale",
      "state": "CA",
      "postalCode": "91202",
      "phone": "555-300-4000",
      "isActive": true
    }
  ],
  "createdAtUtc": "2024-06-05T09:00:00Z",
  "updatedAtUtc": "2024-12-20T11:00:00Z"
}
```

### Practice 4: Retail Partner Location
```json
{
  "id": "prac_004",
  "practiceId": "prac_004",
  "type": "practice",
  "tenantId": "ten_001",
  "name": "VisionCare Partners - Costco Locations",
  "externalRef": "COSTCO-PARTNER-2024",
  "isActive": true,
  "phone": "555-400-5000",
  "email": "costco@visioncarepartners.com",
  "locations": [
    {
      "locationId": "loc_301",
      "name": "Costco Burbank",
      "address1": "1051 W Burbank Boulevard",
      "address2": "Optical Dept",
      "city": "Burbank",
      "state": "CA",
      "postalCode": "91506",
      "phone": "555-400-5001",
      "isActive": true
    },
    {
      "locationId": "loc_302",
      "name": "Costco Northridge",
      "address1": "8810 Tampa Avenue",
      "address2": "Optical Dept",
      "city": "Northridge",
      "state": "CA",
      "postalCode": "91324",
      "phone": "555-400-5002",
      "isActive": true
    }
  ],
  "createdAtUtc": "2024-07-10T14:00:00Z",
  "updatedAtUtc": "2025-01-11T16:45:00Z"
}
```

### Practice 5: Seasonal/Closed Location
```json
{
  "id": "prac_005",
  "practiceId": "prac_005",
  "type": "practice",
  "tenantId": "ten_002",
  "name": "Acme Eye - Santa Monica Beach (Closed)",
  "externalRef": "ACME-SM-LEGACY",
  "isActive": false,
  "phone": "555-500-6000",
  "email": "closed@acmeeye.com",
  "locations": [
    {
      "locationId": "loc_401",
      "name": "Santa Monica Pier Office",
      "address1": "200 Santa Monica Pier",
      "address2": "Suite 1A",
      "city": "Santa Monica",
      "state": "CA",
      "postalCode": "90401",
      "phone": "555-500-6001",
      "isActive": false
    }
  ],
  "createdAtUtc": "2024-04-01T10:00:00Z",
  "updatedAtUtc": "2024-10-31T17:00:00Z"
}
```

---

## 4. Patient Samples

### Patient 1: Dual Coverage (Vision + Medical)
```json
{
  "id": "pat_001",
  "patientId": "pat_001",
  "type": "patient",
  "tenantId": "ten_001",
  "practiceId": "prac_001",
  "firstName": "Emily",
  "lastName": "Rodriguez",
  "dateOfBirth": "1985-03-15T00:00:00Z",
  "email": "emily.rodriguez@email.com",
  "phone": "555-111-2222",
  "coverageEnrollments": [
    {
      "coverageEnrollmentId": "cov_001_vision",
      "payerId": "payer_vsp_001",
      "planType": "Vision",
      "memberId": "VSP87654321",
      "groupNumber": "GRP-TECH-2024",
      "relationshipToSubscriber": "Self",
      "subscriberFirstName": "Emily",
      "subscriberLastName": "Rodriguez",
      "subscriberDob": "1985-03-15T00:00:00Z",
      "isEmployerPlan": true,
      "effectiveDate": "2024-01-01T00:00:00Z",
      "terminationDate": null,
      "isActive": true,
      "cobPriorityHint": 1,
      "isCobLocked": false,
      "cobNotes": "Primary for routine vision. Employer: Tech Solutions Inc."
    },
    {
      "coverageEnrollmentId": "cov_001_medical",
      "payerId": "payer_bcbs_001",
      "planType": "Medical",
      "memberId": "BCBS-XYZ123456789",
      "groupNumber": "TECHSOL-GRP-001",
      "relationshipToSubscriber": "Self",
      "subscriberFirstName": "Emily",
      "subscriberLastName": "Rodriguez",
      "subscriberDob": "1985-03-15T00:00:00Z",
      "isEmployerPlan": true,
      "effectiveDate": "2024-01-01T00:00:00Z",
      "terminationDate": null,
      "isActive": true,
      "cobPriorityHint": 2,
      "isCobLocked": false,
      "cobNotes": "Secondary for medical eye conditions. Same employer."
    }
  ],
  "createdAtUtc": "2024-02-10T10:30:00Z",
  "updatedAtUtc": "2025-01-12T14:20:00Z"
}
```

### Patient 2: Child with Parent's Coverage
```json
{
  "id": "pat_002",
  "patientId": "pat_002",
  "type": "patient",
  "tenantId": "ten_002",
  "practiceId": "prac_002",
  "firstName": "Oliver",
  "lastName": "Chen",
  "dateOfBirth": "2015-07-22T00:00:00Z",
  "email": null,
  "phone": "555-222-3333",
  "coverageEnrollments": [
    {
      "coverageEnrollmentId": "cov_002_vision",
      "payerId": "payer_eyemed_001",
      "planType": "Vision",
      "memberId": "EYEMED-789456123",
      "groupNumber": "FAMILY-PLAN-2024",
      "relationshipToSubscriber": "Child",
      "subscriberFirstName": "David",
      "subscriberLastName": "Chen",
      "subscriberDob": "1982-11-08T00:00:00Z",
      "isEmployerPlan": true,
      "effectiveDate": "2024-01-01T00:00:00Z",
      "terminationDate": null,
      "isActive": true,
      "cobPriorityHint": 1,
      "isCobLocked": false,
      "cobNotes": "Dependent coverage under father's plan. Contact: 555-999-8888"
    }
  ],
  "createdAtUtc": "2024-05-18T11:00:00Z",
  "updatedAtUtc": "2024-11-30T09:15:00Z"
}
```

### Patient 3: Medicare + Supplemental
```json
{
  "id": "pat_003",
  "patientId": "pat_003",
  "type": "patient",
  "tenantId": "ten_003",
  "practiceId": "prac_003",
  "firstName": "Margaret",
  "lastName": "Williams",
  "dateOfBirth": "1952-04-30T00:00:00Z",
  "email": "margaret.w@seniornet.org",
  "phone": "555-333-4444",
  "coverageEnrollments": [
    {
      "coverageEnrollmentId": "cov_003_medicare",
      "payerId": "payer_medicare_001",
      "planType": "Medical",
      "memberId": "1EG4-TE5-MK72",
      "groupNumber": null,
      "relationshipToSubscriber": "Self",
      "subscriberFirstName": "Margaret",
      "subscriberLastName": "Williams",
      "subscriberDob": "1952-04-30T00:00:00Z",
      "isEmployerPlan": false,
      "effectiveDate": "2017-05-01T00:00:00Z",
      "terminationDate": null,
      "isActive": true,
      "cobPriorityHint": 1,
      "isCobLocked": true,
      "cobNotes": "Medicare Part B primary. Use for medical eye conditions only."
    },
    {
      "coverageEnrollmentId": "cov_003_supplemental",
      "payerId": "payer_bcbs_001",
      "planType": "Medical",
      "memberId": "BCBS-SUPP-987654",
      "groupNumber": "MEDIGAP-PLAN-G",
      "relationshipToSubscriber": "Self",
      "subscriberFirstName": "Margaret",
      "subscriberLastName": "Williams",
      "subscriberDob": "1952-04-30T00:00:00Z",
      "isEmployerPlan": false,
      "effectiveDate": "2017-05-01T00:00:00Z",
      "terminationDate": null,
      "isActive": true,
      "cobPriorityHint": 2,
      "isCobLocked": true,
      "cobNotes": "Medigap Plan G - secondary after Medicare."
    }
  ],
  "createdAtUtc": "2024-06-12T14:45:00Z",
  "updatedAtUtc": "2024-12-15T10:30:00Z"
}
```

### Patient 4: Spouse Coverage
```json
{
  "id": "pat_004",
  "patientId": "pat_004",
  "type": "patient",
  "tenantId": "ten_001",
  "practiceId": "prac_001",
  "firstName": "Sarah",
  "lastName": "Johnson",
  "dateOfBirth": "1990-09-12T00:00:00Z",
  "email": "sarah.johnson@workmail.com",
  "phone": "555-444-5555",
  "coverageEnrollments": [
    {
      "coverageEnrollmentId": "cov_004_vision",
      "payerId": "payer_vsp_001",
      "planType": "Vision",
      "memberId": "VSP-SPOUSE-445566",
      "groupNumber": "SPOUSE-GRP-2024",
      "relationshipToSubscriber": "Spouse",
      "subscriberFirstName": "Michael",
      "subscriberLastName": "Johnson",
      "subscriberDob": "1988-12-05T00:00:00Z",
      "isEmployerPlan": true,
      "effectiveDate": "2023-06-15T00:00:00Z",
      "terminationDate": null,
      "isActive": true,
      "cobPriorityHint": 1,
      "isCobLocked": false,
      "cobNotes": "Covered under spouse's employer plan. Primary subscriber: Michael Johnson"
    }
  ],
  "createdAtUtc": "2024-08-20T09:00:00Z",
  "updatedAtUtc": "2025-01-05T11:30:00Z"
}
```

### Patient 5: Coverage Transition (Old Plan Terminated)
```json
{
  "id": "pat_005",
  "patientId": "pat_005",
  "type": "patient",
  "tenantId": "ten_002",
  "practiceId": "prac_002",
  "firstName": "James",
  "lastName": "Martinez",
  "dateOfBirth": "1978-11-25T00:00:00Z",
  "email": "james.martinez@newjob.com",
  "phone": "555-555-6666",
  "coverageEnrollments": [
    {
      "coverageEnrollmentId": "cov_005_old_vision",
      "payerId": "payer_eyemed_001",
      "planType": "Vision",
      "memberId": "EYEMED-OLD-123456",
      "groupNumber": "OLD-EMPLOYER-2023",
      "relationshipToSubscriber": "Self",
      "subscriberFirstName": "James",
      "subscriberLastName": "Martinez",
      "subscriberDob": "1978-11-25T00:00:00Z",
      "isEmployerPlan": true,
      "effectiveDate": "2023-01-01T00:00:00Z",
      "terminationDate": "2024-12-31T00:00:00Z",
      "isActive": false,
      "cobPriorityHint": null,
      "isCobLocked": false,
      "cobNotes": "Terminated - job change. Replaced by new coverage."
    },
    {
      "coverageEnrollmentId": "cov_005_new_vision",
      "payerId": "payer_vsp_001",
      "planType": "Vision",
      "memberId": "VSP-NEW-789012",
      "groupNumber": "NEW-CORP-2025",
      "relationshipToSubscriber": "Self",
      "subscriberFirstName": "James",
      "subscriberLastName": "Martinez",
      "subscriberDob": "1978-11-25T00:00:00Z",
      "isEmployerPlan": true,
      "effectiveDate": "2025-01-01T00:00:00Z",
      "terminationDate": null,
      "isActive": true,
      "cobPriorityHint": 1,
      "isCobLocked": false,
      "cobNotes": "New employer coverage effective 01/01/2025. Previous EyeMed plan terminated."
    }
  ],
  "createdAtUtc": "2024-09-10T13:20:00Z",
  "updatedAtUtc": "2025-01-02T08:00:00Z"
}
```

---

## 5. Encounter Samples

### Encounter 1: Routine Vision Exam with Dual Coverage & Eligibility Check
```json
{
  "id": "enc_001",
  "encounterId": "enc_001",
  "type": "encounter",
  "tenantId": "ten_001",
  "practiceId": "prac_001",
  "locationId": "loc_001",
  "patientId": "pat_001",
  "visitDate": "2025-01-15T10:00:00Z",
  "visitType": "RoutineVision",
  "externalRef": "APPT-2025-0115-001",
  "coverageDecision": {
    "encounterCoverageDecisionId": "cobdec_001",
    "primaryCoverageEnrollmentId": "cov_001_vision",
    "secondaryCoverageEnrollmentId": "cov_001_medical",
    "cobReason": "RoutineVision_UseVisionPlanPrimary",
    "overriddenByUser": false,
    "overrideNote": null,
    "createdAtUtc": "2025-01-15T09:45:00Z",
    "createdByUserId": "user_frontdesk_01"
  },
  "eligibilityChecks": [
    {
      "eligibilityCheckId": "elig_001",
      "coverageEnrollmentId": "cov_001_vision",
      "payerId": "payer_vsp_001",
      "dateOfService": "2025-01-15T00:00:00Z",
      "requestedAtUtc": "2025-01-15T09:46:00Z",
      "completedAtUtc": "2025-01-15T09:46:03Z",
      "status": "Succeeded",
      "rawStatusCode": "1",
      "rawStatusDescription": "Active Coverage",
      "memberIdSnapshot": "VSP87654321",
      "groupNumberSnapshot": "GRP-TECH-2024",
      "planNameSnapshot": "VSP Choice Network",
      "effectiveDateSnapshot": "2024-01-01T00:00:00Z",
      "terminationDateSnapshot": null,
      "errorMessage": null,
      "coverageLines": [
        {
          "serviceTypeCode": "47",
          "coverageDescription": "Routine Vision Exam",
          "copayAmount": 10.00,
          "coinsurancePercent": null,
          "deductibleAmount": null,
          "remainingDeductible": null,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": null,
          "networkIndicator": "IN",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Annual exam covered. Last exam: 01/20/2024"
        },
        {
          "serviceTypeCode": "47",
          "coverageDescription": "Frames",
          "copayAmount": null,
          "coinsurancePercent": null,
          "deductibleAmount": null,
          "remainingDeductible": null,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": 150.00,
          "networkIndicator": "IN",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Frame allowance every 24 months. Last used: Never"
        },
        {
          "serviceTypeCode": "47",
          "coverageDescription": "Lenses - Single Vision",
          "copayAmount": 25.00,
          "coinsurancePercent": null,
          "deductibleAmount": null,
          "remainingDeductible": null,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": null,
          "networkIndicator": "IN",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Standard lenses covered. Upgrades additional cost."
        }
      ],
      "payloads": [
        {
          "payloadId": "payload_req_001",
          "direction": "Request",
          "format": "X12_270",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_001_request.x12",
          "createdAtUtc": "2025-01-15T09:46:00Z"
        },
        {
          "payloadId": "payload_res_001",
          "direction": "Response",
          "format": "X12_271",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_001_response.x12",
          "createdAtUtc": "2025-01-15T09:46:03Z"
        }
      ]
    }
  ],
  "createdAtUtc": "2025-01-15T09:30:00Z",
  "createdByUserId": "user_frontdesk_01"
}
```

### Encounter 2: Medical Eye Exam (Diabetic Retinopathy Screening)
```json
{
  "id": "enc_002",
  "encounterId": "enc_002",
  "type": "encounter",
  "tenantId": "ten_003",
  "practiceId": "prac_003",
  "locationId": "loc_201",
  "patientId": "pat_003",
  "visitDate": "2025-01-10T14:00:00Z",
  "visitType": "Medical",
  "externalRef": "MED-EXAM-2025-010",
  "coverageDecision": {
    "encounterCoverageDecisionId": "cobdec_002",
    "primaryCoverageEnrollmentId": "cov_003_medicare",
    "secondaryCoverageEnrollmentId": "cov_003_supplemental",
    "cobReason": "Medical_UseMedicarePrimary",
    "overriddenByUser": false,
    "overrideNote": null,
    "createdAtUtc": "2025-01-10T13:45:00Z",
    "createdByUserId": "user_doctor_smith"
  },
  "eligibilityChecks": [
    {
      "eligibilityCheckId": "elig_002",
      "coverageEnrollmentId": "cov_003_medicare",
      "payerId": "payer_medicare_001",
      "dateOfService": "2025-01-10T00:00:00Z",
      "requestedAtUtc": "2025-01-10T13:46:00Z",
      "completedAtUtc": "2025-01-10T13:46:05Z",
      "status": "Succeeded",
      "rawStatusCode": "1",
      "rawStatusDescription": "Active Coverage",
      "memberIdSnapshot": "1EG4-TE5-MK72",
      "groupNumberSnapshot": null,
      "planNameSnapshot": "Medicare Part B",
      "effectiveDateSnapshot": "2017-05-01T00:00:00Z",
      "terminationDateSnapshot": null,
      "errorMessage": null,
      "coverageLines": [
        {
          "serviceTypeCode": "30",
          "coverageDescription": "Health Benefit Plan Coverage - Medical Services",
          "copayAmount": null,
          "coinsurancePercent": 20.00,
          "deductibleAmount": 240.00,
          "remainingDeductible": 0.00,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": null,
          "networkIndicator": "IN",
          "effectiveDate": "2025-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Part B deductible met for 2025. 80/20 coinsurance applies."
        },
        {
          "serviceTypeCode": "33",
          "coverageDescription": "Chiropractic",
          "copayAmount": null,
          "coinsurancePercent": 20.00,
          "deductibleAmount": null,
          "remainingDeductible": null,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": null,
          "networkIndicator": "IN",
          "effectiveDate": "2025-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Diabetic eye exams covered at 80% after deductible."
        }
      ],
      "payloads": [
        {
          "payloadId": "payload_req_002",
          "direction": "Request",
          "format": "X12_270",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_002_request.x12",
          "createdAtUtc": "2025-01-10T13:46:00Z"
        },
        {
          "payloadId": "payload_res_002",
          "direction": "Response",
          "format": "X12_271",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_002_response.x12",
          "createdAtUtc": "2025-01-10T13:46:05Z"
        }
      ]
    }
  ],
  "createdAtUtc": "2025-01-10T13:30:00Z",
  "createdByUserId": "user_doctor_smith"
}
```

### Encounter 3: Pediatric Vision Screening (Single Coverage)
```json
{
  "id": "enc_003",
  "encounterId": "enc_003",
  "type": "encounter",
  "tenantId": "ten_002",
  "practiceId": "prac_002",
  "locationId": "loc_102",
  "patientId": "pat_002",
  "visitDate": "2025-01-12T11:00:00Z",
  "visitType": "RoutineVision",
  "externalRef": "PEDS-2025-0112-005",
  "coverageDecision": {
    "encounterCoverageDecisionId": "cobdec_003",
    "primaryCoverageEnrollmentId": "cov_002_vision",
    "secondaryCoverageEnrollmentId": null,
    "cobReason": "SingleCoverage_PrimaryVision",
    "overriddenByUser": false,
    "overrideNote": null,
    "createdAtUtc": "2025-01-12T10:50:00Z",
    "createdByUserId": "user_peds_nurse"
  },
  "eligibilityChecks": [
    {
      "eligibilityCheckId": "elig_003",
      "coverageEnrollmentId": "cov_002_vision",
      "payerId": "payer_eyemed_001",
      "dateOfService": "2025-01-12T00:00:00Z",
      "requestedAtUtc": "2025-01-12T10:51:00Z",
      "completedAtUtc": "2025-01-12T10:51:02Z",
      "status": "Succeeded",
      "rawStatusCode": "1",
      "rawStatusDescription": "Active Coverage",
      "memberIdSnapshot": "EYEMED-789456123",
      "groupNumberSnapshot": "FAMILY-PLAN-2024",
      "planNameSnapshot": "EyeMed Insight Network",
      "effectiveDateSnapshot": "2024-01-01T00:00:00Z",
      "terminationDateSnapshot": null,
      "errorMessage": null,
      "coverageLines": [
        {
          "serviceTypeCode": "47",
          "coverageDescription": "Pediatric Vision Exam",
          "copayAmount": 0.00,
          "coinsurancePercent": null,
          "deductibleAmount": null,
          "remainingDeductible": null,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": null,
          "networkIndicator": "IN",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Pediatric exam fully covered. Annual benefit. Last exam: 01/15/2024"
        },
        {
          "serviceTypeCode": "47",
          "coverageDescription": "Pediatric Eyeglasses",
          "copayAmount": 0.00,
          "coinsurancePercent": null,
          "deductibleAmount": null,
          "remainingDeductible": null,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": 100.00,
          "networkIndicator": "IN",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Annual eyeglass benefit. Available now."
        }
      ],
      "payloads": [
        {
          "payloadId": "payload_req_003",
          "direction": "Request",
          "format": "X12_270",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_003_request.x12",
          "createdAtUtc": "2025-01-12T10:51:00Z"
        },
        {
          "payloadId": "payload_res_003",
          "direction": "Response",
          "format": "X12_271",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_003_response.x12",
          "createdAtUtc": "2025-01-12T10:51:02Z"
        }
      ]
    }
  ],
  "createdAtUtc": "2025-01-12T10:45:00Z",
  "createdByUserId": "user_peds_nurse"
}
```

### Encounter 4: Failed Eligibility Check (Inactive Coverage)
```json
{
  "id": "enc_004",
  "encounterId": "enc_004",
  "type": "encounter",
  "tenantId": "ten_001",
  "practiceId": "prac_001",
  "locationId": "loc_002",
  "patientId": "pat_004",
  "visitDate": "2025-01-08T15:30:00Z",
  "visitType": "RoutineVision",
  "externalRef": "WALKIN-2025-0108-012",
  "coverageDecision": {
    "encounterCoverageDecisionId": "cobdec_004",
    "primaryCoverageEnrollmentId": "cov_004_vision",
    "secondaryCoverageEnrollmentId": null,
    "cobReason": "SingleCoverage_PrimaryVision",
    "overriddenByUser": false,
    "overrideNote": null,
    "createdAtUtc": "2025-01-08T15:25:00Z",
    "createdByUserId": "user_frontdesk_02"
  },
  "eligibilityChecks": [
    {
      "eligibilityCheckId": "elig_004",
      "coverageEnrollmentId": "cov_004_vision",
      "payerId": "payer_vsp_001",
      "dateOfService": "2025-01-08T00:00:00Z",
      "requestedAtUtc": "2025-01-08T15:26:00Z",
      "completedAtUtc": "2025-01-08T15:26:02Z",
      "status": "Failed",
      "rawStatusCode": "6",
      "rawStatusDescription": "Inactive Coverage",
      "memberIdSnapshot": "VSP-SPOUSE-445566",
      "groupNumberSnapshot": "SPOUSE-GRP-2024",
      "planNameSnapshot": null,
      "effectiveDateSnapshot": null,
      "terminationDateSnapshot": "2024-12-31T00:00:00Z",
      "errorMessage": "Coverage terminated 12/31/2024. Patient needs to update insurance information.",
      "coverageLines": [],
      "payloads": [
        {
          "payloadId": "payload_req_004",
          "direction": "Request",
          "format": "X12_270",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_004_request.x12",
          "createdAtUtc": "2025-01-08T15:26:00Z"
        },
        {
          "payloadId": "payload_res_004",
          "direction": "Response",
          "format": "X12_271",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_004_response.x12",
          "createdAtUtc": "2025-01-08T15:26:02Z"
        }
      ]
    }
  ],
  "createdAtUtc": "2025-01-08T15:20:00Z",
  "createdByUserId": "user_frontdesk_02"
}
```

### Encounter 5: Contact Lens Fitting with Multiple Eligibility Checks
```json
{
  "id": "enc_005",
  "encounterId": "enc_005",
  "type": "encounter",
  "tenantId": "ten_001",
  "practiceId": "prac_001",
  "locationId": "loc_001",
  "patientId": "pat_001",
  "visitDate": "2025-01-18T13:00:00Z",
  "visitType": "ContactLensFitting",
  "externalRef": "CL-FIT-2025-0118-003",
  "coverageDecision": {
    "encounterCoverageDecisionId": "cobdec_005",
    "primaryCoverageEnrollmentId": "cov_001_vision",
    "secondaryCoverageEnrollmentId": "cov_001_medical",
    "cobReason": "ContactLens_UseVisionPlanPrimary",
    "overriddenByUser": true,
    "overrideNote": "Patient requested to use VSP for contact lens benefit instead of glasses.",
    "createdAtUtc": "2025-01-18T12:50:00Z",
    "createdByUserId": "user_optician_03"
  },
  "eligibilityChecks": [
    {
      "eligibilityCheckId": "elig_005a",
      "coverageEnrollmentId": "cov_001_vision",
      "payerId": "payer_vsp_001",
      "dateOfService": "2025-01-18T00:00:00Z",
      "requestedAtUtc": "2025-01-18T12:51:00Z",
      "completedAtUtc": "2025-01-18T12:51:03Z",
      "status": "Succeeded",
      "rawStatusCode": "1",
      "rawStatusDescription": "Active Coverage",
      "memberIdSnapshot": "VSP87654321",
      "groupNumberSnapshot": "GRP-TECH-2024",
      "planNameSnapshot": "VSP Choice Network",
      "effectiveDateSnapshot": "2024-01-01T00:00:00Z",
      "terminationDateSnapshot": null,
      "errorMessage": null,
      "coverageLines": [
        {
          "serviceTypeCode": "47",
          "coverageDescription": "Contact Lens Exam",
          "copayAmount": 60.00,
          "coinsurancePercent": null,
          "deductibleAmount": null,
          "remainingDeductible": null,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": null,
          "networkIndicator": "IN",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Contact lens fitting fee. In lieu of eyeglass benefit."
        },
        {
          "serviceTypeCode": "47",
          "coverageDescription": "Contact Lens Materials",
          "copayAmount": null,
          "coinsurancePercent": null,
          "deductibleAmount": null,
          "remainingDeductible": null,
          "outOfPocketMax": null,
          "remainingOutOfPocket": null,
          "allowanceAmount": 150.00,
          "networkIndicator": "IN",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Contact lens allowance in lieu of frames. Annual benefit available."
        }
      ],
      "payloads": [
        {
          "payloadId": "payload_req_005a",
          "direction": "Request",
          "format": "X12_270",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_005a_request.x12",
          "createdAtUtc": "2025-01-18T12:51:00Z"
        },
        {
          "payloadId": "payload_res_005a",
          "direction": "Response",
          "format": "X12_271",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_005a_response.x12",
          "createdAtUtc": "2025-01-18T12:51:03Z"
        }
      ]
    },
    {
      "eligibilityCheckId": "elig_005b",
      "coverageEnrollmentId": "cov_001_medical",
      "payerId": "payer_bcbs_001",
      "dateOfService": "2025-01-18T00:00:00Z",
      "requestedAtUtc": "2025-01-18T12:52:00Z",
      "completedAtUtc": "2025-01-18T12:52:02Z",
      "status": "Succeeded",
      "rawStatusCode": "1",
      "rawStatusDescription": "Active Coverage",
      "memberIdSnapshot": "BCBS-XYZ123456789",
      "groupNumberSnapshot": "TECHSOL-GRP-001",
      "planNameSnapshot": "BCBS PPO Silver",
      "effectiveDateSnapshot": "2024-01-01T00:00:00Z",
      "terminationDateSnapshot": null,
      "errorMessage": null,
      "coverageLines": [
        {
          "serviceTypeCode": "30",
          "coverageDescription": "Health Benefit Plan Coverage",
          "copayAmount": 30.00,
          "coinsurancePercent": 20.00,
          "deductibleAmount": 1500.00,
          "remainingDeductible": 1200.00,
          "outOfPocketMax": 5000.00,
          "remainingOutOfPocket": 4700.00,
          "allowanceAmount": null,
          "networkIndicator": "IN",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "terminationDate": null,
          "additionalInfo": "Medical coverage available for medically necessary contact lenses only."
        }
      ],
      "payloads": [
        {
          "payloadId": "payload_req_005b",
          "direction": "Request",
          "format": "X12_270",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_005b_request.x12",
          "createdAtUtc": "2025-01-18T12:52:00Z"
        },
        {
          "payloadId": "payload_res_005b",
          "direction": "Response",
          "format": "X12_271",
          "storageUrl": "https://blobstorage.example.com/eligibility/elig_005b_response.x12",
          "createdAtUtc": "2025-01-18T12:52:02Z"
        }
      ]
    }
  ],
  "createdAtUtc": "2025-01-18T12:45:00Z",
  "createdByUserId": "user_optician_03"
}
```

---

## Usage Notes

### Cosmos DB Considerations

1. **Partition Keys:**
   - **Tenant**: `id` (self-partitioned)
   - **Payer**: `id` (self-partitioned, can be tenant-specific or global)
   - **Practice**: `tenantId` (multi-practice per tenant)
   - **Patient**: `tenantId` (multi-patient per tenant)
   - **Encounter**: `tenantId` (multi-encounter per tenant)

2. **Embedded Documents:**
   - **Coverage Enrollments**: Embedded in Patient (1-to-many)
   - **Locations**: Embedded in Practice (1-to-many)
   - **Coverage Decision**: Embedded in Encounter (1-to-1)
   - **Eligibility Checks**: Embedded in Encounter (1-to-many)
   - **Coverage Lines**: Embedded in Eligibility Check (1-to-many)
   - **Payloads**: Embedded in Eligibility Check (1-to-many)

3. **Query Patterns:**
   - Patients by tenant: `SELECT * FROM c WHERE c.tenantId = @tenantId`
   - Active coverage: `JOIN e IN c.coverageEnrollments WHERE e.isActive = true`
   - Recent encounters: `SELECT * FROM c WHERE c.tenantId = @tenantId AND c.visitDate >= @startDate`

### Testing Scenarios

These samples support testing:
- ? Single coverage patients
- ? Dual coverage (Vision + Medical)
- ? Coordination of benefits (COB)
- ? Medicare + Supplemental
- ? Dependent coverage (children, spouse)
- ? Coverage transitions (old ? new)
- ? Multiple eligibility checks per encounter
- ? Failed eligibility checks
- ? Contact lens vs. eyeglasses benefits
- ? Medical eye conditions vs. routine vision

### Seeding Data

To use these samples for seeding:
1. Copy JSON to your seed data file
2. Adjust IDs as needed for your environment
3. Ensure tenant IDs match across documents
4. Verify partition key values are correct
5. Use `UpsertItemAsync` to allow re-running seeds

---

**Created:** January 2025  
**Format:** JSON (Newtonsoft.Json compatible)  
**Target:** Azure Cosmos DB for NoSQL
