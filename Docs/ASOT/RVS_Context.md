# RV Service Flow (RVS) — Platform Context

**Authoritative Source of Truth (ASOT) — April 4, 2026**

A comprehensive platform overview for developers, investors, partners, and stakeholders. For detailed architecture and identity specifics, see companion documents [**RVS_PRD.md**](RVS_PRD.md), [**RVS_Core_Architecture_Version3.1.md**](../Obsolete/RVS_Core_Architecture_Version3.1.md), and [**RVS_Auth0_Identity_Version2.md**](Auth0/RVS_Auth0_Identity_Version2.md).

---

## 1. Problem & Opportunity

### The Core Problem

RV dealerships and independent service shops struggle with **service department bottlenecks**. Technicians arrive at units cold, with minimal pre-diagnosis information. Service advisors spend significant time on phone calls collecting incomplete repair descriptions. Waitlists are manual and unstructured. The result is extended **Repair Event Cycle Times (RECT)** — the duration from RV intake to completion — which directly reduces:

- Technician productivity
- Service throughput
- Customer satisfaction
- Dealership revenue

### The Market Opportunity

- **~342,000 new RV shipments annually** in North America
- **~8 million RV-owning households** in the U.S.
- **~2,000 RV dealerships** with service departments
- **Thousands of independent RV repair shops**
- Service departments are a **critical profit center** for dealerships
- Typical dealership: 5–20 technicians, 80–200 service jobs/month, persistent backlog

**Even small improvements in intake accuracy and technician preparation produce measurable gains in throughput and revenue.**


#### Across these local options, a few recurring customer pain points emerge:

- Poor communication between customers and service teams.
- Uncertainty about repair status and delays.
- Difficulty reaching advisors or technicians by phone.

All three of these themes are directly addressable by structured intake processes, automated communication, and clear status tracking — core parts of the RVS product value-proposition.

### The Solution

RVS is a cloud-based **service intake and workflow platform** that sits in front of existing Dealer Management Systems (DMS). It replaces phone-based intake with a structured, mobile-friendly, anonymous customer portal. Customers submit detailed repair information before the RV arrives. Technicians see pre-categorized issues and customer photos before opening the bay door. Service advisors manage a digitized queue instead of a waitlist spreadsheet.

---

## 2. Product Architecture: Three Applications, Four Surfaces

### 2.1 Customer Intake Portal (Mobile-First, Anonymous)

**For RV owners — requires no account.**

- **App:** `RVS.Blazor.Intake` — Blazor WebAssembly (Standalone PWA). All surfaces (landing page, guided wizard, confirmation, status pages) are routes within a single WASM SPA. No SSR, no SignalR. A service worker caches the WASM runtime after first load — subsequent visits skip the network download entirely.
- **URL:** `https://app.rvserviceflow.com/intake/{locationSlug}` (location-specific, accessible via QR code)
- **Submits:** VIN (camera scan or manual entry; AI-assisted photo extraction), make/model/year, issue description (text or speech-to-text with AI transcript cleanup), photos/videos (up to 10 files), urgency level, RV usage (full-time vs. part-time)
- **Flow:** Description-first capture: after the customer enters or records an issue description, AI suggests a top-level issue category (pre-selected in the dropdown, clearly marked as AI-suggested, customer can override). The wizard then presents contextual follow-up questions specific to that category (e.g., for Refrigerator → absorption vs. residential, error codes, shore power; for Slide-out → which slide number, manual override attempted)
- **Outcomes:** Service request created, customer receives confirmation email with magic-link status URL
- **Design principle:** Frictionless, mobile-first, zero account burden

### 2.2 Customer Status Page (Magic-Link, Cross-Dealer)

**For RV owners — check status any time, any dealership.**

- **App:** Part of `RVS.Blazor.Intake` — the status page is a client-side route within the same WASM SPA. No separate server-rendered page; the service worker ensures fast loads on repeat visits.
- **URL:** `https://app.rvserviceflow.com/status/{token}` (anonymous, rate-limited)
- **Token:** Secure magic-link generated on intake submission, embedded in confirmation email
- **Shows:** All active service requests from that customer **across all dealerships** where they've submitted
- **Data:** Location name, status, issue summary, last-updated date, request total count
- **Design principle:** Passive, cross-dealer visibility without account or login friction

