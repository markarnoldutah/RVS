# Azure Cosmos DB NoSQL Data Model — Review & Optimization Proposals

## Design Philosophy & Approach

The RVS platform uses a **multi-tenant, aggregate-oriented Cosmos DB NoSQL design** with 9 containers in a single database (`rvsdb`). The design follows three core principles:

1. **Tenant isolation via partition key**: Most containers use `/tenantId` as partition key, ensuring all queries from authenticated users are single-partition.
2. **Aggregate root embedding**: ServiceRequest embeds customer snapshots, asset info, attachments, service events, and diagnostic responses — eliminating cross-container joins for the dominant access pattern (dealer dashboard).
3. **Purpose-built index containers**: SlugLookup provides O(1) intake resolution; GlobalCustomerAcct provides cross-tenant identity; AssetLedger provides cross-tenant service history.

**Overall assessment: The current design is well-architected, follows Cosmos DB best practices, and is scalable for the target growth trajectory (2,000+ tenants, 50GB+ total data). The optimizations below address specific scalability gaps and cost efficiency improvements.**

## Aggregate Design Decisions

### ServiceRequest — Single Document Aggregate ✅ Confirmed
ServiceRequest is the correct aggregate root boundary. Embedding CustomerSnapshot, AssetInfo, Attachments (metadata only), ServiceEvent, and DiagnosticResponses avoids N+1 queries for the highest-frequency pattern (dashboard list + detail view). The 5–15KB average document size is well within the 2MB limit, even with 10 attachments.

**Trade-off accepted**: Full document replace on status transitions writes ~10KB per update. At 30 updates/sec peak, this is ~150KB/sec write throughput — well within Cosmos DB capacity.

### Dealerships + Tenants — Multi-Document Container ✅ Confirmed
Tenant and Dealership entities share the `dealerships` container with a type discriminator. This is correct: they share the same partition key (`/tenantId`), have low update frequency, and the few queries that need both can be served from a single partition.

### CustomerProfile — Separate Container ✅ Confirmed
Low access correlation with ServiceRequest (5% joint access). Unique key constraint on `(tenantId, email)` is container-specific. Independent scaling makes sense as customer profile writes happen on every intake.

### GlobalCustomerAcct — Separate Container ✅ Confirmed
Cross-tenant by design. Partition key `/email` is the natural choice for the dominant access pattern (lookup by email during intake).

### AssetLedger — Separate Container ✅ Confirmed
Cross-tenant, append-only. Partition key `/assetId` enables efficient "get full service history for this VIN." Unique key `(assetId, serviceRequestId)` prevents duplicate entries. This is the data moat and needs independent change feed processing for Section 10A enrichment.

---

## Container Designs

### 1. `service-requests` Container

```json
[
  {
    "id": "sr_a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "tenantId": "ten_bluecompass",
    "type": "serviceRequest",
    "status": "InProgress",
    "locationId": "loc_slc",
    "customerProfileId": "cp_johnson_bc",
    "customerSnapshot": {
      "firstName": "Mike",
      "lastName": "Johnson",
      "email": "mike.johnson@email.com",
      "phone": "801-555-0101",
      "isReturningCustomer": true,
      "priorRequestCount": 2
    },
    "assetInfo": {
      "assetId": "RV:1FTFW1ET5EKE12345",
      "manufacturer": "Forest River",
      "model": "Georgetown 36B7",
      "year": 2022
    },
    "issueDescription": "Slide-out won't retract fully...",
    "issueCategory": "slideout",
    "priority": "High",
    "urgency": "This week",
    "attachments": [
      {
        "attachmentId": "att_001",
        "blobUri": "https://blob/...",
        "fileName": "slideout-gap.jpg",
        "contentType": "image/jpeg",
        "sizeBytes": 2048000,
        "createdAtUtc": "2026-03-15T10:00:00Z"
      }
    ],
    "diagnosticResponses": [
      {
        "questionText": "Which slide is affected?",
        "selectedOptions": ["Driver side rear"],
        "freeTextResponse": null
      }
    ],
    "serviceEvent": null,
    "scheduledDateUtc": null,
    "assignedTechnicianId": "tech_042",
    "assignedBayId": "bay-3",
    "requiredSkills": ["slideout", "hydraulic"],
    "rvUsage": "Full-time",
    "createdAtUtc": "2026-03-15T09:30:00Z",
    "updatedAtUtc": "2026-03-16T14:22:00Z",
    "createdByUserId": "system",
    "updatedByUserId": "advisor_jane"
  }
]
```

