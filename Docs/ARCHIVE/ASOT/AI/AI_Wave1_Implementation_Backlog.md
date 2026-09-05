# AI Wave 1 Implementation Backlog

## Epic

- **Epic ID:** EPIC-AI-W1
- **Title:** Intake AI Assistive Workflows Wave 1
- **Scope:** Deliver production-ready AI capabilities for issue #227 (VIN extraction from photo) and issue #232 (speech-to-text + transcript cleanup) on top of the architecture in `docs/asot/AI_Architecture_Blueprint.md`.
- **Out of scope for this wave:** issue #231 diagnostics conversation, issue #233 structured issue extraction.

## Epic Outcome

- Intake users can capture a VIN photo and receive an AI-assisted VIN suggestion with confidence and manual fallback.
- Intake users can record issue detail audio and receive a cleaned, editable issue description.
- API and client contracts are standardized under `/api/intake/{locationSlug}/ai/*`.
- AI telemetry and tenant-safe throttling are in place for both workflows.

## Requirements and Constraints

- **REQ-001:** Keep all authoritative AI inference server-side in `RVS.API`.
- **REQ-002:** Preserve existing anonymous intake route base: `api/intake/{locationSlug}`.
- **REQ-003:** AI responses must include confidence and provider metadata.
- **REQ-004:** All AI actions must degrade gracefully to manual entry.
- **SEC-001:** Enforce payload and media type validation before AI provider calls.
- **SEC-002:** Do not expose sensitive implementation details in API errors.
- **CON-001:** Follow existing DI and integration client patterns in `Program.cs`.
- **CON-002:** Follow mandatory TDD sequence from repository testing instructions.

## Exact API Contracts (Wave 1)

## Endpoint 1: VIN extraction from photo (#227)

- **Method/Route:** `POST /api/intake/{locationSlug}/ai/extract-vin`
- **Auth:** `[AllowAnonymous]` (inherits intake controller)
- **Rate limit:** `IntakeEndpoint` policy (existing), plus endpoint-level payload limits

### Request DTO contract

- **Type:** `VinExtractionRequestDto`
- **Namespace:** `RVS.Domain.DTOs`

| Field | Type | Required | Rules |
|------|------|----------|------|
| `imageBase64` | string | Yes | Base64 payload only, no data URL prefix |
| `contentType` | string | Yes | Must start with `image/`; allowed: `image/jpeg`, `image/png`, `image/webp` |

### Response DTO contract

- **Type:** `AiOperationResponseDto<VinExtractionResultDto>`
- **Namespace:** `RVS.Domain.DTOs`

| Field | Type | Required | Notes |
|------|------|----------|------|
| `success` | bool | Yes | `true` when extraction call completed (even if no VIN found) |
| `result` | `VinExtractionResultDto?` | No | Null when extraction failed or no VIN detected |
| `confidence` | double | Yes | `0.0` to `1.0` |
| `warnings` | `IReadOnlyList<string>` | Yes | Non-blocking warnings |
| `provider` | string | Yes | `MockVinExtractionService` or `AzureOpenAiVinExtractionService` |
| `correlationId` | string | Yes | From logging middleware/request trace |

`VinExtractionResultDto`:

| Field | Type | Required | Notes |
|------|------|----------|------|
| `vin` | string? | No | 17 chars if present; never contains I/O/Q |

### Status codes

- `200 OK`: Contract response returned.
- `400 BadRequest`: Invalid payload (empty base64, invalid content type, exceeds size cap).
- `413 PayloadTooLarge`: Image size exceeds max configured limit.

## Endpoint 2: Speech transcription (#232)

- **Method/Route:** `POST /api/intake/{locationSlug}/ai/transcribe-issue`
- **Auth:** `[AllowAnonymous]`
- **Rate limit:** `IntakeEndpoint` policy

### Request DTO contract

- **Type:** `IssueTranscriptionRequestDto`
- **Namespace:** `RVS.Domain.DTOs`

| Field | Type | Required | Rules |
|------|------|----------|------|
| `audioBase64` | string | Yes | Base64 payload only |
| `contentType` | string | Yes | Must start with `audio/`; allowed: `audio/webm`, `audio/wav`, `audio/mp4` |
| `locale` | string | No | Default `en-US` |

