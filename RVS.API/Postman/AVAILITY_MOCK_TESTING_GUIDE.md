# Availity Mock Testing with Postman

This guide explains how to test the Availity eligibility integration using the mock client and Postman.

## API Endpoints

The eligibility checks controller exposes these endpoints:

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks/run` | Run eligibility check |
| GET | `/api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks` | List all checks |
| GET | `/api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks/{eligibilityCheckId}` | Get/poll single check |

> **Note:** `tenantId` is derived from the JWT token claims, not the URL path.

---

## Quick Start

### 1. Import Collection into Postman

Import this file from `BF.API\Postman\`:

| File | Type | Description |
|------|------|-------------|
| `Availity_Mock_Eligibility_Tests.postman_collection.json` | Collection | 7 test requests with scripts |

**Import Steps:**
1. Open Postman
2. Click **Import** button (top left)
3. Drag the file or click **Upload Files**
4. Select **"BF API Local"** environment from dropdown (top right)

### 2. Verify Environment Variables

The collection uses these variables from `BF API Local`:

| Variable | Current Value | Description |
|----------|---------------|-------------|
| `baseUrl` | `https://localhost:7116` | API base URL |
| `practiceId` | `prac_001` | Test practice |
| `patientId` | `pat_001` | Emily Rodriguez (has coverages + encounters) |
| `encounterId` | `enc_001` | Routine vision exam |
| `coverageEnrollmentId` | `cov_001_vision` | VSP Vision coverage |
| `bearerToken` | *(set manually)* | Auth0 bearer token |

### 3. Get Bearer Token

1. Start BF.API: `dotnet run` or F5
2. Open Swagger UI: `https://localhost:7116/swagger`
3. Click **Authorize** button
4. Complete Auth0 login
5. Copy the access token from browser dev tools (Network tab ? look for `token` in response)
6. Paste into `bearerToken` environment variable

### 4. Run Tests

Run requests in order:
1. **1. Success - Immediate Complete** ?
2. **2.1 Polling Flow - Initiate Check** ? **2.2 Poll (InProgress)** x2 ? **2.3 Poll (Complete)** ?
3. **3. Error - Payer Error** ?
4. **4. Success - Vision Coverage** ???
5. **5. Error - Communication Error** ??

---

## Prerequisites

1. BF.API running locally in Development mode
2. `AvailityMock:UseMock` set to `true` in `appsettings.Development.json`
3. Valid Auth0 token for API authentication
4. Test data seeded (pat_001, enc_001, cov_001_vision)

## Configuration

### appsettings.Development.json

```json
{
  "AvailityMock": {
    "UseMock": true,
    "DefaultScenario": "Coverages-Complete-i",
    "SimulatedDelayMs": 200
  }
}
```

| Setting | Description |
|---------|-------------|
| `UseMock` | `true` to use mock, `false` for real Availity API |
| `DefaultScenario` | Scenario used when no header is provided |
| `SimulatedDelayMs` | Artificial delay to simulate network latency |

---

## Collection Structure

```
?? Availity Mock Eligibility Tests
??? ? 1. Success - Immediate Complete
??? ? 2.1 Polling Flow - Initiate Check
??? ? 2.2 Polling Flow - Poll (InProgress)
??? ? 2.3 Polling Flow - Poll (Complete)
??? ? 3. Error - Payer Error
??? ??? 4. Success - Vision Coverage
??? ?? 5. Error - Communication Error
```

---

## Test Scenarios Detail

### 1. Success - Immediate Complete

**Scenario ID:** `Coverages-Complete-i`

**Endpoint:** `POST .../eligibility-checks/run`

Tests immediate success where Availity returns complete response.

**Tests Verified:**
- ? Status code is 200 OK
- ? Mock response confirmed (header check)
- ? Status is "Complete"
- ? Raw status code is "4"
- ? Has coverage lines
- ? Plan name is populated
- ? No error message

---

### 2. Async Polling Flow (3 Requests)

**Scenario ID:** `Coverages-Polling-Success-i`

Simulates real Availity async behavior.

#### 2.1 Initiate Check
**Endpoint:** `POST .../eligibility-checks/run`

**Tests Verified:**
- ? Status is "InProgress"
- ? Raw status code is "0"
- ? Next poll time is provided
- ? Saves eligibilityCheckId to `pollingEligibilityCheckId`

#### 2.2 Poll (InProgress) - Run TWICE
**Endpoint:** `GET .../eligibility-checks/{eligibilityCheckId}`

**Tests Verified:**
- ? Status remains "InProgress"
- ? Poll count increments

#### 2.3 Poll (Complete)
**Endpoint:** `GET .../eligibility-checks/{eligibilityCheckId}`

**Tests Verified:**
- ? Status is "Complete"
- ? Raw status code is "4"
- ? Poll count is 3
- ? Has coverage lines

