# BF.Domain Sample Data

Comprehensive sample documents for all BF.Domain entities with fully populated embedded documents for testing and seeding.

## ?? Files in This Directory

| File | Entities | Count | Description |
|------|----------|-------|-------------|
| `EntitySamples.md` | All | 25+ | Complete documentation with embedded JSON samples |
| `tenants.json` | Tenant | 5 | Tenant organizations (Enterprise, Pro, Starter, Trial, Suspended) |
| `payers.json` | Payer | 5 | Insurance payers (VSP, BCBS, EyeMed, Medicare, Medicaid) |
| `practices.json` | Practice | 5 | Practice locations with embedded Location entities |
| `patients.json` | Patient | 5 | Patients with embedded CoverageEnrollment entities |
| `encounters.json` | Encounter | 5 | Encounters with embedded EligibilityCheck, CoverageLine, Payload entities |

---

## ?? Usage Scenarios

### 1. Manual Testing in Postman
Import individual JSON files to create test data via API endpoints.

**Example: Create Patient**
```http
POST https://localhost:7001/api/patients
Content-Type: application/json

{
  "firstName": "Emily",
  "lastName": "Rodriguez",
  "dateOfBirth": "1985-03-15T00:00:00Z",
  ...
}
```

### 2. Cosmos DB Data Explorer
Use Azure Portal to directly import JSON documents:
1. Navigate to Cosmos DB account
2. Select container (e.g., `patients`)
3. Click **New Item**
4. Paste JSON from sample files
5. Click **Save**

### 3. Automated Seeding
Reference these samples in your `BF.Data.Cosmos.Seed` project:

```csharp
// Read from sample files
var tenantsJson = File.ReadAllText("BF.Domain/SampleData/tenants.json");
var tenants = JsonConvert.DeserializeObject<List<Tenant>>(tenantsJson);

foreach (var tenant in tenants)
{
    await tenantsContainer.UpsertItemAsync(tenant, new PartitionKey(tenant.Id));
}
```

### 4. Integration Tests
Use samples as test fixtures:

```csharp
[Test]
public async Task CreatePatient_WithDualCoverage_Success()
{
    // Arrange
    var patientJson = File.ReadAllText("SampleData/patients.json");
    var patients = JsonConvert.DeserializeObject<List<Patient>>(patientJson);
    var testPatient = patients.First(p => p.CoverageEnrollments.Count == 2);
    
    // Act & Assert
    ...
}
```

---

## ?? Sample Data Overview

### Tenants (5 samples)
- ? **Enterprise**: Large multi-location group (50+ locations)
- ? **Professional**: Mid-size practice (5 locations)
- ? **Starter**: Small independent practice (1 location)
- ? **Trial**: 30-day trial account
- ? **Suspended**: Payment issue scenario

### Payers (5 samples)
- ? **VSP**: National vision plan (global)
- ? **Blue Cross Blue Shield**: National medical plan (global)
- ? **EyeMed**: Vision care network (global)
- ? **Medicare Part B**: Government medical insurance (global)
- ? **Medi-Cal**: California Medicaid (tenant-specific)

### Practices (5 samples)
- ? **Multi-location downtown**: 3 locations with different addresses
- ? **Suburban specialty center**: 2 locations (general + pediatric)
- ? **Single-location family practice**: 1 location
- ? **Retail partner**: 2 Costco optical departments
- ? **Closed location**: Inactive practice scenario

### Patients (5 samples)
- ? **Dual coverage**: Vision + Medical (routine scenario)
- ? **Child dependent**: Under parent's plan
- ? **Medicare + Supplemental**: Senior with dual coverage
- ? **Spouse coverage**: Covered under spouse's employer
- ? **Coverage transition**: Old plan terminated, new plan started

### Encounters (5 samples)
- ? **Routine vision exam**: Dual coverage with successful eligibility
- ? **Medical eye exam**: Medicare diabetic screening
- ? **Pediatric screening**: Single vision coverage
- ? **Failed eligibility**: Inactive coverage scenario
- ? **Contact lens fitting**: Multiple eligibility checks, user override

---

## ?? Key Features

### Embedded Documents
All samples include realistic embedded documents:

**Patient ? Coverage Enrollments**
```json
"coverageEnrollments": [
  {
    "coverageEnrollmentId": "cov_001_vision",
    "payerId": "payer_vsp_001",
    "planType": "Vision",
    "memberId": "VSP87654321",
    ...
  }
]
```

**Practice ? Locations**
```json
"locations": [
  {
    "locationId": "loc_001",
    "name": "Main Street Clinic",
    "address1": "123 Main Street",
    ...
  }
]
```

**Encounter ? Eligibility Checks ? Coverage Lines**
```json
"eligibilityChecks": [
  {
    "eligibilityCheckId": "elig_001",
    "coverageLines": [
      {
        "serviceTypeCode": "47",
        "coverageDescription": "Routine Vision Exam",
        "copayAmount": 10.00,
        ...
      }
    ],
    "payloads": [...]
  }
]
```