### Response DTO contract

- **Type:** `AiOperationResponseDto<IssueTranscriptionResultDto>`

`IssueTranscriptionResultDto`:

| Field | Type | Required | Notes |
|------|------|----------|------|
| `rawTranscript` | string? | No | Direct speech-to-text output |
| `cleanedDescription` | string? | No | AI-cleaned text suitable for `IssueDescription` |

### Status codes

- `200 OK`: Contract response returned.
- `400 BadRequest`: Invalid audio payload.
- `413 PayloadTooLarge`: Audio size exceeds max configured limit.

## Endpoint 3: Transcript refinement (#232)

- **Method/Route:** `POST /api/intake/{locationSlug}/ai/refine-issue-text`
- **Auth:** `[AllowAnonymous]`
- **Rate limit:** `IntakeEndpoint` policy

### Request DTO contract

- **Type:** `IssueTextRefinementRequestDto`

| Field | Type | Required | Rules |
|------|------|----------|------|
| `rawTranscript` | string | Yes | 1..4000 chars |
| `issueCategory` | string | No | Optional context for cleanup |

### Response DTO contract

- **Type:** `AiOperationResponseDto<IssueTextRefinementResultDto>`

`IssueTextRefinementResultDto`:

| Field | Type | Required | Notes |
|------|------|----------|------|
| `cleanedDescription` | string? | No | Customer-editable final draft |

### Status codes

- `200 OK`: Contract response returned.
- `400 BadRequest`: Invalid transcript payload.

## Endpoint 4: Category suggestion from description (#232 + FR-004)

- **Method/Route:** `POST /api/intake/{locationSlug}/ai/suggest-category`
- **Auth:** `[AllowAnonymous]`
- **Rate limit:** `IntakeEndpoint` policy

### Request DTO contract

- **Type:** `IssueCategorySuggestionRequestDto`

| Field | Type | Required | Rules |
|------|------|----------|------|
| `issueDescription` | string | Yes | 1..2000 chars |

### Response DTO contract

- **Type:** `AiOperationResponseDto<IssueCategorySuggestionResultDto>`

`IssueCategorySuggestionResultDto`:

| Field | Type | Required | Notes |
|------|------|----------|------|
| `issueCategory` | string? | No | Null when no confident suggestion is available |

### Status codes

- `200 OK`: Contract response returned.
- `400 BadRequest`: Invalid description payload.

## Endpoint 5: Insights suggestion (urgency + RV usage)

- **Method/Route:** `POST /api/intake/{locationSlug}/ai/suggest-insights`
- **Auth:** `[AllowAnonymous]`
- **Rate limit:** `IntakeEndpoint` policy

### Request DTO contract

- **Type:** `IssueInsightsSuggestionRequestDto`

| Field | Type | Required | Rules |
|------|------|----------|------|
| `issueDescription` | string | Yes | 1..2000 chars |

### Response DTO contract

- **Type:** `AiOperationResponseDto<IssueInsightsSuggestionResultDto>`

`IssueInsightsSuggestionResultDto`:

| Field | Type | Required | Notes |
|------|------|----------|------|
| `urgency` | string? | No | One of: Low, Medium, High, Critical; null when not determinable |
| `rvUsage` | string? | No | One of: Full-Time, Part-Time, Seasonal, Occasional; null when not determinable |

### Status codes

- `200 OK`: Contract response returned.
- `400 BadRequest`: Invalid description payload.

## Endpoint 6: Diagnostic questions

- **Method/Route:** `POST /api/intake/{locationSlug}/diagnostic-questions`
- **Auth:** `[AllowAnonymous]`
- **Rate limit:** `IntakeEndpoint` policy

### Request DTO contract

- **Type:** `DiagnosticQuestionsRequest`

| Field | Type | Required | Rules |
|------|------|----------|------|
| `issueCategory` | string | Yes | Category from LookupSet |
| `issueDescription` | string? | No | Optional context for more targeted questions |
| `manufacturer` | string? | No | Vehicle manufacturer |
| `model` | string? | No | Vehicle model |
| `year` | int? | No | Vehicle year |

### Response DTO contract

- **Type:** `DiagnosticQuestionsResponseDto`

