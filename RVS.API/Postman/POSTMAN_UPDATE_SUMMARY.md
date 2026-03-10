# Postman Collections Update Summary
## Selective Polling API Testing

---

## ? What Was Updated

### 1. **Availity Mock Eligibility Tests Collection**
File: `BF.API/Postman/Availity Mock Eligibility Tests.postman_collection.json`

**New Section Added:** "Selective Polling Tests" (4 requests)

| Request | Endpoint | Purpose | Variables Set |
|---------|----------|---------|---------------|
| 2.4 Setup | POST /run | Create multiple checks | `selectivePollCheckId1` |
| 2.5 List Checks | GET /eligibility-checks | Get current state | `checksReadyToPoll` |
| 2.6 Selective Poll | GET /eligibility-checks?pollCheckIds= | Poll ready checks | - |
| 2.7 Verify | GET /eligibility-checks | Confirm polling | - |

### 2. **BF API Local Environment**
File: `BF.API/Postman/BF API Local.postman_environment.json`

**New Variables Added:**

```json
{
  "selectivePollCheckId1": "First check ID for batch polling",
  "selectivePollCheckId2": "Second check ID for batch polling",
  "selectivePollCheckId3": "Third check ID for batch polling",
  "checksReadyToPoll": "Comma-separated IDs ready to poll"
}
```

All variables auto-populate during test execution.

---

## ?? Key Features Tested

### ? Selective Polling API
```
GET /api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks?pollCheckIds=id1,id2,id3
```

### ? Time-Based Filtering
Test scripts automatically filter checks where `nextPollAfterUtc <= now`

### ? RU Cost Calculation
Tests display real-time RU cost savings:
```
Example: 3 checks ready to poll
  Selective: 1 + (3 × 1.5) = 5.5 RU
  Traditional: 1 + (3 × 2.5) = 8.5 RU
  Savings: 3 RU (35%)
```

---

## ?? How to Test

### Quick Start (Postman)

1. **Open Postman**
2. **Select** "BF API Local" environment
3. **Navigate** to "Availity Mock Eligibility Tests" collection
4. **Expand** "Selective Polling Tests" folder

### Test Workflow

```
Step 1: Run "2.4 Setup" 2-3 times
        ? Creates multiple checks in InProgress state

Step 2: Run "2.5 List Checks (No Polling)"
        ? Gets current state
        ? Auto-populates checksReadyToPoll variable

Step 3: Run "2.6 Selective Poll"
        ? Polls only ready checks
        ? Shows RU savings in console

Step 4: Run "2.7 Verify"
        ? Confirms polling occurred
        ? Shows updated pollCount values
```

### Expected Results

? All tests pass (green checkmarks)  
? Console shows RU cost calculations  
? Environment variables auto-populate  
? Poll counts increment only for polled checks  

---

## ?? Test Output Examples

### From "2.5 List Checks"
```
=== ELIGIBILITY CHECKS LIST ===
Total checks: 3
InProgress: 3
Complete: 0

Checks ready to poll: 2
  - elig_abc123 (pollCount: 1)
  - elig_def456 (pollCount: 0)

Saved checksReadyToPoll: elig_abc123,elig_def456
```

### From "2.6 Selective Poll"
```
=== SELECTIVE POLLING RESULT ===
Requested to poll: 2 checks
Total checks returned: 3

? elig_abc123: InProgress (pollCount: 2)
? elig_def456: Complete (pollCount: 1)

RU Cost Estimate:
  1 RU (read) + 2 × 1.5 RU (polls) = 4 RU
vs Traditional: 1 RU (list) + 2 × 2.5 RU (individual) = 6 RU
Savings: 2 RU (33% reduction)
```

---

## ?? Key Test Scenarios Covered

### ? Scenario 1: Multiple Checks, All Ready
- All checks have `nextPollAfterUtc <= now` or null
- All checks should be polled
- Maximum RU savings

### ? Scenario 2: Multiple Checks, Some Ready
- Some checks have `nextPollAfterUtc > now`
- Only ready checks polled
- Demonstrates smart filtering