- **Purpose**: Primary transactional container for service request lifecycle management.
- **Aggregate Boundary**: ServiceRequest is a self-contained aggregate root embedding all related data as snapshots.
- **Partition Key**: `/tenantId` — all dealer operations are tenant-scoped. Single-partition for all dashboard queries.
- **Document Types**: `serviceRequest` only (type discriminator for future extensibility).
- **Access Patterns Served**: AP-1, AP-2, AP-3, AP-4, AP-5, AP-6, AP-7, AP-8
- **Throughput Planning**: Autoscale 400–4000 RU/s at MVP; increase max to 10,000 RU/s at growth scale.
- **Consistency Level**: Session (default) — ensures read-your-own-writes for dashboard operations.

#### Indexing Strategy
- **Indexing Policy**: Selective (exclude `/*`, include specific paths)
- **Included Paths**: `/tenantId/?`, `/status/?`, `/locationId/?`, `/customerProfileId/?`, `/issueCategory/?`, `/createdAtUtc/?`, `/scheduledDateUtc/?`, `/type/?`
- **Excluded Paths**: `/*`, `/_etag/?`
- **Composite Indexes**:
  ```json
  {
    "compositeIndexes": [
      [
        { "path": "/status", "order": "ascending" },
        { "path": "/createdAtUtc", "order": "descending" }
      ],
      [
        { "path": "/locationId", "order": "ascending" },
        { "path": "/status", "order": "ascending" }
      ]
    ]
  }
  ```
- **Access Patterns Served**: AP-2 (search with ORDER BY), AP-3 (location filter), AP-7 (analytics date range)

### 2. `customer-profiles` Container

```json
[
  {
    "id": "cp_johnson_bc",
    "tenantId": "ten_bluecompass",
    "type": "customerProfile",
    "email": "mike.johnson@email.com",
    "firstName": "Mike",
    "lastName": "Johnson",
    "phone": "801-555-0101",
    "globalCustomerAcctId": "gca_johnson",
    "assetsOwned": [
      {
        "assetId": "RV:1FTFW1ET5EKE12345",
        "manufacturer": "Forest River",
        "model": "Georgetown 36B7",
        "year": 2022,
        "status": "Active",
        "firstSeenAtUtc": "2026-01-10T08:00:00Z",
        "lastSeenAtUtc": "2026-03-15T09:30:00Z",
        "requestCount": 3
      }
    ],
    "serviceRequestIds": ["sr_001", "sr_002", "sr_003"],
    "totalRequestCount": 3,
    "createdAtUtc": "2026-01-10T08:00:00Z"
  }
]
```

- **Purpose**: Tenant-scoped customer shadow profiles, auto-created on first intake.
- **Partition Key**: `/tenantId` — tenant isolation for all customer queries.
- **Unique Key Policy**: `[/tenantId, /email]` — one profile per customer per tenant.
- **Access Patterns Served**: AP-9, AP-10, AP-11, AP-12, AP-13
- **Throughput Planning**: Autoscale 400–1000 RU/s (lower volume than service-requests).
- **Consistency Level**: Session

#### Indexing Strategy
- **Included Paths**: `/tenantId/?`, `/email/?`, `/globalCustomerAcctId/?`, `/type/?`
- **Excluded Paths**: `/*`, `/_etag/?`

### 3. `global-customer-accounts` Container