### 2.3 Service Manager Desktop — `RVS.Blazor.Manager` (Blazor WebAssembly, Standalone)

**For service advisors, managers, regional managers, and corporate staff — desktop browser.**

- **App:** `RVS.Blazor.Manager` — Blazor WebAssembly (Standalone). Optimized for large-screen desktop browsers on reliable office networks. **MVP:** Long polling (configurable interval, default 5 min) for near-real-time Service Board updates when technicians complete jobs. **vNEXT:** A dedicated SignalR hub will push real-time updates, eliminating polling latency. Deployed to Azure Static Web Apps.
- **Authentication:** Auth0 JWT Bearer, organization-scoped
- **For Service Advisors:** Create service requests, search/filter queue, view detail, update status, add notes, view attachments
- **For Managers:** Drag-and-drop Service Board, triage intake queue, batch outcome entry, workload visibility across location or region
- **For Corporate Admin / Owner:** Cross-location analytics dashboard, user management, intake config, reporting
- **Data isolation:** Multi-tenant by corporation (Auth0 Organization), location-scoped roles filter within tenant
- **Design principle:** Structured, permission-based, real-time, supports lean to enterprise dealer groups

### 2.4 Technician Mobile App — `RVS.MAUI.Tech` (MAUI Blazor Hybrid)

**For technicians — phones and tablets in service bays.**

- **App:** `RVS.MAUI.Tech` — MAUI Blazor Hybrid (iOS + Android). Employer-provisioned install via MDM or app store.
- **Authentication:** Auth0 JWT Bearer, organization-scoped
- **Offline-first:** Poor bay connectivity is expected. Outcome entries store locally (SQLite) and sync on reconnect via sequential `PUT` calls.
- **Key interactions:** QR/VIN scan to open a job (native barcode SDK), photo capture, voice notes (platform speech-to-text via MAUI Essentials), log Section 10A repair fields (failure mode, action, parts, labor)
- **Speed target:** 3–5 second total interaction per job completion (glove-friendly large tap targets, full-screen native experience)
- **Design principle:** Minimal, fast, device-native; reuses shared Blazor components from `RVS.UI.Shared`

---

## 3. Why This Matters: The Blue Compass Problem

Large dealer groups like **Blue Compass RV** operate 100+ locations under a single corporation. The platform must support both:

- **Single-location independents** (1 dealership = 1 location)
- **Multi-location enterprises** (1 corporation = 100+ geographic service locations)

**Without multi-location support**, you'd need separate subscriptions, duplicated configuration, fragmented customer data across locations, and cross-location cost visibility becomes impossible. With multi-location, one corporation gets:

- **1 Auth0 Organization** (unified user management, consistent branding)
- **1 Cosmos DB partition** (corporate-wide customer view, intra-partition queries are cheap)
- **Role-based location scoping** (e.g., regional managers see their regions, advisors see their locations)
- **Cross-location service history** (one customer, multiple locations, unified ledger)

This is a **hard architectural constraint** discovered early in RVS design — not an afterthought. Every data model, every partition key, every API endpoint is designed with this in mind.

---

## 4. Multi-Tenant Data Isolation

### 4.1 The Hierarchy

```
Blue Compass RV (Dealership / Auth0 Organization / Cosmos Partition)
├── Blue Compass RV - Salt Lake City (Location)
├── Blue Compass RV - Denver (Location)
├── Blue Compass RV - Tampa (Location)
├── ExploreUSA RV - San Antonio (Location, subsidiary brand, same organization)
└── ... 100+ more locations

Happy Trails RV (Single-location dealership / Auth0 Organization / Cosmos Partition)
└── Happy Trails RV - Boise (Location)
```

### 4.2 Partition Key Strategy

- **Cosmos partition key:** `/tenantId` (Auth0 Organization ID, e.g., `org_blue_compass_rv`)
- **All locations within a corporation share one partition**
- **Location-scoped filters happen inside the partition** (no cross-partition queries for most operations)
- **Customers belong to a corporation, not a location** (John Doe is "Blue Compass John", visible across all Blue Compass locations)
- **Asset ownership transfers within a corporation are intra-partition** (fast, cheap)

### 4.3 Auth0 Organization Mapping