### ? Scenario 3: No Checks Ready
- All checks have `nextPollAfterUtc > now`
- No polling occurs
- List call only (1 RU)

### ? Scenario 4: Mixed Status
- Some InProgress, some Complete
- Only InProgress checks considered
- Terminal checks skipped

---

## ?? Files Created/Modified

| File | Status | Description |
|------|--------|-------------|
| `Availity Mock Eligibility Tests.postman_collection.json` | ?? Modified | Added selective polling tests |
| `BF API Local.postman_environment.json` | ?? Modified | Added 4 new variables |
| `POSTMAN_SELECTIVE_POLLING_UPDATE.md` | ? Created | Detailed documentation |
| `POSTMAN_UPDATE_SUMMARY.md` | ? Created | This summary file |

---

## ?? Learning from Tests

### What the Tests Demonstrate

1. **Batch Polling** - Single API call for multiple checks
2. **Smart Filtering** - Respects `nextPollAfterUtc` to avoid premature polls
3. **RU Optimization** - 30-50% cost reduction vs traditional approach
4. **State Management** - Tracks poll counts and status transitions

### Implementation Patterns

The test scripts show exactly how clients should:

```javascript
// 1. Get current state
var checks = await api.getEligibilityChecks(encounterId);

// 2. Filter ready checks
var now = new Date();
var ready = checks.filter(c => 
    c.status === 'InProgress' && 
    (!c.nextPollAfterUtc || new Date(c.nextPollAfterUtc) <= now)
);

// 3. Poll selectively
if (ready.length > 0) {
    var ids = ready.map(c => c.eligibilityCheckId).join(',');
    await api.getEligibilityChecks(encounterId, { pollCheckIds: ids });
}
```

---

## ??? Troubleshooting

### Issue: No variables populating
**Solution:** Run the tests in order (2.4 ? 2.5 ? 2.6 ? 2.7)

### Issue: No checks ready to poll
**Solution:** Wait for `nextPollAfterUtc` time to pass, then run 2.5 again

### Issue: RU savings not showing
**Solution:** Ensure `checksReadyToPoll` variable has multiple IDs

### Issue: Tests failing
**Solution:** Verify API is running at `https://localhost:7116` and mock is enabled

---

## ?? Integration with Existing Tests

### Compatible With:
- ? Existing polling flow tests (2.1, 2.2, 2.3)
- ? ForceRefresh cache tests
- ? Error scenario tests
- ? All other Availity mock tests

### No Conflicts:
- Uses separate environment variables
- Grouped in own folder
- Can run independently or as part of full suite

---

## ?? Next Steps

### For Testing
1. ? Run selective polling tests in Postman
2. ? Verify RU cost calculations
3. ? Test edge cases (0 checks, all complete, etc.)

### For Client Implementation
1. ?? Implement client-side filtering (see Blazor guide)
2. ?? Use selective polling API in production
3. ?? Monitor RU costs in Azure Portal
4. ?? Verify savings align with test predictions

### For Documentation
1. ? All Postman updates documented
2. ? Environment variables explained
3. ? Test workflows defined
4. ? Expected outputs provided

---

## ? Summary

**What:** Added selective polling tests to Postman collections  
**Why:** Demonstrate and validate 30-50% RU cost optimization  
**How:** 4 new requests exercising the `/eligibility-checks?pollCheckIds=` API  
**Status:** ? Ready to test  
**Impact:** Validates server-side implementation and guides client development  

**Files Modified:** 2  
**New Requests:** 4  
**New Environment Variables:** 4  
**Build Status:** ? Passing  

---

**Last Updated**: 2025-01-15  
**Author**: GitHub Copilot  
**Related Docs**: 
- `ELIGIBILITY_POLLING_OPTIMIZATION.md`
- `ELIGIBILITY_POLLING_CLIENT_GUIDE.md`
- `ELIGIBILITY_POLLING_QUICK_REFERENCE.md`