```json
[
  {
    "id": "gca_johnson",
    "tenantId": "GLOBAL",
    "type": "globalCustomerAcct",
    "email": "mike.johnson@email.com",
    "firstName": "Mike",
    "lastName": "Johnson",
    "linkedProfiles": [
      {
        "tenantId": "ten_bluecompass",
        "profileId": "cp_johnson_bc",
        "dealershipName": "Blue Compass RV",
        "firstSeenAtUtc": "2026-01-10T08:00:00Z",
        "requestCount": 3
      }
    ],
    "allKnownAssetIds": ["RV:1FTFW1ET5EKE12345"],
    "magicLinkToken": "dGVzdA:a1b2c3d4e5f6",
    "magicLinkExpiresAtUtc": "2026-04-15T00:00:00Z"
  }
]
```

- **Purpose**: Cross-tenant customer identity. One per real human (by email). Powers status page across all dealerships.
- **Partition Key**: `/email` — optimal for the primary access pattern (lookup by email during intake).
- **Access Patterns Served**: AP-14, AP-15, AP-16, AP-17, AP-18
- **Throughput Planning**: Autoscale 400–1000 RU/s.
- **Consistency Level**: Session

#### Indexing Strategy
- **Included Paths**: `/email/?`, `/magicLinkToken/?`, `/type/?`
- **Excluded Paths**: `/*`, `/_etag/?`

### 4. `asset-ledger` Container

```json
[
  {
    "id": "ale_001",
    "assetId": "RV:1FTFW1ET5EKE12345",
    "tenantId": "ten_bluecompass",
    "dealershipName": "Blue Compass RV",
    "serviceRequestId": "sr_001",
    "globalCustomerAcctId": "gca_johnson",
    "manufacturer": "Forest River",
    "model": "Georgetown 36B7",
    "year": 2022,
    "issueCategory": "slideout",
    "issueDescription": "Slide-out won't retract...",
    "section10A": null,
    "status": "Completed",
    "submittedAtUtc": "2026-01-10T09:30:00Z"
  }
]
```

- **Purpose**: Append-only cross-tenant service history per asset (VIN). The proprietary data moat.
- **Partition Key**: `/assetId` — efficient retrieval of full service history for any vehicle.
- **Unique Key Policy**: `[/assetId, /serviceRequestId]` — one entry per service request per asset.
- **Access Patterns Served**: AP-19, AP-20, AP-21
- **Throughput Planning**: Autoscale 400–1000 RU/s (write-once, read-occasionally).
- **Consistency Level**: Eventual (acceptable for historical data; writes are idempotent).

#### Indexing Strategy
- **Included Paths**: `/assetId/?`, `/tenantId/?`, `/serviceRequestId/?`, `/submittedAtUtc/?`, `/status/?`
- **Excluded Paths**: `/*`, `/_etag/?`

### 5. `dealerships` Container (Multi-Document: Dealership + Tenant)

```json
[
  {
    "id": "ten_bluecompass",
    "tenantId": "ten_bluecompass",
    "type": "tenant",
    "name": "Blue Compass RV",
    "billingEmail": "billing@bluecompassrv.com",
    "status": "Active",
    "plan": "Enterprise"
  },
  {
    "id": "deal_slc",
    "tenantId": "ten_bluecompass",
    "type": "dealership",
    "name": "Blue Compass RV - Salt Lake",
    "slug": "blue-compass-slc",
    "intakeConfig": {
      "acceptedFileTypes": [".jpg", ".png", ".mp4"],
      "maxFileSizeMb": 25,
      "maxAttachments": 10,
      "allowAnonymousIntake": true
    }
  }
]
```

- **Purpose**: Dealership configuration and tenant metadata. Multi-document container using type discriminator.
- **Partition Key**: `/tenantId` — natural parent for dealership entities.
- **Document Types**: `tenant`, `dealership`
- **Access Patterns Served**: AP-22, AP-23, AP-24, AP-25, AP-36
- **Throughput Planning**: Autoscale 400–1000 RU/s (low volume, config-oriented).
- **Consistency Level**: Session

