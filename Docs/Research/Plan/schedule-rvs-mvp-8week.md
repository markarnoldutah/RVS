---
goal: Week-by-Week Execution Calendar for RVS MVP
version: 1.0
date_created: 2026-03-18
last_updated: 2026-03-18
owner: Solo Developer (AI-assisted)
status: 'Planned'
tags: [schedule, calendar, milestone, daily-standup]
---

# RVS MVP 8-Week Execution Calendar

## Overview

This calendar translates [plan/feature-rvs-mvp-1.md](feature-rvs-mvp-1.md) phases into a day-by-day schedule with task assignments, time estimates (in developer hours), and risk/dependency checkpoints. A typical dev day is 67 billable hours (accounting for meetings, context switching, lunch). Red flags () indicate blocking dependencies that must be resolved immediately.

**Start Date:** Monday, March 25, 2026  
**Target Completion:** Friday, May 17, 2026 (Week 8 end)

---

## Week 1: Foundation (Mar 25 - Mar 29)  Phase 1a

**Phase Goal:** Security + middleware baseline complete.  
**Completion Criteria:** API starts, auth validates, tenant gate enforces, secrets in Key Vault.

| Day | Task | Subtasks | Est. Hours | Notes |
|-----|------|----------|-----------|-------|
| **Mon 3/25** | TASK-001: Program.cs middleware pipeline | Register exception handler, auth, CORS, rate limiters, health checks | 6 | Blockers: Auth0 config must exist ( verify DEP-001 in parallel). Start with exception handler and CORS to unblock other work. |
| **Tue 3/26** | TASK-002: ClaimsService implementation | GetTenantIdOrThrow(), GetUserIdOrThrow(), GetRoles(), GetLocationIds() + unit tests | 5 | Blocks all downstream controllers. Create mockable test doubles for JWT claim patterns. |
| **Wed 3/27** | TASK-003: TenantAccessGateMiddleware | Allowlist paths, 403 response contract, integration with TenantConfig gate checks | 5 | Light lift if TenantConfig query is defined. Blockers: Cosmos 	enantConfigs container must exist ( DEP-002). |
| **Thu 3/28** | TASK-004: Key Vault integration + TASK-005: App Insights wiring | Add AddAzureKeyVault() in Program.cs; move secrets; telemetry enricher for tenant context | 6 | Blockers: Key Vault resource + managed identity RBAC must be set up ( DEP-004). Stub telemetry trace if KV unavailable. |
| **Fri 3/29** | TASK-006: Auth/gate/rate limit tests | Unit tests for 401, 403, 429 behavior; rate limit policy enforcement | 5 | Integration tests against Cosmos Emulator on local. Verify baseline: dotnet run  GET /health returns 200. |

**End-of-Week Gate:** Can start the API successfully, /health is 200, invalid auth  401, missing tenantId  403. All tasks COMPLETE or plan is at risk.

---

## Week 2: Foundation Cont. + Intake Start (Apr 1 - Apr 5)  Phase 1b + Phase 2a

**Phase Goals:** Finish middleware/observability; start intake endpoint + service layer.  
**Completion Criteria:** Intake endpoint accepts POST; orchestration skeleton is callable.

| Day | Task | Subtasks | Est. Hours | Notes |
|-----|------|----------|-----------|-------|
| **Mon 4/1** | Intake codegen pass | AI-generate skeleton repository interfaces, entity mappings, DTO stubs from architecture doc | 3 | Use copilot-instructions.md Service, DTO, Mapper patterns. Place in commits for review, not merged until tested. |
| **Tue 4/2** | TASK-007: IntakeController.cs | POST /api/intake/{slug}/service-requests, status endpoint, attachment upload URL endpoint | 6 | Stub service calls. Orchestration logic is in next tasks. Validate input: dangerous chars, file type/size limits. |
| **Wed 4/3** | TASK-008: ServiceRequestService orchestration | Steps 1-6 outline (stub out service calls to downstream services). Intake payload  ServiceRequest creation. | 6 | Core flow skeleton. Integration test for happy path (will be red until Week 3). |
| **Thu 4/4** | TASK-010: GlobalCustomerAcctService token logic | Token generation, rotation, linked profiles update; single-partition lookup by email. | 5 | Blocks magic-link status page. Unit tests for token format and expiry. |
| **Fri 4/5** | Integration test scaffolding | Create RVS.API.Tests/Intake/ folder structure, base fixture for Cosmos seeding, test base class | 4 | Weekend prep: stack of integration tests to be written Mon/Tue Week 3. |