| Concept | RVS Implementation |
|---|---|
| Auth0 Organization | One dealer corporation (e.g., Blue Compass RV) |
| Organization ID (`org_id`) | RVS `tenantId` — the Cosmos partition key |
| Organization members | Dealer staff (not customers; customers are anonymous in MVP) |
| Per-org roles | `dealer:corporate-admin`, `dealer:owner`, `dealer:regional-manager`, `dealer:manager`, `dealer:advisor`, `dealer:technician`, `dealer:readonly` |
| Per-org identity providers | Each dealership can use their own SSO (Google, Azure AD, username/password) |

---

## 5. Roles & Permissions

**All dealer staff roles are Auth0-native and organization-scoped.** Permissions carried in the JWT access token, enforced server-side.

### 5.1 Dealer Staff Roles

| Role | Scope | Capabilities |
|---|---|---|
| **`dealer:corporate-admin`** | Multi-location, org-wide | Full access across all locations, user management, all settings, all data, analytics |
| **`dealer:owner`** | Organization-wide | Equivalent to corporate-admin for single-location dealers or dealership owners |
| **`dealer:regional-manager`** | Multi-location, region-scoped | Visibility limited to locations matching their `regionTag` claim (*e.g.*, "west" region, "northeast" region); can manage SRs, view analytics across their region |
| **`dealer:manager`** | Single location | Full SR management, analytics, and location-specific settings for their site |
| **`dealer:advisor`** | Single location | Creates, searches, updates SRs; primary daily user of the dashboard |
| **`dealer:technician`** | Single location | Views assigned SRs, updates only Section 10A repair fields (parts, labor, action); cannot modify status or customer data |
| **`dealer:readonly`** | Single location | Read-only access (accounting, auditors, observers) |

### 5.2 Platform Admin Role

| Role | Scope | Capabilities |
|---|---|---|
| **`platform:admin`** | Cross-tenant, global | RVS operators; manage all tenants, access gates, global lookup sets; not scoped to any organization |

### 5.3 Customer Status

**No role, anonymous, rate-limited.**

- Customers never create Auth0 accounts in MVP
- They access intake via direct URL, QR code, or dealer deep link
- They check status via **magic-link token** (secure, single-use, expiring)
- In Phase 2+, optional upgrade to Auth0 OIDC for persistent customer accounts

---

## 6. The Nine-Container Cosmos DB Design

RVS uses **Azure Cosmos DB** as the source of truth, partitioned for high-cardinality tenancy, multi-location queries, and append-only asset history. Nine containers optimize different access patterns:

| # | Container | Partition Key | Documents | Purpose | RU Mode |
|---|---|---|---|---|---|
| 1 | `serviceRequests` | `/tenantId` | `serviceRequest` | Core SR data, customer snapshot, issue categorization, attachments, repair fields | Autoscale 400–4000 |
| 2 | `customerProfiles` | `/tenantId` | `customerProfile` | Tenant-scoped shadow customer record, asset ownership tracking | Autoscale 400–1000 |
| 3 | `globalCustomerAccts` | `/email` | `globalCustomerAcct` | Cross-dealer identity federation, magic-link tokens, linked profiles | Manual 400 |
| 4 | `assetLedger` | `/assetId` | `assetLedgerEntry` | **Data moat:** Append-only asset service history, section 10A records, cross-dealer intelligence | Autoscale 400–1000 |
| 5 | `dealerships` | `/tenantId` | `dealership` | Corporation/dealer group profiles, branding, multi-location config | Manual 400 |
| 6 | `locations` | `/tenantId` | `location` | Physical service locations, intake config, hours, contact info, regional tags | Autoscale 400–1000 |
| 7 | `tenantConfigs` | `/tenantId` | `tenantConfig` | Tenant settings, access gate (onboarding/disabling), feature flags | Manual 400 |
| 8 | `lookupSets` | `/category` | `lookupSet` | Issue categories, component types, failure modes, repair actions — shared and customizable per tenant | Manual 400 |
| 9 | `slugLookup` | `/slug` | `slugLookup` | Mapping from location URL slug → tenantId + locationId (fast intake prefix resolution) | Autoscale 400–1000 |

### 6.1 Why Three Identity Containers?

One document can't serve three different access patterns:

