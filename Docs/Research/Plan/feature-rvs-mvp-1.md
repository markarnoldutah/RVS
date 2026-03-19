---
goal: Implement RVS MVP Features From PRD In 8 Weeks
version: 1.0
date_created: 2026-03-18
last_updated: 2026-03-18
owner: Solo Developer (AI-assisted)
status: 'Planned'
tags: [feature, architecture, mvp, saas, azure, blazor, aspnet]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This plan defines a deterministic, execution-ready implementation sequence for RVS MVP features documented in the PRD, constrained to 8 calendar weeks for one developer using AI coding assistance. The sequence prioritizes critical user flows, tenant isolation, and production-risk controls identified in ASOT documents.

## 1. Requirements & Constraints

- **REQ-001**: Implement all Critical and High-priority PRD functional requirements required for MVP user adoption: FR-001, FR-002, FR-006, FR-008, FR-009, FR-013, FR-014, FR-015, FR-017, FR-018 from [Docs/Research/ASOT/RVS_PRD.md](Docs/Research/ASOT/RVS_PRD.md).
- **REQ-002**: Implement customer intake capabilities in this MVP window: FR-003 VIN scan/lookup, FR-004 guided issue wizard, FR-005 speech-to-text cleanup, FR-007 attachment upload limits, FR-008 magic-link status.
- **REQ-003**: Enforce multi-tenant isolation by `tenantId` and location-scoped access claims on all data operations per [Docs/Research/ASOT/RVS_Core_Architecture_Version3.md](Docs/Research/ASOT/RVS_Core_Architecture_Version3.md).
- **REQ-004**: Preserve role and permission behavior from [Docs/Research/ASOT/RVS_Auth0_Identity_Version2.md](Docs/Research/ASOT/RVS_Auth0_Identity_Version2.md).
- **SEC-001**: Store SFTP and provider secrets in Azure Key Vault only; do not persist raw private keys in Cosmos documents.
- **SEC-002**: Apply anonymous endpoint rate limiting for intake and status APIs with `429` and `Retry-After` behavior.
- **SEC-003**: Keep blob containers private and use SAS URLs with 1-hour read expiry for attachment access.
- **REL-001**: Add health endpoints `/health`, `/health/live`, `/health/ready` before launch.
- **REL-002**: Add Application Insights telemetry with tenant-aware dimensions (`TenantId`, `LocationId`, `UserId`) before production.
- **PER-001**: Use direct-to-blob SAS upload flow for attachments to avoid API worker saturation.
- **PER-002**: Keep all hot-path service request queries single-partition by `tenantId`.
- **CON-001**: Total implementation duration shall not exceed 8 calendar weeks (56 days).
- **CON-002**: Team size is 1 developer; all tasks must be scoped for solo throughput with AI assistance.
- **CON-003**: No replacement of external DMS platforms in MVP; only CSV/SFTP export integration.
- **GUD-001**: Follow controller/service/mapper and middleware conventions in `.github/copilot-instructions.md`.
- **GUD-002**: Keep domain layer (`RVS.Domain`) free of infrastructure dependencies.
- **PAT-001**: Maintain interface-driven service abstractions (`ICategorizationService`, `INotificationService`, repository interfaces) to allow later provider swaps.

## 2. Implementation Steps

### Implementation Phase 1

- **GOAL-001**: Establish production-safe foundation (Weeks 1-2) by completing tenant security baseline, middleware, and observability before feature acceleration.
- **Completion Criteria**: API starts successfully, auth and tenant gate are enforced, health checks and telemetry are emitting, and secrets are retrieved from Key Vault.

