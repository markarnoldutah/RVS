# AI-232: Sentiment Analysis Implementation Plan (Option B)

## Overview

Add server-side sentiment analysis to the intake orchestration pipeline. Sentiment is derived from the customer's `IssueDescription` text **exclusively on the server** during `IntakeOrchestrationService.ExecuteAsync()` — the customer never sees or sends any sentiment data. Results are stored on the `ServiceRequest` entity and surfaced only in the dealer-facing Manager app (detail page + service board cards).

**Sentiment Scale:** `"Calm"` | `"Concerned"` | `"Frustrated"` | `"Distressed"` (4 levels)

---

## Phase 1: Domain Layer — Interface + Entity + DTOs

### 1.1 Create `ISentimentService` interface

- **File:** `RVS.Domain/Integrations/ISentimentService.cs` (new)
- **Pattern:** Mirrors `ICategorizationService` — single async method with cancellation token
- **Method:** `Task<string> AnalyzeSentimentAsync(string text, CancellationToken cancellationToken = default)`
- Returns one of: `"Calm"`, `"Concerned"`, `"Frustrated"`, `"Distressed"`
- XML doc comments per convention

### 1.2 Add `CustomerSentiment` property to `ServiceRequest` entity

- **File:** `RVS.Domain/Entities/ServiceRequest.cs`
- Add after `RvUsage` property:

```csharp
/// <summary>
/// AI-derived customer tone indicator. Populated server-side during intake —
/// never exposed to the customer.
/// </summary>
[JsonProperty("customerSentiment")]
public string? CustomerSentiment { get; set; }
```

### 1.3 Add `CustomerSentiment` to `ServiceRequestDetailResponseDto`

- **File:** `RVS.Domain/DTOs/ServiceRequestDetailResponseDto.cs`
- Add `public string? CustomerSentiment { get; init; }` after `RvUsage`

### 1.4 Add `CustomerSentiment` to `ServiceRequestSummaryResponseDto`

- **File:** `RVS.Domain/DTOs/ServiceRequestSummaryResponseDto.cs`
- Add `public string? CustomerSentiment { get; init; }` after `Priority`

### 1.5 Intentional omissions

- `ServiceRequestCreateRequestDto` — **no** `CustomerSentiment` field. The customer never submits sentiment; it is server-derived only.
- `ServiceRequestUpdateRequestDto` — **no** `CustomerSentiment` field. Dealers cannot override AI-derived sentiment; it is immutable from intake.

---

## Phase 2: API Layer — Service Implementations

### 2.1 Create `AzureOpenAiSentimentService`

- **File:** `RVS.API/Integrations/AzureOpenAiSentimentService.cs` (new)
- **Pattern:** Identical to `AzureOpenAiCategorizationService`:
  - Constructor: `HttpClient`, `RuleBasedSentimentService` fallback, `ILogger`
  - POST to `"sentiment"` endpoint with `{ prompt = text }` payload
  - Parse response and validate it is one of the 4 valid labels
  - On `HttpRequestException` / `TaskCanceledException` → log warning → fall back to `RuleBasedSentimentService`
  - If response is not a valid label → fall back

### 2.2 Create `RuleBasedSentimentService`

- **File:** `RVS.API/Integrations/RuleBasedSentimentService.cs` (new)
- **Pattern:** Mirrors `RuleBasedCategorizationService` — keyword matching with cascading priority (first match from highest severity wins):

| Sentiment | Keywords |
|-----------|----------|
| **Distressed** | `dangerous`, `unsafe`, `emergency`, `stranded`, `fire`, `smoke`, `carbon monoxide` |
| **Frustrated** | `angry`, `unacceptable`, `terrible`, `worst`, `fed up`, `ridiculous`, `multiple times`, `still broken`, `again`, `never fixed`, `weeks`, `months`, `incompetent` |
| **Concerned** | `worried`, `concerned`, `not sure`, `afraid`, `don't know`, `struggling`, `help` |
| **Calm** | *(default — absence of negative indicators)* |

### 2.3 Create `MockSentimentService`

- **File:** `RVS.API/Integrations/MockSentimentService.cs` (new)
- **Pattern:** Mirrors `MockCategorizationService`
- Always returns `"Calm"`, logs at DEBUG level

### 2.4 Register in DI — `Program.cs`

- **File:** `RVS.API/Program.cs`
- After the categorization registration block, add a sentiment registration block:
  - Always register `RuleBasedSentimentService` as singleton
  - If `Integrations:UseMocks = true` → register `MockSentimentService` as `ISentimentService`
  - Else if `AzureOpenAi:Endpoint` is configured → register `AzureOpenAiSentimentService` via `AddHttpClient` with the same resilience handler settings
  - Else → fall back to `RuleBasedSentimentService` as `ISentimentService`

---

## Phase 3: Orchestration — Wire Sentiment into Intake Flow

### 3.1 Inject `ISentimentService` into `IntakeOrchestrationService`