---

### 3. Error - Payer Error

**Scenario ID:** `Coverages-PayerError1-i`

**Endpoint:** `POST .../eligibility-checks/run`

Tests payer rejection scenario.

**Tests Verified:**
- ? Status is "Failed"
- ? Raw status code is "19"
- ? Error message is present
- ? Validation messages exist
- ? No coverage lines on failure

---

### 4. Success - Vision Coverage

**Scenario ID:** `Coverages-Vision-i`

**Endpoint:** `POST .../eligibility-checks/run`

Tests vision-specific coverage response.

**Tests Verified:**
- ? Status is "Complete"
- ? Plan name contains "Vision"
- ? Has vision exam coverage
- ? Has frames coverage
- ? Has lenses coverage
- ? Service type code is "35"

---

### 5. Error - Communication Error

**Scenario ID:** `Coverages-CommunicationError-i`

**Endpoint:** `POST .../eligibility-checks/run`

Tests payer timeout scenario.

**Tests Verified:**
- ? Status is "Failed"
- ? Raw status code is "7"
- ? Error message indicates communication failure
- ? No coverage lines

---

## Available Mock Scenarios

Use these values in the `X-Api-Mock-Scenario-ID` header:

### Success Scenarios

| Scenario ID | Behavior |
|-------------|----------|
| `Coverages-Complete-i` | Immediate success with full benefits data |
| `Coverages-Vision-i` | Vision coverage (frames, lenses, exam) |
| `Coverages-Medical-HDHP-i` | High deductible health plan |
| `Coverages-Dental-i` | Dental coverage (preventive, basic, major) |

### Async Polling Scenarios

| Scenario ID | Behavior |
|-------------|----------|
| `Coverages-InProgress-i` | Always returns InProgress (stays pending) |
| `Coverages-Polling-Success-i` | InProgress ? InProgress ? Complete (3 polls) |
| `Coverages-Polling-Failure-i` | InProgress x3 ? Payer Error (4 polls) |
| `Coverages-Polling-Timeout-i` | Always InProgress (tests max poll limit) |
| `Coverages-Retrying-i` | Retrying ? Complete (2 polls) |

### Error Scenarios

| Scenario ID | Status Code | Description |
|-------------|-------------|-------------|
| `Coverages-PayerError1-i` | 19 | Provider ineligible for inquiries |
| `Coverages-PayerError2-i` | 19 | Subscriber name invalid |
| `Coverages-RequestError1-i` | 400 | Validation failed |
| `Coverages-CommunicationError-i` | 7 | Payer timeout |
| `Coverages-PartialResponse-i` | 3 | Complete but partial data |

---

## Environment Variables

### From BF API Local (Pre-configured)

| Variable | Value | Description |
|----------|-------|-------------|
| `baseUrl` | `https://localhost:7116` | API base URL |
| `practiceId` | `prac_001` | Test practice ID |
| `patientId` | `pat_001` | Emily Rodriguez |
| `encounterId` | `enc_001` | Routine vision exam |
| `coverageEnrollmentId` | `cov_001_vision` | VSP Vision coverage |
| `bearerToken` | *(manual)* | Auth0 bearer token |

### Auto-Managed by Test Scripts

| Variable | Description |
|----------|-------------|
| `createdEligibilityCheckId` | Saved after successful POST |
| `pollingEligibilityCheckId` | Used by polling flow |
| `pollCount` | Tracks polling iterations |

---

## Verifying Mock is Active

Every mock response includes this header:

```
X-Api-Mock-Response: true
```

Each test script checks for this header to confirm mock is being used.

---

## Switching to Real Availity API

1. Set `AvailityMock:UseMock` to `false` in appsettings
2. Configure real Availity credentials
3. Remove the `X-Api-Mock-Scenario-ID` header from requests
4. The `X-Api-Mock-Response` header will NOT appear in responses

---

## Troubleshooting

### "Missing environment variable" warning

Ensure you have the **BF API Local** environment selected in Postman.

### Tests fail with 401 Unauthorized

Your bearer token has expired. Get a new one from Swagger UI.

### Tests fail with 404 Not Found

1. Check that seed data exists. Run the `BF.Data.Cosmos.Seed` project to populate test data.
2. Verify the route is correct: POST uses `/eligibility-checks/run` not just `/eligibility-checks`

### Mock header not in response

1. Ensure `AvailityMock:UseMock` is `true` in appsettings
2. Restart the API after config changes
3. Verify running in Development environment

### Polling flow not completing

1. Run requests in order: 2.1 ? 2.2 ? 2.2 ? 2.3
2. Check that `X-Api-Mock-Scenario-ID` is `Coverages-Polling-Success-i` on all requests
3. If stuck, manually reset `pollCount` to `0` in the environment