| Field | Type | Required | Notes |
|------|------|----------|------|
| `questions` | `List<DiagnosticQuestionDto>` | Yes | 2–4 follow-up questions |
| `smartSuggestion` | string? | No | Optional tip for the customer |

`DiagnosticQuestionDto`:

| Field | Type | Required | Notes |
|------|------|----------|------|
| `questionText` | string | Yes | The question text |
| `options` | `List<string>` | Yes | 2–6 predefined answer options |
| `allowFreeText` | bool | Yes | Whether free-text is accepted |
| `helpText` | string? | No | Optional context for the customer |

### Status codes

- `200 OK`: Questions returned.

## AI Provenance Contract (Manager-Facing)

The `ServiceRequestDetailResponseDto` includes an optional `AiEnrichment` field of type
`AiEnrichmentMetadataDto` that records which AI capabilities were used during intake:

| Field | Type | Notes |
|------|------|------|
| `categorySuggestionProvider` | string? | Provider that suggested the issue category |
| `categorySuggestionConfidence` | double? | Confidence of category suggestion (0.0–1.0) |
| `diagnosticQuestionsProvider` | string? | Provider that generated diagnostic questions |
| `transcriptionProvider` | string? | Provider that transcribed the issue audio |
| `transcriptionConfidence` | double? | Confidence of transcription (0.0–1.0) |
| `vinExtractionProvider` | string? | Provider that extracted the VIN from a photo |
| `vinExtractionConfidence` | double? | Confidence of VIN extraction (0.0–1.0) |
| `insightsSuggestionProvider` | string? | Provider that inferred urgency/RV usage |
| `insightsSuggestionConfidence` | double? | Confidence of insights suggestion (0.0–1.0) |
| `enrichedAtUtc` | DateTime? | When AI enrichment was last computed |

This provenance trail allows service advisors in the manager app to distinguish AI-generated
fields from human-entered data and assess the confidence of AI-provided values.

## AI Telemetry Events

All Wave 1 AI endpoints emit structured log events with the following fields, captured
by the `CorrelationLoggingMiddleware` scope (which adds `TenantId` and `CorrelationId`):

| Field | Type | Description |
|------|------|-------------|
| `Capability` | string | AI capability name (e.g. `VinExtraction`, `TranscribeIssue`) |
| `Provider` | string | Service implementation that fulfilled the request |
| `Confidence` | double | Confidence score (0.0–1.0) |
| `LatencyMs` | long | Wall-clock time in milliseconds for the AI call |
| `Fallback` | bool | Whether a fallback provider was used |
| `Success` | bool | Whether the AI call produced an actionable result |

These fields are queryable in Application Insights custom events and can be used to build
dashboards for cost per tenant, confidence distribution, fallback rate, and latency tracking.

## Epic Phases and Issue Breakdown

### Phase 0: Cross-cutting foundation

- **GOAL-001:** Establish common AI contract envelope and DI wiring pattern before issue implementation.
- **Issues:** Platform foundation for #227 and #232.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create generic response envelope DTO `AiOperationResponseDto<T>` in `RVS.Domain/DTOs/AiOperationResponseDto.cs`. |  |  |
| TASK-002 | Add AI settings model for payload limits/timeouts in `RVS.API/Integrations/AiOptions.cs`. |  |  |
| TASK-003 | Add AI configuration section to `RVS.API/appsettings.json` and `RVS.API/appsettings.Development.json` (`MaxImageBytes`, `MaxAudioBytes`, `AllowedImageTypes`, `AllowedAudioTypes`). |  |  |
| TASK-004 | Register AI options and integration clients in `RVS.API/Program.cs` with same resilience pattern as existing OpenAI categorization client. |  |  |

### Phase 1: Issue #227 VIN extraction from photo

