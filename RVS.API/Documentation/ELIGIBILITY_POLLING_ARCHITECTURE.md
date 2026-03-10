# Eligibility Check Polling Architecture

## System Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          BLAZOR WASM CLIENT                                 │
│                                                                             │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │ EncounterDetailsPage.razor                                         │     │
│  │                                                                    │     │
│  │  1. OnInitialized()                                                │     │
│  │     └─→ LoadChecksAsync()                                          │     │
│  │         └─→ GET /eligibility-checks (no polling) [1 RU]            │     │
│  │                                                                    │     │
│  │  2. If any Status = "InProgress"                                   │     │
│  │     └─→ StartPollingAsync()                                        │     │
│  └────────────────────────────────────────────────────────────────────┘     │
│                              │                                              │
│                              ▼                                              │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │ EligibilityCheckPollingService                                     │     │
│  │                                                                    │     │
│  │  Sequential Polling Loop (await Task.Delay between calls):         │     │
│  │    │                                                               │     │
│  │    ├─→ 1. GET /eligibility-checks [1 RU]                           │     │
│  │    │                                                               │     │
│  │    ├─→ 2. Filter checks where:                                     │     │
│  │    │      • Status = "InProgress"                                  │     │
│  │    │      • NextPollAfterUtc is null OR <= Now                     │     │
│  │    │        (meaning: no restriction OR wait time has passed)      │     │
│  │    │                                                               │     │
│  │    ├─→ 3. If checks ready:                                         │     │
│  │    │      GET /eligibility-checks?pollCheckIds=id1,id2             │     │
│  │    │      [1 RU + 1.5 RU per check]                                │     │
│  │    │                                                               │     │
│  │    ├─→ 4. If all terminal → Stop polling                           │     │
│  │    │                                                               │     │
│  │    └─→ 5. await Task.Delay(2s) → Loop back to step 1               │     │
│  │          (Delay starts AFTER response, not during call)            │     │
│  └────────────────────────────────────────────────────────────────────┘     │
│                              │                                              │
└──────────────────────────────┼──────────────────────────────────────────────┘
                               │ HTTP
                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            ASP.NET CORE API                                 │
│                                                                             │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │ EligibilityChecksController                                        │     │
│  │                                                                    │     │
│  │  GET /eligibility-checks?pollCheckIds={ids}                        │     │
│  │    │                                                               │     │
│  │    ├─→ Parse comma-separated IDs                                   │     │
│  │    │                                                               │     │
│  │    ├─→ If pollCheckIds provided:                                   │     │
│  │    │     └─→ GetEligibilityChecksWithSelectivePollingAsync()       │     │
│  │    │                                                               │     │
│  │    └─→ Else:                                                       │     │
│  │          └─→ GetEligibilityChecksAsync()                           │     │
│  └────────────────────────────────────────────────────────────────────┘     │
│                              │                                              │
│                              ▼                                              │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │ EligibilityCheckService                                            │     │
│  │                                                                    │     │
│  │  GetEligibilityChecksWithSelectivePollingAsync(pollCheckIds)       │     │
│  │    │                                                               │     │
│  │    ├─→ 1. Load Patient document (1 RU - point read)                │     │
│  │    │                                                               │     │
│  │    ├─→ 2. Get encounter → eligibilityChecks[]                      │     │
│  │    │                                                               │     │
│  │    ├─→ 3. If pollCheckIds empty → return (no polling)              │     │
│  │    │                                                               │     │
│  │    └─→ 4. For each checkId in pollCheckIds:                        │     │
│  │          │                                                         │     │
│  │          ├─→ Find check in array                                   │     │
│  │          │                                                         │     │
│  │          ├─→ If IsPollingRequired:                                 │     │
│  │          │     └─→ PollAndUpdateAsync()                            │     │
│  │          │         │                                               │     │
│  │          │         ├─→ Call Availity API                           │     │
│  │          │         │                                               │     │
│  │          │         ├─→ Update check.Status, PollCount, etc.        │     │
│  │          │         │                                               │     │
│  │          │         └─→ Write to Cosmos (~1.5 RU)                   │     │
│  │          │                                                         │     │
│  │          └─→ Return all checks (updated in-place)                  │     │
│  └────────────────────────────────────────────────────────────────────┘     │
│                              │                                              │
└──────────────────────────────┼──────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         COSMOS DB PATIENT CONTAINER                         │
│                                                                             │
│  Patient Document (~124KB)                                                  │
│  {                                                                          │
│    "id": "patient123",                                                      │
│    "practiceId": "practice1",  ← Partition Key                              │
│    "encounters": [                                                          │
│      {                                                                      │
│        "id": "enc1",                                                        │
│        "eligibilityChecks": [                                               │
│          {                                                                  │
│            "eligibilityCheckId": "check1",                                  │
│            "status": "InProgress",                                          │
│            "nextPollAfterUtc": "2025-01-15T10:30:05Z",  ← Client uses this  │
│            "pollCount": 2,                                                  │
│            "availityCoverageId": "av_12345",                                │
│            ...                                                              │
│          },                                                                 │
│          {                                                                  │
│            "eligibilityCheckId": "check2",                                  │
│            "status": "Complete",  ← Terminal state                          │
│            "completedAtUtc": "2025-01-15T10:29:45Z",                        │
│            ...                                                              │
│          }                                                                  │
│        ]                                                                    │
│      }                                                                      │
│    ]                                                                        │
│  }                                                                          │
│                                                                             │
│  RU Costs:                                                                  │
│  • Point Read (by id + practiceId): ~1 RU                                   │
│  • Write (full document): ~1-1.5 RU                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          AVAILITY API (External)                            │
│                                                                             │
│  POST /v1/coverages                                                         │
│    ← Request eligibility check                                              │
│    → Returns: { coverageId, statusCode: "0" (InProgress) }                  │
│                                                                             │
│  GET /v1/coverages/{coverageId}                                             │
│    ← Poll for status                                                        │
│    → Returns:                                                               │
│       • StatusCode "0" or "R1" → Still processing (NextPollAfterUtc)        │
│       • StatusCode "4" or "3" → Complete (with results)                     │
│       • StatusCode "19", "7", etc. → Error                                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Why Sequential Polling (Not Fixed Timer)

