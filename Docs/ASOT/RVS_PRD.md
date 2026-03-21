# PRD: RV Service Flow (RVS) — MVP

## 1. Product overview

### 1.1 Document title and version

- PRD: RV Service Flow (RVS) — MVP
- Version: 1.1
- Date: March 19, 2026

### 1.2 Product summary

RV Service Flow (RVS) is a multi-tenant SaaS platform that digitizes the service intake workflow at RV dealerships and independent RV repair shops. The core problem is that service departments rely on phone calls, emails, and manual notes to collect repair information before an RV arrives, resulting in incomplete diagnostics, wasted technician time, and extended Repair Event Cycle Times (RECT).

The MVP delivers three integrated surfaces: a frictionless, mobile-first customer intake portal (`Cust_Intake`) that captures structured repair information (including photos, video, VIN, and an AI-guided issue wizard) before the RV arrives; a manager and advisor dashboard (`Mngr_Desktop`) where service advisors and managers manage, triage, and act on incoming service requests from a desktop browser; and a technician mobile app (`Tech_Mobile`) purpose-built for service bay use with offline-first job access, native barcode scanning, voice notes, and a 3–5 second interaction target. Customers are never required to create an account — a magic-link token gives them passive status visibility across all their dealerships.

The platform is designed as the intake layer that sits in front of existing Dealer Management Systems (DMS), not a DMS replacement. A simple SFTP-based DMS export makes it easy for dealers to pull structured service request data into their existing workflow on day one.

---

## 2. Goals

### 2.1 Business goals

- Reach an initial paying customer base of 10–20 RV dealerships to validate the subscription model.
- Target $50K ARR milestone through a $199–$499/month tiered subscription.
- Establish a structured service event dataset (Section 10A data moat) from day one, accumulating proprietary cross-dealer asset intelligence that increases acquisition value.
- Position RVS as the intake layer that integrates with existing DMS tools, minimizing displacement risk and sales resistance.
- Recruit 5 design partner dealerships before general availability to validate flow and co-develop the product.

### 2.2 User goals

- **Customers:** Submit a service request from a phone in under 3 minutes without creating an account; describe issues using speech-to-text and photo/video; check request status at any time via a magic link.
- **Service advisors:** Replace the phone intake process with a structured queue; see AI-generated technician summaries for every request; update request status and communicate with customers through the dashboard.
- **Technicians:** See an organized, AI-categorized queue with pre-diagnosis information and photos before the RV arrives; access job data offline in service bays with poor connectivity; log repair actions and parts used via a glove-friendly native app in 3–5 seconds per job completion.
- **Dealership managers and owners:** Monitor service request volume, status distribution, and technician workload across all locations from one dashboard.

### 2.3 Non-goals

- Replacement of existing DMS systems (Lightspeed, IDS Astra, EverLogic, Motility). RVS sits in front of them.
- Service appointment scheduling and bay assignment (Phase 2).
- Technician skill routing and assignment algorithms (Phase 3).
- Parts ordering integration or backorder tracking (Phase 4).
- Customer Auth0 accounts and full customer login (Phase 2+); the MVP uses anonymous intake plus magic-link.
- Warranty claim processing or warranty data lookups.
- OEM or manufacturer data integrations.
- Customer-facing native mobile apps (iOS/Android app store distribution); customer intake is browser-based (Blazor WASM) requiring zero install. The `Tech_Mobile` technician app is a MAUI Blazor Hybrid native app distributed to employer-provisioned devices — not a consumer app store release.

---

## 3. User personas

### 3.1 Key user types

- RV owner / customer (unauthenticated)
- Service advisor (authenticated dealer staff)
- Technician (authenticated dealer staff)
- Service manager (authenticated dealer staff)
- Dealership owner / corporate admin (authenticated dealer staff)
- Platform administrator (RVS internal operations)

### 3.2 Basic persona details

- **Alex (RV Owner):** Owns a 2023 Grand Design fifth-wheel. Has limited patience for phone tag. Wants to describe the problem once, upload a short video of the noise, and know when his rig is ready without calling the dealer.
- **Maria (Service Advisor):** Handles 15–25 intake calls per day at a mid-size dealership. Spends significant time collecting incomplete repair descriptions and managing a paper waitlist. Wants a structured queue that eliminates repeat callbacks.
- **Jordan (Technician):** Diagnoses 5–10 units per week. Arrives at units cold with minimal pre-diagnosis context. Wants to know the issue category, component, and see customer photos before opening the bay door.
- **Sam (Service Manager):** Manages a 10-technician department across one location. Needs visibility into queue depth, status distribution, and which requests are stalled.
- **Chris (Dealership Owner / Corporate Admin):** Owns a 3-location dealer group. Wants cross-location service visibility and the ability to export intake data to their existing DMS.
- **Pat (Platform Admin):** RVS internal operator. Provisions new tenants, manages global lookup sets, and monitors platform health.

### 3.3 Role-based access

- **`platform:admin`:** Global cross-tenant access. Manages all tenant configurations, access gates, and platform-wide lookup sets. Not scoped to any dealership.
- **`dealer:corporate-admin`:** Full access across all locations within the corporation. Manages users, settings, all service requests, and analytics. Equivalent to owner for multi-location groups.
- **`dealer:owner`:** Same as `corporate-admin` for single-location dealers. Full control of their corporation's config, users, analytics, and all service requests.
- **`dealer:regional-manager`:** Cross-location visibility limited to locations matching their assigned `regionTag` claim. Can view and manage SRs across their geographic region.
- **`dealer:manager`:** Location-scoped. Full SR management, analytics, and location settings for their specific service site.
- **`dealer:advisor`:** Location-scoped. Creates, searches, and updates service requests. Primary daily user of the dealer dashboard.
- **`dealer:technician`:** Location-scoped. Views assigned service requests and updates Section 10A fields (repair action, parts used, labor hours). Cannot modify status or customer data.
- **`dealer:readonly`:** Location-scoped. Read-only access to service requests and analytics (e.g., accounting, external auditors).
- **Customer (anonymous):** No Auth0 account. Accesses intake form via direct URL, QR code, or dealer deep link. Accesses status page via magic-link token.