| Task | Description | Completed | Date |
| -------- | ----------- | --------- | ---- |
| TASK-001 | Update `RVS.API/Program.cs` to register `ExceptionHandlingMiddleware`, `TenantAccessGateMiddleware`, JWT auth, CORS policy, health checks (`/health`, `/health/live`, `/health/ready`), and ASP.NET rate limiter policies (`IntakePolicy`, `StatusPolicy`, `PerTenantPolicy`). |  |  |
| TASK-002 | Implement/verify `RVS.API/Services/ClaimsService.cs` methods: `GetTenantIdOrThrow()`, `GetUserIdOrThrow()`, `GetRoles()`, `GetLocationIds()`, and integrate in every controller entrypoint. |  |  |
| TASK-003 | Implement/verify `RVS.API/Middleware/TenantAccessGateMiddleware.cs` allowlist and deny response contract (`403` structured JSON) using `TenantConfig` access gate checks. |  |  |
| TASK-004 | Integrate Azure Key Vault configuration provider in `RVS.API/Program.cs` and move sensitive config references out of `RVS.API/appsettings*.json`. |  |  |
| TASK-005 | Instrument Application Insights in `RVS.API/Program.cs` and add telemetry enrichment for tenant context in middleware/service pipeline. |  |  |
| TASK-006 | Add validation tests for 401/403/429 behavior in `RVS.API.Tests` (or equivalent test project) covering auth, access gate, and anonymous rate limit endpoints. |  |  |

### Implementation Phase 2

- **GOAL-002**: Deliver end-to-end anonymous intake orchestration and data writes (Weeks 2-3) for service request creation with structured output.
- **Completion Criteria**: Single intake submission creates/updates identity, profile, service request, and asset ledger records and returns `201 Created` with status link.

| Task | Description | Completed | Date |
| -------- | ----------- | --------- | ---- |
| TASK-007 | Implement `RVS.API/Controllers/IntakeController.cs` endpoints for intake bootstrap, submission, and attachment upload URL generation at route `api/intake/{locationSlug}/service-requests`. |  |  |
| TASK-008 | Implement `RVS.API/Services/ServiceRequestService.cs` orchestration sequence: slug resolution, global identity upsert, customer profile resolution, request creation, asset ledger append, notification dispatch trigger. |  |  |
| TASK-009 | Implement `RVS.API/Services/CustomerProfileService.cs` VIN ownership transfer and reactivation logic using tenant-scoped profile repository operations. |  |  |
| TASK-010 | Implement `RVS.API/Services/GlobalCustomerAcctService.cs` token generation/rotation and linked profile updates with single-partition lookup strategy. |  |  |
| TASK-011 | Implement `RVS.API/Services/CategorizationService.cs` MVP rule-based categorization and technician summary generation under `ICategorizationService`. |  |  |
| TASK-012 | Implement `RVS.API/Services/VinDecodeService.cs` and `RVS.API/Integrations/Vin/vPICClient.cs` for VIN decode with fallback behavior when external decode is unavailable. |  |  |
| TASK-013 | Add integration tests in `RVS.API.Tests/Intake/` for first-time customer, returning customer, VIN transfer, invalid slug, and partial attachment failure paths. |  |  |

### Implementation Phase 3

- **GOAL-003**: Deliver dealer dashboard backend workflows (Weeks 4-5) for queue search, status transitions, notes, attachments, and Section 10A updates.
- **Completion Criteria**: Dealer roles can search/view/update service requests with permission enforcement and attachment access via SAS.

| Task | Description | Completed | Date |
| -------- | ----------- | --------- | ---- |
| TASK-014 | Implement `RVS.API/Controllers/ServiceRequestsController.cs` actions: `GET {id}`, `POST search`, `PUT {id}`, `DELETE {id}`, with tenant claim enforcement and `PagedResult` responses. |  |  |
| TASK-015 | Implement `RVS.API/Services/ServiceRequestSearchService.cs` and repository query methods with indexed filters for status, category, date range, and location scope. |  |  |
| TASK-016 | Implement validated status transition enforcement in `RVS.API/DomainRules/StatusTransitions.cs` and use from `ServiceRequestService.UpdateAsync`. |  |  |
| TASK-017 | Implement Section 10A update endpoint/service method in `RVS.API/Controllers/ServiceRequestsController.cs` and `RVS.API/Services/ServiceRequestService.cs` with permission `service-requests:update-service-event`. |  |  |
| TASK-018 | Implement `RVS.API/Controllers/AttachmentsController.cs` and `RVS.API/Services/AttachmentService.cs` for read SAS generation (`1-hour`), delete operation, and missing blob handling (`404`). |  |  |
| TASK-019 | Implement advisor status-change notification trigger (`InProgress`, `Completed`) through `INotificationService` abstraction. |  |  |
| TASK-020 | Add tests in `RVS.API.Tests/ServiceRequests/` for role restrictions, status transitions, dangerous character validation, and attachment permission gates. |  |  |