A fixed interval timer (e.g., `System.Timers.Timer` firing every 2 seconds) creates race conditions
when API calls take longer than the interval—especially during Polly retry scenarios:

```
PROBLEM: Fixed Timer Creates Overlapping Calls
──────────────────────────────────────────────

Time 0s:   Timer fires → Start API call #1
Time 2s:   Timer fires → Start API call #2 (call #1 still running with Polly retries!)
Time 4s:   Timer fires → Start API call #3 (calls #1 and #2 still running!)
Time 6s:   API call #1 returns (was doing retries: 2s + 4s backoff)
Time 8s:   API call #2 returns
           → Duplicate polls, wasted RU, race conditions, unpredictable state
```

The solution is to use `await Task.Delay()` **after** each API call completes:

```
SOLUTION: Sequential Loop with Delay After Response
────────────────────────────────────────────────────

Time 0s:    API call starts
Time 6s:    API call returns (after Polly retries)
Time 6s:    Start 2s delay
Time 8s:    Delay complete → Start next API call
Time 8.2s:  API call returns (fast this time)
Time 8.2s:  Start 2s delay
Time 10.2s: Delay complete → Start next API call
            → No overlapping calls, predictable behavior
```

## Client Polling Service Implementation

```csharp
public class EligibilityCheckPollingService : IDisposable
{
    private readonly IEligibilityCheckApiService _apiService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);
    
    private CancellationTokenSource? _cts;
    private bool _isPolling;

    /// <summary>
    /// Starts polling loop. Uses delay BETWEEN calls, not a fixed interval timer.
    /// This prevents overlapping calls during Polly retry scenarios.
    /// </summary>
    public async Task StartPollingAsync(
        string patientId, 
        string encounterId,
        Action<List<EligibilityCheckDto>> onUpdate)
    {
        if (_isPolling) return;
        _isPolling = true;
        _cts = new CancellationTokenSource();

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // 1. Make API call (may take 200ms or 14+ seconds with retries)
                var checks = await FetchAndPollChecksAsync(patientId, encounterId);
                
                // 2. Notify UI of updates
                onUpdate(checks);
                
                // 3. Check if we should stop polling
                if (AllChecksTerminal(checks))
                {
                    break;
                }
                
                // 4. Wait AFTER the call completes before next poll
                //    This is the key difference from a fixed-interval timer!
                await Task.Delay(_pollInterval, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
        }
        finally
        {
            _isPolling = false;
        }
    }

    public void StopPolling()
    {
        _cts?.Cancel();
    }

    private async Task<List<EligibilityCheckDto>> FetchAndPollChecksAsync(
        string patientId, 
        string encounterId)
    {
        // First: Get current state (fast, 1 RU)
        var checks = await _apiService.GetEligibilityChecksAsync(patientId, encounterId);
        
        // Filter for checks ready to poll
        var readyToPoll = checks
            .Where(c => c.Status == "InProgress")
            .Where(c => c.NextPollAfterUtc == null || c.NextPollAfterUtc <= DateTime.UtcNow)
            .Select(c => c.EligibilityCheckId)
            .ToList();
        
        if (readyToPoll.Count == 0)
        {
            return checks; // Nothing to poll, return current state
        }
        
        // Poll only the ready checks (this call may take a while with retries)
        return await _apiService.GetEligibilityChecksWithPollingAsync(
            patientId, 
            encounterId, 
            readyToPoll);
    }

    private static bool AllChecksTerminal(List<EligibilityCheckDto> checks)
    {
        return checks.All(c => c.Status is "Complete" or "Failed" or "Cancelled");
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
```