---

## 4. Functional requirements

- **FR-001: Multi-tenant, multi-location data isolation** (Priority: Critical)
  - Each dealer corporation is a separate tenant. The Auth0 Organization `org_id` serves as the `tenantId` partition key in Cosmos DB.
  - A corporation may have one or many physical service locations. A single-location independent has exactly one location.
  - All data reads and writes are scoped by `tenantId`. Cross-tenant data access is impossible by design.
  - Location-scoped roles filter within the tenant partition; they do not cross tenant boundaries.

- **FR-002: Anonymous customer intake** (Priority: Critical)
  - The intake form is accessible at a location-specific URL: `https://app.rvserviceflow.com/intake/{locationSlug}`.
  - No customer account or login is required to submit a service request.
  - The form collects: first name, last name, email, phone, VIN (manual entry or camera scan), make, model, year, issue description (text or speech-to-text), photo/video attachments, urgency level (routine, urgent, emergency), and full-time or part-time RV use.
  - On submission, the API automatically creates or updates a tenant-scoped `CustomerProfile` and a cross-dealer `GlobalCustomerAcct` (resolved by email).
  - The customer receives a confirmation email containing their magic-link status URL.
  - After submission, the customer is prompted (not required) to create a full profile for easier future submissions.

- **FR-003: VIN scan and lookup** (Priority: High)
  - The intake form provides a VIN camera scanner that uses the device camera to capture and parse a VIN from a photo.
  - Captured VIN is decoded to pre-populate make, manufacturer, model year, and asset details.
  - Manual VIN entry is available as a fallback.
  - The decoded VIN is stored in the structured `AssetInfoEmbedded.AssetId` field as `RV:{vin}`.

- **FR-004: AI-guided issue wizard** (Priority: High)
  - After the customer selects a top-level issue category (e.g., Slide System, Electrical, Plumbing, HVAC), the wizard presents contextual follow-up questions specific to that category.
  - Examples: for Refrigerator → ask absorption or residential type, error codes visible, shore power connected; for Slide-out → which slide number, manual override attempted; for Generator → runtime hours, last service date.
  - Follow-up answers are captured as structured `ServiceEventEmbedded` fields alongside the free-text description.

- **FR-005: Speech-to-text issue description** (Priority: High)
  - The issue description field supports voice input via the browser's Web Speech API (or a native device microphone prompt on mobile).
  - After recording, AI cleans up and reformats the raw transcript into a coherent description.
  - The customer reviews and edits the AI-cleaned description before submission.

- **FR-006: AI issue categorization and technician summary** (Priority: High)
  - On intake submission, the API runs rule-based issue categorization (MVP) against the issue description and AI wizard answers to assign an `IssueCategory` and `ComponentType`.
  - The API generates a structured, technician-ready summary that includes: issue category, component, customer-reported symptoms, wizard-captured structured fields, and attachment count.
  - Both `IssueCategory` and `TechnicianSummary` are written to the `ServiceRequest` document and surfaced on the dealer dashboard.
  - The categorization service interface (`ICategorizationService`) is AI-ready: the MVP implementation uses rule-based keyword matching; an LLM-backed implementation can be swapped in behind the same interface without changing the API.

- **FR-007: Photo and video upload** (Priority: High)
  - The intake form accepts up to 10 file attachments per service request.
  - Accepted file types: `.jpg`, `.jpeg`, `.png`, `.mp4` (configurable per location via `IntakeFormConfigEmbedded`).
  - Maximum file size: 25 MB per file (configurable per location).
  - Files are uploaded directly to Azure Blob Storage in a tenant-scoped container path: `{tenantId}/{serviceRequestId}/{attachmentId}_{filename}`.
  - Attachment metadata is embedded within the `ServiceRequest` document as `Attachments` list.
  - Dealer dashboard photo/video viewing uses time-limited SAS URLs (1-hour expiry) generated on demand.

- **FR-008: Magic-link customer status page** (Priority: High)
  - Each customer receives a magic-link token in their submission confirmation email, embedded in a status URL: `https://app.rvserviceflow.com/status/{token}`.
  - The token is stored on the `GlobalCustomerAcct` with an expiry. Token format encodes an email-hash prefix so the lookup is a single-partition point read (no cross-partition scan).
  - The status page shows all active service requests for that customer across all dealerships where they have submitted.
  - Each request shows: location name, status, issue summary, last updated date.
  - Expired or invalid tokens return a 404 with a "request a new link" prompt.
  - The status endpoint is `[AllowAnonymous]` and rate-limited per IP.

- **FR-009: Dealer service request dashboard** (Priority: Critical)
  - Authenticated dealer staff access a dashboard scoped to their location (or all locations for corporate/owner roles).
  - The dashboard displays: service request queue with status, customer name, VIN/asset, issue category, technician summary, submission date, attachment count.
  - Supported actions: search and filter (by status, category, date range, location), view detail, update status (`New` → `InProgress` → `Completed` / `Cancelled`), add advisor notes, view/download photo and video attachments, delete a service request.
  - The detail view renders: all structured intake fields, embedded customer snapshot, AI-generated technician summary, structured service event fields, and attachment previews.

