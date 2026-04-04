# Implementation Plan: VIN Extraction from Photo (Option B2 — Azure OpenAI GPT-4o Vision)

**Issue:** #227 — Intake: implement VIN extraction from photo
**Approach:** Server-side VIN OCR via Azure OpenAI GPT-4o Vision, following existing `IVinDecoderService` / `AzureOpenAiCategorizationService` patterns

---

## Phase 1: Domain Layer — Interface + DTOs (TDD: RED first)

### 1.1 Create `IVinExtractionService` interface

- **File:** `RVS.Domain/Integrations/IVinExtractionService.cs`
- Define `Task<VinExtractionResult?> ExtractVinFromImageAsync(byte[] imageData, string contentType, CancellationToken)`
- Define `VinExtractionResult` record with `Vin` (string) and `Confidence` (double, 0.0–1.0)
- Follows the exact pattern of `IVinDecoderService` (single-method interface + companion record in same file)

### 1.2 Create request/response DTOs

- **File:** `RVS.Domain/DTOs/VinExtractionRequestDto.cs`
  - `sealed record` with `required string ImageBase64` and `required string ContentType`
  - Matches existing DTO naming pattern: `{Entity}{Action}RequestDto`
- **File:** `RVS.Domain/DTOs/VinExtractionResponseDto.cs`
  - `sealed record` with `required string? Vin`, `required double Confidence`, `string? ErrorMessage`
  - Matches `VinDecodeResponseDto` naming convention

---

## Phase 2: Domain Tests (TDD RED — write failing tests before implementation)

### 2.1 VinExtractionRequestDto / VinExtractionResponseDto validation

- **File:** `Tests/RVS.Domain.Tests/DTOs/VinExtractionDtoTests.cs`
- Test record instantiation and required properties

---

## Phase 3: API Layer — Service Implementations

### 3.1 Create `AzureOpenAiVinExtractionService`

- **File:** `RVS.API/Integrations/AzureOpenAiVinExtractionService.cs`
- `sealed class` implementing `IVinExtractionService`
- Constructor: `HttpClient`, `ILogger<AzureOpenAiVinExtractionService>`
- Flow:
  1. Convert `byte[]` image to base64 data URL
  2. Build GPT-4o Vision chat completion request with system prompt: *"Extract the 17-character Vehicle Identification Number (VIN) from this image. VINs contain only alphanumeric characters and never include the letters I, O, or Q. Return ONLY a JSON object: `{"vin": "<the VIN>", "confidence": <0.0–1.0>}`. If no VIN is visible, return `{"vin": null, "confidence": 0.0}`."*
  3. POST to Azure OpenAI chat completions endpoint (uses the existing `AzureOpenAi:Endpoint` config)
  4. Parse JSON response, validate extracted VIN via `VinValidator.ValidateFormat()`
  5. Return `VinExtractionResult` or `null` on failure
- Error handling: `catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)` → log warning, return `null` (matches `AzureOpenAiCategorizationService` pattern exactly)
- Never throws — graceful degradation to manual entry

### 3.2 Create `MockVinExtractionService`

- **File:** `RVS.API/Integrations/MockVinExtractionService.cs`
- `sealed class` implementing `IVinExtractionService`
- Returns a hardcoded valid VIN (`"1RGDE4428R1000001"`, confidence `0.95`) for any image
- Matches `MockVinDecoderService` pattern

### 3.3 Register in DI (`Program.cs`)

- Add a new section under `#region Integration Clients` after the VIN Decoder block:
  - `useMockIntegrations = true` → `MockVinExtractionService` (singleton)
  - `useMockIntegrations = false` → `AzureOpenAiVinExtractionService` via `AddHttpClient` with:
    - `BaseAddress` from `AzureOpenAi:Endpoint` config
    - `StandardResilienceHandler`: 5 s attempt / 10 s total timeout
- Follows the exact same `useMockIntegrations` toggle pattern as VIN decoder and categorization

### 3.4 Add config section to `appsettings.json`

- The existing `AzureOpenAi` section already has `Endpoint`, `ApiKey`, `DeploymentName`
- Add `VisionDeploymentName` field (defaults to same deployment if GPT-4o supports vision, or a separate deployment name)

---

## Phase 4: API Controller Endpoint

### 4.1 Add `POST extract-vin` endpoint to `IntakeController`