## Sequence Diagram: Sequential Polling Loop

```
CLIENT                         API                         AVAILITY
  │                              │                            │
  │ StartPollingAsync()          │                            │
  │                              │                            │
  │ ┌────────────────────────────────────────────────────────────────────┐
  │ │ LOOP: while (!cancelled && !allTerminal)                           │
  │ │                                                                    │
  │ │  ┌─────────────────────────────────────────────────────────────┐   │
  │ │  │ Step 1: Fetch current state                                 │   │
  │ ├──┼─────────────────────────>│ GET /eligibility-checks          │   │
  │ │  │                          ├─────────────────────────────────>│   │
  │ │  │                          │<─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│   │
  │ │<─┼─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ │ [check1: InProgress, check2: Complete]
  │ │  └─────────────────────────────────────────────────────────────┘   │
  │ │                              │                                     │
  │ │  ┌─────────────────────────────────────────────────────────────┐   │
  │ │  │ Step 2: Filter ready checks (client-side)                   │   │
  │ │  │ • check1: InProgress, NextPollAfterUtc <= Now ✓ READY       │   │
  │ │  │ • check2: Complete ✗ TERMINAL                               │   │
  │ │  └─────────────────────────────────────────────────────────────┘   │
  │ │                              │                                     │
  │ │  ┌─────────────────────────────────────────────────────────────┐   │
  │ │  │ Step 3: Poll ready checks (may take 14+ seconds with Polly) │   │
  │ ├──┼─────────────────────────>│ GET /checks?pollCheckIds=check1  │   │
  │ │  │                          ├─────────────────────────────────>│   │
  │ │  │                          │      ... Polly retries ...       │   │
  │ │  │                          │      ... 2s + 4s backoff ...     │   │
  │ │  │                          │<─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│   │
  │ │<─┼─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ │ [check1: Complete, check2: Complete]│
  │ │  └─────────────────────────────────────────────────────────────┘   │
  │ │                              │                                     │
  │ │  ┌─────────────────────────────────────────────────────────────┐   │
  │ │  │ Step 4: Update UI                                           │   │
  │ │  │ onUpdate(checks) → Blazor StateHasChanged()                 │   │
  │ │  └─────────────────────────────────────────────────────────────┘   │
  │ │                              │                                     │
  │ │  ┌─────────────────────────────────────────────────────────────┐   │
  │ │  │ Step 5: Check if all terminal                               │   │
  │ │  │ AllChecksTerminal() = true → EXIT LOOP                      │   │
  │ │  └─────────────────────────────────────────────────────────────┘   │
  │ │                              │                                     │
  │ │  ┌─────────────────────────────────────────────────────────────┐   │
  │ │  │ Step 6: Wait AFTER call completes (if not terminal)         │   │
  │ │  │ await Task.Delay(2s) ← KEY: Delay starts here, not earlier! │   │
  │ │  └─────────────────────────────────────────────────────────────┘   │
  │ │                              │                                     │
  │ └────────────────────────────────────────────────────────────────────┘
  │                              │                                       │
  │ Polling complete (all terminal)                                      │
  │                              │                                       │
```