### Implementation Phase 4

- **GOAL-004**: Deliver tenant configuration, location management, lookup sets, QR, and export capabilities (Week 6).
- **Completion Criteria**: Tenant admins can manage location/intake config, download QR, and execute CSV export + SFTP push using Key Vault-backed credentials.

| Task | Description | Completed | Date |
| -------- | ----------- | --------- | ---- |
| TASK-021 | Implement/complete `RVS.API/Controllers/LocationsController.cs` and `RVS.API/Services/LocationService.cs` for create/update/list with immutable slug behavior and slug lookup synchronization. |  |  |
| TASK-022 | Implement/complete `RVS.API/Controllers/LookupsController.cs` and `RVS.API/Services/LookupService.cs` for category retrieval and tenant overrides. |  |  |
| TASK-023 | Implement `RVS.API/Controllers/LocationsController.cs` endpoint `GET /api/locations/{id}/qr-code` returning PNG payload for intake URL. |  |  |
| TASK-024 | Implement `RVS.API/Controllers/ServiceRequestsExportController.cs` and `RVS.API/Services/ExportService.cs` to stream RFC 4180-compliant CSV export for filtered requests. |  |  |
| TASK-025 | Implement `RVS.API/Services/SftpExportService.cs` scheduled push and on-demand push-now path; read private keys from Key Vault reference in `TenantConfig`. |  |  |
| TASK-026 | Add tests in `RVS.API.Tests/Export/` for CSV escaping, schedule-triggered export, key retrieval failure handling, and SFTP retry behavior. |  |  |

### Implementation Phase 5

- **GOAL-005**: Deliver Blazor WebAssembly user surfaces for intake, status, and dealer workflows (Week 7).
- **Completion Criteria**: Mobile-first intake and dealer queue flows operate against API end-to-end with role-appropriate UI and validation.

| Task | Description | Completed | Date |
| -------- | ----------- | --------- | ---- |
| TASK-027 | Implement intake pages/components in `RVS.BlazorWASM/Pages/Intake/` for contact, VIN, wizard prompts, speech input, attachment upload progress, review, and submit. |  |  |
| TASK-028 | Implement JavaScript interop wrappers in `RVS.BlazorWASM/Services/Interop/` for VIN camera scan (`BarcodeDetector` with fallback) and speech-to-text capture. |  |  |
| TASK-029 | Implement customer status page in `RVS.BlazorWASM/Pages/Status/` consuming `GET /api/status/{token}` and handling invalid/expired tokens. |  |  |
| TASK-030 | Implement dealer queue and detail pages in `RVS.BlazorWASM/Pages/Dashboard/` with search/filter panel, status updates, Section 10A editor, and attachment preview actions. |  |  |
| TASK-031 | Implement tenant settings UI in `RVS.BlazorWASM/Pages/Settings/` for location config and QR code download action. |  |  |
| TASK-032 | Add component and API integration tests in `RVS.BlazorWASM.Tests/` for intake validation, fallback UX, and dashboard role-based rendering. |  |  |

### Implementation Phase 6

- **GOAL-006**: Complete hardening, deployment readiness, and launch verification (Week 8).
- **Completion Criteria**: CI/CD runs green, staging smoke tests pass, PRD MVP acceptance scenarios are verified, and production launch checklist is complete.

