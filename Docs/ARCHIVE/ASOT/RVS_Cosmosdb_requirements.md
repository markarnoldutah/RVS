# Azure Cosmos DB NoSQL Modeling Session

## Application Overview
- **Domain**: Multi-tenant SaaS — RV Service Intake & Workflow Platform
- **Key Entities**: Tenant (1:M) Dealership, Dealership (1:M) Location, Tenant (1:M) ServiceRequest, Tenant (1:M) CustomerProfile, GlobalCustomerAcct (1:M) CustomerProfile (cross-tenant), Asset (1:M) AssetLedgerEntry, LookupSet (global reference data), SlugLookup (URL resolution index), TenantConfig (1:1 per tenant)
- **Business Context**: RV dealerships use a customer-facing intake portal (anonymous, no auth) to submit service requests. Service managers triage and assign via desktop app. Technicians complete jobs via mobile app. Platform supports single-location independents and 100+ location enterprise dealer groups (e.g., Blue Compass RV). All data is tenant-isolated except GlobalCustomerAcct (cross-tenant by email) and AssetLedger (cross-tenant by assetId). Section 10A service event data is the proprietary "data moat."
- **Scale**:
  - MVP: 2–5 tenants, ~5–20 locations total, ~80–200 service requests/month per location
  - Growth target: ~2,000 RV dealerships, ~5–20 locations each = ~10k–40k locations
  - Estimated total service requests: ~200–800k SR/month across all tenants at full scale
  - Concurrent users: ~50–200 during peak hours at MVP; ~5k–10k at full scale
  - Average document sizes: ServiceRequest ~5–15KB, CustomerProfile ~2–5KB, AssetLedgerEntry ~1–2KB, Dealership ~1–2KB, Location ~1KB, GlobalCustomerAcct ~1–3KB, LookupSet ~5–20KB, SlugLookup ~0.5KB, TenantConfig ~0.5KB
  - Total estimated data: ~50GB at full scale (dominated by service-requests + asset-ledger)
- **Geographic Distribution**: Single-region (West US 3) for MVP. Multi-region read replicas planned for Phase 2+.

## Access Patterns Analysis