**End-of-Week Gate:** POST /api/intake/{slug}/service-requests accepts valid payloads without error (no persistence yet). TASK-007, TASK-008, TASK-010 are code-complete. Tests are red/incomplete but infrastructure is ready.

---

## Week 3: Intake Orchestration (Apr 8 - Apr 12)  Phase 2b

**Phase Goal:** Full intake workflow persists data correctly (identity, profile, SR, ledger).  
**Completion Criteria:** E2E intake submission test passes for first-time customer, returning customer, and VIN transfer.

| Day | Task | Subtasks | Est. Hours | Notes |
|-----|------|----------|-----------|-------|
| **Mon 4/8** | TASK-009: CustomerProfileService | Tenant-scoped profile resolution, VIN ownership transfer, reactivation logic | 6 | Blocks TASK-008 completion. Unit tests: transfer inactive to active, reactivate sold asset. |
|  | TASK-012: VinDecodeService + vPIC client | Decode call to NHTSA vPIC, fallback on network error, validation | 5 | External dependency ( DEP-007). Implement fallback gracefully. No blocking issue if vPIC is slow; timeouts are acceptable. |
| **Tue 4/9** | TASK-011: CategorizationService MVP | Rule-based keyword matching for issue categories, technician summary gen | 6 | Stub Azure OpenAI calls (will be real in Phase 2+). Test with reference category set. |
| **Wed 4/10** | TASK-008 complete + integration tests | Complete ServiceRequestService orchestration; wire all dependencies | 7 | Blocks Week 4. Link CustomerProfileService, GlobalCustomerAcctService, CategorizationService. Cosmos writes must happen in order. |
| **Thu 4/11** | TASK-013: Intake integration tests | First-time customer, returning customer, VIN transfer, slug not found, partial file upload failure | 6 | Run against Cosmos Emulator. Should achieve 80% pass rate on happy path. Accept some fixture flakiness. |
| **Fri 4/12** | Smoke test + documentation | Intake scenario documentation, curl examples, Postman collection for manual testing | 3 | Prepare for Monday Phase 3 handoff. Document known issues if any. |

**End-of-Week Gate:** Intake E2E tests pass (green) for first-time + returning customer. VIN transfer test passes. Phase 2 COMPLETE.

---

## Week 4: Dealer Dashboard Backend Pt. 1 (Apr 15 - Apr 19)  Phase 3a

**Phase Goal:** Service request search, read, and update endpoints for dealer staff.  
**Completion Criteria:** GET detail, POST search work with filters; permissions enforced.

| Day | Task | Subtasks | Est. Hours | Notes |
|-----|------|----------|-----------|-------|
| **Mon 4/15** | TASK-014a: ServiceRequestsController skeleton | GET /{id}, POST search, PUT /{id}, DELETE /{id} endpoint stubs | 5 | Light lift; service calls are stubbed. Add role-based [Authorize] attributes now. |
| **Tue 4/16** | TASK-015: ServiceRequestSearchService | Implement search logic: filter by status, category, location, date range, keyword; apply pagination | 7 | Blocks TASK-014. Index all required filter paths in Cosmos. Test combinations. |
| **Wed 4/17** | TASK-014b: Complete ServiceRequestsController | Wire SearchService, UpdateService, DeleteService into controller actions; add validation | 6 | By end of day, all 4 actions should call their respective services. Test with Postman. |
| **Thu 4/18** | TASK-016: Status transition enforcement | Create RVS.API/DomainRules/StatusTransitions.cs with state machine, integrate into UpdateAsync | 5 | Light lift. Validate transitions in ServiceRequestService before Cosmos write. Unit test all transitions. |
| **Fri 4/19** | Permission + search tests | TASK-020 partial: tests for role restrictions (advisor vs manager vs tech), invalid transitions, pagination cap | 6 | Auth isolation tests, dangerous character validation, pagination > 100 rejected. Smoke test search performance. |