- **FR-010: Section 10A structured service event fields** (Priority: High)
  - Each service request embeds a `ServiceEventEmbedded` document for structured repair data: `ComponentType`, `FailureMode`, `RepairAction`, `PartsUsed` (list), `LaborHours`, `ServiceDateUtc`.
  - Technicians can update Section 10A fields via the dashboard (requires `service-requests:update-service-event` permission).
  - On intake submission, `IssueCategory` and `ComponentType` are pre-populated from the AI categorization step. All other Section 10A fields default to null and are completed post-repair.
  - Each service request submission also writes an append-only `AssetLedgerEntry` to the `assetLedger` container, partitioned by `assetId`. This is the strategic data moat that accumulates cross-dealer service event history per asset.

- **FR-011: Dealership and location management** (Priority: High)
  - Corporate admins and owners can view and update their dealership (corporation) record: corporate name and logo.
  - Admins can create and manage physical locations: display name, address, service email, phone, logo, intake form config (accepted file types, max file size), region tag.
  - Each location has a globally unique slug used in intake URLs and QR codes.
  - A `slugLookup` container provides O(1) slug → `tenantId` + `locationId` resolution at intake time.

- **FR-012: QR code generation** (Priority: Medium)
  - The dealer dashboard exposes a `GET /api/locations/{id}/qr-code` endpoint that returns a QR code image encoding the intake URL for that location.
  - The QR code can be downloaded and printed for physical placement at the dealership service drive.

- **FR-013: SFTP-based DMS export** (Priority: High)
  - Dealers can trigger a structured CSV export of service requests for a given date range from the dashboard.
  - The export is also available on a scheduled daily basis via SFTP push to a dealer-configured SFTP endpoint.
  - Exported fields: service request ID, status, submission date, customer name, customer email, customer phone, VIN, make, model, year, issue category, component type, issue description, technician summary, repair action, parts used, labor hours, service date, location name, attachment count.
  - SFTP credentials (host, port, username, private key path, remote directory) are configured per tenant in `TenantConfig`.
  - The export is formatted as a standard CSV with a header row. A PDF summary per service request is also available as an alternative for DMS systems that accept document upload.

- **FR-014: Lookup sets** (Priority: High)
  - Issue categories, component types, and failure modes are managed as `LookupSet` documents in Cosmos DB, partitioned by `/category`.
  - The platform admin can manage global lookup sets. Dealer admins can customize lookup sets for their tenant.
  - Lookup data is returned to the intake form and dealer dashboard via `GET /api/lookups/{category}`.

- **FR-015: Tenant provisioning and access gate** (Priority: Critical)
  - New tenants are provisioned via the platform admin. Each tenant has a `TenantConfig` document and a `TenantAccessGateEmbedded` flag.
  - The `TenantAccessGateMiddleware` checks the access gate on every authenticated request. Disabled tenants receive a structured 403 response.
  - Tenant provisioning bootstraps an Auth0 Organization (or `app_metadata` entry in MVP), a Cosmos `TenantConfig`, a `Dealership`, and a default `Location`.

- **FR-016: Notifications** (Priority: Medium)
  - On service request submission, the customer receives a confirmation email containing: summary of the submitted request, magic-link status URL, and dealership contact info.
  - On status change (`InProgress`, `Completed`), the customer receives a status update notification via email.
  - Notification dispatch is abstracted behind `INotificationService`. The MVP implementation uses a simple transactional email provider (e.g., SendGrid or similar). SMS is a future enhancement.

- **FR-017: Rate limiting** (Priority: High)
  - Anonymous intake and status endpoints are rate-limited per IP address.
  - Default limits: 10 intake submissions per IP per hour, 30 status lookups per IP per hour.
  - Rate limiting uses ASP.NET Core's built-in `RateLimiter` with a sliding window policy.
  - Exceeded limits return `429 Too Many Requests` with a `Retry-After` header.

- **FR-018: Private labeling** (Priority: Medium)
  - The intake form renders with the dealership's logo (location-specific if configured, otherwise corporate).
  - The intake URL uses the dealer's location slug, creating a dealership-branded experience without requiring a custom domain in the MVP.
  - Future: custom domain support per dealership (Phase 2+).

---

## 5. User experience

### 5.1 Entry points and first-time user flow

- A customer receives the intake URL via the dealer's website, email, or a QR code displayed at the service drive.
- Scanning the QR code or clicking the link opens the intake form pre-scoped to that location (no dealer search needed).
- If no link or QR code is available, the customer can search for a participating dealer by name or ZIP code on the platform landing page.
- The form opens immediately — no sign-in, no account creation, no app installation.

### 5.2 Core experience

- **Step 1 — Contact info:** First name, last name, email, phone. If the customer has submitted before (email matched), their contact fields are pre-filled from their `GlobalCustomerAcct`.
- **Step 2 — VIN and vehicle details:** VIN field with camera scan button. On scan or manual entry, the VIN is decoded and make/model/year fields are pre-populated. If the customer's `GlobalCustomerAcct` has prior assets, their known VINs are offered as one-tap options.
- **Step 3 — Issue description:** A category selector (top-level lookup set) followed by the AI-guided wizard with contextual follow-up questions. The issue description text field supports speech-to-text input. After voice capture, AI cleans the transcript and the customer reviews the result before proceeding.
- **Step 4 — Photos and videos:** A file upload area that accepts camera capture (on mobile) or file selection. Progress indicators shown per file. Files exceeding the configured size limit are rejected with a clear error message before submission.
- **Step 5 — Urgency and usage:** A simple selector for urgency (routine, urgent, emergency) and whether the RV is a full-time or part-time residence.
- **Step 6 — Review and submit:** A summary card showing all entered data. The customer can edit any section before submitting. On submit, the API runs the 6-step orchestration (identity resolution, profile upsert, VIN ownership check, asset ledger write, categorization, notification dispatch) and returns a 201.
- **Confirmation:** The customer sees a confirmation screen with a summary and their magic-link status URL. They are offered (but not required) to save a profile for faster future submissions.