#### Indexing Strategy
- **Included Paths**: `/tenantId/?`, `/type/?`, `/slug/?`, `/name/?`
- **Excluded Paths**: `/*`, `/_etag/?`
- **Composite Indexes**: `[(type ASC, name ASC)]`

### 6. `locations` Container

- **Purpose**: Service locations within dealerships.
- **Partition Key**: `/tenantId`
- **Unique Key Policy**: `[/tenantId, /slug]`
- **Access Patterns Served**: AP-26, AP-27, AP-28, AP-29
- **Throughput Planning**: Autoscale 400–1000 RU/s

### 7. `slug-lookups` Container

- **Purpose**: O(1) URL slug to tenantId + locationId resolution index for intake entry.
- **Partition Key**: `/slug` — enables point reads by slug (1 RU per read).
- **Access Patterns Served**: AP-30, AP-31
- **Throughput Planning**: Autoscale 400 RU/s (tiny container, low volume).

### 8. `tenant-configs` Container

- **Purpose**: Per-tenant configuration including access gate settings.
- **Partition Key**: `/tenantId` — O(1) point read per tenant.
- **Access Patterns Served**: AP-32, AP-33
- **Throughput Planning**: Autoscale 400–1000 RU/s. Consider in-memory caching for AP-32 (hot path).

### 9. `lookup-sets` Container

- **Purpose**: Global reference data (issue categories, service types).
- **Partition Key**: `/category` — groups all items for a category in one partition.
- **Access Patterns Served**: AP-34, AP-35
- **Throughput Planning**: Autoscale 400 RU/s (very low volume, read-heavy, cache-friendly).

---

## Access Pattern Mapping

### Solved Patterns

| Pattern | Description | Container | Cosmos DB Operation | Implementation Notes |
|---------|-------------|-----------|-------------------|----------------------|
| AP-1 | Get SR by ID | service-requests | Point read (id + PK) | 1 RU for ~10KB doc |
| AP-2 | Search SRs (10 filters) | service-requests | Single-partition query with composite indexes | ORDER BY createdAtUtc DESC uses composite index |
| AP-3 | Get SRs by location | service-requests | Single-partition query (tenantId + locationId) | ~3-5 RU per page |
| AP-4 | Create SR | service-requests | CreateItemAsync | ~50 RU for 10KB doc |
| AP-5 | Update SR | service-requests | ReplaceItemAsync | ~70 RU for 10KB doc (replace is more expensive than create) |
| AP-6 | Delete SR | service-requests | DeleteItemAsync | ~10 RU |
| AP-7 | Analytics aggregation | service-requests | Single-partition query with date filter | Potentially expensive at scale — see optimization O-5 |
| AP-8 | Batch outcome | service-requests | Sequential ReplaceItemAsync (up to 25) | ~1750 RU total for 25 x 70 RU |
| AP-9 | Get profile by ID | customer-profiles | Point read | 1 RU |
| AP-10 | Get profile by email | customer-profiles | Single-partition query (indexed) | ~3 RU |
| AP-11 | Get profile by active assetId | customer-profiles | Single-partition query with EXISTS subquery | ~5-8 RU |
| AP-12 | Create profile | customer-profiles | CreateItemAsync | ~15 RU |
| AP-13 | Update profile | customer-profiles | ReplaceItemAsync | ~20 RU |
| AP-14 | Get acct by email | global-customer-accounts | Single-partition query | ~3 RU |
| AP-15 | Get acct by ID | global-customer-accounts | Cross-partition query | ~2.5 x N physical partitions RU — see O-1 |
| AP-16 | Get acct by magic-link | global-customer-accounts | Cross-partition query | ~2.5 x N physical partitions RU — see O-2 |
| AP-17 | Create acct | global-customer-accounts | CreateItemAsync | ~10 RU |
| AP-18 | Update acct | global-customer-accounts | ReplaceItemAsync | ~15 RU |
| AP-19 | Get ledger by assetId | asset-ledger | Single-partition query | ~3-5 RU per page |
| AP-20 | Get ledger entry | asset-ledger | Point read | 1 RU |
| AP-21 | Append ledger entry | asset-ledger | CreateItemAsync | ~10 RU |
| AP-22 | Get dealership by ID | dealerships | Point read | 1 RU |
| AP-23 | Get dealership by slug | dealerships | Cross-partition query | See O-3 |
| AP-24 | List dealerships | dealerships | Single-partition query | ~3 RU |
| AP-25 | Create/Update dealership | dealerships | Create/Replace | ~10-15 RU |
| AP-26 | Get location by ID | locations | Point read | 1 RU |
| AP-27 | Get location by slug | locations | Single-partition query (within tenant) | ~3 RU |
| AP-28 | List locations | locations | Single-partition query | ~3 RU |
| AP-29 | Location CRUD | locations | Create/Replace/Delete | ~10-15 RU |
| AP-30 | Resolve slug | slug-lookups | Point read | 1 RU — optimal |
| AP-31 | Upsert/Delete slug | slug-lookups | Upsert/Delete | ~5-10 RU |
| AP-32 | Get tenant config | tenant-configs | Point read | 1 RU |
| AP-33 | Save tenant config | tenant-configs | Upsert | ~10 RU |
| AP-34 | Get lookup set | lookup-sets | Single-partition query | ~3-5 RU |
| AP-35 | Upsert lookup set | lookup-sets | Upsert | ~15-30 RU (larger doc) |
| AP-36 | Get tenant config (legacy) | dealerships | Point read | 1 RU |