**End-of-Week Gate:** GET /api/service-requests/{id} returns 200 with full detail. POST /api/service-requests/search returns paged results. PUT status transitions are validated. Permission tests pass.

---

## Week 5: Dealer Dashboard Backend Pt. 2 (Apr 22 - Apr 26)  Phase 3b

**Phase Goal:** Attachments, Section 10A, and notifications.  
**Completion Criteria:** Attachment SAS URLs work; Section 10A updates persist; notifications send on status change.

| Day | Task | Subtasks | Est. Hours | Notes |
|-----|------|----------|-----------|-------|
| **Mon 4/22** | TASK-018: AttachmentsController + AttachmentService | GET (SAS read), DELETE (remove from SR and blob), missing blob handling  404 | 6 | SAS tokens: 1-hour expiry, scoped to attachment blob. Blob ACL must be Private. |
| **Tue 4/23** | TASK-017: Section 10A endpoint/service | PUT /{id}/service-event to update ComponentType, FailureMode, RepairAction, PartsUsed, LaborHours | 5 | Permission gate: service-requests:update-service-event. Advisor + tech have different allowed fields (stub for now, exact perms in Phase 2+). |
| **Wed 4/24** | TASK-019: Notification abstraction + stub impl | Create INotificationService, stub implementation logs to console, wire email provider stub | 4 | Blocks status-change notifications. Implement concrete SendGrid provider later. Test notification on status  InProgress, Completed. |
| **Thu 4/25** | Integration tests for attachments + Section 10A | TASK-020 partial: test attachment SAS generation, verify 404 on missing blob, test Section 10A update and persistence | 6 | Run against blob emulator. Verify attachment metadata is stored in SR document. |
| **Fri 4/26** | End-to-end dashboard workflow | Create scenario test: search  detail  update status  add note  update Section 10A  download attachment | 5 | Demonstrates full dealer workflow end-to-end. Document any gaps. |

**End-of-Week Gate:** GET /api/service-requests/{id}/attachments/{id}/sas returns valid SAS URI. PUT /api/service-requests/{id}/service-event updates fields. Status updates trigger notifications. Phase 3 COMPLETE.

---

## Week 6: Tenant Config + Export (Apr 29 - May 3)  Phase 4

**Phase Goal:** Location management, lookup sets, QR codes, CSV export, SFTP push.  
**Completion Criteria:** Admins can create locations, download QR; export to CSV works; SFTP push is callable.

| Day | Task | Subtasks | Est. Hours | Notes |
|-----|------|----------|-----------|-------|
| **Mon 4/29** | TASK-021: LocationsController + LocationService | POST create, PUT update, GET list; slug management + slug lookup sync | 6 | Immutable slug after creation (documented). Update slug lookup on location changes. Reference architecture Section 2.2. |
| **Tue 4/30** | TASK-022: LookupsController + LookupService | GET lookups by category; tenant override logic (global vs tenant-specific) | 4 | Light endpoint. Mostly queries against lookupSets container. Seed reference data. |
| **Wed 5/1** | TASK-023: QR code generation | Endpoint GET /api/locations/{id}/qr-code returns PNG image of intake URL | 4 | Library choice: QRCoder or similar. Simple integration. Test manual download from dashboard. |
| **Thu 5/2** | TASK-024: CSV export service | ExportService streaming RFC 4180-compliant CSV with headers, field escaping, filtered by tenant + date range | 6 | Blockers: All prior SR endpoints must be stable. Test file generation locally. Include parts used (semicolon-separated). |
| **Fri 5/3** | TASK-025: SFTP export service + TASK-026: export tests | SftpExportService reads private key from Key Vault, pushes CSV to dealer's SFTP endpoint; schedule + on-demand | 6 | Blockers: Key Vault secret must exist ( DEP-006, DEP-004). SSH.NET dependency. Stub SFTP endpoint for testing. Log failures with tenant context. |

**End-of-Week Gate:** GET /api/locations/{id}/qr-code returns PNG. POST /api/service-requests/export streams valid CSV. SFTP export is callable (may fail on blocked endpoint, but code is complete). Phase 4 COMPLETE.

---

## Week 7: Blazor WebAssembly Frontend (May 6 - May 10)  Phase 5