- **Tenant-scoped customer** (partition: `/tenantId`) — optimizes dashboard reads, asset ownership, customer search
- **Global customer** (partition: `/email`) — optimizes intake email resolution (~1 RU point read) and cross-dealer status lookups
- **Global asset** (partition: `/assetId`) — optimizes asset service history and Section 10A analytics across all dealers

---

## 7. The Data Moat: AssetLedger

### 7.1 What Is It?

An **append-only service event ledger** keyed by asset (e.g., `RV:1ABC234567`). One entry per service request, written at intake time. Contains:

- **Asset metadata:** Manufacturer, model, year
- **Customer & dealer context:** Who owns it, which dealership, which location
- **Section 10A fields:** Issue category, failure mode, repair action, parts used, labor hours, service date
- **Timestamps:** When submitted, when serviced

### 7.2 Why It Matters

**Data Moat:** This is proprietary, accumulating, non-transferable intelligence that increases platform value over time. By year 2–3, RVS has:

- Cross-dealer service history for thousands of RV models
- Failure mode intelligence per asset type
- Repair patterns and cost data
- Technician efficiency benchmarks

This data is **strategic** — it powers:

- **Phase 5+:** Predictive maintenance recommendations
- **Phase 6+:** Smart parts ordering and inventory optimization
- **Competitive advantage:** Dealers retain data within RVS; competitors cannot access it

### 7.3 Asset Identification

Assets are identified by a **compound global key:** `{AssetType}:{Identifier}`

Examples:

```
RV:1ABC234567             (RV by VIN)
Boat:HIN987654321         (Boat by HIN)
Excavator:CAT320GX999     (Heavy equipment by serial)
Tractor:JD8R3001234       (Farm equipment by serial)
```

This format:
- Works across industries (RVS expansion from RVs to marine, heavy equipment, agricultural equipment)
- Preserves domain-specific identifiers (VIN, HIN, serial number)
- Globally unique within the platform

---

## 8. Authentication & Identity

### 8.1 Auth0 as Identity Provider

RVS uses **Auth0** for all dealer staff authentication. MVP uses Auth0 Free plan; transitions to Essentials B2B on commercialization.

| Phase | Auth0 Strategy | Tenant Management | Org Limit |
|---|---|---|---|
| **MVP** | Hybrid: `tenantId` injected via Login Action from `app_metadata` | Unlimited (dev-friendly) | Free plan (unlimited tenants) |
| **Commercialization** | Auth0 Organizations (native) | Native Organizations feature | Essentials B2B (~50+ orgs) |
| **Scale** | Auth0 Professional/Enterprise | Enterprise scale | Higher/unlimited |

**Key insight:** The RVS backend code is **agnostic** to this transition. `ClaimsService.GetTenantIdOrThrow()` reads the same JWT claim regardless of whether it comes from `app_metadata` or native Organizations. The difference is purely Auth0 configuration.

### 8.2 ClaimsService

An injected scoped service that reads JWT claims (injected by Auth0 Login Action) and provides strongly-typed accessors:

```csharp
var tenantId = _claimsService.GetTenantIdOrThrow();           // org_blue_compass_rv
var roles = _claimsService.GetRoles();                        // [dealer:advisor, ...]
var userId = _claimsService.GetUserIdOrThrow();              // auth0|xyz789
var locationIds = _claimsService.GetLocationIds();            // [loc_slc, loc_denver, ...]
var regionTag = _claimsService.GetRegionTag();                // "west"
```

Used by every controller and service method to enforce tenant isolation, role checks, and location-scoped access.

### 8.3 Customers (MVP)

**No Auth0 account.** Customers are anonymous in MVP:

- **Intake:** `[AllowAnonymous]`, rate-limited, auto-created shadow profile
- **Status page:** `[AllowAnonymous]`, magic-link token validation
- **Phase 2+:** Optional upgrade to Auth0 OIDC for persistent customer login

---

## 9. Technology Stack

### 9.1 Backend