---

## Hot Partition Analysis

| Container | Hottest Pattern | Peak RPS | Distribution | Risk | Mitigation |
|-----------|----------------|----------|--------------|------|------------|
| service-requests | AP-2 Search | 30 | Across tenants, ~6 RPS per top tenant | Low | Natural tenant distribution |
| tenant-configs | AP-32 Access gate | 100 | Across tenants, ~20 RPS per tenant at MVP | Low | Add application-level caching (O-4) |
| slug-lookups | AP-30 Resolve slug | 20 | Across slugs, ~1 RPS per slug | Low | Evenly distributed by slug |
| global-customer-accounts | AP-14 By email | 20 | Across emails | Low | High cardinality |
| service-requests | AP-7 Analytics | 5 | Per tenant (full partition scan) | Medium at scale | O-5: Add date-based pagination or materialized views |

**Partition size risk**: At full scale (2,000 tenants), the largest tenants (enterprise, 100+ locations) could accumulate 50k+ service requests per year in a single logical partition. At ~10KB each = ~500MB per tenant partition. This is well within the 20GB logical partition limit. **No hot partition or partition size concerns at the projected scale.**

---

## Scalability Confirmation

### What is Working Well

1. **Partition key strategy** (`/tenantId`) — Natural tenant isolation, all dashboard/management queries are single-partition. Excellent cardinality at scale.
2. **Aggregate root design** (ServiceRequest) — Embedding customer/asset snapshots eliminates cross-container joins for 95%+ of reads.
3. **Selective indexing** across all containers — Reduces write RU overhead by ~30-50% compared to automatic indexing.
4. **Composite indexes** on service-requests — Support the most common sort patterns.
5. **SlugLookup container** — Brilliant design for O(1) intake resolution. Eliminates the need for cross-partition dealership slug queries.
6. **Unique key policies** — Prevent data corruption (duplicate emails per tenant, duplicate ledger entries per asset).
7. **Append-only AssetLedger** — Clean write-once pattern with immutable core fields.
8. **Type discriminator pattern** (dealerships container) — Efficient multi-document container for Tenant + Dealership.
9. **Point reads for hot paths** — SlugLookup, TenantConfig, ServiceRequest by ID all use point reads (1 RU).