## Sequence Diagram: Polling with Polly Retries (No Overlap)

```
CLIENT                         API                         AVAILITY
  │                              │                            │
  │ ─────────────────────────────────────────────────────────────────────
  │ ITERATION 1: API call takes 6 seconds due to Polly retries
  │ ─────────────────────────────────────────────────────────────────────
  │                              │                            │
  │ GET /checks                  │                            │
  ├─────────────────────────────>│ (1 RU - fast read)         │
  │<─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│ 200ms                      │
  │                              │                            │
  │ GET /checks?pollCheckIds=... │                            │
  ├─────────────────────────────>│                            │
  │                              │ GET /v1/coverages/...      │
  │                              ├───────────────────────────>│
  │                              │<─ ─ ─ ─ ─ ─ ✗ HTTP 503     │
  │                              │                            │
  │                              │ ⏱ Polly: Wait 2s           │
  │                              │                            │
  │                              │ GET /v1/coverages/... (Retry #1)  │
  │                              ├──────────────────>│                    │
  │                              │                  │                    │
  │                              │      ✗ FAIL      │    ┌──────────────────────────────────────────┐
  │                              │<─ ─ ─ ─ ─ ─ ─ ─ ─│    │ Retry Policy: Attempt 1 Failed           │
  │                              │  (HTTP 503)       │    │ Wait 2 seconds (exponential backoff)     │
  │                              │                  │    └──────────────────────────────────────────┘
  │                              │                  │                    │
  │ ⏱ Wait 2s...       │                  │                    │
  │                    │                  │                    │
  │                    │  GET /v1/coverages/av_001 (Retry #1)  │
  │                    ├──────────────────>│                    │
  │                    │                  │                    │
  │                    │      ✗ FAIL      │    ┌──────────────────────────────────────────┐
  │                    │<─ ─ ─ ─ ─ ─ ─ ─ ─│    │ Retry Policy: Attempt 2 Failed           │
  │                    │  (HTTP 503)       │    │ Wait 4 seconds (exponential backoff)     │
  │                    │                  │    └──────────────────────────────────────────┘
  │                    │                  │                    │
  │ ⏱ Wait 4s...       │                  │                    │
  │                    │                  │                    │
  │                    │  GET /v1/coverages/av_001 (Retry #2)  │
  │                    ├──────────────────>│                    │
  │                    │                  │                    │
  │                    │      ✓ SUCCESS   │    ┌──────────────────────────────────────────┐
  │                    │<─ ─ ─ ─ ─ ─ ─ ─ ─│    │ Success! Reset retry counter             │
  │                    │  StatusCode: "4"  │    │ Circuit Breaker: Record success          │
  │                    │                  │    └──────────────────────────────────────────┘
  │                    │                  │                    │
  │ Update check1:     │                  │                    │
  │ • Status = "Complete"                 │                    │
  │ • CompletedAtUtc = Now                │                    │
  │                    │                  │                    │
  │ Write Document     │                  │                    │
  ├───────────────────>│                  │                    │
  │<─ ─ ─ ─ ─ ─ ─ ─ ─ ─│                  │                    │
  │     1.5 RU         │                  │                    │
  │                    │                  │                    │
  │ Return success     │                  │                    │
  │                    │                  │                    │


SCENARIO 2: Circuit Breaker Trips After Max Retries
────────────────────────────────────────────────────

SERVICE LAYER          COSMOS DB          AVAILITY          POLLY POLICIES
      │                    │                  │                    │
      │ PollAndUpdate(check2)                 │                    │
      ├──────────────────────────────────────────────────────────>│
      │                    │                  │                    │
      │                    │                  │    ┌──────────────────────────────────────────┐
      │                    │                  │    │ Circuit Breaker: State = Closed          │
      │                    │                  │    └──────────────────────────────────────────┘
      │                    │                  │                    │
      │                    │  GET /v1/coverages/av_002             │
      │                    ├──────────────────>│                    │
      │                    │      ✗ FAIL      │                    │
      │                    │<─ ─ ─ ─ ─ ─ ─ ─ ─│                    │
      │                    │  (Timeout)        │                    │
      │                    │                  │                    │
      │ ⏱ Wait 2s... (Retry #1)               │                    │
      │                    ├──────────────────>│                    │
      │                    │      ✗ FAIL      │                    │
      │                    │<─ ─ ─ ─ ─ ─ ─ ─ ─│                    │
      │                    │                  │                    │
      │ ⏱ Wait 4s... (Retry #2)               │                    │
      │                    ├──────────────────>│                    │
      │                    │      ✗ FAIL      │    ┌──────────────────────────────────────────┐
      │                    │<─ ─ ─ ─ ─ ─ ─ ─ ─│    │ Max Retries Exhausted (3 failures)       │
      │                    │                  │    │ Circuit Breaker: Trip to OPEN            │
      │                    │                  │    │ Block all calls for 30 seconds           │
      │                    │                  │    └──────────────────────────────────────────┘
      │                    │                  │                    │
      │ Update check2:     │                  │                    │
      │ • Status = "Failed"                   │                    │
      │ • ErrorMessage = "Availity API unavailable after 3 retries"
      │ • FailedAtUtc = Now                   │                    │
      │                    │                  │                    │
      │ Write Document     │                  │                    │
      ├───────────────────>│                  │                    │
      │<─ ─ ─ ─ ─ ─ ─ ─ ─ ─│                  │                    │
      │     1.5 RU         │                  │                    │
      │                    │                  │                    │
      │ Return failure     │                  │                    │
      │                    │                  │                    │


SCENARIO 3: Circuit Breaker is OPEN (Blocking Calls)
─────────────────────────────────────────────────────

SERVICE LAYER          COSMOS DB          AVAILITY          POLLY POLICIES
      │                    │                  │                    │
      │ PollAndUpdate(check3)                 │                    │
      ├──────────────────────────────────────────────────────────>│
      │                    │                  │                    │
      │                    │                  │    ┌──────────────────────────────────────────┐
      │                    │                  │    │ Circuit Breaker: State = OPEN            │
      │                    │                  │    │ Reject call immediately (fail-fast)      │
      │                    │                  │    │ (Remaining block time: 25 seconds)       │
      │                    │                  │    └──────────────────────────────────────────┘
      │                    │                  │                    │
      │<─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│
      │ BrokenCircuitException                │                    │
      │                    │                  │                    │
      │ ⚠ NO CALL TO AVAILITY (blocked)       │                    │
      │                    │                  │                    │
      │ Update check3:     │                  │                    │
      │ • Status = "InProgress" (unchanged)   │                    │
      │ • NextPollAfterUtc = Now + 30s        │                    │
      │   (Wait for circuit breaker to reset) │                    │
      │ • PollCount++ (increment failure counter)                  │
      │                    │                  │                    │
      │ Write Document     │                  │                    │
      ├───────────────────>│                  │                    │
      │<─ ─ ─ ─ ─ ─ ─ ─ ─ ─│                  │                    │
      │     1.5 RU         │                  │                    │
      │                    │                  │                    │
      │ Return (no change) │                  │                    │
      │                    │                  │                    │


SCENARIO 4: Circuit Breaker Transitions to HALF-OPEN
─────────────────────────────────────────────────────

SERVICE LAYER          COSMOS DB          AVAILITY          POLLY POLICIES
      │                    │                  │                    │
      │ ⏱ 30 seconds pass...                  │    ┌──────────────────────────────────────────┐
      │                    │                  │    │ Circuit Breaker: OPEN → HALF-OPEN        │
      │                    │                  │    │ Allow ONE test call to check health      │
      │                    │                  │    └──────────────────────────────────────────┘
      │                    │                  │                    │
      │ PollAndUpdate(check4)                 │                    │
      ├──────────────────────────────────────────────────────────>│
      │                    │                  │                    │
      │                    │                  │    ┌──────────────────────────────────────────┐
      │                    │                  │    │ Circuit Breaker: State = HALF-OPEN       │
      │                    │                  │    │ Allow this call (testing)                │
      │                    │                  │    └──────────────────────────────────────────┘
      │                    │                  │                    │
      │                    │  GET /v1/coverages/av_004             │
      │                    ├──────────────────>│                    │
      │                    │      ✓ SUCCESS   │    ┌──────────────────────────────────────────┐
      │                    │<─ ─ ─ ─ ─ ─ ─ ─ ─│    │ Success! Circuit Breaker: HALF-OPEN → CLOSED│
      │                    │  StatusCode: "4"  │    │ Resume normal operations                 │
      │                    │                  │    └──────────────────────────────────────────┘
      │                    │                  │                    │
      │ Update check4:     │                  │                    │
      │ • Status = "Complete"                 │                    │
      │ • CompletedAtUtc = Now                │                    │
      │                    │                  │                    │
      │ Write Document     │                  │                    │
      ├───────────────────>│                  │                    │
      │<─ ─ ─ ─ ─ ─ ─ ─ ─ ─│                  │                    │
      │                    │                  │                    │
      │ Return success     │                  │                    │
      │                    │                  │                    │


Polly Policy Configuration Summary:
────────────────────────────────────

Retry Policy (Exponential Backoff):
┌────────────────────────────────────────────────┐
│ Max Retries: 3                                 │
│ Backoff:                                       │
│   • Attempt 1: Wait 2s                         │
│   • Attempt 2: Wait 4s                         │
│   • Attempt 3: Wait 8s                         │
│ Retryable Errors:                              │
│   • HTTP 5xx (503, 500, 502, 504)              │
│   • HttpRequestException (timeout, DNS, etc.)  │
│   • TaskCanceledException                      │
└────────────────────────────────────────────────┘

Circuit Breaker Policy:
┌────────────────────────────────────────────────┐
│ Failure Threshold: 5 consecutive failures      │
│ Break Duration: 30 seconds                     │
│ State Transitions:                             │
│   • Closed → Open: After 5 failures            │
│   • Open → Half-Open: After 30 seconds         │
│   • Half-Open → Closed: On 1 success           │
│   • Half-Open → Open: On any failure           │
│ Handles:                                       │
│   • Same exceptions as retry policy            │
└────────────────────────────────────────────────┘

Impact on NextPollAfterUtc:
┌────────────────────────────────────────────────┐
│ • Retry in progress: No change (internal)      │
│ • Circuit breaker OPEN: Set NextPoll = +30s    │
│ • Max retries failed: Status = "Failed"        │
│ • Success after retry: Continue normal polling │
└────────────────────────────────────────────────┘

````````markdown
# Eligibility Check Polling Architecture

// ...existing code...

## Sequence Diagram: Polling with Polly Retries (No Overlap)

// ...existing code (all sequence diagrams remain unchanged)...

```