| Component | Technology | Notes |
|---|---|---|
| **API** | ASP.NET Core (.NET 10), C# 14 | RESTful, OpenAPI/Swagger documented |
| **Database** | Azure Cosmos DB (SQL API) | 9 containers, multi-region ready |
| **Storage** | Azure Blob Storage | Photos, videos, attachments; tenant-scoped paths |
| **Cache** | Azure Cosmos DB Integrated Cache | TTL on high-read containers |
| **Identity** | Auth0 | JWT Bearer, Organizations (future) |
| **AI** | Azure OpenAI (`gpt-4o-mini`); Azure AI Speech (STT) | **Wave 1 (active):** VIN extraction from photo, speech transcript cleanup, AI category suggestion. **Wave 2+:** Diagnostic questions, technician summaries, LLM-backed categorization |
| **Notifications** | Azure Communication Services (ACS) | Transactional email + SMS, unified provider |

### 9.2 Frontend

| Application | Technology | Purpose | Notes |
|---|---|---|---|
| **`RVS.Blazor.Intake`** | Blazor WebAssembly (Standalone PWA) | Customer intake portal + status pages | Zero-install, URL-based. All surfaces are client-side routes in a single WASM SPA. Service worker caches WASM runtime after first load. No SSR. Deployed to Azure Static Web Apps (CDN). |
| **`RVS.Blazor.Manager`** | Blazor WebAssembly (Standalone) | Service manager desktop app | Service Board, triage, analytics, batch operations. Large-screen desktop browser. Long polling for near-real-time updates (MVP); SignalR hub for real-time push (vNEXT). Deployed to Azure Static Web Apps. |
| **`RVS.MAUI.Tech`** | MAUI Blazor Hybrid (iOS + Android) | Technician mobile app | Offline-first, native barcode/QR/camera/voice. 3–5 sec interaction target. Employer-provisioned via MDM. Shares Razor components from `RVS.UI.Shared`. |
| **`RVS.UI.Shared`** | Razor Class Library | Shared UI components | DTOs, Razor components, CSS design tokens, and typed API client services consumed by all three apps. |

### 9.3 Infrastructure

| Component | Technology |
|---|---|
| **Hosting** | Azure Container Apps (future) or App Service |
| **CI/CD** | GitHub Actions |
| **Monitoring** | Azure App Insights, Log Analytics |
| **IaC** | Bicep (or Terraform) |

---

## 10. Implementation Roadmap: Phase Vocabulary

**Phases are defined by MVP scope → expansion.** Each phase is a deployable, testable increment with clear success criteria.

### 10.1 MVP (Phase 1)

**Goal:** Customer intake portal + dealer dashboard for one dealership, one location, basic SR status management.

**Delivers:**
- Anonymous customer intake form (VIN scan, issue description, photos)
- Magic-link customer status page (cross-deal visibility not required, just intake confirmation)
- Dealer dashboard: SR queue, detail view, status updates, attachment viewing
- Nine-container Cosmos schema (prepped for scale)
- Auth0 integration (basic)
- Basic rules-based issue categorization (keyword matching)

**Ship criteria:** 5 design partner dealerships validate flow, intake time < 3 min, zero account friction.

### 10.2 Phase 2: Multi-Location & RBAC

**Goal:** Support multi-location dealer groups; add sophisticated role-based access.

**Adds:**
- Multi-location operation (e.g., Blue Compass 100+ locations)
- Regional manager roles and location scoping
- Corporate-wide analytics dashboard
- Optional customer Auth0 accounts (persistent login + preference saving)
- Enhanced magic-link (cross-dealer aggregation)

### 10.3 AI Assistive Intake (Wave 1 — Active)

**Goal:** Deliver production-ready AI assistance directly in the customer intake flow.

**Active sprint (Wave 1):**
- VIN extraction from photo (`POST .../ai/extract-vin`) — AI parses VIN from a camera image with manual fallback
- Speech-to-text + AI transcript cleanup (`POST .../ai/transcribe-issue`, `POST .../ai/refine-issue-text`) — raw audio → cleaned, editable description
- AI category suggestion (`POST .../ai/suggest-category`) — description-first flow auto-suggests issue category, customer can override

**Architecture:** All AI inference is server-side (`RVS.API`), wrapped in `AiOperationResponseDto<T>` envelopes with confidence, provider, and `correlationId`. Provider is `gpt-4o-mini`; all AI calls degrade gracefully on failure.

### 10.4 Phase 3: Advanced AI & Diagnostics