| Task | Description | Completed | Date |
| -------- | ----------- | --------- | ---- |
| TASK-033 | Create/update GitHub Actions workflows in `.github/workflows/` for build, test, publish artifacts, and staged deployment gates. |  |  |
| TASK-034 | Add infrastructure templates under `infra/` (Bicep or Terraform) for API host, Cosmos DB containers, Blob Storage, Key Vault, App Insights, and frontend hosting resources. |  |  |
| TASK-035 | Configure environment-specific CORS origins and secure defaults in `RVS.API/appsettings.*.json` and deployment variables. |  |  |
| TASK-036 | Execute performance and reliability smoke tests: intake throughput, attachment upload concurrency, search latency, and rate limiter correctness; record results in `Docs/Research/ASOT/`. |  |  |
| TASK-037 | Run end-to-end UAT checklist mapped to PRD user stories RVS-001 through RVS-018 and capture pass/fail in `Docs/Research/ASOT/RVS_MVP_UAT.md`. |  |  |
| TASK-038 | Publish launch runbook and rollback plan in `Docs/Research/ASOT/RVS_MVP_LAUNCH_RUNBOOK.md` with monitoring alerts and on-call triage steps. |  |  |

## 3. Alternatives

- **ALT-001**: Deliver all backend features before any frontend work. Rejected because delayed UI integration increases late-stage defect risk and extends stabilization.
- **ALT-002**: Implement SFTP export in Azure Functions during MVP. Rejected for timeline compression; in-process hosted service is faster for solo delivery, with abstraction retained for later migration.
- **ALT-003**: Skip telemetry and Key Vault until first customer. Rejected due to critical security/reliability gaps identified in ASOT assessments.
- **ALT-004**: Build custom RBAC persistence in app database. Rejected because Auth0 role/permission model already defines required tenancy and access behavior.

## 4. Dependencies

- **DEP-001**: Auth0 tenant/application configured for JWT bearer API audience and custom claims injection.
- **DEP-002**: Azure Cosmos DB account with required containers and partition keys.
- **DEP-003**: Azure Blob Storage account and private attachment container.
- **DEP-004**: Azure Key Vault with managed identity access for API host.
- **DEP-005**: Transactional email provider integration behind `INotificationService`.
- **DEP-006**: SFTP client dependency (e.g., `SSH.NET`) for export push.
- **DEP-007**: VIN decode external service availability (`vPIC`) and fallback logic.
- **DEP-008**: Frontend JS interop dependencies for barcode and speech APIs.

## 5. Files

- **FILE-001**: `RVS.API/Program.cs` — middleware pipeline, auth, rate limiting, telemetry, health endpoints.
- **FILE-002**: `RVS.API/Middleware/ExceptionHandlingMiddleware.cs` — centralized exception contract.
- **FILE-003**: `RVS.API/Middleware/TenantAccessGateMiddleware.cs` — tenant gate enforcement.
- **FILE-004**: `RVS.API/Controllers/IntakeController.cs` — anonymous intake orchestration endpoints.
- **FILE-005**: `RVS.API/Controllers/ServiceRequestsController.cs` — dealer CRUD/search/status endpoints.
- **FILE-006**: `RVS.API/Controllers/AttachmentsController.cs` — attachment SAS/read/delete endpoints.
- **FILE-007**: `RVS.API/Controllers/LocationsController.cs` — location management and QR endpoint.
- **FILE-008**: `RVS.API/Controllers/LookupsController.cs` — lookup retrieval endpoints.
- **FILE-009**: `RVS.API/Services/ServiceRequestService.cs` — core workflow orchestration.
- **FILE-010**: `RVS.API/Services/CategorizationService.cs` — category + technician summary generation.
- **FILE-011**: `RVS.API/Services/GlobalCustomerAcctService.cs` — magic-link lifecycle.
- **FILE-012**: `RVS.API/Services/CustomerProfileService.cs` — ownership/identity logic.
- **FILE-013**: `RVS.API/Services/AttachmentService.cs` — SAS and metadata handling.
- **FILE-014**: `RVS.API/Services/ExportService.cs` and `RVS.API/Services/SftpExportService.cs` — DMS exports.
- **FILE-015**: `RVS.Domain/Entities/` — entity definitions and embedded models.
- **FILE-016**: `RVS.Domain/DTOs/` — request/response contracts.
- **FILE-017**: `RVS.Domain/Interfaces/` — repository and service interfaces.
- **FILE-018**: `RVS.Infra.AzCosmosRepository/Repositories/` — Cosmos implementations and query paths.
- **FILE-019**: `RVS.Infra.AzBlobRepository/BlobRepository.cs` — storage operations and ACL assumptions.
- **FILE-020**: `RVS.BlazorWASM/Pages/` and `RVS.BlazorWASM/Services/` — intake, status, and dashboard surfaces.
- **FILE-021**: `.github/workflows/` — CI/CD workflows.
- **FILE-022**: `infra/` — IaC definitions.