- **Method:** `ExtractVin(string locationSlug, [FromBody] VinExtractionRequestDto request, CancellationToken ct)`
- **Route:** `[HttpPost("extract-vin")]`
- Inject `IVinExtractionService` into `IntakeController` constructor (5th dependency)
- Validate:
  - `request.ImageBase64` is not empty
  - `request.ContentType` starts with `image/`
  - Base64 decodes to ≤ 10 MB
- Call `_vinExtractionService.ExtractVinFromImageAsync(imageBytes, contentType, ct)`
- If result is `null` → return `Ok(new VinExtractionResponseDto { Vin = null, Confidence = 0, ErrorMessage = "Could not extract VIN from image." })`
- If result has VIN → return `Ok(new VinExtractionResponseDto { Vin = result.Vin, Confidence = result.Confidence })`
- No 404 pattern — always returns 200 with a confidence indicator (0 = failed)

---

## Phase 5: API Tests (TDD RED → GREEN)

### 5.1 `MockVinExtractionServiceTests`

- **File:** `Tests/RVS.API.Tests/Integrations/MockVinExtractionServiceTests.cs`
- Test: null/empty image data → `ArgumentNullException` or `ArgumentException`
- Test: valid image data → returns hardcoded VIN with 0.95 confidence
- Follows `MockVinDecoderServiceTests` pattern exactly

### 5.2 `AzureOpenAiVinExtractionServiceTests`

- **File:** `Tests/RVS.API.Tests/Integrations/AzureOpenAiVinExtractionServiceTests.cs`
- Test: successful response parsing
- Test: HTTP failure → returns null (graceful degradation)
- Test: timeout → returns null
- Test: malformed JSON response → returns null
- Follows `AzureOpenAiCategorizationServiceTests` pattern

### 5.3 `IntakeControllerTests` additions

- **File:** `Tests/RVS.API.Tests/Controllers/IntakeControllerTests.cs`
- Add `Mock<IVinExtractionService>` to constructor setup
- Test: `ExtractVin_WhenExtractionSucceeds_ShouldReturnOkWithVin`
- Test: `ExtractVin_WhenExtractionFails_ShouldReturnOkWithNullVinAndErrorMessage`
- Test: `ExtractVin_WhenImageBase64IsEmpty_ShouldReturn400` (if adding validation)

---

## Phase 6: UI.Shared — API Client

### 6.1 Add `ExtractVinFromImageAsync` to `IntakeApiClient`

- **File:** `RVS.UI.Shared/Services/IntakeApiClient.cs`
- New method:
  ```
  Task<VinExtractionResponseDto?> ExtractVinFromImageAsync(
      string locationSlug, VinExtractionRequestDto request, CancellationToken)
  ```
- POST to `api/intake/{slug}/extract-vin` with JSON body
- Returns `null` on network error (matches `DecodeVinAsync` error handling)

### 6.2 IntakeApiClient tests

- **File:** `Tests/RVS.UI.Shared.Tests/Services/IntakeApiClientTests.cs`
- Test: null/empty `locationSlug` → `ArgumentException`
- Test: null `request` → `ArgumentNullException`
- Test: successful round-trip deserialization
- Test: non-success status → throws `HttpRequestException`
- Follows existing `DecodeVinAsync` test patterns in same file

---

## Phase 7: Blazor Intake — VinLookupStep UI Integration

### 7.1 Modify `HandleCameraCapture` in `VinLookupStep.razor`

- After capturing the image (existing base64 conversion), automatically trigger VIN extraction:
  1. Set new `_isExtracting = true` flag → show spinner overlay on the image preview
  2. Build `VinExtractionRequestDto` from the captured base64 and content type
  3. Call `IntakeApi.ExtractVinFromImageAsync(State.Slug, request)`
  4. On success (confidence ≥ threshold, e.g., 0.7):
     - Populate `State.Vin` with extracted VIN
     - Show success alert: *"VIN extracted from photo: {vin}"*
     - Optionally auto-trigger the existing `HandleLookup()` flow if confidence ≥ 0.9
  5. On failure or low confidence:
     - Show info alert: *"We couldn't read the VIN from the photo. Please enter it manually."*
     - Keep the captured image visible for manual reference (existing behavior)
  6. Set `_isExtracting = false` → remove spinner

### 7.2 UI state additions

