# ? Implementation Checklist
## Eligibility Check Polling Optimization

---

## ?? What's Already Done (Server-Side)

### ? Backend Implementation Complete

- [x] **Service Layer** (`BF.API/Services/EligibilityCheckService.cs`)
  - [x] `GetEligibilityChecksAsync()` - Basic list without polling
  - [x] `GetEligibilityChecksWithSelectivePollingAsync()` - Optimized selective polling
  - [x] `GetEligibilityCheckAsync()` - Individual check with auto-poll
  - [x] `PollAndUpdateAsync()` - Core polling logic
  - [x] Proper RU cost tracking in XML comments

- [x] **Interface** (`BF.Domain/Interfaces/IEligibilityCheckService.cs`)
  - [x] All method signatures defined
  - [x] XML documentation with RU cost estimates
  - [x] Usage patterns documented

- [x] **Controller** (`BF.API/Controllers/EligibilityChecksController.cs`)
  - [x] `GET /eligibility-checks` endpoint
  - [x] Query parameter support: `?pollCheckIds=id1,id2,id3`
  - [x] Comma-separated ID parsing
  - [x] Proper routing and authorization

- [x] **Entity Model** (`BF.Domain/Entities/Patient.cs`)
  - [x] `NextPollAfterUtc` property on `EligibilityCheckEmbedded`
  - [x] `PollCount` property
  - [x] `IsPollingRequired` computed property
  - [x] `IsTerminal` computed property

- [x] **DTOs** (`BF.Domain/DTOs/`)
  - [x] `EligibilityCheckSummaryResponseDto` includes polling properties
  - [x] `EligibilityCheckResponseDto` extends summary
  - [x] All necessary mappers in place

- [x] **Build Status**
  - [x] Solution builds successfully
  - [x] No compilation errors
  - [x] All dependencies resolved

---

## ?? What You Need to Do (Client-Side)

### ?? Blazor WASM Implementation

#### Step 1: API Service Layer
```
Location: BF.BlazorWASM/Services/EligibilityCheckApiService.cs
Status: ?? TO DO
```

**Tasks:**
- [ ] Create `IEligibilityCheckApiService` interface
- [ ] Implement `EligibilityCheckApiService` class
- [ ] Add `GetEligibilityChecksAsync()` method
- [ ] Add `GetEligibilityChecksWithPollingAsync()` method with pollCheckIds parameter
- [ ] Add `RunEligibilityCheckAsync()` method
- [ ] Register in DI container (`Program.cs`)

**Reference**: See `BF.BlazorWASM/ELIGIBILITY_POLLING_CLIENT_GUIDE.md` Section 1

#### Step 2: Polling Service
```
Location: BF.BlazorWASM/Services/EligibilityCheckPollingService.cs
Status: ?? TO DO
```

**Tasks:**
- [ ] Create `IEligibilityCheckPollingService` interface
- [ ] Implement `EligibilityCheckPollingService` class
- [ ] Add timer-based polling logic (2-second interval)
- [ ] Implement time-based filtering (respect `NextPollAfterUtc`)
- [ ] Add lifecycle management (stop when all checks terminal)
- [ ] Add error handling with circuit breaker
- [ ] Add logging for debugging
- [ ] Register in DI container (`Program.cs`)

**Reference**: See `BF.BlazorWASM/ELIGIBILITY_POLLING_CLIENT_GUIDE.md` Section 2

#### Step 3: Razor Components
```
Location: BF.BlazorWASM/Pages/ or BF.BlazorWASM/Components/
Status: ?? TO DO
```

**Tasks:**
- [ ] Update or create encounter details page
- [ ] Add polling service injection
- [ ] Implement `OnInitializedAsync()` to load checks
- [ ] Add logic to start/stop polling based on check states
- [ ] Create `EligibilityCheckCard` component (optional)
- [ ] Add visual indicators for polling state
- [ ] Display `NextPollAfterUtc` countdown (optional)
- [ ] Add error handling UI
- [ ] Implement `IDisposable` to clean up timer

**Reference**: See `BF.BlazorWASM/ELIGIBILITY_POLLING_CLIENT_GUIDE.md` Section 3

#### Step 4: DI Registration
```
Location: BF.BlazorWASM/Program.cs
Status: ?? TO DO
```

**Tasks:**
- [ ] Add `builder.Services.AddScoped<IEligibilityCheckApiService, EligibilityCheckApiService>()`
- [ ] Add `builder.Services.AddScoped<IEligibilityCheckPollingService, EligibilityCheckPollingService>()`
- [ ] Ensure HttpClient is configured for API calls

**Reference**: See `BF.BlazorWASM/ELIGIBILITY_POLLING_CLIENT_GUIDE.md` Section 4

---

## ?? Testing Checklist

### Manual Testing

- [ ] **Test 1: Single Check Lifecycle**
  1. Run an eligibility check
  2. Verify status starts as "InProgress"
  3. Observe polling in browser network tab
  4. Confirm polling uses `?pollCheckIds` parameter
  5. Verify status changes to "Complete" or "Failed"
  6. Confirm polling stops when complete

- [ ] **Test 2: Multiple Checks**
  1. Run 2-3 eligibility checks simultaneously
  2. Verify selective polling with multiple IDs: `?pollCheckIds=id1,id2,id3`
  3. Observe that only ready checks are polled (time-based filtering)
  4. Confirm polling stops when all checks are terminal