| Pattern # | Description | RPS (Peak/Avg) | Type | Container | Partition Key Used | Key Requirements | Status |
|-----------|-------------|-----------------|------|-----------|--------------------|------------------|--------|
| AP-1 | Get service request by ID (dealer dashboard detail view) | 50/20 | Read | service-requests | tenantId | Point read, <10ms latency | ✅ |
| AP-2 | Search/filter service requests (10+ filters, paginated) | 30/10 | Read | service-requests | tenantId | Single-partition query with composite filters, ORDER BY createdAtUtc DESC | ✅ |
| AP-3 | Get service requests by locationId (location queue) | 20/5 | Read | service-requests | tenantId | Single-partition query, filter by locationId | ✅ |
| AP-4 | Create service request (intake submission) | 20/5 | Write | service-requests | tenantId | Create, ~10KB doc | ✅ |
| AP-5 | Update service request (status transition, assignment, service event) | 30/10 | Write | service-requests | tenantId | Replace full document | ✅ |
| AP-6 | Delete service request | 2/0.5 | Write | service-requests | tenantId | Point delete | ✅ |
| AP-7 | Get service requests for analytics (date range + optional location) | 5/1 | Read | service-requests | tenantId | Single-partition scan, potentially large result set | ✅ |
| AP-8 | Batch outcome update (up to 25 SRs) | 2/0.5 | Write | service-requests | tenantId | Sequential replaces within same partition | ✅ |
| AP-9 | Get customer profile by ID | 10/3 | Read | customer-profiles | tenantId | Point read | ✅ |
| AP-10 | Get customer profile by email (intake dedup) | 20/5 | Read | customer-profiles | tenantId | Single-partition query on email (indexed) | ✅ |
| AP-11 | Get customer profile by active assetId (ownership transfer) | 5/1 | Read | customer-profiles | tenantId | Single-partition query with EXISTS subquery on embedded array | ✅ |
| AP-12 | Create customer profile (auto-created on first intake) | 5/2 | Write | customer-profiles | tenantId | Unique key: tenantId + email | ✅ |
| AP-13 | Update customer profile (asset ownership, request count) | 20/5 | Write | customer-profiles | tenantId | Replace full document | ✅ |
| AP-14 | Get global customer account by email | 20/5 | Read | global-customer-accounts | email | Single-partition query (PK = email) | ✅ |
| AP-15 | Get global customer account by ID | 5/1 | Read | global-customer-accounts | ⚠️ Cross-partition | Fan-out query — PK is email, querying by id | ⚠️ |
| AP-16 | Get global customer account by magic-link token (status page) | 10/3 | Read | global-customer-accounts | ⚠️ Cross-partition | Fan-out query — PK is email, querying by magicLinkToken | ⚠️ |
| AP-17 | Create global customer account | 5/2 | Write | global-customer-accounts | email | Create | ✅ |
| AP-18 | Update global customer account (link profile, rotate token) | 10/3 | Write | global-customer-accounts | email | Replace | ✅ |
| AP-19 | Get asset ledger entries by assetId (service history) | 5/1 | Read | asset-ledger | assetId | Single-partition query, ORDER BY submittedAtUtc | ✅ |
| AP-20 | Get single asset ledger entry | 2/0.5 | Read | asset-ledger | assetId | Point read | ✅ |
| AP-21 | Append asset ledger entry (write-once at intake) | 5/2 | Write | asset-ledger | assetId | Unique key: assetId + serviceRequestId | ✅ |
| AP-22 | Get dealership by ID | 10/3 | Read | dealerships | tenantId | Point read | ✅ |
| AP-23 | Get dealership by slug (intake resolution) | 10/3 | Read | dealerships | ⚠️ Cross-partition | Fan-out query — PK is tenantId, querying by slug | ⚠️ |
| AP-24 | List dealerships by tenant | 5/1 | Read | dealerships | tenantId | Single-partition query | ✅ |
| AP-25 | Create/Update dealership | 2/0.5 | Write | dealerships | tenantId | Create/Replace | ✅ |
| AP-26 | Get location by ID | 10/3 | Read | locations | tenantId | Point read | ✅ |
| AP-27 | Get location by slug within tenant | 5/1 | Read | locations | tenantId | Single-partition query | ✅ |
| AP-28 | List locations by tenant | 10/3 | Read | locations | tenantId | Single-partition query | ✅ |
| AP-29 | Create/Update/Delete location | 3/1 | Write | locations | tenantId | CRUD operations | ✅ |
| AP-30 | Resolve slug → tenantId + locationId (intake entry point) | 20/5 | Read | slug-lookups | slug | O(1) point read — PK = slug | ✅ |
| AP-31 | Upsert/Delete slug lookup (location CRUD side-effect) | 3/1 | Write | slug-lookups | slug | Upsert/Delete | ✅ |
| AP-32 | Get tenant config (access gate check on every auth request) | 100/30 | Read | tenant-configs | tenantId | Point read, hot path | ✅ |
| AP-33 | Create/Update tenant config | 1/0.1 | Write | tenant-configs | tenantId | Upsert | ✅ |
| AP-34 | Get global lookup set by category | 10/3 | Read | lookup-sets | category | Single-partition query (PK = category) | ✅ |
| AP-35 | Upsert global lookup set | 0.1/0.01 | Write | lookup-sets | category | Upsert (seeding/admin only) | ✅ |
| AP-36 | Get tenant config from tenants container (legacy/config repo) | 20/5 | Read | dealerships (tenants) | tenantId | Point read with id = `{tenantId}_config` | ✅ |