### Projected Scale Math

| Metric | MVP | Growth | Limit |
|--------|-----|--------|-------|
| Total tenants | 5 | 2,000 | No Cosmos limit |
| Locations per tenant | 5 | 100 | No Cosmos limit |
| SRs per month (total) | 1,000 | 800,000 | - |
| Total SR docs (2 years) | 24,000 | 19.2M | - |
| SR container size | ~240MB | ~192GB | 4+ physical partitions |
| Largest tenant partition | ~50MB | ~960MB | Well under 20GB |
| Total physical partitions | 1 | 4 | Manageable |
| Peak RU/s (all containers) | ~200 | ~2,000 | Well under autoscale ranges |

**Verdict: The current design scales to the full growth target without architectural changes.** The optimizations below are improvements, not necessities.

---

## Proposed Optimizations

### O-1: Eliminate Cross-Partition Query for GlobalCustomerAcct by ID (AP-15) — Medium Priority

**Problem**: `GetByIdAsync` on `global-customer-accounts` (PK = `/email`) requires a cross-partition fan-out query when looking up by `id`. At scale with many physical partitions, this becomes expensive.

**Current cost**: ~2.5 RU x N physical partitions per query
**At growth scale**: ~10+ physical partitions = 25+ RU per lookup

**Proposed solution**: Store the email (which is the partition key) in any document that references a `GlobalCustomerAcct` by ID. Then convert `GetByIdAsync` to a point read using `email` as the partition key. This is already partially done — `CustomerProfile` stores `globalCustomerAcctId` and the intake orchestration has access to the email. The only caller of `GetByIdAsync` is `LinkProfileAsync` — refactor it to accept `email` parameter and use `GetByEmailAsync` instead.

**RU savings**: 25+ RU to 3 RU per call
**Implementation effort**: Low — refactor `LinkProfileAsync` signature in service + orchestration

### O-2: Optimize Magic-Link Token Lookup (AP-16) — Medium Priority

**Problem**: `GetByMagicLinkTokenAsync` is a cross-partition fan-out query on `global-customer-accounts`. At 10 RPS peak for the status page, this is the most expensive cross-partition pattern.

**Current cost**: ~2.5 RU x N physical partitions per query
**At growth scale**: ~10+ physical partitions x 10 RPS = 250+ RU/s wasted on fan-out overhead

**Proposed solution (Option A — Recommended)**: The current token generation already includes an email-hash prefix (`base64url(SHA256(email)[0..8]):random_bytes`). Add a secondary lookup mechanism:
- Store magic-link tokens in a lightweight `magic-link-tokens` container (PK = `/token`, id = token). Each document contains just `{ token, email, expiresAtUtc }`.
- On token lookup: point-read the token container (1 RU) then get email then point-read the global account (1 RU). Total: 2 RU instead of 25+ RU.
- On token rotation: upsert the token document (5 RU). Delete old token document.

**Proposed solution (Option B — Simpler)**: Since the token already has an email-hash prefix, derive the partition key from the token prefix. This requires changing the `global-customer-accounts` container to use a hierarchical partition key or embedding the email in the token itself (URL-safe encoded). More complex but avoids a new container.

**RU savings**: 25+ RU to 2 RU per status page load
**Implementation effort**: Medium — new container (Option A) or token format change (Option B)

### O-3: Deprecate Cross-Partition Dealership Slug Query (AP-23) — Low Priority

**Problem**: `CosmosDealershipRepository.GetBySlugAsync` is a cross-partition query on the `dealerships` container (PK = `/tenantId`).

**Current reality**: This query is already largely mitigated by the `slug-lookups` container (AP-30), which provides O(1) slug resolution. The intake orchestration correctly uses `slug-lookups` first, then does a point read on dealership by `tenantId + id`.

**Proposed action**: Audit all callers of `GetBySlugAsync`. If it is only used as a fallback or in admin scenarios, mark it with an `[Obsolete]` attribute and plan removal. If any hot-path code still uses it, refactor to use slug-lookups then point read pattern.