## Idempotency: How the System Handles Duplicate Requests

The eligibility check system is designed to be **idempotent**, meaning the same operation can be called multiple times without creating duplicate checks or causing inconsistent state.

### Key Mechanism: Deterministic ID Generation

**Problem Scenario:**
```
┌─────────────────────────────────────────────────────────┐
│ What if the client retries a request due to network    │
│ timeout, but the first request already succeeded?       │
│                                                          │
│ Time 0s:  Client → POST /run → Server creates check1   │
│ Time 2s:  Network timeout (but check1 was created!)    │
│ Time 3s:  Client retries → POST /run → ???              │
│                                                          │
│ WITHOUT idempotency: Creates check2 (duplicate!)        │
│ WITH idempotency: Returns existing check1               │
└─────────────────────────────────────────────────────────┘
```

### How It Works in Code

#### 1. **Entity IDs Use `Guid.NewGuid()` at Object Creation**

From `BF.Domain/Entities/Patient.cs`:
```csharp
public class EligibilityCheckEmbedded
{
    [JsonProperty("eligibilityCheckId")]
    public string EligibilityCheckId { get; init; } = Guid.NewGuid().ToString();
    
    [JsonProperty("coverageEnrollmentId")]
    public required string CoverageEnrollmentId { get; init; }
    
    // Other properties...
}
```