- **GOAL-002:** Deliver VIN extraction end to end (API + shared client + Intake UX) with confidence and fallback.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-005 | RED: Add failing domain DTO tests in `Tests/RVS.Domain.Tests/DTOs/VinExtractionDtoTests.cs` for request/response contracts. |  |  |
| TASK-006 | GREEN: Add `RVS.Domain/Integrations/IVinExtractionService.cs` and DTOs `RVS.Domain/DTOs/VinExtractionRequestDto.cs`, `RVS.Domain/DTOs/VinExtractionResultDto.cs`. |  |  |
| TASK-007 | RED: Add failing integration tests in `Tests/RVS.API.Tests/Integrations/MockVinExtractionServiceTests.cs` and `Tests/RVS.API.Tests/Integrations/AzureOpenAiVinExtractionServiceTests.cs`. |  |  |
| TASK-008 | GREEN: Implement `RVS.API/Integrations/MockVinExtractionService.cs` and `RVS.API/Integrations/AzureOpenAiVinExtractionService.cs` with fallback behavior. |  |  |
| TASK-009 | RED: Add failing controller tests in `Tests/RVS.API.Tests/Controllers/IntakeControllerTests.cs` for `POST ai/extract-vin`. |  |  |
| TASK-010 | GREEN: Add endpoint in `RVS.API/Controllers/IntakeController.cs` and request validation helpers. |  |  |
| TASK-011 | GREEN: Add shared client method in `RVS.UI.Shared/Services/IntakeApiClient.cs`: `ExtractVinFromImageAsync(...)`. |  |  |
| TASK-012 | GREEN: Update Intake UI in `RVS.Blazor.Intake/Pages/VinLookupStep.razor` to call endpoint after capture and apply confidence UX rules. |  |  |
| TASK-013 | REFACTOR: Add/adjust client tests in `Tests/RVS.UI.Shared.Tests/Services/IntakeApiClientTests.cs` and update existing Intake component tests if present. |  |  |

### Phase 2: Issue #232 speech-to-text and transcript cleanup

- **GOAL-003:** Deliver microphone workflow that produces cleaned, editable issue description.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | RED: Add failing domain tests in `Tests/RVS.Domain.Tests/DTOs/IssueTranscriptionDtoTests.cs` and `Tests/RVS.Domain.Tests/DTOs/IssueTextRefinementDtoTests.cs`. |  |  |
| TASK-015 | GREEN: Add interfaces `RVS.Domain/Integrations/ISpeechToTextService.cs` and `RVS.Domain/Integrations/IIssueTextRefinementService.cs`. |  |  |
| TASK-016 | GREEN: Add DTOs `RVS.Domain/DTOs/IssueTranscriptionRequestDto.cs`, `RVS.Domain/DTOs/IssueTranscriptionResultDto.cs`, `RVS.Domain/DTOs/IssueTextRefinementRequestDto.cs`, `RVS.Domain/DTOs/IssueTextRefinementResultDto.cs`. |  |  |
| TASK-017 | RED: Add failing integration tests in `Tests/RVS.API.Tests/Integrations/AzureSpeechToTextServiceTests.cs`, `Tests/RVS.API.Tests/Integrations/MockSpeechToTextServiceTests.cs`, `Tests/RVS.API.Tests/Integrations/AzureOpenAiIssueTextRefinementServiceTests.cs`. |  |  |
| TASK-018 | GREEN: Implement services `RVS.API/Integrations/AzureSpeechToTextService.cs`, `RVS.API/Integrations/MockSpeechToTextService.cs`, `RVS.API/Integrations/AzureOpenAiIssueTextRefinementService.cs`, `RVS.API/Integrations/RuleBasedIssueTextRefinementService.cs`. |  |  |
| TASK-019 | RED: Add failing controller tests in `Tests/RVS.API.Tests/Controllers/IntakeControllerTests.cs` for `POST ai/transcribe-issue` and `POST ai/refine-issue-text`. |  |  |
| TASK-020 | GREEN: Implement endpoints in `RVS.API/Controllers/IntakeController.cs` and wire into DI in `RVS.API/Program.cs`. |  |  |
| TASK-021 | GREEN: Extend `RVS.UI.Shared/Services/IntakeApiClient.cs` with `TranscribeIssueAsync(...)`, `RefineIssueTextAsync(...)`, and `SuggestIssueCategoryAsync(...)`. |  |  |
| TASK-022 | GREEN: Update `RVS.Blazor.Intake` issue-description step component to support mic capture, spinner state, editable cleaned output, speech-first ordering, and fallback text input. |  |  |
| TASK-023 | GREEN: Add debounced category suggestion call on description completion and render an "AI suggested" indicator while keeping category override enabled. |  |  |
| TASK-023A | REFACTOR: Add/adjust tests in `Tests/RVS.UI.Shared.Tests/Services/IntakeApiClientTests.cs` and relevant Blazor tests for issue step UX behavior and suggestion override logic. |  |  |