### 5.3 Advanced features and edge cases

- **VIN ownership transfer:** If a customer submits for a VIN that is currently active under a different customer profile in the same tenant, the previous owner's `AssetsOwnedEmbedded` record is set to `Inactive` and the new submission is recorded as the current owner. No manual intervention required.
- **Returning customer cross-dealer prefill:** A returning customer submitting to a new dealership for the first time receives their contact information and active asset VINs pre-filled from their `GlobalCustomerAcct`. No account login required.
- **Magic-link expiry:** If a customer tries to access a status page with an expired token, they are directed to re-submit their email address to receive a fresh magic link.
- **File upload failure:** If an attachment upload fails mid-submission, the customer is notified per-file with a retry option. The service request is not blocked on attachment failure — the text submission is independent of attachment upload.
- **No slug match:** If a customer navigates to an invalid or disabled location slug, a clear "This location is not found" message is returned with a link to search for participating dealers.
- **Rate limit exceeded:** Customers exceeding the IP rate limit see a friendly "Please wait and try again" message with the retry time.

### 5.4 UI/UX highlights

- Mobile-first layout with large tap targets throughout the intake form.
- VIN camera scan is the prominent default; manual entry is clearly accessible but secondary.
- Speech-to-text is surfaced as a microphone icon on the issue description field — no instruction needed.
- AI wizard follow-up questions load inline beneath the category selector, not on a new page or modal.
- The dealer dashboard is a desktop-primary layout with a responsive fallback for tablet/mobile use.
- Status badges on the dealer queue use consistent color coding: New (blue), In Progress (amber), Completed (green), Cancelled (grey).
- Attachment previews render inline on the service request detail page; video attachments autoplay muted on hover.
- The `Tech_Mobile` technician app is glove-friendly with extra-large tap targets throughout the job list and outcome entry form.
- QR/VIN scanning is the primary job access method in `Tech_Mobile` — one scan opens the assigned job immediately.
- Outcome entries in `Tech_Mobile` store locally when the device goes offline and sync automatically when connectivity returns; no data is lost in poor-signal bays.
- The `Mngr_Desktop` Service Board uses drag-and-drop status columns; status changes push to all connected sessions in real time via SignalR.

---

## 6. Narrative

Alex, a Grand Design owner, notices his slide-out making a grinding noise on a Thursday evening. He scans the QR code on a card the dealership handed him at his last purchase. The intake form opens on his phone instantly — no login, no app. He taps the camera icon, holds his phone up to the VIN plate, and the year, make, and model fill in automatically. He selects "Slide System" from the issue categories and answers three quick follow-up questions about which slide is affected and whether he tried the manual override. He taps the microphone, describes the grinding noise in his own words, and the AI cleans it up into a professional description that he confirms in two seconds. He records a 20-second video of the noise, uploads it, selects "Urgent," and hits submit. Thirty seconds later he has a confirmation email with a link to check his status anytime.

Maria, the service advisor, opens her dealer dashboard the next morning to find Alex's request already categorized as "Slide System — Hydraulic," with a technician summary that reads: "Customer reports grinding noise from rear slide-out during extension/retraction. Manual override not attempted. Video of noise included (1 attachment). Recommend hydraulic pump inspection prior to intake." She moves it to "In Progress," assigns a bay, and the customer gets an automatic email update. Jordan, the technician, walks up to Alex's unit with the symptom summary and video already on his tablet. He opens the bay knowing exactly what he needs to check.

---

## 7. Technical considerations

### 7.1 Integration points

- **Auth0:** Identity provider for all authenticated dealer staff. MVP uses `app_metadata` to inject `tenantId`, `locationIds`, and role claims via a Login Action. Migration path to Auth0 Organizations (multi-tenant B2B) is a configuration-only change — the ASP.NET Core `ClaimsService` reads the same custom claim namespace regardless of Auth0 plan tier.
- **Azure Cosmos DB:** Nine containers covering service requests, customer profiles, global customer identities, asset ledger, dealerships, locations, tenant configs, lookup sets, and slug lookup. Autoscale RU mode for high-throughput containers; manual 400 RU for low-volume config containers.
- **Azure Blob Storage:** Tenant-scoped, location-scoped path hierarchy for all photo and video attachments. SAS URL generation for time-limited read access.
- **Azure Table Storage:** Lightweight append-only store for analytics counters and audit log caching.
- **Email notifications:** Transactional email provider (e.g., SendGrid) injected behind `INotificationService`. Swap without changing the service layer.
- **SFTP / DMS export:** ASP.NET Core background service or Azure Function triggered on schedule or on demand. Uses `SSH.NET` (or equivalent) for SFTP push. Per-tenant SFTP configuration stored in `TenantConfig`.
- **AI categorization:** `ICategorizationService` abstraction. MVP: keyword-matching rule engine. AI upgrade path: Azure OpenAI or Azure AI Language API behind the same interface.
- **VIN decoding:** NHTSA vPIC API (`https://vpic.nhtsa.dot.gov/api/`) for VIN decode (free, public). No API key required. VIN camera scanning uses the browser's `BarcodeDetector` API or a lightweight JavaScript barcode library (e.g., `zxing-js`) for client-side decode before sending to the API.