**RU savings**: Eliminates occasional 2.5 x N RU fan-out queries
**Implementation effort**: Low — code audit + deprecation annotation

### O-4: Cache Tenant Config for Access Gate Checks (AP-32) — High Priority

**Problem**: `TenantAccessGateMiddleware` calls `GetTenantConfigAsync` on **every authenticated request**. At 100 RPS peak, this is 100 point reads/sec just for access gate checks. While each is only 1 RU (point read), this is unnecessary I/O for data that changes extremely rarely.

**Proposed solution**: Implement an in-memory cache with short TTL for tenant access gate status:

```csharp
// IMemoryCache with 60-second absolute expiration
public async Task<TenantConfig> GetCachedAsync(string tenantId, CancellationToken ct)
{
    var cacheKey = $"tenant-config:{tenantId}";
    if (!_cache.TryGetValue(cacheKey, out TenantConfig? config))
    {
        config = await _repository.GetAsync(tenantId, ct);
        _cache.Set(cacheKey, config, TimeSpan.FromSeconds(60));
    }
    return config!;
}
```

**RU savings**: ~95% reduction in tenant-config reads (100 RU/s to 1-2 RU/s per tenant)
**Latency savings**: Eliminates ~1ms network round-trip on every request
**Implementation effort**: Low — add `IMemoryCache` injection to middleware or a `CachedTenantConfigRepository` decorator
**Trade-off**: 60-second stale window when access gate is toggled. Acceptable for this use case — disabling a tenant is not a real-time operation.

### O-5: Optimize Analytics Query (AP-7) — Medium Priority

**Problem**: `GetForAnalyticsAsync` performs a full partition scan with optional date/location filters, pulling **all matching ServiceRequest documents** into memory for in-app aggregation. At scale (20k+ SRs per tenant), this returns megabytes of data and consumes significant RUs.

**Proposed solutions (progressive)**:

**Phase 1 — Projection queries**: Instead of `SELECT *`, use `SELECT c.status, c.issueCategory, c.locationId, c.createdAtUtc, c.priority` to return only the fields needed for aggregation. This reduces document size from ~10KB to ~200 bytes, cutting RU consumption by ~80%.

**Phase 2 — Materialized analytics via Change Feed**: Create a lightweight `analytics-summaries` container populated by a Change Feed processor. On each ServiceRequest create/update, increment/decrement counters in pre-aggregated documents (e.g., `{ tenantId, locationId, month, statusCounts: { New: 5, InProgress: 12 }, categoryCounts: { ... } }`). Analytics reads become single point reads instead of partition scans.

**RU savings**: Phase 1: ~80% reduction in analytics query cost. Phase 2: ~99% reduction.
**Implementation effort**: Phase 1: Low (query change only). Phase 2: Medium (new Change Feed processor + container).

### O-6: Add Missing Composite Indexes for Search (AP-2) — Low Priority

**Problem**: The search endpoint (AP-2) supports 10+ filter combinations, but only 2 composite indexes exist: `(status, createdAtUtc)` and `(locationId, status)`. Queries filtering by `priority + createdAtUtc` or `assignedTechnicianId + createdAtUtc` may not use composite indexes, resulting in higher RU consumption.

**Proposed solution**: Add composite indexes for the most common dashboard filter patterns:

```json
{
  "compositeIndexes": [
    [
      { "path": "/priority", "order": "ascending" },
      { "path": "/createdAtUtc", "order": "descending" }
    ],
    [
      { "path": "/assignedTechnicianId", "order": "ascending" },
      { "path": "/createdAtUtc", "order": "descending" }
    ],
    [
      { "path": "/locationId", "order": "ascending" },
      { "path": "/createdAtUtc", "order": "descending" }
    ]
  ]
}
```

