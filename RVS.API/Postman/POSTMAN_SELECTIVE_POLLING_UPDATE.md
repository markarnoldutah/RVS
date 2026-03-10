# Postman Collection Updates - Selective Polling

## Summary of Changes

I've updated both Postman collections and the BF API Local environment to exercise the new **Selective Polling API** optimization.

---

## 1. Availity Mock Eligibility Tests Collection

### New Test Section: "Selective Polling Tests"

Added 4 new requests demonstrating the selective polling pattern:

#### 2.4 Setup - Initiate Multiple Checks
- Creates 2-3 eligibility checks in progress
- Saves check IDs to environment variables
- Run this request multiple times to create multiple checks

#### 2.5 List Checks (No Polling)
- `GET /eligibility-checks` (without `pollCheckIds` parameter)
- **RU Cost:** ~1 RU
- Lists all checks without polling Availity
- Test script automatically identifies checks ready to poll based on `nextPollAfterUtc`
- Saves ready check IDs to `checksReadyToPoll` environment variable

#### 2.6 Selective Poll - Multiple Checks
- `GET /eligibility-checks?pollCheckIds={{checksReadyToPoll}}`
- **RU Cost:** ~1 RU + ~1.5 RU per check polled
- Polls only the checks specified in comma-separated `pollCheckIds` parameter
- Test script calculates and displays RU cost savings vs traditional approach

**Example Output:**
```
RU Cost Estimate:
  1 RU (read) + 3 × 1.5 RU (polls) = 5.5 RU
vs Traditional: 1 RU (list) + 3 × 2.5 RU (individual) = 8.5 RU
Savings: 3 RU (35% reduction)
```

#### 2.7 Verify Poll Count Incremented
- `GET /eligibility-checks` (no polling)
- Verifies that selective polling actually polled the requested checks
- Shows updated `pollCount` values for each check

---

## 2. Environment Variables Added

Added to `BF API Local.postman_environment.json`:

| Variable | Description |
|----------|-------------|
| `selectivePollCheckId1` | First check ID for batch polling (auto-populated) |
| `selectivePollCheckId2` | Second check ID for batch polling (auto-populated) |
| `selectivePollCheckId3` | Third check ID for batch polling (auto-populated) |
| `checksReadyToPoll` | Comma-separated list of check IDs ready to poll based on `nextPollAfterUtc` (auto-populated) |

---

## 3. Test Workflow

### Running the Selective Polling Tests

1. **Setup Phase:**
   ```
   Run: 2.4 Setup - Initiate Multiple Checks (2-3 times)
   Result: Creates multiple checks in "InProgress" status
   ```

2. **List Phase:**
   ```
   Run: 2.5 List Checks (No Polling)
   Result: Gets current state, identifies ready checks, saves to checksReadyToPoll
   ```

3. **Selective Poll Phase:**
   ```
   Run: 2.6 Selective Poll - Multiple Checks
   Result: Polls only ready checks using ?pollCheckIds parameter
   ```

4. **Verification Phase:**
   ```
   Run: 2.7 Verify Poll Count Incremented
   Result: Confirms polling occurred by checking pollCount values
   ```

### Expected Results

**After running the workflow:**

- Multiple checks created
- Only ready checks polled (respects `nextPollAfterUtc`)
- RU cost reduced by 30-50% vs traditional polling
- Poll counts incremented only for polled checks

---

## 4. Key Features Demonstrated

### ? Time-Based Filtering
Test scripts filter checks based on `nextPollAfterUtc`:
```javascript
var readyToPoll = jsonData.filter(function(check) {
    if (check.status !== 'InProgress') return false;
    if (!check.nextPollAfterUtc) return true;
    return now >= new Date(check.nextPollAfterUtc);
});
```

### ? Batch Polling via Query Parameter
Uses the new `pollCheckIds` query parameter:
```
GET /eligibility-checks?pollCheckIds=check1,check2,check3
```