### 7.2 Data storage and privacy

- All tenant data is partitioned by `tenantId` (Auth0 `org_id`). Cross-tenant queries are structurally impossible in the Cosmos DB access patterns used.
- Customer email addresses are normalized (lowercased, trimmed) before storage and used as the partition key for `GlobalCustomerAcct`. No plaintext passwords are ever stored — customers use anonymous intake only in the MVP.
- `GlobalCustomerAcct` cross-dealer records are partitioned by `/email`. Only the platform admin has cross-tenant read access to this container.
- Magic-link tokens are cryptographically random, time-limited (configurable expiry, default 30 days), and stored hashed if the implementation requires additional security hardening.
- Blob storage paths are tenant-scoped. SAS URLs are time-limited (1-hour expiry) and generated per request, never embedded permanently.
- SFTP private keys are stored in Azure Key Vault and referenced by name in `TenantConfig`. Keys are never written to the database directly.
- No PII is logged in application telemetry. Structured logging captures `tenantId`, `locationId`, and anonymized request identifiers only.

### 7.3 Scalability and performance

- Cosmos DB autoscale 400–4,000 RU on `serviceRequests` and `locations` containers handles burst intake volume without manual scaling.
- `slugLookup` container is a point-read-only container with `slug` as both the partition key and the document `id`, ensuring O(1) intake routing at < 1 RU per request.
- Magic-link token lookup encodes an email-hash prefix in the token, enabling single-partition point reads on `GlobalCustomerAcct` without a cross-partition query.
- All multi-location queries within a dealership (e.g., corporate admin viewing all SRs) are single-partition operations because all locations share the same `tenantId` partition key.
- Attachment uploads route directly to Azure Blob Storage from the client — the API issues a pre-authorized SAS upload URL so binary data never passes through the API tier.

### 7.4 Potential challenges

- **AI wizard content coverage:** The question trees for each issue category must be authored and maintained. Starting with the 8–10 most common RV issue categories (slide systems, electrical, plumbing, HVAC, generator, appliances, roof/seals, chassis) reduces the initial authoring burden.
- **VIN scanner accuracy on phone cameras:** Low-light or worn VIN plates reduce barcode scan reliability. The form must always offer a clean manual entry fallback with a clear affordance.
- **SFTP compatibility:** Dealer DMS SFTP configurations vary widely in authentication type (password vs. key), port, and directory structure. The initial implementation should support both key-based and password-based auth, with well-documented configuration.
- **Auth0 Organization migration:** Moving from `app_metadata` (MVP) to Auth0 Organizations (commercial launch) requires re-configuring existing user accounts. A migration script and Auth0 Login Action update handles the transition with zero downtime.
- **Magic-link token abuse:** Anonymous endpoints that validate tokens require IP rate limiting and token expiry enforcement to prevent enumeration attacks.

### 7.5 Front-end architecture

The MVP comprises three distinct front-end applications, each optimized for its user class and device context, all sharing a common `RVS.Domain` library and a `RVS.UI.Shared` Razor Class Library.

| Application | Framework | Rationale |
|---|---|---|
| **Cust_Intake** | Blazor WebAssembly (Interactive WebAssembly + Static SSR pages) | Zero install friction; customer accesses via dealer-specific URL. Dealer landing page and confirmation screens are Static SSR (instant load, SEO). The guided intake wizard (`@rendermode InteractiveWebAssembly`) runs entirely client-side — critical for multi-step form state and photo upload progress without round-trips. |
| **Mngr_Desktop** | Blazor SSR (Interactive Server) | Desktop browser on reliable office network. SignalR connection enables real-time push updates to the Service Board when technicians complete jobs. All business logic executes server-side; no sensitive data ships to the client. |
| **Tech_Mobile** | MAUI Blazor Hybrid (iOS + Android) | Offline-first mode is critical — service bays have poor connectivity. Outcome entries are queued locally and synced on reconnect. Native barcode SDK provides the fast, reliable VIN/QR scanning required for the 3–5 second interaction target. MAUI Essentials provides device speech-to-text for voice notes. Employer-provisioned install eliminates consumer app store friction. |

**Code reuse strategy:**

| Shared asset | Cust_Intake | Mngr_Desktop | Tech_Mobile |
|---|---|---|---|
| `RVS.Domain` (DTOs, entities, validation) | ✅ | ✅ | ✅ |
| `RVS.UI.Shared` Razor component library | ✅ | ✅ | ✅ |
| CSS / design tokens | ✅ | ✅ | ✅ |
| API client (typed `HttpClient` services) | ✅ | ✅ (server-side) | ✅ + offline queue |
| MAUI Essentials (camera, speech, local storage) | ❌ | ❌ | ✅ |

---

## 8. Milestones and sequencing

### 8.1 Project estimate

- **Size:** Medium (solo developer, full-stack)
- **Time estimate:** 8–10 weeks to production-ready MVP

### 8.2 Team size and composition

- **Team size:** 1 developer
- **Roles:** Full-stack developer (ASP.NET Core API, Blazor WebAssembly, Blazor SSR, MAUI Blazor Hybrid, Azure infrastructure)

### 8.3 Suggested phases

- **Phase 1: Solution scaffold and domain foundation** (Week 1)
  - Create solution, all projects, `EntityBase`, all domain entities, DTOs, and interfaces.
  - Zero infrastructure dependencies. Validates: `dotnet build` green.