## Entity Relationships Deep Dive
- **Tenant → Dealerships**: 1:Many (1–5 dealerships per tenant typical, max ~100 for enterprise)
- **Tenant → Locations**: 1:Many (1–100+ locations per tenant)
- **Tenant → ServiceRequests**: 1:Many (hundreds to thousands per month)
- **Tenant → CustomerProfiles**: 1:Many (one per unique customer per tenant)
- **GlobalCustomerAcct → CustomerProfiles**: 1:Many cross-tenant (one per email globally, linked to N tenant-scoped profiles)
- **Asset → AssetLedgerEntries**: 1:Many cross-tenant (one per service request per asset)
- **ServiceRequest → CustomerProfile**: Many:1 (each SR references a customer profile)
- **Location → ServiceRequests**: 1:Many (each SR is at one location)

## Enhanced Aggregate Analysis

### ServiceRequest Container Item Analysis
- **Access Correlation**: ServiceRequest is a self-contained aggregate root with embedded snapshots (CustomerSnapshot, AssetInfo, Attachments, ServiceEvent, DiagnosticResponses)
- **Query Patterns**:
  - SR only: 95% of queries (dashboard, search, detail, status transitions)
  - SR + CustomerProfile: 5% (intake orchestration only)
- **Size Constraints**: Average 5–15KB per document, max ~50KB with full attachments metadata
- **Update Patterns**: Frequent updates (status, assignment, service event), full document replace
- **Decision**: Single Document Aggregate ✅
- **Justification**: Embedding customer/asset/attachment snapshots eliminates cross-container joins for the dominant access pattern (dealer dashboard). Write amplification is acceptable since SRs are updated 3–5 times in lifecycle.

### Dealerships + Tenants Container Item Analysis
- **Access Correlation**: 40% of queries need both (intake config resolution)
- **Query Patterns**:
  - Dealership only: 60% (list, detail, update)
  - Tenant only: 30% (config reads, access gate)
  - Both together: 10% (intake orchestration)
- **Size Constraints**: Dealership ~1–2KB, Tenant ~0.5KB — combined well under limits
- **Update Patterns**: Dealerships updated occasionally, Tenants rarely
- **Identifying Relationship**: Tenants and Dealerships share tenantId as partition key
- **Decision**: Multi-Document Container (dealerships) ✅ (already implemented via type discriminator)
- **Justification**: Shared PK, low size, low update frequency, operational coupling acceptable. Tenant entity uses `type = "tenant"` discriminator. Eliminates need for separate tenants container.

### CustomerProfile (Tenant-Scoped) Analysis
- **Access Correlation**: Only 5% of queries need SR + Profile together
- **Query Patterns**:
  - Profile by email (intake dedup): 60%
  - Profile by ID (management): 30%
  - Profile by active assetId (ownership transfer): 10%