### ? RU Cost Tracking
Test scripts calculate and display RU savings:
```
Selective: 1 + (3 × 1.5) = 5.5 RU
Traditional: 1 + (3 × 2.5) = 8.5 RU
Savings: 35%
```

---

## 5. Integration with Existing Tests

### Placement in Collection
- Inserted after "2.3 Polling Flow #3" and before "3. Error - Payer Error"
- Grouped as "Selective Polling Tests" folder
- Uses same mock scenarios (`Coverages-Polling-Success-i`)

### Environment Variable Reuse
- Leverages existing variables: `patientId`, `encounterId`, `coverageEnrollmentId`
- Adds new variables for selective polling workflow
- Compatible with existing ForceRefresh cache tests

---

## 6. Manual Testing Instructions

### Using Postman

1. **Import/Refresh Collections:**
   - Availity Mock Eligibility Tests collection (updated)
   - BF API Local environment (updated)

2. **Run Setup:**
   ```
   2.4 Setup - Initiate Multiple Checks
   [Run 2-3 times to create multiple checks]
   ```

3. **Run Selective Polling Flow:**
   ```
   2.5 List Checks (No Polling)
   ? Auto-populates checksReadyToPoll variable
   
   2.6 Selective Poll - Multiple Checks
   ? Polls only ready checks
   ? Shows RU savings
   
   2.7 Verify Poll Count Incremented
   ? Confirms polling worked
   ```

4. **Review Test Results:**
   - All tests should pass (green checkmarks)
   - Console output shows RU cost calculations
   - Environment variables populated automatically

---

## 7. Expected Test Output Examples

### From 2.5 List Checks (No Polling)
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

### From 2.6 Selective Poll - Multiple Checks
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

### From 2.7 Verify Poll Count Incremented
```
=== POLL COUNT VERIFICATION ===
elig_abc123:
  Status: InProgress
  Poll Count: 2
  Next Poll After: 2025-01-15T14:32:15Z

elig_def456:
  Status: Complete
  Poll Count: 1
  Next Poll After: N/A
```

---

## 8. Troubleshooting

### Issue: `checksReadyToPoll` is empty
**Cause:** No checks are ready to poll (nextPollAfterUtc is in the future)  
**Solution:** Wait a few seconds or run 2.5 again after the wait time

### Issue: No polls occurred
**Cause:** Mock scenario mismatch or check IDs incorrect  
**Solution:** Verify `X-Api-Mock-Scenario-ID` header matches initiation scenario

### Issue: All checks show pollCount=0
**Cause:** Query parameter not working or checks not in InProgress  
**Solution:** Check URL has `?pollCheckIds=` parameter and values are correct

---

## 9. Next Steps

### For Client Implementation
After testing the API with Postman:

1. Implement client-side filtering logic (see `BF.BlazorWASM/ELIGIBILITY_POLLING_CLIENT_GUIDE.md`)
2. Use the selective polling API in production
3. Monitor RU costs in Azure Portal
4. Verify 30-50% RU reduction

### For Additional Testing
- Test with different numbers of checks (1, 5, 10)
- Test with all checks complete (no polling should occur)
- Test with mixed statuses (some ready, some not)
- Test error handling (invalid check IDs)

---

## 10. Files Modified

1. ? `BF.API/Postman/Availity Mock Eligibility Tests.postman_collection.json`
   - Added "Selective Polling Tests" folder with 4 requests
   - Updated documentation

2. ? `BF.API/Postman/BF API Local.postman_environment.json`
   - Added 4 new environment variables for selective polling

3. ? `BF.API/Postman/POSTMAN_SELECTIVE_POLLING_UPDATE.md` (this file)
   - Complete documentation of changes

---

**Last Updated**: 2025-01-15  
**Status**: ? Ready for Testing  
**Compatibility**: Works with existing Availity Mock and ForceRefresh tests