- **Phase 2: Infrastructure — Cosmos DB and Blob Storage** (Week 2)
  - All repository implementations, Cosmos containers and indexing, Blob storage, seed data.
  - Validates: CRUD against Cosmos Emulator, all partition key patterns verified.

- **Phase 3: API bootstrap — Program.cs, middleware, auth** (Week 2–3)
  - Full middleware pipeline, Auth0 JWT validation, `ClaimsService`, `TenantAccessGateMiddleware`, `ExceptionHandlingMiddleware`.
  - Validates: API starts, `/health` returns 200, auth/tenant gate returns correct 401/403.

- **Phase 4: Dealer dashboard — lookups, dealerships, tenants, locations** (Week 3)
  - `TenantService`, `DealershipService`, `LocationService`, `LookupService`, all corresponding controllers.
  - Validates: Authenticated call to `GET /api/dealerships` returns seeded data; location CRUD works.

- **Phase 5: Core intake flow** (Week 4–5)
  - `IntakeController`, full 6-step orchestration (identity resolution → profile upsert → VIN ownership → asset ledger → categorization → notification), VIN scan endpoint, AI wizard, speech-to-text pipeline.
  - Validates: End-to-end intake submission creates all documents correctly; VIN ownership transfer scenarios pass.

- **Phase 6: Dealer dashboard — service request CRUD and attachments** (Week 5–6)
  - `ServiceRequestsController` (search, detail, update, delete), `AttachmentsController` (SAS URL, delete), `AnalyticsController` (basic counts by status).
  - Validates: Full dealer workflow — search, open detail, update status, view attachment.

- **Phase 7: Customer status page and magic link** (Week 6)
  - `CustomerStatusController`, token validation, cross-dealer SR summary.
  - Validates: Post intake → use magic-link token → see SR summary; expired token returns 404.

- **Phase 8: SFTP DMS export** (Week 7)
  - CSV export endpoint, scheduled SFTP push, SFTP config in TenantConfig, Azure Key Vault integration for SFTP keys.
  - Validates: Export generates correct CSV; SFTP push delivers file to configured endpoint.

- **Phase 9: Front-end applications** (Week 7–9)
  - **Cust_Intake (Blazor WASM):** Dealer landing page (Static SSR with WASM preload), 5-step guided intake wizard (Interactive WebAssembly — VIN scan, AI wizard, speech-to-text, photo/video upload), submission confirmation (Static SSR), magic-link status page (Static SSR).
  - **Mngr_Desktop (Blazor SSR — Interactive Server):** Service request queue, drag-and-drop Service Board, search/filter, service request detail view, status update, attachment viewer, analytics dashboard, dealer settings (location management, QR code download), real-time push updates via SignalR.
  - **Tech_Mobile (MAUI Blazor Hybrid):** Assigned job list, QR/VIN native barcode scan to open job, outcome entry form (offline queue + sync), voice notes via MAUI speech-to-text, photo capture, glove-friendly tap targets.

- **Phase 10: QR codes, seed data, polish, and deployment** (Week 9–10)
  - QR code generation endpoint, comprehensive seed data (multi-tenant, VIN transfers), rate limiting fine-tuning, Swagger/OpenAPI documentation, structured logging, Azure App Service deployment.

---

## 9. User stories

### 9.1. Submit a service request without an account

- **ID:** RVS-001
- **Description:** As an RV owner, I want to submit a service request without creating an account so that I can report a problem quickly from my phone.
- **Acceptance criteria:**
  - The intake form is accessible via a public URL requiring no login.
  - The form collects first name, last name, email, phone, VIN, make, model, year, issue description, and urgency.
  - Submission creates a `ServiceRequest`, a `CustomerProfile`, a `GlobalCustomerAcct`, and an `AssetLedgerEntry` within a single API call.
  - A confirmation email is sent to the customer within 60 seconds of submission.
  - The API returns `201 Created` with a service request summary and the customer's magic-link status URL.
  - Duplicate email detection correctly links the new request to the existing `GlobalCustomerAcct`.

### 9.2. Scan a VIN with a phone camera

- **ID:** RVS-002
- **Description:** As an RV owner, I want to scan my VIN plate with my phone camera so that I don't have to manually type a 17-character code.
- **Acceptance criteria:**
  - A camera scan button is present on the VIN field on mobile and desktop browsers that support `BarcodeDetector` or the fallback JS library.
  - A successful scan populates the VIN field with the decoded value.
  - The VIN is sent to the API which calls the NHTSA vPIC endpoint to decode make, manufacturer, model year, and asset type.
  - Decoded values pre-populate the corresponding form fields.
  - Manual entry remains available if the scan fails or is not supported.
  - An invalid VIN (wrong check digit or length) surfaces a validation error before submission.

### 9.3. Use the AI-guided issue wizard

- **ID:** RVS-003
- **Description:** As an RV owner, I want to answer a few guided questions about my issue so that the service advisor and technician have structured context before my RV arrives.
- **Acceptance criteria:**
  - After selecting a top-level issue category, contextual follow-up questions appear inline within the form.
  - At minimum, the following categories have distinct question trees: Slide System, Electrical, Plumbing, HVAC, Generator, Appliances, Roof/Seals, Chassis.
  - Wizard answers are submitted as structured key-value pairs alongside the free-text description.
  - Structured wizard fields populate the corresponding `ServiceEventEmbedded` fields in the `ServiceRequest`.
  - If the customer skips follow-up questions, submission proceeds without error.

### 9.4. Describe an issue using speech-to-text

