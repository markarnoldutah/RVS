# RVS — Data Model

**Version:** 1.0 · September 4, 2026
**Scope:** Cosmos DB and Blob Storage as actually declared. Verified against `modules/cosmos-db.bicep` and `RVS.Data.Cosmos.Seed/Program.cs`.

Database `rvs-db`, SQL API, Session consistency, serverless in every environment today. Provisioned mode puts autoscale at the database level, not per container. `ConnectionMode.Gateway`.

---

## Containers

**Ten containers, kebab-case.** Bicep and the seeder agree. Older documentation claimed nine camelCase containers; that was never true of the deployed resource.

| Container | Partition key | Unique key | Repository |
|---|---|---|---|
| `service-requests` | `/tenantId` | — | Yes |
| `customer-profiles` | `/tenantId` | `/tenantId`, `/email` | Yes |
| `global-customer-accounts` | `/email` | — | Yes |
| `asset-ledger` | `/assetId` | `/assetId`, `/serviceRequestId` | Yes |
| `dealerships` | `/tenantId` | — | Yes — also stores `Tenant` documents |
| `locations` | `/tenantId` | `/tenantId`, `/slug` | Yes |
| `slug-lookups` | `/slug` | — | Yes |
| `tenant-configs` | `/tenantId` | — | Yes |
| `lookup-sets` | `/category` | — | Yes |
| `rv-warranty-rules` | `/manufacturer` | — | **None — seeded, never read** |

`service-requests` carries two composite indexes. Everything else uses default indexing.

Two containers are partitioned by something other than `tenantId` — `global-customer-accounts` by `/email` and `asset-ledger` by `/assetId`. Both are deliberately cross-tenant. They are the only places where a query is not tenant-scoped by partition, and both are read through repositories that scope explicitly.

---

## Entities

All inherit `EntityBase`: `id`, `tenantId`, `type`, `name`, `isEnabled`, plus audit fields (`createdAtUtc`, `createdByUserId`, `updatedAtUtc`, `updatedByUserId`). Identity fields are `init`-only. `MarkAsUpdated(userId)` stamps the update pair. All properties carry `[JsonProperty("camelCase")]`.

### ServiceRequest — `service-requests`

The central document. Field groups:

| Group | Fields | Scope |
|---|---|---|
| Workflow | `status`, `priority`, `boardSequence` | `boardSequence` is Kanban-only — **archived** |
| Issue | issue text, `issueCategory`, `technicianSummary` | Core. `technicianSummary` is the closest thing to a paste block today |
| Customer | embedded `customerSnapshot` | Core |
| Asset | `assetInfo` — VIN, make, model, year | Core |
| Attachments | `attachments[]` | Core |
| Diagnostics | `diagnosticResponses[]` | Core — this is the packet's most valuable block |
| AI | `aiEnrichment` metadata | Core |
| Packet | `packetGeneration` — `status`, `attemptCount`, `lastAttemptAtUtc`, `lastError`, `generatedAtUtc`, `packetVersion`, `pdfBlobPath`, `alertRaised` | Core (issue #434). Async packet-generation state; `lastError` never holds customer issue text (`Spec X-7`); `MaxAttempts` = 3 then a `LogCritical` alert |
| Outcome | `serviceEvent` — component, failure mode, repair action, parts, labor | **Archived** — technician workflow |
| Scheduling | `scheduledDateUtc`, `assignedBayId`, `assignedTechnicianId`, `requiredSkills` | **Archived** |
| Messaging | `messages[]` | **Archived** — defined, referenced nowhere in the codebase |

### CustomerProfile — `customer-profiles`

Per-tenant customer record. Contact fields, email/SMS opt-out flags, `assetsOwned[]`, `serviceRequestIds[]`, aggregate counts.

### GlobalCustomerAcct — `global-customer-accounts`

Cross-tenant, partitioned by email. Contact, opt-outs, `linkedProfiles[]`, `allKnownAssetIds[]`, `auth0UserId`, and `magicLinkToken` / expiry.

This document is what powers both the customer status page and returning-customer prefill. Under the reduced scope its cross-tenant graph (`linkedProfiles`, `allKnownAssetIds`) exists to serve a multi-dealer customer history that the product no longer promises. The token fields are load-bearing. Per the X-5 decision (issue #427, closes Q7), `magicLinkToken` becomes `magicLinkTokenHash` (SHA-256, raw token never stored), TTL drops to ≤ 30 days with sliding renewal, and the status token stays per-customer while C-7 action links are per-request/per-action; see `RVS_Architecture.md` and `RVS_Identity.md`.

### AssetLedgerEntry — `asset-ledger`

Append-only. `assetId`, `tenantId`, `serviceRequestId`, `globalCustomerAcctId`, make/model/year, issue, status, `submittedAt`, and an optional `Section10A` outcome block.

Written once per intake submission by `IntakeOrchestrationService`, best-effort — a failure is swallowed and does not roll back the request. Also read back for vehicle prefill.

This is Spec X-2. Nothing else reads it, and that is correct. It exists so the record is there later. The `Section10A` block on it is archived scope; the entry itself is not.

### Location — `locations`

`slug`, address, phone, `intakeConfig`, `enabledCapabilities[]`. The slug is the public intake URL segment and is unique per tenant.

**Gap:** Spec B-6 requires per-location packet configuration — recipient list, attach-PDF, include-photos, paste-block cap, status-link TTL, logo. None of it exists. Today the only email address in the model is `Dealership.ServiceEmail`, which is dealership-level and read by no code.

### TenantConfig — `tenant-configs`

`accessGate` (`loginsEnabled`, `disabledReason`) and `availableCapabilities[]`. Read by `TenantAccessGateMiddleware`.

### Supporting

`Dealership` and `Tenant` share the `dealerships` container. `slug-lookups` resolves a public slug to a tenant and location without a cross-partition query. `lookup-sets` holds controlled vocabulary, partitioned by `/category`.

---

## Controlled vocabulary

At launch the Spec keeps **one** vocabulary: `issue-category`, roughly 10–14 codes. The four technician-side vocabularies — component type, failure mode, repair action, part number — are archived. They were never populated at intake; they filled in after a technician closed a job, and that workflow no longer exists.

`lookup-sets` is structured to hold all five. Only `issue-category` needs seeding and maintenance.

---

## Blob Storage

Single container `rvs-attachments` on a `Standard_LRS` StorageV2 account, Hot tier, `allowBlobPublicAccess: false`, shared-key access disabled.

Access is entirely SAS-based and time-limited: the browser requests an upload URL, `PUT`s directly to blob with `x-ms-blob-type: BlockBlob`, then confirms. Read access is a per-request SAS, generated on demand and never persisted. Binaries never transit the API — **except** generated packet PDFs, which the API renders in-process (`#434`) and writes server-side to the same container under the `packets/{tenantId}/{serviceRequestId}/v{n}.pdf` prefix; the path and version are recorded on `ServiceRequest.packetGeneration`.

Accepted upload types: jpeg, png, mp4, m4a, wav, pdf. Cap is 10 files, 25 MB each.

CORS on the blob service allows GET/HEAD/PUT from the Static Web App custom domains only.

---

## Descope notes

- `rv-warranty-rules` has no repository and no reader. It is OEM/warranty reference data for an archived capability.
- `ServiceRequest.serviceEvent`, `.messages[]`, and the scheduling/assignment fields are dead weight in the document. They cost storage and read RUs on every fetch.
- Removing fields from Cosmos documents is a migration, not a code edit. Sequence it deliberately; nothing forces it before B ships.