### Phase 3: Wave 1 hardening and release

- **GOAL-004:** Ensure tenant-safe operations, telemetry, documentation, and rollout readiness.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-024 | Add AI telemetry events (capability, tenantId, provider, confidence, latency, fallback path) in `IntakeController` for all AI endpoints. | ✅ | 2026-04-07 |
| TASK-025 | Update API documentation: add endpoint 5 (suggest-insights), endpoint 6 (diagnostic-questions), and AI provenance contract docs. Verify OpenAPI reflects new endpoints. | ✅ | 2026-04-07 |
| TASK-026 | Add manager-facing AI provenance contract: `AiEnrichmentMetadataEmbedded` on `ServiceRequest`, `AiEnrichmentMetadataDto`, mapper, and `ServiceRequestDetailResponseDto.AiEnrichment`. | ✅ | 2026-04-07 |
| TASK-027 | Execute full test suite (229 domain + 768 API tests). 5 pre-existing failures in `IntakeOrchestrationServiceTests` unrelated to Wave 1. All new tests pass. | ✅ | 2026-04-07 |

## File-by-File Task Matrix (Wave 1)

| File | Wave | Task IDs | Action |
|------|------|----------|--------|
| `RVS.Domain/DTOs/AiOperationResponseDto.cs` | Foundation | TASK-001 | Create generic AI envelope DTO |
| `RVS.API/Integrations/AiOptions.cs` | Foundation | TASK-002 | Create AI options model |
| `RVS.API/appsettings.json` | Foundation | TASK-003 | Add AI limits and allowed media types |
| `RVS.API/appsettings.Development.json` | Foundation | TASK-003 | Add AI limits and allowed media types |
| `RVS.API/Program.cs` | Foundation, #227, #232 | TASK-004, TASK-020 | Register options and integration services |
| `RVS.Domain/Integrations/IVinExtractionService.cs` | #227 | TASK-006 | Create interface and result contract |
| `RVS.Domain/DTOs/VinExtractionRequestDto.cs` | #227 | TASK-006 | Create request DTO |
| `RVS.Domain/DTOs/VinExtractionResultDto.cs` | #227 | TASK-006 | Create result DTO |
| `RVS.API/Integrations/MockVinExtractionService.cs` | #227 | TASK-008 | Create mock implementation |
| `RVS.API/Integrations/AzureOpenAiVinExtractionService.cs` | #227 | TASK-008 | Create OpenAI implementation |
| `RVS.API/Controllers/IntakeController.cs` | #227, #232 | TASK-010, TASK-020 | Add 4 AI endpoints and validation |
| `RVS.UI.Shared/Services/IntakeApiClient.cs` | #227, #232 | TASK-011, TASK-021 | Add typed client methods |
| `RVS.Blazor.Intake/Pages/VinLookupStep.razor` | #227 | TASK-012 | Invoke VIN extraction and confidence UX |
| `RVS.Blazor.Intake/Pages/Issue*.razor` | #232 | TASK-022 | Mic UX + cleaned text review |
| `RVS.Domain/DTOs/IssueCategorySuggestionRequestDto.cs` | #232 | TASK-016 | Create request DTO |
| `RVS.Domain/DTOs/IssueCategorySuggestionResultDto.cs` | #232 | TASK-016 | Create result DTO |
| `RVS.Domain/Integrations/ISpeechToTextService.cs` | #232 | TASK-015 | Create interface |
| `RVS.Domain/Integrations/IIssueTextRefinementService.cs` | #232 | TASK-015 | Create interface |
| `RVS.Domain/DTOs/IssueTranscriptionRequestDto.cs` | #232 | TASK-016 | Create request DTO |
| `RVS.Domain/DTOs/IssueTranscriptionResultDto.cs` | #232 | TASK-016 | Create result DTO |
| `RVS.Domain/DTOs/IssueTextRefinementRequestDto.cs` | #232 | TASK-016 | Create request DTO |
| `RVS.Domain/DTOs/IssueTextRefinementResultDto.cs` | #232 | TASK-016 | Create result DTO |
| `RVS.API/Integrations/AzureSpeechToTextService.cs` | #232 | TASK-018 | Create speech provider adapter |
| `RVS.API/Integrations/MockSpeechToTextService.cs` | #232 | TASK-018 | Create mock speech adapter |
| `RVS.API/Integrations/AzureOpenAiIssueTextRefinementService.cs` | #232 | TASK-018 | Create AI cleanup adapter |
| `RVS.API/Integrations/RuleBasedIssueTextRefinementService.cs` | #232 | TASK-018 | Create fallback cleanup adapter |
| `Tests/RVS.Domain.Tests/DTOs/VinExtractionDtoTests.cs` | #227 | TASK-005 | Add failing then passing DTO tests |
| `Tests/RVS.API.Tests/Integrations/MockVinExtractionServiceTests.cs` | #227 | TASK-007 | Add integration tests |
| `Tests/RVS.API.Tests/Integrations/AzureOpenAiVinExtractionServiceTests.cs` | #227 | TASK-007 | Add integration tests |
| `Tests/RVS.API.Tests/Controllers/IntakeControllerTests.cs` | #227, #232 | TASK-009, TASK-019 | Add endpoint tests |
| `Tests/RVS.UI.Shared.Tests/Services/IntakeApiClientTests.cs` | #227, #232 | TASK-013, TASK-023A | Add client contract tests |
| `Tests/RVS.Domain.Tests/DTOs/IssueTranscriptionDtoTests.cs` | #232 | TASK-014 | Add DTO tests |
| `Tests/RVS.Domain.Tests/DTOs/IssueTextRefinementDtoTests.cs` | #232 | TASK-014 | Add DTO tests |
| `Tests/RVS.API.Tests/Integrations/AzureSpeechToTextServiceTests.cs` | #232 | TASK-017 | Add integration tests |
| `Tests/RVS.API.Tests/Integrations/MockSpeechToTextServiceTests.cs` | #232 | TASK-017 | Add integration tests |
| `Tests/RVS.API.Tests/Integrations/AzureOpenAiIssueTextRefinementServiceTests.cs` | #232 | TASK-017 | Add integration tests |