- **ID:** RVS-004
- **Description:** As an RV owner, I want to speak my issue description instead of typing it so that I can quickly explain the problem while standing next to my RV.
- **Acceptance criteria:**
  - A microphone button is present on the issue description field.
  - Pressing the button activates the browser's Web Speech API (or native device microphone on mobile).
  - After recording ends, the captured transcript is sent to the AI cleanup endpoint.
  - The AI-cleaned description is displayed for customer review and editing before submission.
  - If speech recognition is unsupported by the browser, the microphone button is hidden and the text field remains primary.
  - The raw transcript is discarded after AI cleanup; only the reviewed text is submitted.

### 9.5. Upload photos and videos at intake

- **ID:** RVS-005
- **Description:** As an RV owner, I want to attach photos and videos of the problem so that the technician can see the issue before my appointment.
- **Acceptance criteria:**
  - The form supports up to 10 file attachments per service request.
  - Accepted file types: `.jpg`, `.jpeg`, `.png`, `.mp4`. Files of other types are rejected with a clear error message before upload.
  - Files exceeding the location's configured maximum size (default 25 MB) are rejected before upload.
  - Each accepted file is uploaded directly to Azure Blob Storage using a pre-authorized SAS upload URL.
  - Upload progress is displayed per file.
  - Failed uploads surface a per-file retry option without blocking submission of the text data.
  - Attachment metadata (blob URI, filename, content type, size) is embedded in the `ServiceRequest`.

### 9.6. Check service request status via magic link

- **ID:** RVS-006
- **Description:** As an RV owner, I want to check my service request status without logging in so that I can stay informed without calling the dealership.
- **Acceptance criteria:**
  - The confirmation email contains a magic-link URL in the format `https://app.rvserviceflow.com/status/{token}`.
  - The status page shows all active service requests for that customer across all dealerships, each showing: dealership name, location name, status, issue category, submission date, last updated date.
  - Expired or invalid tokens return a message prompting the customer to request a new link by email.
  - The status endpoint is rate-limited to 30 requests per IP per hour.
  - Accessing the page does not require any login or account creation.

### 9.7. Receive pre-filled contact and asset data as a returning customer

- **ID:** RVS-007
- **Description:** As a returning RV owner, I want my contact information and known vehicles to be pre-filled when I submit a new service request so that I don't have to re-enter information I've already provided.
- **Acceptance criteria:**
  - When a customer enters an email that matches an existing `GlobalCustomerAcct`, their first name, last name, and phone are pre-filled.
  - If the `GlobalCustomerAcct` has active asset VINs, they are offered as a selectable list above the VIN entry field.
  - Selecting a known VIN pre-fills the VIN, make, model, and year fields.
  - The customer can still enter a different VIN if needed.
  - Pre-fill data is fetched client-side after the email field loses focus; no page reload required.

### 9.8. View and manage the service request queue

- **ID:** RVS-008
- **Description:** As a service advisor, I want to see all incoming service requests in a structured queue so that I can triage and act on them without searching through emails or voicemails.
- **Acceptance criteria:**
  - Authenticated dealer staff see a dashboard with all service requests for their authorized locations.
  - The queue displays: customer full name, VIN, issue category, AI-generated technician summary (truncated), status badge, submission date, and attachment count.
  - The queue defaults to sorting by submission date descending.
  - Clicking a row opens the full service request detail.
  - Status badge colors are consistent: New (blue), In Progress (amber), Completed (green), Cancelled (grey).
  - The queue refreshes automatically or on demand (manual refresh button).

### 9.9. Search and filter service requests

