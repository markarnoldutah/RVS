# 🚀 Quick Test Guide: Selective Polling API

## One-Page Reference for Postman Testing

---

## Prerequisites ✓

- [ ] Postman installed
- [ ] "BF API Local" environment selected
- [ ] API running at `https://localhost:7116`
- [ ] `bearerToken` environment variable set
- [ ] `AvailityMock:UseMock` = `true` in appsettings.Development.json

---

## 6-Step Test Workflow

### Step 1️⃣: Create Check #1 (InProgress)

**Request:** `7.1 Setup - Initiate Check #1 (InProgress)`

```
POST /eligibility-checks/run
Body: { coverageEnrollmentId, serviceTypeCodes: ["30"], forceRefresh: true }
Header: X-Api-Mock-Scenario-ID: Coverages-Polling-Success-i
```

**Result:** Creates check in "InProgress" status  
**Variable Set:** `selectivePollCheckId1`

---

### Step 2️⃣: Create Check #2 (InProgress)

**Request:** `7.2 Setup - Initiate Check #2 (InProgress)`

```
POST /eligibility-checks/run
Body: { coverageEnrollmentId, serviceTypeCodes: ["35"], forceRefresh: true }
Header: X-Api-Mock-Scenario-ID: Coverages-Polling-Success-i
```

**Result:** Creates second check in "InProgress" status  
**Variables Set:** `selectivePollCheckId2`, `checksReadyToPoll` (comma-separated)

---

### Step 3️⃣: List All Checks (No Polling)

**Request:** `7.3 List Checks (No Polling)`

```
GET /eligibility-checks
```

**RU Cost:** ~1 RU  
**Result:** Lists all checks without triggering any Availity polls  

**Console Output:**
```
=== ELIGIBILITY CHECKS LIST (NO POLLING) ===
Total checks: 2
InProgress: 2
Complete: 0

elig_abc123: pollCount=0
elig_def456: pollCount=0

Now run 7.4 to selectively poll both checks.
```

---

### Step 4️⃣: Selective Poll - Both Checks

**Request:** `7.4 Selective Poll - Both Checks`

```
GET /eligibility-checks?pollCheckIds={{checksReadyToPoll}}
Header: X-Api-Mock-Scenario-ID: Coverages-Polling-Success-i
```

**RU Cost:** ~1 RU + ~1.5 RU per check  
**Result:** Polls only specified checks in a single API call  

**Console Output:**
```
=== SELECTIVE POLLING RESULT ===
Requested to poll: 2 checks

elig_abc123:
  Status: InProgress
  Poll Count: 1
  Next Poll After: 2025-01-15T14:32:15Z

elig_def456:
  Status: InProgress
  Poll Count: 1
  Next Poll After: 2025-01-15T14:32:15Z

RU Cost Estimate:
  Selective: 1 RU (read) + 2 × 1.5 RU (polls) = 4 RU
  Traditional: 2 × 2.5 RU (individual GETs) = 5 RU
  Savings: 1 RU (20% reduction)
```

---

### Step 5️⃣: Verify Polling Occurred

**Request:** `7.5 Verify Poll Counts Incremented`

```
GET /eligibility-checks
```

**Result:** Confirms polling occurred by checking pollCount >= 1  

**Console Output:**
```
=== POLL COUNT VERIFICATION ===
Check #1 (elig_abc123):
  Status: InProgress
  Poll Count: 1

Check #2 (elig_def456):
  Status: InProgress
  Poll Count: 1

Selective polling verified! Both checks were polled in a single API call.
```

---

### Step 6️⃣: Continue Polling Until Complete

**Request:** `7.6 Continue Polling Until Complete`

```
GET /eligibility-checks?pollCheckIds={{checksReadyToPoll}}
Header: X-Api-Mock-Scenario-ID: Coverages-Polling-Success-i
```

**Run 2-3 times until both checks are Complete** (mock requires 3 polls)

**Console Output (when complete):**
```
=== POLLING STATUS ===
Check #1: Complete (pollCount: 3)
Check #2: Complete (pollCount: 3)

All checks complete! Selective polling flow finished.
```

---

## Expected Test Results

| Test | Expected Outcome |
|------|------------------|
| 7.1 Setup #1 | ✅ 200 OK, status="InProgress", selectivePollCheckId1 set |
| 7.2 Setup #2 | ✅ 200 OK, status="InProgress", checksReadyToPoll populated |
| 7.3 List Checks | ✅ 200 OK, array returned, both checks at pollCount=0 |
| 7.4 Selective Poll | ✅ 200 OK, mock called, pollCount incremented |
| 7.5 Verify | ✅ 200 OK, pollCount >= 1 for both checks |
| 7.6 Complete | ✅ 200 OK, both checks reach Complete status |

---

## RU Cost Comparison

### Example: 2 Checks in Progress

| Method | RU Cost | Calculation |
|--------|---------|-------------|
| **Selective Poll** | 4 RU | 1 (read) + 2×1.5 (polls) |
| **Traditional** | 5 RU | 2×2.5 (individual GETs) |
| **Savings** | **1 RU** | **20% reduction** |

### Example: 3 Checks in Progress

| Method | RU Cost | Calculation |
|--------|---------|-------------|
| **Selective Poll** | 5.5 RU | 1 (read) + 3×1.5 (polls) |
| **Traditional** | 7.5 RU | 3×2.5 (individual GETs) |
| **Savings** | **2 RU** | **27% reduction** |

---

## Key Environment Variables

| Variable | Auto-Populated By | Purpose |
|----------|-------------------|---------|
| `selectivePollCheckId1` | 7.1 Setup | First check ID |
| `selectivePollCheckId2` | 7.2 Setup | Second check ID |
| `selectivePollCheckId3` | (optional) | Third check ID |
| `checksReadyToPoll` | 7.2 Setup | Comma-separated IDs for pollCheckIds |

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No mock response header | Verify `X-Api-Mock-Scenario-ID` header is set |
| `checksReadyToPoll` is empty | Run 7.1 and 7.2 first to create checks |
| Checks not completing | Mock requires 3 polls - run 7.6 multiple times |
| Tests fail | Verify API running, mock enabled, bearer token set |

---

## Quick Commands

### Full Test Cycle
```
7.1 → 7.2 → 7.3 → 7.4 → 7.5 → 7.6 → 7.6 → 7.6
```

### Reset Variables
```
Environment → Edit → Delete values for:
- selectivePollCheckId1
- selectivePollCheckId2
- selectivePollCheckId3
- checksReadyToPoll
```

---

## Success Criteria

✅ All 6 tests pass (green checkmarks)  
✅ Console shows RU cost calculations  
✅ Environment variables populated  
✅ Poll counts increment with each 7.4/7.6 call  
✅ Both checks reach Complete after 3 polls  

---

## Next Steps After Testing

1. ✅ Verify API behavior
2. 📝 Implement client-side logic (see `ELIGIBILITY_POLLING_CLIENT_GUIDE.md`)
3. 📊 Monitor RU costs in Azure Portal
4. 🎯 Deploy to production with confidence

---

**Collection:** Availity Mock Eligibility Tests v2  
**Section:** Selective Polling Tests  
**Requests:** 7.1, 7.2, 7.3, 7.4, 7.5, 7.6  
**Time to Complete:** ~3 minutes  
**Difficulty:** ⭐ Easy  

---

**Print this page and keep it handy while testing!** 📄