**Key Point:** The `EligibilityCheckId` is generated **when the C# object is instantiated**, not when the database write occurs.

#### 2. **Service Layer Creates Object Before API Call**

From `EligibilityCheckService.RunWithPatientAsync()`:
```csharp
// Step 1: Create entity with auto-generated ID
var pending = new EligibilityCheckEmbedded
{
    CoverageEnrollmentId = request.CoverageEnrollmentId,
    PayerId = coverage.PayerId,
    Status = "Pending",
    // EligibilityCheckId is auto-generated by object initializer
};

// Step 2: Add to in-memory collection
encounter.EligibilityChecks.Add(pending);

// Step 3: FIRST WRITE - Persist with "Pending" status
await _patientRepository.UpdateAsync(patient);
// ✓ At this point, check has an ID and is in the database

// Step 4: Call Availity (may fail or timeout)
try
{
    var response = await _availityClient.InitiateCoverageCheckAsync(
        availityRequest, 
        cancellationToken);
    
    // Step 5: Update same check object
    pending.AvailityCoverageId = response.CoverageId;
    pending.Status = "InProgress";
    
    // Step 6: SECOND WRITE - Update with Availity response
    await _patientRepository.UpdateAsync(patient);
}
catch (Exception ex)
{
    // Step 7: THIRD WRITE - Mark as failed
    pending.Status = "Failed";
    pending.ErrorMessage = ex.Message;
    await _patientRepository.UpdateAsync(patient);
}
```

