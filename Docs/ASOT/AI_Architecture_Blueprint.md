# AI Architecture Blueprint for Intake, Manager, and MAUI

## Purpose

Define a durable AI architecture that covers current AI issues (#227, #231, #232, #233) and scales to future use cases without reworking API contracts or client app foundations.

## Business Model Validation

- **Model:** B2B SaaS (dealership corporations are tenants).
- **Tenant definition:** dealer corporation (`tenantId`) with one or more physical locations.
- **Architecture implication:** AI operations must be tenant-scoped by default, with optional per-tenant capability toggles and usage limits.

## Source Inputs Considered

- GitHub issues with label `AI`: #227, #231, #232, #233
- Existing proposals:
  - `Docs/ASOT/AI_227_plan.md`
  - `Docs/ASOT/AI_232_plan.md`
- Product and technical context:
  - `Docs/ASOT/RVS_PRD.md`
  - `Docs/ASOT/RVS_Technical_PRD.md`
- Azure SaaS/multitenant guidance:
  - SaaS and multitenant architecture guidance
  - SaaS design principles
  - Deployment stamps pattern
  - Noisy neighbor antipattern

## Architecture Goals

- Keep **AI orchestration server-side** for security, governance, and consistency.
- Keep clients **thin and capability-aware** (camera/microphone/native speech), not AI business-logic heavy.
- Preserve current domain-driven layering and swappable integration interfaces.
- Support **graceful degradation**: local/native fallback, rule-based fallback, and manual UX fallback.
- Make tenant-level AI usage observable, throttleable, and billable.

## Core Principles

- **P1: Server-side AI decisions**
  - AI-derived fields that influence triage or records are produced in API services, not trusted from clients.
- **P2: Contract-first AI APIs**
  - Strong request/response DTOs with confidence, provenance, and warnings.
- **P3: Use-case isolation over generic prompt endpoint**
  - Expose explicit endpoints per workflow (VIN extraction, transcript cleanup, structured issue extraction, diagnostics chat).
- **P4: Provider abstraction + policy routing**
  - Domain interfaces remain provider-agnostic; API layer chooses provider/fallback by policy.
- **P5: Tenant protection**
  - Per-tenant quotas, token budgets, and payload limits to mitigate noisy neighbor impacts.

## Target API Architecture

## Layering

- **Domain (`RVS.Domain`)**
  - Add/extend AI interfaces in `Integrations/` for each capability.
  - Add AI response value objects/records that include confidence and diagnostics metadata.
- **API (`RVS.API`)**
  - `Services/` orchestrate workflow and enforce business rules.
  - `Integrations/` contain provider adapters (Azure OpenAI/Azure Speech/mock/rule-based).
  - `Controllers/` expose explicit user-journey endpoints, validate payloads, and return deterministic DTOs.
- **Infra (`RVS.Infra.*`)**
  - Persist conversation/session artifacts and AI telemetry envelopes in tenant-scoped stores where needed.

## AI Capability Interfaces (Domain)

- `IVinExtractionService`
- `ISpeechToTextService`
- `IIssueTextRefinementService`
- `IIssueStructuringService`
- `IDiagnosticsConversationService`
- `ISentimentService` (already proposed)

Each interface returns a typed result contract with:

- `Result` payload
- `Confidence` (`0.0` to `1.0`)
- `Provider` (`Mock`, `RuleBased`, `AzureOpenAI`, `AzureSpeech`, etc.)
- `Warnings` (validation soft-fail notes)
- `LatencyMs`

## AI Orchestration Services (API)

Create one orchestration service per journey instead of a monolith:

- `IntakeAiOrchestrationService`
  - Handles #227, #232, #233 for Intake submission workflow.
- `DiagnosticsConversationOrchestrationService`
  - Handles #231 session lifecycle and message turns.
- `ManagerInsightsService`
  - Handles dealer-facing summaries/sentiment rendering contracts.

These services coordinate parallel/conditional calls and defaulting behavior, for example:

- Run categorization + sentiment + issue-structuring in parallel when transcript/description is available.
- Fail soft per capability; never block intake submission when enrichment fails.

## API Endpoint Strategy

Use explicit nested routes under intake and service requests.

### Intake AI endpoints

- `POST /api/intake/{locationSlug}/ai/extract-vin`
- `POST /api/intake/{locationSlug}/ai/transcribe-issue`
- `POST /api/intake/{locationSlug}/ai/refine-issue-text`
- `POST /api/intake/{locationSlug}/ai/structure-issue`

### Diagnostics conversation endpoints

- `POST /api/intake/{locationSlug}/ai/diagnostics/sessions`
- `POST /api/intake/{locationSlug}/ai/diagnostics/sessions/{sessionId}/messages`
- `GET /api/intake/{locationSlug}/ai/diagnostics/sessions/{sessionId}`
- `POST /api/intake/{locationSlug}/ai/diagnostics/sessions/{sessionId}/finalize`

### Manager/Advisor enrichment endpoints

- `POST /api/dealers/{dealerId}/service-requests/{serviceRequestId}/ai/recompute-insights`
- `GET /api/dealers/{dealerId}/service-requests/{serviceRequestId}/ai/insights`

## Contract Shape (DTO Guidance)

Standardize API responses to reduce client complexity:

- `success` (bool)
- `result` (typed object or null)
- `confidence` (double)
- `warnings` (string list)
- `provider` (string)
- `correlationId` (string)

For conversation turns add:

- `sessionId`
- `turnId`
- `state` (`Active`, `NeedsHumanReview`, `Complete`)

## Client-Side Architecture

## Shared client package (`RVS.UI.Shared`)

Introduce `AiClient` abstraction consumed by all UI apps:

- `IAiClient` for cross-app contracts.
- `IntakeAiClient`, `ManagerAiClient`, `TechAiClient` specializations.
- Shared DTOs for AI calls and results.
- Built-in retry/backoff for transient failures.

## Intake app (`RVS.Blazor.Intake`)

- Keep existing local capability adapters:
  - Camera capture (VIN photo)
  - Browser/native microphone capture
  - Optional local speech capture where available
- Send media/transcript to API for canonical AI processing.
- UX pattern for every AI assistive action:
  - `Draft generated` -> `Customer review/edit` -> `Accept`.
- Never trust client-side extracted fields as final; server returns canonical values.

## Manager app (`RVS.Blazor.Manager`)

- Display AI outputs with confidence and provenance badges.
- Highlight low-confidence extraction/summary items for advisor review.
- Provide a one-click "Recompute AI insights" action for stale or corrected records.
- Keep AI metadata visible to staff only; never expose to customer-facing status pages.

## MAUI app (technician)

- Offline-first queue for AI-dependent requests.
- If offline, capture voice/text/photo locally and submit for AI processing when online.
- Provide local draft capture and deferred enrichment status.
- Prefer native capabilities for capture only; keep model inference in API for consistency.

## Data and Persistence Design

- Add AI enrichment sub-document on `ServiceRequest`:
  - `AiInsights`: sentiment, issue structure, technician summary, confidence values, provider metadata, last computed timestamp.
- Add diagnostics conversation container:
  - Partition key: `/tenantId`
  - Suggested id scope: `{serviceRequestId}:{sessionId}`
  - Store summarized turn history and final structured extraction.
- Keep raw media/transcript retention minimal and policy-driven.

## Security, Privacy, and Compliance

- Tenant guardrails on all AI endpoints:
  - Derive `tenantId` from claims/slug resolution, never trust caller-provided tenant values.
- PII handling:
  - Redact or hash sensitive fields before long-term telemetry persistence.
- Prompt safety:
  - Use strict system prompts and JSON schema-constrained responses where possible.
- Abuse controls:
  - Rate limiting per tenant + per IP for anonymous intake routes.
  - Payload size limits for audio/image.

## Reliability and Performance

- Timeouts and fallbacks:
  - short per-attempt timeout + bounded total timeout + fallback provider.
- Queueable workloads:
  - non-blocking enrichments can run async after submission and patch `ServiceRequest`.
- Idempotency:
  - deterministic idempotency key for repeated client retries.
- Avoid noisy neighbor:
  - per-tenant quotas, concurrency caps, and model budget tracking.

## Deployment and Scale Strategy (SaaS)

- Start with shared multitenant AI services and tenant-level throttles.
- Plan migration path to deployment stamps as tenant count/traffic grows.
- Keep tenant-to-stamp mapping externalized for routing when stamping is introduced.
- Support mixed isolation tiers:
  - shared tier (default)
  - premium isolated tier for high-volume or compliance-sensitive tenants.

## Observability and Cost Governance

- Emit AI telemetry per request:
  - tenantId, locationId, capability, provider, latency, token/audio/image usage, success/fallback path.
- Build dashboards for:
  - cost per tenant
  - confidence distribution by capability
  - fallback rate and timeout rate
  - top throttled tenants (early noisy-neighbor signal)
- Add configurable per-tenant monthly AI budgets and soft/hard limits.

## Mapping Current Issues to This Architecture

- **#227 VIN extraction from photo**
  - Capability: `IVinExtractionService`
  - Endpoint: `POST /ai/extract-vin`
  - Client: Intake camera capture + review/accept UX.
- **#232 speech-to-text**
  - Capability: `ISpeechToTextService` + `IIssueTextRefinementService`
  - Endpoints: `POST /ai/transcribe-issue`, `POST /ai/refine-issue-text`
  - Client: microphone affordance on issue field, editable result.
- **#233 structured issue extraction from transcript**
  - Capability: `IIssueStructuringService`
  - Endpoint: `POST /ai/structure-issue`
  - Output: structured issue fields feeding service event model.
- **#231 AI diagnostics conversation**
  - Capability: `IDiagnosticsConversationService`
  - Endpoints: diagnostics session lifecycle endpoints.
  - Persistence: conversation session artifacts under tenant scope.

## Changes to Existing Proposals

- Keep `AI_227_plan.md` and `AI_232_plan.md` as implementation-focused issue slices.
- Align them to:
  - shared AI contract envelope
  - common orchestration service pattern
  - standard endpoint naming under `/ai/*`
  - tenant-aware quotas and telemetry requirements
- Add #231 and #233 to the same architecture track instead of standalone one-off implementations.

## Recommended Incremental Roadmap

1. **Foundation**
   - Introduce shared AI contracts, `IAiClient`, common telemetry envelope, and endpoint naming standards.
2. **Issue delivery wave 1**
   - Ship #227 and #232 on the shared foundation.
3. **Issue delivery wave 2**
   - Ship #233 structuring and #231 diagnostics sessions.
4. **Operational hardening**
   - Add per-tenant AI budgets, confidence analytics, and async enrichment workers.
5. **Scale-out readiness**
   - Add deployment-stamp routing abstractions before high-scale rollout.

## Architectural Decisions Summary

- AI enrichment remains **API-owned** and tenant-governed.
- Clients remain **capture-first and review-first**, not inference engines.
- Endpoints are **workflow-specific**, not generic prompt pipes.
- Provider implementations are **swappable** behind domain interfaces.
- Multitenant resiliency is addressed through **quotas, telemetry, and stamp-ready design**.