- [ ] **Test 3: Time-Based Filtering**
  1. Run a check that requires polling
  2. Observe `NextPollAfterUtc` in response
  3. Verify client waits until that time before polling
  4. Confirm no unnecessary polls occur

- [ ] **Test 4: Error Handling**
  1. Simulate network error (disable network)
  2. Verify error is caught and logged
  3. Confirm circuit breaker stops polling after 3 errors
  4. Re-enable network and verify recovery

- [ ] **Test 5: Mock Scenarios**
  1. Use `X-Api-Mock-Scenario-ID: Coverages-Polling-Success-i` header
  2. Verify check completes after 3 polls
  3. Use `X-Api-Mock-Scenario-ID: Coverages-Complete-i` for immediate success
  4. Use `X-Api-Mock-Scenario-ID: Coverages-PayerError1-i` for failure scenario

### RU Cost Verification

- [ ] **Monitor in Azure Portal**
  1. Navigate to Cosmos DB account ? Metrics
  2. Select "Total Request Units" metric
  3. Filter by operation: `ReadDocument` and `ReplaceDocument`
  4. Observe RU consumption during polling cycles
  5. Verify costs align with expected values (~1 RU + 1.5 RU per check)

- [ ] **Application Insights Queries** (if configured)
  ```kusto
  requests
  | where operation_Name contains "GetEligibilityChecks"
  | extend hasPollParam = url contains "pollCheckIds"
  | summarize count(), avg(duration) by operation_Name, hasPollParam
  ```

---

## ?? Success Metrics

After implementation, you should see:

? **RU Cost Reduction**
- Traditional approach: ~8.5 RU per cycle (3 checks)
- Optimized approach: ~5.5 RU per cycle (3 checks)
- **Target: 30-50% reduction**

? **Polling Efficiency**
- Average polls per check: < 5
- Time to completion (p90): < 30 seconds
- Premature polls avoided: > 50%

? **User Experience**
- No UI freezing during polls
- Real-time updates every 2 seconds
- Clear status indicators (InProgress, Complete, Failed)
- Automatic stop when all checks complete

---

## ?? Documentation Reference

| Document | Purpose | Location |
|----------|---------|----------|
| **Architecture Diagram** | System flow and sequence diagrams | `BF.API/Services/ELIGIBILITY_POLLING_ARCHITECTURE.md` |
| **Optimization Guide** | Detailed RU cost analysis and patterns | `BF.API/Services/ELIGIBILITY_POLLING_OPTIMIZATION.md` |
| **Client Implementation** | Step-by-step Blazor WASM guide | `BF.BlazorWASM/ELIGIBILITY_POLLING_CLIENT_GUIDE.md` |
| **Quick Reference** | API usage and examples | `BF.API/Services/ELIGIBILITY_POLLING_QUICK_REFERENCE.md` |
| **Mock Testing Guide** | Availity mock scenarios | `BF.API/Integrations/Availity/Mock/README.md` |

---

## ?? Deployment Steps

### Pre-Deployment

- [ ] Complete all client-side implementation tasks
- [ ] Run unit tests (if applicable)
- [ ] Run integration tests with mock scenarios
- [ ] Verify build succeeds: `dotnet build`
- [ ] Test locally with real API and mock Availity

### Deployment

- [ ] Deploy API changes (already complete, no deployment needed)
- [ ] Deploy Blazor WASM app
- [ ] Verify no console errors in browser
- [ ] Monitor Application Insights for errors
- [ ] Monitor Cosmos DB RU consumption

### Post-Deployment

- [ ] Run smoke tests on production
- [ ] Verify polling stops correctly
- [ ] Check RU metrics in Azure Portal
- [ ] Gather user feedback
- [ ] Monitor for 24-48 hours

---

## ?? Troubleshooting Guide

### Common Issues

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| Polling never starts | No checks in "InProgress" status | Verify check creation and Availity mock |
| Polling too frequent | Not respecting `NextPollAfterUtc` | Add time-based filter in client |
| High RU costs | Not using selective API | Verify `?pollCheckIds` parameter is used |
| Polling never stops | No terminal state check | Add `!checks.Any(c => c.Status == "InProgress")` |
| 401 errors | Missing authentication | Check bearer token in HttpClient |
| CORS errors | API not configured | Add CORS policy for Blazor origin |

---

## ?? Next Steps (Optional Enhancements)

Future optimizations you can consider:

- [ ] **SignalR/WebSockets** - Push notifications instead of polling
- [ ] **Exponential Backoff** - Increase delay between polls progressively
- [ ] **Batch Check Initiation** - Run multiple checks in one API call
- [ ] **Client-Side Caching** - Cache terminal checks in localStorage
- [ ] **Progressive Web App** - Background sync for offline support
- [ ] **Analytics Dashboard** - Track RU costs over time

---

## ?? Support

If you encounter issues:

1. Check the troubleshooting guide above
2. Review the documentation in `/ELIGIBILITY_POLLING_*.md` files
3. Check browser console for errors
4. Verify API responses in browser network tab
5. Review Application Insights logs
6. Check Cosmos DB metrics in Azure Portal

---

**Implementation Priority**: ?? HIGH  
**Estimated Effort**: 4-6 hours (client-side)  
**Expected ROI**: 30-50% RU cost reduction  
**Status**: ? Server Complete | ?? Client Pending