- Add `_isExtracting` (bool) local field — UI-only, not persisted to state
- Add `_extractionMessage` (string?) local field for success/failure message
- Show `MudProgressCircular` over image preview during extraction
- Show `MudAlert` with extraction result (success = green, failure = info)

### 7.3 UX flow after changes

```
Tap "Capture VIN with Camera" → Native camera opens
Photo taken → Image preview displayed + "Extracting VIN…" spinner
   ├── Success (confidence ≥ 0.7) → VIN field auto-populated + "✓ VIN extracted: XXXXX" alert
   │   └── If confidence ≥ 0.9 → auto-trigger VIN decode (existing Look Up flow)
   ├── Low confidence (0 < confidence < 0.7) → VIN field populated + "⚠ Please verify" warning
   └── Failure → "Couldn't read VIN. Enter manually." info alert (existing manual flow)
```

---

## Phase 8: Configuration & Documentation

### 8.1 Update `appsettings.json` / `appsettings.Development.json`

- Add `AzureOpenAi:VisionDeploymentName` key (can be same as `DeploymentName` if using GPT-4o)

### 8.2 Update PRD or docs (if applicable)

- Mark FR-003 VIN extraction as implemented in relevant docs

---

## Files Changed Summary

| Layer | New Files | Modified Files |
|---|---|---|
| **Domain** | `IVinExtractionService.cs`, `VinExtractionRequestDto.cs`, `VinExtractionResponseDto.cs` | — |
| **API** | `AzureOpenAiVinExtractionService.cs`, `MockVinExtractionService.cs` | `IntakeController.cs`, `Program.cs`, `appsettings.json` |
| **UI.Shared** | — | `IntakeApiClient.cs` |
| **Blazor.Intake** | — | `VinLookupStep.razor` |
| **Tests (Domain)** | `VinExtractionDtoTests.cs` | — |
| **Tests (API)** | `MockVinExtractionServiceTests.cs`, `AzureOpenAiVinExtractionServiceTests.cs` | `IntakeControllerTests.cs` |
| **Tests (UI.Shared)** | — | `IntakeApiClientTests.cs` |

**Total: ~7 new files, ~6 modified files**

---

## TDD Execution Order

Following the project's mandatory Red → Green → Refactor cycle:

1. 🔴 **RED:** Write `MockVinExtractionServiceTests` → fail (no implementation)
2. 🟢 **GREEN:** Create `IVinExtractionService` + `MockVinExtractionService` → tests pass
3. 🔴 **RED:** Write `AzureOpenAiVinExtractionServiceTests` → fail
4. 🟢 **GREEN:** Create `AzureOpenAiVinExtractionService` → tests pass
5. 🔴 **RED:** Write `IntakeControllerTests` for `ExtractVin` → fail
6. 🟢 **GREEN:** Add endpoint to `IntakeController`, register DI → tests pass
7. 🔴 **RED:** Write `IntakeApiClientTests` for `ExtractVinFromImageAsync` → fail
8. 🟢 **GREEN:** Add method to `IntakeApiClient` → tests pass
9. 🔵 **REFACTOR:** Clean up, add XML docs, verify all existing tests still pass
10. Implement Blazor UI changes (`VinLookupStep.razor`)
11. Build & verify full solution compiles

---

## Risk Mitigations

| Risk | Mitigation |
|---|---|
| Azure OpenAI GPT-4o Vision not available in tenant's region | Graceful fallback — extraction returns `null`, user enters VIN manually (existing flow unchanged) |
| Image too large for GPT-4o context | Validate ≤ 10 MB on API side; JPEG compression on client if needed |
| OCR hallucination (returns wrong VIN) | Server-side `VinValidator.ValidateFormat()` rejects invalid format; user always sees extracted VIN and confirms before decode |
| Cost spike on high-volume tenants | Rate limiting already applies via `[EnableRateLimiting("IntakeEndpoint")]`; monitor via Azure OpenAI metrics |
| `Integrations:UseMocks` toggle consistency | Mock returns hardcoded valid VIN, exactly matching VIN decoder mock pattern |

---

## Azure Infrastructure Prerequisites

- GPT-4o deployment in Azure OpenAI (with vision capability)
- No new Azure resources needed — reuses existing `AzureOpenAi:Endpoint` and `AzureOpenAi:ApiKey`
- Estimated cost: ~$0.01–0.02 per extraction