### Realistic Data
- ? Proper date/time formats (ISO 8601)
- ? Valid US addresses and phone numbers
- ? Realistic insurance member IDs and group numbers
- ? X12 service type codes (47 = Vision, 30 = Medical)
- ? COB (Coordination of Benefits) scenarios
- ? Coverage status variations (Active, Terminated, Failed)

---

## ?? Quick Start

### Option 1: Manual Import (Fastest)
1. Copy JSON from `EntitySamples.md`
2. Paste into Postman request body
3. Send to appropriate API endpoint

### Option 2: File-Based Import
1. Use individual JSON files (`tenants.json`, `patients.json`, etc.)
2. Import via Cosmos DB Data Explorer
3. Or reference in seed script

### Option 3: Programmatic Seed
```csharp
// In BF.Data.Cosmos.Seed/Program.cs
var sampleDataPath = "../../BF.Domain/SampleData";

var tenantsJson = File.ReadAllText($"{sampleDataPath}/tenants.json");
var tenants = JsonConvert.DeserializeObject<List<Tenant>>(tenantsJson);

foreach (var tenant in tenants)
{
    await container.UpsertItemAsync(tenant, new PartitionKey(tenant.Id));
    Console.WriteLine($"Seeded tenant: {tenant.Name}");
}
```

---

## ?? Testing Scenarios

### Scenario 1: Routine Vision Visit
```
Patient: Emily Rodriguez (pat_001)
Coverage: VSP Vision (cov_001_vision) PRIMARY
         BCBS Medical (cov_001_medical) SECONDARY
Encounter: Routine exam (enc_001)
Eligibility: Successful, copay $10, frames $150 allowance
```

### Scenario 2: Medical Eye Condition
```
Patient: Margaret Williams (pat_003)
Coverage: Medicare Part B (cov_003_medicare) PRIMARY
         BCBS Medigap (cov_003_supplemental) SECONDARY
Encounter: Diabetic eye exam (enc_002)
Eligibility: Successful, 80/20 coinsurance, deductible met
```

### Scenario 3: Pediatric Visit
```
Patient: Oliver Chen (pat_002)
Coverage: EyeMed (cov_002_vision) as dependent
Encounter: Annual screening (enc_003)
Eligibility: Successful, $0 copay, $100 eyeglass allowance
```

### Scenario 4: Eligibility Failure
```
Patient: Sarah Johnson (pat_004)
Coverage: VSP (cov_004_vision) TERMINATED 12/31/2024
Encounter: Walk-in visit (enc_004)
Eligibility: FAILED - Coverage inactive
```

### Scenario 5: Contact Lens Benefit
```
Patient: Emily Rodriguez (pat_001)
Coverage: VSP Vision PRIMARY, BCBS Medical SECONDARY
Encounter: Contact lens fitting (enc_005)
Eligibility: Two checks performed
            VSP: $60 fitting fee, $150 materials allowance
            BCBS: Available for medically necessary only
```

---

## ?? Data Relationships

### Foreign Keys
```
Patient.practiceId ? Practice.id
Patient.tenantId ? Tenant.id
CoverageEnrollment.payerId ? Payer.id
Encounter.patientId ? Patient.id
Encounter.practiceId ? Practice.id
Encounter.locationId ? Location.locationId (embedded)
```

### Partition Keys
```
Tenant: id (self)
Payer: id (self)
Practice: tenantId
Patient: tenantId
Encounter: tenantId
```

---

## ?? Important Notes

### ID Consistency
Ensure IDs match across related documents:
- Patient references must use valid `practiceId`
- Encounter references must use valid `patientId`
- Coverage enrollments must use valid `payerId`

### Date Formats
All dates use ISO 8601 format with UTC timezone:
```json
"visitDate": "2025-01-15T10:00:00Z"
```

### Partition Key Values
Cosmos DB queries require partition key:
```csharp
// Correct
await container.ReadItemAsync<Patient>("pat_001", new PartitionKey("ten_001"));

// Will fail - wrong partition key
await container.ReadItemAsync<Patient>("pat_001", new PartitionKey("ten_002"));
```

---

## ?? Updating Sample Data

### To Add New Samples
1. Edit `EntitySamples.md` with new JSON
2. Extract to individual JSON file if needed
3. Update counts in this README
4. Document new scenarios

### To Modify Existing
1. Maintain ID consistency across files
2. Keep partition keys correct
3. Update related documents if IDs change
4. Test in Postman before committing

---

## ?? Additional Resources

- [Cosmos DB Best Practices](https://docs.microsoft.com/azure/cosmos-db/best-practice-dotnet)
- [JSON.NET Documentation](https://www.newtonsoft.com/json/help)
- [Postman Collections](../API/Postman/)
- [BF.API Controllers](../API/Controllers/)

---

**Created:** January 2025  
**Format:** JSON (Newtonsoft.Json)  
**Target:** Azure Cosmos DB for NoSQL  
**Schema Version:** 1.0