- **Size Constraints**: 2–5KB avg, could grow with many assetsOwned entries (bounded by # of assets a customer has serviced)
- **Update Patterns**: Updated on every intake submission (asset ownership, request count)
- **Decision**: Separate Container ✅
- **Justification**: Low access correlation with SRs, different update frequency. Unique key (tenantId + email) is specific to this container. Separate throughput allocation optimal.

### GlobalCustomerAcct (Cross-Tenant) Analysis
- **Access Correlation**: Only during intake orchestration and status page
- **Size Constraints**: 1–3KB, grows slowly with linkedProfiles
- **Decision**: Separate Container ✅
- **Justification**: Cross-tenant by design (PK = email). Cannot be co-located with tenant-scoped data. This is the correct isolation boundary.

### AssetLedger (Cross-Tenant) Analysis
- **Access Correlation**: Read during intake (to check history), read for Section 10A intelligence
- **Size Constraints**: 1–2KB per entry, append-only. Could accumulate 10–50 entries per asset over years.
- **Decision**: Separate Container ✅
- **Justification**: Cross-tenant by design (PK = assetId). Append-only pattern with unique key (assetId + serviceRequestId). Data moat — requires independent scaling and change feed processing.

## Container Consolidation Analysis

### Consolidation Candidates Review
| Parent | Child | Relationship | Access Overlap | Consolidation Decision | Justification |
|--------|-------|--------------|----------------|------------------------|---------------|
| Tenant | Dealership | 1:Many | 40% | ✅ Consolidated | Already sharing `dealerships` container with type discriminator |
| Tenant | TenantConfig | 1:1 | 30% | ❌ Keep Separate | TenantConfig is on hot path (every auth request), needs independent throughput |
| Dealership | Location | 1:Many | 35% | ❌ Keep Separate | Location has unique key (slug), different CRUD lifecycle, independent throughput beneficial |
| ServiceRequest | CustomerProfile | Many:1 | 5% | ❌ Keep Separate | Very low access correlation; different update patterns |
| GlobalCustomerAcct | CustomerProfile | 1:Many | 10% | ❌ Keep Separate | Different partition strategies (email vs tenantId), cross-tenant boundary |

## Design Considerations

### ⚠️ Cross-Partition Query Concerns (Flagged)
1. **AP-15: GetByIdAsync on global-customer-accounts** — PK is `/email`, querying by `id` requires fan-out. Low RPS (5/1) mitigates cost but is suboptimal.
2. **AP-16: GetByMagicLinkTokenAsync on global-customer-accounts** — PK is `/email`, querying by `magicLinkToken` requires fan-out. Medium RPS (10/3). **This is a scalability concern at growth scale.**
3. **AP-23: GetBySlugAsync on dealerships** — PK is `/tenantId`, querying by `slug` requires fan-out. Medium RPS (10/3). **Mitigated by slug-lookups container (AP-30) which provides O(1) resolution.** The dealership slug query should be deprecated in favor of slug-lookups → then point-read dealership by tenantId + id.

### Hot Partition Concerns
- **tenant-configs (AP-32)**: 100 RPS peak on access gate check. With ~5 tenants at MVP, each partition gets ~20 RPS — well within 10,000 RU/s limit. At scale (2,000 tenants), distributes to 0.05 RPS per partition. **No concern.**
- **service-requests (AP-2)**: Search queries at 30 RPS peak. Distributed across tenants. Large tenants (100+ locations, 200+ SR/month per location = 20k+/month) could accumulate large partition sizes over time. **Monitor partition size at scale.**

### Indexing Strategy
- All containers use **selective indexing** (include specific paths, exclude `/*`). This is optimal for write-heavy patterns and reduces RU overhead.
- **service-requests**: Composite indexes on (status ASC, createdAtUtc DESC) and (locationId ASC, status ASC) support search queries (AP-2).
- **Missing composite index**: Search supports `ORDER BY c.createdAtUtc DESC` but many filter combinations may not be covered. Consider adding composite indexes for (priority, createdAtUtc) and (assignedTechnicianId, createdAtUtc) if those search filters are frequently used.
- **lookup-sets**: Composite index on (category, name) supports ordered listing.

### Denormalization Observations (Already Applied)
- **ServiceRequest.CustomerSnapshot**: Customer info denormalized into SR — eliminates cross-container joins for dashboard. **Excellent pattern.**
- **ServiceRequest.AssetInfo**: Asset info denormalized into SR. **Excellent pattern.**
- **SlugLookup.DealershipName/LocationName**: Denormalized for display. **Correct for read-heavy intake path.**
- **AssetLedgerEntry.DealershipName**: Denormalized for cross-tenant display. **Correct.**
- **GlobalCustomerAcct.LinkedProfiles.DealershipName**: Denormalized for status page. **Correct.**

### Global Distribution
- MVP: Single region (West US 3)
- Phase 2+: Multi-region read replicas with Session consistency
- No multi-region writes needed (service operations are location-specific)

## Validation Checklist
- [x] Application domain and scale documented ✅
- [x] All entities and relationships mapped ✅
- [x] Aggregate boundaries identified based on access patterns ✅
- [x] Identifying relationships checked for consolidation opportunities ✅
- [x] Container consolidation analysis completed ✅
- [x] Every access pattern documented with RPS, type, container, partition key ✅
- [x] Write pattern exists for every read pattern ✅
- [x] Hot partition risks evaluated ✅
- [x] Consolidation framework applied; candidates reviewed ✅
- [x] Design considerations captured ✅