**Phase Goal:** Intake form, status page, and dealer dashboard UI.  
**Completion Criteria:** Intake form submits succeeds; status page loads; dashboard queue renders.

| Day | Task | Subtasks | Est. Hours | Notes |
|-----|------|----------|-----------|-------|
| **Mon 5/6** | TASK-027: Intake page scaffolding | Pages: Contact, VIN, Wizard, Speech, Attachments, Review, Confirmation; Blazor component structure | 6 | Layout: mobile-first, PWA-quality. Stub API calls (will wire Week 8). Use Blazor Fluent UI components. |
| **Tue 5/7** | TASK-028: JS interop wrappers | Barcode scanner (BarcodeDetector + zxing-js fallback), Web Speech API wrapper for speech-to-text | 6 | Browser API compatibility Matrix. Build graceful fallbacks. Test on mobile device. Document API support. |
| **Wed 5/8** | TASK-029: Status page + TASK-030: Dashboard pages | Status page consuming /api/status/{token}; dashboard queue, detail, search panel, settings | 7 | Heaviest frontend day. Render role-appropriate UI (tech vs advisor vs manager). Auth0 integration for dashboard. |
| **Thu 5/9** | TASK-031: Settings UI | Location config form, QR code download button, lookup category list editor (if time) | 4 | Light component, mostly CRUD UI. Reuse form patterns from intake. |
| **Fri 5/10** | TASK-032: Frontend tests + wiring | Component unit tests, API integration tests, fallback UX verification, role-based rendering tests | 6 | High test velocity. Verify intake form validation, status link error states, dashboard filtering. Smoke test all flows. |

**End-of-Week Gate:** Intake form on mobile submits successfully. Status page with magic link displays SRs. Dashboard queue renders and searches. Role-based UI hides tech-only fields from advisors. Phase 5 COMPLETE.

---

## Week 8: Hardening + Launch (May 13 - May 17)  Phase 6

**Phase Goal:** CI/CD, IaC, UAT, launch readiness.  
**Completion Criteria:** Pipeline is green; smoke tests pass; launch runbook is ready.

| Day | Task | Subtasks | Est. Hours | Notes |
|-----|------|----------|-----------|-------|
| **Mon 5/13** | TASK-033: GitHub Actions CI/CD | Build, test, artifact publish, deploy-staging, manual approval gate, deploy-prod jobs | 6 | Use existing .github/workflows patterns from similar projects. Test pipeline locally first (act tool). |
| **Tue 5/14** | TASK-034: Bicep IaC templates | App Service, Cosmos (containers + indexing), Blob Storage, Key Vault, App Insights, Static Web Apps | 7 | Heavy lift. Create infra/main.bicep + modular files. Document parameter files for staging/prod. Reference Azure Well-Architected Framework. |
| **Wed 5/15** | TASK-035: Environment config + TASK-036: Smoke tests | CORS origins per env, secure defaults, intake throughput test, search latency test, rate limiter correctness | 6 | Record baseline performance metrics. Document any bottlenecks. Create Docs/Research/ASOT/RVS_MVP_PERF_BASELINE.md. |
| **Thu 5/16** | TASK-037: End-to-end UAT | Run PRD stories RVS-001 through RVS-018 against staging env; capture evidence (screenshots, video, logs) | 6 | Checklist-driven UAT. Create Docs/Research/ASOT/RVS_MVP_UAT.md with pass/fail matrix and ticket links. |
| **Fri 5/17** | TASK-038: Launch runbook + handoff | Runbook for prod deployment, rollback procedure, alert config, on-call escalation, post-launch checklist | 5 | Create Docs/Research/ASOT/RVS_MVP_LAUNCH_RUNBOOK.md and Docs/Research/ASOT/RVS_MVP_ROLLBACK_PLAN.md. Schedule launch meeting. |

**End-of-Week Gate:** CI/CD pipeline passes all checks. Smoke tests green. UAT checklist is 100% signed off. Launch runbook is complete and reviewed. ALL PHASES COMPLETE. Ready for production deployment.

---

## Risk Log & Mitigation