## 6. Testing

- **TEST-001**: Intake orchestration integration test validates creation/update of `GlobalCustomerAcct`, `CustomerProfile`, `ServiceRequest`, and `AssetLedgerEntry` in one submission.
- **TEST-002**: VIN transfer scenario test validates previous owner asset record transitions to inactive and new owner becomes active.
- **TEST-003**: AuthN/AuthZ tests validate `401`, `403`, and location-scoped access restrictions for each staff role.
- **TEST-004**: Anonymous endpoint rate-limit tests validate `10/hour` intake and `30/hour` status limits with `Retry-After`.
- **TEST-005**: Attachment tests validate file type/size enforcement, direct SAS upload flow, and `404` behavior for missing blobs.
- **TEST-006**: Search endpoint tests validate filtering combinations, pagination cap (`pageSize <= 100`), and dangerous-character input rejection.
- **TEST-007**: Status transition tests validate only allowed state changes are accepted.
- **TEST-008**: CSV export tests validate header schema, RFC 4180 escaping, and semicolon formatting for parts lists.
- **TEST-009**: SFTP tests validate key retrieval from Key Vault, successful push, retry, and alert logging on failure.
- **TEST-010**: Frontend tests validate intake mobile flow, speech/VIN fallback UX, dashboard role-based rendering, and status link error states.
- **TEST-011**: Observability smoke tests validate App Insights traces include tenant context dimensions.
- **TEST-012**: Final UAT validates PRD stories RVS-001 through RVS-018 with pass/fail evidence.

## 7. Risks & Assumptions

- **RISK-001**: Underestimated complexity in Blazor WASM camera/speech interop could delay Week 7 completion.
- **RISK-002**: SFTP endpoint variability across dealers may require additional compatibility work beyond MVP assumptions.
- **RISK-003**: Public external VIN service latency/outages may reduce prefill reliability.
- **RISK-004**: Search/index tuning may require additional Cosmos index updates under real workload.
- **RISK-005**: Solo developer context switching across backend, frontend, and infra may reduce net throughput.
- **ASSUMPTION-001**: Existing repository already contains baseline controllers/services/entities that can be extended instead of built from scratch.
- **ASSUMPTION-002**: Auth0 tenant and claims action can be configured in parallel with development.
- **ASSUMPTION-003**: AI coding assistance is available for scaffold generation, tests, and repetitive DTO/repository tasks.
- **ASSUMPTION-004**: MVP launch can defer non-goal features (appointment scheduling, advanced routing, warranty integrations, native mobile apps).

## 8. Related Specifications / Further Reading

[Docs/Research/ASOT/RVS_PRD.md](Docs/Research/ASOT/RVS_PRD.md)
[Docs/Research/ASOT/RVS_Core_Architecture_Version3.md](Docs/Research/ASOT/RVS_Core_Architecture_Version3.md)
[Docs/Research/ASOT/RVS_Auth0_Identity_Version2.md](Docs/Research/ASOT/RVS_Auth0_Identity_Version2.md)
[Docs/Research/ASOT/RVS_Context.md](Docs/Research/ASOT/RVS_Context.md)
[Docs/Research/ASOT/RVS_SaaS_Architecture_Assessment.md](Docs/Research/ASOT/RVS_SaaS_Architecture_Assessment.md)
[Docs/Research/ASOT/RVS_Cloud_Arch_Assessment.md](Docs/Research/ASOT/RVS_Cloud_Arch_Assessment.md)