- **File:** `RVS.API/Services/IntakeOrchestrationService.cs`
- Add `ISentimentService _sentimentService` private field
- Add constructor parameter after `ICategorizationService`

### 3.2 Run sentiment analysis in parallel with categorization (Step 4)

- **File:** `RVS.API/Services/IntakeOrchestrationService.cs`
- Refactor Step 4 to run both calls concurrently via `Task.WhenAll`:

```csharp
var categorizationTask = _categorizationService.CategorizeAsync(request.IssueDescription, cancellationToken);
var sentimentTask = _sentimentService.AnalyzeSentimentAsync(request.IssueDescription, cancellationToken);
await Task.WhenAll(categorizationTask, sentimentTask);
```

- Each call is wrapped in its own try/catch — a failure in one does not block the other
- On sentiment failure: log warning, default to `null` (no data is safer than wrong data)
- Set `serviceRequest.CustomerSentiment = sentimentResult` in the entity construction block

---

## Phase 4: Mapper Updates

### 4.1 Update `ToDetailDto()` in `ServiceRequestMapper`

- **File:** `RVS.API/Mappers/ServiceRequestMapper.cs`
- Add `CustomerSentiment = entity.CustomerSentiment` after `RvUsage` in the detail mapping

### 4.2 Update `ToSummaryDto()` in `ServiceRequestMapper`

- **File:** `RVS.API/Mappers/ServiceRequestMapper.cs`
- Add `CustomerSentiment = entity.CustomerSentiment` after `Priority` in the summary mapping

---

## Phase 5: Manager UI — Dealer-Facing Display

### 5.1 `ServiceRequestDetail.razor` — Issue Details card

- **File:** `RVS.Blazor.Manager/Pages/ServiceRequestDetail.razor`
- In the Issue Details card, add a 5th grid item for "Customer Tone":

```razor
<MudItem xs="12" sm="6" md="3">
    <MudText Typo="Typo.caption" Color="Color.Secondary">Customer Tone</MudText>
    @if (!string.IsNullOrWhiteSpace(_sr.CustomerSentiment))
    {
        <MudChip T="string" Size="Size.Small"
                 Color="GetSentimentColor(_sr.CustomerSentiment)"
                 Variant="Variant.Filled">
            @_sr.CustomerSentiment
        </MudChip>
    }
    else
    {
        <MudText Typo="Typo.body1">—</MudText>
    }
</MudItem>
```

- Add `@code` helpers:
  - `GetSentimentColor(string sentiment)` → `Calm=Success`, `Concerned=Info`, `Frustrated=Warning`, `Distressed=Error`

### 5.2 `ServiceBoard.razor` — Kanban cards

- **File:** `RVS.Blazor.Manager/Pages/ServiceBoard.razor`
- After the category chip, before the Age/Priority row, add a conditional sentiment chip:

```razor
@if (!string.IsNullOrWhiteSpace(sr.CustomerSentiment) && sr.CustomerSentiment != "Calm")
{
    <MudChip T="string" Size="Size.Small"
             Color="GetSentimentColor(sr.CustomerSentiment)">
        @sr.CustomerSentiment
    </MudChip>
}
```

- Only non-`"Calm"` sentiment is shown on board cards — calm is the expected baseline and showing it adds visual noise
- Add same `GetSentimentColor()` helper to `@code` section

---

## Phase 6: Tests (TDD — Red → Green → Refactor)

### 6.1 Domain Tests

**`Tests/RVS.Domain.Tests/Mappers/ServiceRequestMapperTests.cs`** (existing, add tests):

- `ToDetailDto_ShouldMapCustomerSentiment`
- `ToDetailDto_WhenCustomerSentimentIsNull_ShouldMapNull`
- `ToSummaryDto_ShouldMapCustomerSentiment`

### 6.2 API Tests

**`Tests/RVS.API.Tests/Integrations/RuleBasedSentimentServiceTests.cs`** (new):

- `AnalyzeSentimentAsync_WithDistressedKeywords_ShouldReturnDistressed`
- `AnalyzeSentimentAsync_WithFrustratedKeywords_ShouldReturnFrustrated`
- `AnalyzeSentimentAsync_WithConcernedKeywords_ShouldReturnConcerned`
- `AnalyzeSentimentAsync_WithNeutralText_ShouldReturnCalm`
- `AnalyzeSentimentAsync_WhenTextIsNullOrWhiteSpace_ShouldThrowArgumentException`
- `AnalyzeSentimentAsync_DistressedTrumpsFrustrated_WhenBothPresent`

**`Tests/RVS.API.Tests/Integrations/MockSentimentServiceTests.cs`** (new):

- `AnalyzeSentimentAsync_ShouldReturnCalm`
- `AnalyzeSentimentAsync_WhenTextIsNullOrWhiteSpace_ShouldThrowArgumentException`

**`Tests/RVS.API.Tests/Integrations/AzureOpenAiSentimentServiceTests.cs`** (new):