| Risk | Identified | Mitigation | Escalation |
|------|----------|-----------|-----------|
| Auth0 config not ready ( Week 1) | DEP-001 | Start Day 1 in parallel; stub JWT claims in unit test doubles | If missing by Tue 3/26, pivot to mock claims and mock auth for initial tests |
| Cosmos containers missing ( Week 1) | DEP-002 | Pre-create containers before Week 1; provide connection string | Delay Phase 2 integration tests to Week 3 if needed; test locally first |
| Key Vault access ( Week 1) | DEP-004 | Set up managed identity + RBAC before start; test locally | Use local Key Vault emulator or stub provider if Azure KV unavailable |
| Barcode/speech interop complexity | Week 7 | Allocate 2 days; use proven libraries; test on device early (Week 6 if possible) | Defer advanced integrations (real-time barcode), keep fallback simple |
| SFTP compatibility | Week 6 | Build against SSH.NET; document auth methods (key + password); test with mock SFTP server | If dealer SFTP unusual, defer advanced features to Phase 2 |
| VIN service latency | Week 3 | Implement timeout (2s), cache results, graceful fallback | If vPIC down, use rule-based defaults; document as Phase 2 enhancement |
| Index tuning | Week 4 | Monitor query RU cost; add indexes proactively if needed | If search throughput is poor (>50 RU/query), add composite indexes dynamically |
| Context switching | Ongoing | Time-box by task, minimize context switches within a day | If velocity drops >20% below estimate, negotiate scope reduction with stakeholders |

---

## Daily Standup Template

Use this template for daily progress updates (async or sync):

`
**Date:** [Day, Date]
**Week:** [Week Number]
**Phase:** [Phase Name]

 **Completed Today:**
- [TASK-XXX]: [Brief status]
- [File created/modified]: [Artifact]

 **In Progress:**
- [TASK-XXX]: [Blocker or progress?]

 **Blockers:**
- [DEP-XXX]: [Issue and mitigation]

 **Metrics:**
- Lines of code: [+X]
- Test coverage: [X%]
- Time spent: [X hours]

 **Tomorrow:**
- [TASK-XXX]: [Plan]
'''

---

## Milestone Verification Checklist

| Milestone | Gate | Verified By |
|-----------|------|-------------|
| **End Week 1** | API starts, /health = 200, auth + gate enforce, Key Vault integration works | QA + team review |
| **End Week 2** | Intake endpoint callable, orchestration skeleton compiled | Code review + test pass |
| **End Week 3** | E2E intake test passes (first-time, returning, VIN transfer) | Integration test suite |
| **End Week 4** | GET detail + search endpoints work with permissions | Postman smoke test |
| **End Week 5** | Attachments, Section 10A, notifications integrated | E2E scenario test |
| **End Week 6** | Location mgmt, QR generation, CSV export, SFTP callable | Manual testing + logs |
| **End Week 7** | Intake form, status page, dashboard UI render correctly | Browser + mobile device test |
| **End Week 8** | CI/CD green, UAT 100%, launch runbook approved | Prod deployment sign-off |

---

## Weekly Sync Topics

Suggest these discussion points in weekly standups:

- **Monday:** This week's blockers and dependencies. Any on-call/urgent issues from previous week.
- **Wednesday:** Midweek checkpoint. Course-correct if tasks are tracking behind (slide scope forward if needed).
- **Friday:** Week summary, metrics (lines added, test case count), next week's preview, document and demo completed work.

---

## Success Criteria Summary

 **Week 1 Done**  API is secure, observable, and ready for feature development.  
 **Week 3 Done**  Customers can submit intake; data persists correctly across identity, profile, SR, ledger.  
 **Week 5 Done**  Dealer staff can read, update, and manage service requests end-to-end on the backend.  
 **Week 7 Done**  Users interact with intake form and dashboard UI without errors.  
 **Week 8 Done**  Pipeline is automated, infrastructure is codified, launch is scheduled.

---

## References

- Main implementation plan: [plan/feature-rvs-mvp-1.md](feature-rvs-mvp-1.md)
- PRD: [Docs/Research/ASOT/RVS_PRD.md](../Docs/Research/ASOT/RVS_PRD.md)
- Architecture: [Docs/Research/ASOT/RVS_Core_Architecture_Version3.md](../Docs/Research/ASOT/RVS_Core_Architecture_Version3.md)
- Coding standards: [.github/copilot-instructions.md](../.github/copilot-instructions.md)