### Why This Achieves Idempotency

#### Scenario 1: Client Retries After Network Timeout (First Request Succeeded)

```
┌─────────────────────────────────────────────────────────────────┐
│ FIRST REQUEST (succeeds server-side, but client times out)     │
└─────────────────────────────────────────────────────────────────┘

Client → POST /run (coverageEnrollmentId=cov_001, encounterId=enc_001)
    ↓
Server creates check object → eligibilityCheckId = "abc-123-def" (GUID)
    ↓
Server writes to Cosmos → check status "Pending" (1 RU)
    ↓
Server calls Availity → returns coverageId = "av_999"
    ↓
Server updates check → status "InProgress", availityCoverageId = "av_999"
    ↓
Server writes to Cosmos → check updated (1 RU)
    ↓
Server responds → HTTP 200 with eligibilityCheckId "abc-123-def"
    ↓
Network timeout → CLIENT NEVER RECEIVES RESPONSE


┌─────────────────────────────────────────────────────────────────┐
│ RETRY REQUEST (client thinks first request failed)             │
└─────────────────────────────────────────────────────────────────┘

Client → POST /run (SAME coverageEnrollmentId=cov_001, encounterId=enc_001)
    ↓
Server creates NEW check object → eligibilityCheckId = "xyz-789-ghi" (NEW GUID)
    ↓
Server writes to Cosmos → NEW check with status "Pending" (1 RU)
    ↓
Server calls Availity → returns coverageId = "av_888" (NEW coverage ID)
    ↓
Result: TWO separate eligibility checks exist for same coverage!
    ✗ NOT IDEMPOTENT - but this is BY DESIGN for flexibility
```

#### Scenario 2: Client Retries Same Request (Idempotency Key Pattern)

```
Client → POST /run (idempotencyKey=enc_001|cov_001|2025-01-15)
    ↓
Server checks for existing check with same idempotency key
    │
    ├─> Found: eligibilityCheckId = "abc-123-def" (same as in Scenario 1)
    │
    └─> Return existing check (skip creation, avoid duplicate)
    ↓
Client receives existing check → status "InProgress"
    ↓
Poll for updates using GET /eligibility-checks/{id}
    ↓
Server responds with current status
    → Allows client to update UI without duplicates
```

### Important: System is NOT Automatically Idempotent by Coverage/Encounter

**The system allows multiple eligibility checks for the same coverage enrollment and encounter.** This is intentional because:

1. **Valid Business Reason:** A coverage may be checked multiple times:
   - Initial check at check-in
   - Re-check after correcting member ID
   - Re-check for different service types

2. **Client Responsibility:** The client must use **GET before POST** pattern to check if a check already exists:

```csharp
// CLIENT-SIDE IDEMPOTENCY PATTERN

// Step 1: Check if eligibility already checked
var existingChecks = await _apiService.GetEligibilityChecksAsync(
    patientId, 
    encounterId);

var existingCheck = existingChecks.FirstOrDefault(c => 
    c.CoverageEnrollmentId == targetCoverageId &&
    c.Status is "InProgress" or "Complete");

if (existingCheck is not null)
{
    // Idempotent behavior: Return existing check
    return existingCheck;
}

// Step 2: Only create new check if none exists
var newCheckRequest = new EligibilityCheckRequestDto
{
    CoverageEnrollmentId = coverageEnrollmentId
};

var newCheck = await _apiService.RunEligibilityCheckAsync(
    patientId,
    encounterId,
    newCheckRequest);
```