- `AnalyzeSentimentAsync_WhenApiSucceeds_ShouldReturnResult`
- `AnalyzeSentimentAsync_WhenApiReturnsInvalidLabel_ShouldFallBack`
- `AnalyzeSentimentAsync_WhenApiTimesOut_ShouldFallBack`
- `AnalyzeSentimentAsync_WhenApiThrowsHttpRequestException_ShouldFallBack`

**`Tests/RVS.API.Tests/Services/IntakeOrchestrationServiceTests.cs`** (existing, add):

- Add `Mock<ISentimentService> _sentimentMock` field and wire into constructor
- `ExecuteAsync_ShouldCallSentimentAnalysisOnIssueDescription`
- `ExecuteAsync_ShouldSetCustomerSentimentOnServiceRequest`
- `ExecuteAsync_WhenSentimentFails_ShouldStillCreateServiceRequest`
- `ExecuteAsync_WhenSentimentFails_ShouldSetCustomerSentimentToNull`
- Update `SetupFullHappyPath()` to include `_sentimentMock.Setup(...).ReturnsAsync("Calm")`

**`Tests/RVS.API.Tests/Mappers/ServiceRequestMapperTests.cs`** (existing, add):

- `CustomerSentiment` coverage in existing `ToDetailDto` and `ToSummaryDto` test methods

---

## Files Changed Summary

| File | Action | Layer |
|------|--------|-------|
| `RVS.Domain/Integrations/ISentimentService.cs` | **Create** | Domain |
| `RVS.Domain/Entities/ServiceRequest.cs` | Edit — add `CustomerSentiment` property | Domain |
| `RVS.Domain/DTOs/ServiceRequestDetailResponseDto.cs` | Edit — add `CustomerSentiment` | Domain |
| `RVS.Domain/DTOs/ServiceRequestSummaryResponseDto.cs` | Edit — add `CustomerSentiment` | Domain |
| `RVS.API/Integrations/AzureOpenAiSentimentService.cs` | **Create** | API |
| `RVS.API/Integrations/RuleBasedSentimentService.cs` | **Create** | API |
| `RVS.API/Integrations/MockSentimentService.cs` | **Create** | API |
| `RVS.API/Services/IntakeOrchestrationService.cs` | Edit — inject + call `ISentimentService` | API |
| `RVS.API/Mappers/ServiceRequestMapper.cs` | Edit — map `CustomerSentiment` in 2 methods | API |
| `RVS.API/Program.cs` | Edit — DI registration block | API |
| `RVS.Blazor.Manager/Pages/ServiceRequestDetail.razor` | Edit — add "Customer Tone" chip | UI |
| `RVS.Blazor.Manager/Pages/ServiceBoard.razor` | Edit — add sentiment chip on cards | UI |
| `Tests/RVS.API.Tests/Integrations/RuleBasedSentimentServiceTests.cs` | **Create** | Tests |
| `Tests/RVS.API.Tests/Integrations/MockSentimentServiceTests.cs` | **Create** | Tests |
| `Tests/RVS.API.Tests/Integrations/AzureOpenAiSentimentServiceTests.cs` | **Create** | Tests |
| `Tests/RVS.API.Tests/Services/IntakeOrchestrationServiceTests.cs` | Edit — add sentiment test cases | Tests |
| `Tests/RVS.API.Tests/Mappers/ServiceRequestMapperTests.cs` | Edit — add sentiment mapping tests | Tests |
| `Tests/RVS.Domain.Tests/Mappers/ServiceRequestMapperTests.cs` | Edit — add sentiment mapping tests | Tests |

**Total: 12 new/edited production files, 6 new/edited test files**

---

## Security & Privacy Safeguards

1. **`ServiceRequestCreateRequestDto` has no `CustomerSentiment` field** — the customer cannot submit or tamper with their own sentiment label.
2. **`ServiceRequestUpdateRequestDto` has no `CustomerSentiment` field** — dealers cannot override the AI-derived value.
3. **Intake Blazor app (`RVS.Blazor.Intake`) has zero references to sentiment** — no state property, no UI, no `sessionStorage` persistence.
4. **Sentiment is computed server-side only** inside `IntakeOrchestrationService` — not visible in browser network traffic to the customer.
5. **Displayed only in Manager app** which is behind Auth0 JWT authentication — the customer never sees it.
6. **UI label is "Customer Tone"** (not "Sentiment Score") — soft framing for dealership staff.
7. **No auto-prioritization** — sentiment is informational only and never feeds into status transitions or scheduling logic.

---

## Backward Compatibility

- Existing Cosmos DB documents without `customerSentiment` will deserialize to `null` — the UI renders `"—"` as a placeholder.
- No migration is needed — Cosmos DB's schema-free design handles missing properties gracefully.
- Existing Manager app clients will see `null` for pre-feature records.
- No breaking changes to any existing API endpoint contracts.