## Acceptance Criteria by Issue

## Issue #227

- **AC-227-01:** Captured image can be submitted to API and returns envelope response with provider and confidence.
- **AC-227-02:** VIN is auto-populated only when response contains a valid 17-char VIN.
- **AC-227-03:** User can always override VIN manually.
- **AC-227-04:** Invalid image payloads return `400` without internal stack details.

## Issue #232

- **AC-232-01:** Recorded audio returns raw transcript and cleaned description.
- **AC-232-02:** Cleaned description is editable before submission.
- **AC-232-03:** Failures do not block manual issue entry.
- **AC-232-04:** Transcript/refinement endpoints return standardized envelope and confidence/provider metadata.
- **AC-232-05:** Description-first flow auto-suggests category and preselects dropdown while preserving manual override.

## Risks and Mitigations

- **RISK-001:** Model latency spikes on peak usage.
  - **Mitigation:** Timeout + fallback pattern already standardized in existing integrations.
- **RISK-002:** Hallucinated VIN output.
  - **Mitigation:** Validate VIN format/check digit before UI auto-fill acceptance.
- **RISK-003:** Large media payload abuse on anonymous endpoints.
  - **Mitigation:** strict size caps + allowed media list + rate limiting.
- **RISK-004:** Inconsistent UX between browser and MAUI capture paths.
  - **Mitigation:** keep shared API contracts and shared client DTOs in `RVS.UI.Shared`.

## Dependencies

- Azure OpenAI deployment for vision and text cleanup.
- Speech service path decision:
  - Azure Speech service adapter, or
  - OpenAI transcription adapter if preferred.
- Existing intake route and rate limiting policies in `RVS.API`.

## Recommended Tracking Structure in GitHub

- **Epic:** EPIC-AI-W1 Intake AI Assistive Workflows
- **Phase labels:** `phase:foundation`, `phase:227`, `phase:232`, `phase:hardening`
- **Issue labels:** `AI`, `intake`, `api`, `client`, `tests`