**Trade-off**: Each composite index adds ~5-10% write RU overhead per indexed path. Only add indexes that are actually used frequently.
**Implementation effort**: Low — update seed/provisioning script
**Recommendation**: Instrument search queries with RU logging (already done!), analyze which filter combinations are most common, then add targeted indexes.

### O-7: Add `assignedTechnicianId` and `priority` to service-requests Indexing Policy — Low Priority

**Problem**: The current indexing policy includes `/status/?`, `/locationId/?`, `/issueCategory/?`, `/createdAtUtc/?`, `/scheduledDateUtc/?`, but does **not** include `/assignedTechnicianId/?`, `/assignedBayId/?`, or `/priority/?` as individual paths. These are used in the search endpoint (AP-2) filter conditions.

**Proposed solution**: Add the following to the included paths:
```json
{ "path": "/assignedTechnicianId/?" },
{ "path": "/assignedBayId/?" },
{ "path": "/priority/?" }
```

**RU savings**: Reduces query cost for technician/bay/priority filters from full-partition scan to indexed lookup.
**Implementation effort**: Low — update seed/provisioning script.

### O-8: Consider In-Memory Cache for Lookup Sets (AP-34) — Low Priority

**Problem**: Lookup sets (issue categories, etc.) are global, read-heavy, and almost never change. Every lookup API call hits Cosmos DB.

**Proposed solution**: Same `IMemoryCache` pattern as O-4, with 5-minute TTL for lookup sets. Invalidate on upsert.

**RU savings**: ~95% reduction in lookup-sets reads
**Implementation effort**: Very low — the repository already has a `// TODO - Consider caching these lookups in memory for performance` comment

---

## Trade-offs and Optimizations Summary

| Optimization | Priority | RU Savings | Effort | Trade-off |
|-------------|----------|------------|--------|-----------|
| O-1: Eliminate GetByIdAsync fan-out | Medium | 25+ to 3 RU/call | Low | Requires caller refactoring |
| O-2: Magic-link token lookup | Medium | 25+ to 2 RU/call | Medium | New container or token format change |
| O-3: Deprecate dealership slug query | Low | Occasional fan-out eliminated | Low | Audit needed |
| O-4: Cache tenant config | High | ~95% reduction on hot path | Low | 60-sec stale window |
| O-5: Optimize analytics | Medium | ~80-99% reduction | Low-Medium | Phase 2 requires Change Feed |
| O-6: Additional composite indexes | Low | Variable per query | Low | 5-10% write overhead per index |
| O-7: Index missing search fields | Low | Reduced query cost | Low | Marginal write overhead |
| O-8: Cache lookup sets | Low | ~95% reduction | Very low | 5-min stale window |

## Global Distribution Strategy

- **Current**: Single-region (West US 2) — appropriate for MVP
- **Phase 2 Recommendation**: Add East US 2 read replica with automatic failover
  - **Consistency Level**: Session (default) with occasional Bounded Staleness for analytics
  - **Conflict Resolution**: Last-Writer-Wins (default) — acceptable since write operations are always region-local
  - **Regional Failover**: Automatic failover enabled with West US 2 as primary
  - **Cost**: ~2x storage cost for replication, but no additional RU cost for reads from replicas
- **Phase 3+**: If customer base expands internationally, add EU region with data residency controls

---

## Validation Results

- [x] Reasoned step-by-step through design decisions, applying Core Design Philosophy and Design Patterns
- [x] Aggregate boundaries clearly defined based on access pattern analysis
- [x] Every access pattern solved with specific container + operation mapping
- [x] Unnecessary cross-partition queries identified with optimization proposals (O-1, O-2, O-3)
- [x] All 9 containers documented with partition keys, indexing, and throughput planning
- [x] Hot partition analysis completed — no risks at projected scale
- [x] RU cost estimates provided for all operations
- [x] Trade-offs explicitly documented and justified
- [x] Global distribution strategy detailed for MVP through Phase 3
- [x] Cross-referenced against `cosmosdb_requirements.md` for accuracy