- **ID:** RVS-009
- **Description:** As a service advisor, I want to search and filter service requests so that I can quickly find a specific customer's request or review all requests with a given status.
- **Acceptance criteria:**
  - The search endpoint `POST /api/service-requests/search` accepts: keyword (customer name, VIN, description snippet), status filter, issue category filter, location filter (multi-location roles only), date range filter, and page/pageSize.
  - Results are returned as a `PagedResult<ServiceRequestSummaryResponseDto>`.
  - Page size is capped at 100 records per request.
  - Search input is validated to block dangerous characters (`<`, `>`, `;`, `'`, `"`, `\`, `\0`).
  - The UI renders a search bar and filter panel above the queue.

### 9.10. Update service request status and add notes

- **ID:** RVS-010
- **Description:** As a service advisor, I want to update a service request's status and add notes so that the customer and my team know the current state of the repair.
- **Acceptance criteria:**
  - Each service request detail page has a status selector showing allowed transitions: `New` → `InProgress`, `InProgress` → `Completed` or `Cancelled`.
  - On status change to `InProgress` or `Completed`, the customer receives an email notification.
  - Advisors can add free-text advisor notes stored on the service request.
  - All status changes and note additions are stamped with the updating user's ID and timestamp via `MarkAsUpdated`.
  - Technicians cannot change status (requires `service-requests:update` permission, not granted to `dealer:technician`).

### 9.11. Update Section 10A structured repair data

- **ID:** RVS-011
- **Description:** As a technician, I want to record the repair action, parts used, and labor hours against a service request so that a structured record of the work is captured.
- **Acceptance criteria:**
  - The service request detail view shows an editable Section 10A panel for users with `service-requests:update-service-event` permission.
  - Editable fields: `ComponentType`, `FailureMode`, `RepairAction`, `PartsUsed` (add/remove list), `LaborHours`, `ServiceDateUtc`.
  - The technician can save Section 10A fields without changing the service request's overall status.
  - Saved values are reflected on the corresponding `AssetLedgerEntry` (updated via patch on the ledger entry).
  - Advisors and managers also have access to update Section 10A fields, not technicians only.

### 9.12. View and download service request attachments

- **ID:** RVS-012
- **Description:** As a technician, I want to view photos and videos attached to a service request so that I can understand the issue before the RV arrives.
- **Acceptance criteria:**
  - The service request detail page renders inline thumbnail previews for image attachments.
  - Video attachments are represented with a play icon preview; tapping/clicking opens a lightbox or inline player.
  - Each attachment is accessed via a time-limited SAS URL (1-hour expiry) generated by `GET /api/service-requests/{id}/attachments/{attachmentId}`.
  - The SAS URL fetch fails gracefully if the blob is missing (returns 404 with a clear message).
  - Users require `attachments:read` permission to access SAS URLs.

### 9.13. Manage dealership locations and intake configuration

- **ID:** RVS-013
- **Description:** As a dealership manager or owner, I want to configure my service locations and their intake form settings so that each location presents a branded, correctly configured experience to customers.
- **Acceptance criteria:**
  - Authorized users can create a new location with: display name, address, service email, phone, and region tag.
  - Authorized users can update location settings: display name, contact info, logo, `IntakeFormConfig` (accepted file types, max file size).
  - Each location is assigned a unique slug that generates the intake URL: `https://app.rvserviceflow.com/intake/{slug}`.
  - The slug is immutable after creation (changing it would break existing QR codes and links).
  - Creating a location automatically writes a corresponding `slugLookup` document.

### 9.14. Download a QR code for the service intake form

- **ID:** RVS-014
- **Description:** As a dealership manager, I want to download a QR code that links directly to my location's intake form so that I can print and display it in the service drive.
- **Acceptance criteria:**
  - `GET /api/locations/{id}/qr-code` returns a QR code image encoding `https://app.rvserviceflow.com/intake/{locationSlug}`.
  - The QR code is returned as a PNG image.
  - The dealer dashboard provides a download button that triggers the endpoint.
  - Users require `locations:read` permission to access the QR code endpoint.

### 9.15. Export service requests to CSV for DMS import

- **ID:** RVS-015
- **Description:** As a dealership owner or manager, I want to export service request data as a CSV so that I can import it into our existing Dealer Management System.
- **Acceptance criteria:**
  - An export action in the dashboard triggers `POST /api/service-requests/export` with an optional date range and location filter.
  - The response streams a CSV file with a header row and one row per service request.
  - CSV fields include: service request ID, status, submission date, customer name, email, phone, VIN, make, model, year, issue category, component type, issue description, technician summary, repair action, parts used (semicolon-separated), labor hours, service date, location name, attachment count.
  - Users require `service-requests:read` permission to access the export endpoint.
  - CSV values containing commas or quotes are correctly RFC 4180-escaped.

### 9.16. Configure SFTP push for automated DMS delivery

- **ID:** RVS-016
- **Description:** As a dealership owner, I want my service request data pushed automatically to our DMS SFTP endpoint on a daily schedule so that our systems stay synchronized without manual effort.
- **Acceptance criteria:**
  - SFTP configuration (host, port, username, private key reference, remote directory, schedule) is stored in `TenantConfig`.
  - The SFTP private key is stored in Azure Key Vault; only the Key Vault secret name is persisted in the database.
  - A daily scheduled job pushes a CSV of the prior day's service requests to the configured SFTP endpoint.
  - Both key-based and password-based SFTP authentication are supported.
  - Connection failures are logged with tenant context and surface as an alert in the dealer dashboard.
  - An on-demand "push now" action is available for testing the SFTP configuration.

### 9.17. Authenticate as dealer staff with role-based access

- **ID:** RVS-017
- **Description:** As a dealer staff member, I want to log in with my work credentials and have access scoped to my role and location so that I only see and can do what is appropriate for my job.
- **Acceptance criteria:**
  - Authentication is handled by Auth0. All dealer staff authenticate via the Auth0-hosted login page or SSO if configured.
  - The JWT contains custom claims: `tenantId`, `locationIds`, `role`, injected via an Auth0 Login Action from the user's `app_metadata`.
  - `ClaimsService.GetTenantIdOrThrow()` throws `UnauthorizedAccessException` (→ HTTP 401) if the `tenantId` claim is missing.
  - `TenantAccessGateMiddleware` returns HTTP 403 if the tenant's access gate is disabled.
  - Location-scoped roles (`dealer:advisor`, `dealer:technician`, `dealer:manager`, `dealer:readonly`) can only access service requests for locations in their `locationIds` claim.
  - Corporate-scoped roles (`dealer:owner`, `dealer:corporate-admin`) can access all locations within their `tenantId` partition.
  - All unauthorized access attempts are logged with the requesting `tenantId`, user ID, and request path.

### 9.18. Provision a new tenant

- **ID:** RVS-018
- **Description:** As a platform administrator, I want to provision a new dealership tenant so that they can begin onboarding their staff and configuring their intake form.
- **Acceptance criteria:**
  - The platform admin can create a new tenant via `POST /api/platform/tenants`.
  - Provisioning creates: a `TenantConfig` (with access gate enabled), a `Dealership` document, a default `Location`, and an Auth0 `app_metadata` entry for the initial admin user.
  - The provisioning endpoint requires the `platform:tenants:manage` permission.
  - A tenant can be disabled via `PUT /api/platform/tenants/{id}/access-gate`, which sets the `TenantAccessGateEmbedded.IsEnabled` flag. All subsequent authenticated requests from that tenant receive HTTP 403.
  - Tenant IDs are immutable once created.