**Goal:** Intelligent diagnostic conversation and technician summaries powered by Azure OpenAI.

**Adds:**
- AI-guided diagnostic questions (contextual based on issue category; issues #231 and #233)
- AI-generated technician summaries (context from customer description + wizard answers)
- LLM-backed issue categorization as the default path (replaces rule-based fallback)
- Section 10A field auto-population (repair action suggestions)

### 10.5 Phase 4: Scheduling & Assignment

**Goal:** Booking, bay assignment, technician routing.

**Adds:**
- Service appointment scheduling calendar
- Bay assignment and reservation
- Technician skill-based routing algorithm
- Real-time technician availability
- Customer appointment confirmation + reminders

### 10.6 Phase 5: Parts Integration & Predictive Analytics

**Goal:** Supply chain integration + data moat monetization.

**Adds:**
- Parts ordering integration (OEM / aftermarket APIs)
- Backorder tracking
- Parts inventory optimization (using asset ledger history)
- Predictive maintenance recommendations
- Cross-dealer benchmarking reports

### 10.7 Phase 6+: Industry Expansion & Verticalization

**Goal:** Beyond RVs — marine, heavy equipment, agricultural.

**Adds:**
- Industry-specific intake wizards
- Vertical-specific lookups and asset types
- Industry-specific vendor integrations
- Specialized technician certifications
- Industry-focused analytics

---

## 11. Business Model

### 11.1 Revenue

**SaaS subscription** — tiered by location count:

- **Tier 1:** Single-location independent, $199–$299/month
- **Tier 2:** 2–10 locations, $399–$699/month
- **Tier 3:** 11–50 locations, $999–$1,999/month
- **Tier 4:** 50+ locations, custom enterprise pricing

**Future add-ons:**
- Predictive maintenance reports
- Parts procurement optimization
- Advanced analytics
- API access for DMS integrations

### 11.2 Positioning

**"The intake layer for service operations."** Sits in front of (not replacing) existing DMS tools. Minimal displacement risk. Day-1 integration: SFTP export of structured SR data into the dealer's existing workflow.

### 11.3 Go-to-Market

- **Design partners:** 5 dealerships during MVP, co-develop features
- **Early access:** Q3 2026, 10–20 paying customers ($50K ARR target)
- **General availability:** Q4 2026 / Q1 2027

---

## 12. Key Architectural Decisions

### 12.1 Why Not Replace the DMS?

Building "DMS replacement" would require:
- Accounting, warranty claims, parts inventory, CRM
- 10+ year engineering investment to compete with Lightspeed, IDS Astra, EverLogic
- Massive sales/support burden to migrate dealership data

**RVS strategy:** Sit in front, focus on intake and service advisor experience. Let dealers keep their DMS. Export structured data for easy integration.

### 12.2 Why Anonymous Intake?

- **Removes friction.** Customers submit in < 3 minutes, no account setup, no username/password, no sign-up email confirmation delay.
- **Magic-link scales.** Tokens are ephemeral, stateless, secure. No customer account management burden.
- **Privacy-first.** Customers can submit without identifying themselves to the dealer until they want to.
- **Phase 2+ upgrade.** Optional Auth0 account for customers who want persistent profiles.

### 12.3 Why Three Identity Containers?

Each container serves a different query pattern:
- **Tenant-scoped reads** (dashboard, asset ownership): partition by `/tenantId`
- **Global intake resolution** (intake form email check): partition by `/email` (fast point read)
- **Asset history analytics** (Section 10A ledger, cross-dealer): partition by `/assetId`

A single document structure can't optimize all three. Three containers, three partition keys, one consistent data model.

### 12.4 Why Cosmos DB Multi-Region Ready?

Building multi-region replication from day 1:
- Enables future **global expansion** (RVs sold worldwide)
- Asynchronous replication reduces latency in far regions
- Supports disaster recovery without rearchitecture

---

## 13. Why This Matters: Competitive Advantage

### 13.1 Service Layer Entry

**No competitor has tackled service intake at this level.** DMS vendors focus on accounting and warranty. Independents (e.g., Fixd) focus on consumer apps, not dealer operations. **RVS fills the gap.**

### 13.2 Data Moat Timeline

- **Year 1:** Platform validated, intake flow proven
- **Year 2:** 100+ dealerships, 10,000+ service events/month in ledger, predictive signals emerging
- **Year 3:** Proprietary failure mode intelligence, parts optimization, benchmarking — competitors can't replicate

### 13.3 Vertical Expansion

**Asset-agnostic architecture.** Once intake and Section 10A are working for RVs, bolt on marine, heavy equipment, agricultural. Each vertical is $50M+ addressable market.

---

## 14. For Investors & Partners

### 14.1 Market Size

- **RV market:** 8M households, ~2,000 dealerships, $50B+ industry
- **Serviceable:** 500–800 dealerships with service departments that can justify SaaS spend
- **3-year TAM:** $50–80M (assuming 20% penetration at $50K per dealership average)

### 14.2 Product-Market Fit Signals

- **Design partners**: Dealerships iterating with product team, validating problem
- **Intake time**: < 3 min (vs. 15–30 min phone calls today)
- **Technician prep**: 90% of advisors report SRs are actionable (vs. 40% of phone notes)
- **NPS**: Target 60+ by end of Phase 2

### 14.3 Due Diligence Talking Points

- **Defensible data moat:** Asset ledger is non-transferable, grows over time
- **Vertical expansion:** Playbook proven in RVs, replicable to 5+ industries  
- **Free-to-paid motion:** MVP free for design partners, clear tiered pricing for commercialization
- **Unit economics:** $10–15 CAC, $2K–3K LTV per dealership (3-year), < 12 month payback

---

## 15. For Developers: Key Documentation

This context document is a **platform overview**. For deep dives, see:

- **[RVS_PRD.md](RVS_PRD.md)** — Product requirements, user personas, functional/non-functional goals, feature list, Phase 1 MVP scope
- **[RVS_Technical_PRD.md](RVS_Technical_PRD.md)** — Per-endpoint contracts, performance KPIs, security constraints, DTO appendix, telemetry events
- **[RVS_Core_Architecture_Version3.1.md](../Obsolete/RVS_Core_Architecture_Version3.1.md)** — Domain entities, Cosmos schema, repository interfaces, data flows, middleware pipeline, API surface, front-end application formats
- **[Auth0/RVS_Auth0_Identity_Version2.md](Auth0/RVS_Auth0_Identity_Version2.md)** — Auth0 Organizations, RBAC matrix, JWT structure, ClaimsService, authorization policies, Login Action
- **[AI_Architecture_Blueprint.md](AI/AI_Architecture_Blueprint.md)** — Durable AI architecture: provider abstraction, envelope DTO, all AI endpoint routes, client patterns, and per-issue mapping
- **[AI_Wave1_Implementation_Backlog.md](AI/AI_Wave1_Implementation_Backlog.md)** — Wave 1 task breakdown: VIN extraction, speech cleanup, category suggestion — exact contracts, tasks, acceptance criteria
- **[RVS_implementation_plan.md](RVS_implementation_plan.md)** — 8-phase implementation roadmap, per-phase deliverables, validation criteria, suggested starting point
- **[.github/copilot-instructions.md](../../.github/copilot-instructions.md)** — RVS-specific coding conventions, patterns, project structure

---

## 16. Quick Reference: Terminology

| Term | Meaning | Example |
|---|---|---|
| **Tenant** | Dealer corporation; Auth0 Organization boundary; Cosmos partition | `org_blue_compass_rv` |
| **Location** | Physical service site | `loc_blue_compass_slc` |
| **Service Request (SR)** | Customer repair intake; core domain entity | `a1b2c3d4-e5f6-7890-abcd-ef1234567890` |
| **Section 10A** | Structured repair data: component, failure mode, action, parts, labor | From ASE/industry standard |
| **Asset Ledger** | Append-only asset service history; the data moat | `RV:1ABC234567` |
| **Magic-link token** | Secure, stateless, expiring URL token for anonymous customer status access | Emailed to customer on intake |
| **ClaimsService** | Scoped service that provides JWT claim accessors (tenant, roles, user, location) | `_claimsService.GetTenantIdOrThrow()` |
| **Slug** | URL-safe identifier for location intake portal | `blue-compass-salt-lake` |

---

**End of RVS_Context.md**

*Last updated: April 4, 2026. For questions, refer to Architecture documents or contact the RVS platform team.*