### Where True Idempotency Exists

#### 1. **Polling Operations Are Idempotent**

Calling `GET /eligibility-checks/{id}` multiple times is safe:
- If status is "InProgress", it polls Availity and updates
- If status is "Complete" or "Failed", it returns current state
- **Same result** whether called 1 time or 100 times

#### 2. **Update Operations Use Object References**

Once a check exists in memory, updates modify the **same object reference**:
```csharp
// This modifies the SAME check object in memory
pending.Status = "InProgress";
pending.AvailityCoverageId = response.CoverageId;

// Multiple calls to this update the SAME check in Cosmos
await _patientRepository.UpdateAsync(patient);
```

#### 3. **Cosmos DB Document Writes Use Optimistic Concurrency (ETags)**

Cosmos DB's ETag mechanism prevents lost updates:
```
Request 1: Read patient doc (ETag = "v1") → Modify → Write with ETag "v1"
Request 2: Read patient doc (ETag = "v1") → Modify → Write with ETag "v1"

Result: Request 2 fails with 412 Precondition Failed
        → Client retries with fresh read
        → NO DATA LOSS
```

### Recommended Client Implementation for Idempotency

```csharp
public async Task<EligibilityCheckDto> RunEligibilityCheckIdempotentAsync(
    string patientId,
    string encounterId,
    string coverageEnrollmentId)
{
    // IDEMPOTENCY KEY: (encounterId + coverageEnrollmentId + dateOfService)
    var today = DateTime.UtcNow.Date;
    
    // Step 1: Check for existing check with matching criteria
    var existingChecks = await _apiService.GetEligibilityChecksAsync(
        patientId, 
        encounterId);
    
    var existingCheck = existingChecks
        .Where(c => c.CoverageEnrollmentId == coverageEnrollmentId)
        .Where(c => c.DateOfService.Date == today)
        .Where(c => c.Status != "Failed") // Ignore failed checks
        .OrderByDescending(c => c.RequestedAtUtc)
        .FirstOrDefault();
    
    if (existingCheck is not null)
    {
        // Found existing check - return it (idempotent)
        if (existingCheck.Status == "InProgress")
        {
            // Poll for updated status
            return await _apiService.GetEligibilityCheckAsync(
                patientId, 
                encounterId, 
                existingCheck.EligibilityCheckId);
        }
        
        return existingCheck;
    }
    
    // Step 2: No existing check found - safe to create new one
    var newCheckRequest = new EligibilityCheckRequestDto
    {
        CoverageEnrollmentId = coverageEnrollmentId
    };
    
    var newCheck = await _apiService.RunEligibilityCheckAsync(
        patientId,
        encounterId,
        newCheckRequest);
    
    return newCheck;
}
```

### Summary: Idempotency Guarantees

| Operation | Idempotent? | Notes |
|-----------|-------------|-------|
| **POST /run** (create check) | ❌ No | Each call creates a new check (by design for flexibility) |
| **GET /checks/{id}** (poll) | ✅ Yes | Safe to call multiple times - returns same result |
| **GET /checks?pollCheckIds=...** | ✅ Yes | Selective polling is idempotent |
| **Cosmos DB writes** | ⚠️ Partial | Protected by ETags (optimistic concurrency) |
| **Client retry with GET-before-POST** | ✅ Yes | Client-side idempotency pattern |

### Why Not Use Idempotency Keys?

Some APIs use **client-provided idempotency keys** (e.g., Stripe's `Idempotency-Key` header):
```http
POST /eligibility-checks/run
Idempotency-Key: enc_001|cov_001|2025-01-15
```

**We chose not to implement this because:**
1. **Business requirement:** Multiple checks per coverage are valid
2. **Cosmos DB limitations:** No native support for idempotency keys without additional index overhead
3. **Client responsibility:** The client must decide when to reuse vs. create new checks
4. **Simplicity:** GET-before-POST pattern is more explicit and debuggable

If strict idempotency is needed in the future, consider adding:
- `IdempotencyKey` property to `EligibilityCheckEmbedded`
- Unique index on `(encounterId, coverageEnrollmentId, idempotencyKey)`
- Service layer deduplication logic
