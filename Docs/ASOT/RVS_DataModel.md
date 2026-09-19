# RVS — Data Model

**Version:** 1.0 · September 4, 2026
**Scope:** Cosmos DB, Blob Storage and Table Storage as actually declared. Verified against `modules/cosmos-db.bicep` and `RVS.Data.Cosmos.Seed/Program.cs`.

Database `rvs-db`, SQL API, serverless in every environment today. Provisioned mode puts autoscale at the database level, not per container. Session consistency is set account-level in `modules/cosmos-db.bicep` (`defaultConsistencyLevel: 'Session'`), not in client code. `CosmosClient` is constructed with `ConnectionMode.Gateway` explicitly in [RVS.API/Program.cs](../../RVS.API/Program.cs) and [RVS.Data.Cosmos.Seed/Program.cs](../../RVS.Data.Cosmos.Seed/Program.cs) — the .NET SDK default is Direct. No integrated cache is configured (it would require a provisioned dedicated gateway, absent from the Bicep).

---

## Containers

**Eleven containers, kebab-case.** Bicep and the seeder agree. Older documentation claimed nine camelCase containers; that was never true of the deployed resource. `intake-invites` is the eleventh (issue #663).

| Container | Partition key | Unique key | Repository |
|---|---|---|---|
| `service-requests` | `/tenantId` | — | Yes |
| `customer-profiles` | `/tenantId` | `/tenantId`, `/email` | Yes |
| `global-customer-accounts` | `/email` | — | Yes |
| `asset-ledger` | `/assetId` | `/assetId`, `/serviceRequestId` | Yes |
| `dealerships` | `/tenantId` | — | Yes — also stores `Tenant` documents (`CosmosTenantRepository`, #563) |
| `locations` | `/tenantId` | `/tenantId`, `/slug` | Yes |
| `slug-lookups` | `/slug` | — | Yes |
| `tenant-configs` | `/tenantId` | — | Yes |
| `lookup-sets` | `/category` | — | Yes |
| `rv-warranty-rules` | `/manufacturer` | — | **None — seeded, never read** |
| `intake-invites` | `/tenantId` | — | Yes (`CosmosIntakeInviteRepository`, #663). **No TTL** |

`service-requests` carries two composite indexes.

Indexing is an explicit include-list on every container, so a new filter needs its path added in both Bicep and the seeder. The seeder only creates containers that don't exist, so an index added to an existing container reaches a deployed environment through Bicep alone. `customer-profiles` indexes `/smsOptOut` for the A-14 opt-out check (#663).

Two containers are partitioned by something other than `tenantId` — `global-customer-accounts` by `/email` and `asset-ledger` by `/assetId`. Both are deliberately cross-tenant, and both are read through repositories that scope explicitly.

**One deliberate cross-partition query over tenant data:** `CosmosTenantRepository.ListAllAsync` reads every `type = 'tenant'` document in `dealerships` across partitions, to list tenants for the platform-admin tool (Spec P-7, #563). It is reachable only through `api/admin/tenants`, behind the `PlatformAdmin` policy (permission plus allowlist). Nothing else lists across tenants.

---

## Entities

All inherit `EntityBase`: `id`, `tenantId`, `type`, `name`, `isEnabled`, plus audit fields (`createdAtUtc`, `createdByUserId`, `updatedAtUtc`, `updatedByUserId`). Identity fields are `init`-only. `MarkAsUpdated(userId)` stamps the update pair. All properties carry `[JsonProperty("camelCase")]`.

### ServiceRequest — `service-requests`

The central document. Field groups:

| Group | Fields | Scope |
|---|---|---|
| Workflow | `status`, `priority`, `boardSequence` | `boardSequence` is Kanban-only — **archived** |
| Issue | issue text, `issueCategory`, `technicianSummary` | Core. `technicianSummary` is the closest thing to a paste block today |
| Customer status note | `customerStatusNote` — `text`, `updatedAtUtc`, `updatedByUserId`; nullable, one per request, overwritten on edit | Core (issue #500, `Spec C-9`). Manager-authored, one-directional; rendered on the customer status page next to the status. `CustomerStatusNoteValidator` caps `text` at 280 chars and rejects `< > ` plus control characters (ordinary punctuation is allowed — it is a human sentence); the text is never written to application logs (same rule as issue text, `Spec X-7`) |
| Customer | embedded `customerSnapshot` — name, email, phone, `preferredContact` (`Phone`/`Text`/`Email`, captured at intake per `#472`; null for pre-existing requests) | Core. `preferredContact` chooses the confirmation channel, and the profile's opt-outs veto it (see *Notification opt-outs* below, `#662`) |
| Asset | `assetInfo` — VIN, make, model, year | Core |
| Attachments | `attachments[]` | Core |
| Diagnostics | `diagnosticResponses[]` | Core — this is the packet's most valuable block |
| AI | `aiEnrichment` metadata | Core |
| Channel | `intakeSource` — normalised `src` from the `go.rvintake.com` redirect (`textrepl`, `quickreply`, `qr`, `print`, or an ad-hoc tag) | Core (issue #599, `Spec A-13`). The authoritative source-of-job record: raw redirect hits say how many links were fetched, this says which channel produced a request. Never blank on a new request — an absent `src` is stored as `print` — and `null` only on requests created before the field existed. Exposed on the detail DTO; grouped by `GET api/locations/{id}/intake-sources` |
| Preliminary assessment | `preliminaryAssessment` — `probableCause`, `possibleFixes[]`, `likelyParts[]`, `confidence` (`high` / `medium` / `low` / `abstain`), `provider`, `generatedAtUtc` | Core (issue #507). Null until the first packet generation, which fills it once; regenerations reuse it. Rendered in the packet's Preliminary assessment section; on `abstain` the other fields are empty and nothing structured renders. Not exposed on any API DTO |
| Packet | `packetGeneration` — `status`, `attemptCount`, `lastAttemptAtUtc`, `lastError`, `generatedAtUtc`, `packetVersion`, `pdfBlobPath`, `alertRaised`, `expectedAttachmentCount` | Core (issue #434). Async packet-generation state; `lastError` never holds customer issue text (`Spec X-7`); `MaxAttempts` = 3 then a `LogCritical` alert. The packet's "short reference code" is **not stored** — it is derived at compose time as the first hyphen-delimited segment of `id`, upper-cased (`#472`). `expectedAttachmentCount` (issue #516) is how many attachments intake declared it was about to upload; generation defers while `attachments` is short of it and the 2-minute window from `createdAtUtc` is open, so a packet is never rendered before the customer's photos land. `0` for every non-intake origin. Preserved across `ResetForRegeneration` |
| Packet delivery | `packetEmailDelivery` — `status`, `attemptCount`, `lastAttemptAtUtc`, `deliveredPacketVersion`, `deliveredAtUtc`, `lastError`, `alertRaised` | Core (issue #438). Idempotent, retried packet-email state; delivery is skipped when `deliveredPacketVersion` already equals the current `packetGeneration.packetVersion` (idempotency per `(serviceRequestId, packetVersion)`, `Spec B-4`); `MaxAttempts` = 3 with exponential backoff, then a `LogCritical` alert; `lastError` never holds customer issue text (`Spec X-7`) |
| Outcome | `serviceEvent` — component, failure mode, repair action, parts, labor | **Archived** — technician workflow |
| Scheduling | `scheduledDateUtc`, `assignedBayId`, `assignedTechnicianId`, `requiredSkills` | **Archived** |
| Messaging | `messages[]` | **Archived** — defined, referenced nowhere in the codebase |

### CustomerProfile — `customer-profiles`

Per-tenant customer record. Contact fields, email/SMS opt-out flags, `assetsOwned[]`, `serviceRequestIds[]`, aggregate counts.

### GlobalCustomerAcct — `global-customer-accounts`

Cross-tenant, partitioned by email. Contact, opt-outs, `linkedProfiles[]`, `allKnownAssetIds[]`, `auth0UserId`, and `magicLinkToken` / expiry.

### Notification opt-outs

`smsOptOut` / `emailOptOut` (each with an `…AtUtc` stamp, set on first opt-out and cleared on opt-in) live on `CustomerProfile` and `GlobalCustomerAcct`, **not** on `ServiceRequest`. Intake writes both from the submission. They are a **hard veto** over `customerSnapshot.preferredContact` (`Spec A-2`, `#577` / `#662`): RVS never sends on an opted-out channel, whatever the preference says.

- **An opted-out channel is never the preference.** `NotificationPreferenceValidator` (Domain) rejects `Text` + `smsOptOut` and `Email` + `emailOptOut`. The intake wizard disables the vetoed radio and clears a conflicting selection; `POST api/intake/{slug}/service-requests` returns **422** for a hand-built request that pairs them. `Phone` is always allowed.
- **Routing.** `NotificationOrchestrator` sends exactly one confirmation: SMS when the preference is `Text` and SMS is permitted (enabled, phone present, not opted out); otherwise email when permitted, with a Warning logged when a `Text` preference fell back; otherwise SMS when permitted; otherwise nothing, logged at Warning. `Phone` and a null preference confirm by email.

This document is what powers both the customer status page and returning-customer prefill. Under the reduced scope its cross-tenant graph (`linkedProfiles`, `allKnownAssetIds`) exists to serve a multi-dealer customer history that the product no longer promises. The token fields are load-bearing. Per the X-5 decision (issue #427, closes Q7), `magicLinkToken` becomes `magicLinkTokenHash` (SHA-256, raw token never stored), TTL drops to ≤ 30 days with sliding renewal, and the status token stays per-customer while C-7 action links are per-request/per-action; see `RVS_Architecture.md` and `RVS_Identity.md`.

### IntakeInvite — `intake-invites`

An advisor-initiated intake invite (`Spec A-14`, issue #663). `id` is `InviteToken.Hash(token)`: lowercase hex SHA-256 of a 32-byte, base64url token (`RVS.Domain/Security/InviteToken.cs`). The raw token is never stored; it exists only in the texted link (or the self-entry URL returned once, on create). Making the hash the id is what makes redemption a point read once `slug-lookups` has given the tenant.

| Field | Meaning |
|---|---|
| `locationId`, `advisorUserId` | Where the invite opens, and who sent it. The resulting request is attributed to the advisor (#664) |
| `firstName`, `phone` | Prefill for the intake form. `phone` is E.164; optional only for self-entry |
| `isSelfEntry` | *Fill it in myself*: minted for the advisor, never texted |
| `consentCapturedAtUtc` | When the advisor confirmed the caller's verbal consent. Separate from `sentAtUtc` and from delivery, never cleared; `null` only for self-entry |
| `sentAtUtc` | When ACS accepted the text; `null` for self-entry or a send that never reached ACS |
| `expiresAtUtc` | `createdAtUtc` + `IntakeInvites:ExpiryHours` (72) |
| `redeemedAtUtc`, `serviceRequestId` | Set on intake **submission**, not on open (#664) |
| `acsMessageId` | The ACS message id; delivery reports are matched back by it (#665) |
| `deliveryStatus` | `pending` → `queued` / `failed`; then `delivered` / `failed` from delivery reports. `notSent` for self-entry |

**No TTL, and never deleted.** The consent fields are the opt-in evidence for toll-free verification and for any complaint, so the document outlives the invite. `expiresAtUtc` retires the token, not the record.

The opt-out check before a send reads `customer-profiles` in the tenant's partition for `smsOptOut = true` and compares each stored phone after E.164 normalisation, because stored phones are as the customer typed them. It does not read `global-customer-accounts` (partitioned by email, so that would be cross-partition).

### AssetLedgerEntry — `asset-ledger`

Append-only. `assetId`, `tenantId`, `serviceRequestId`, `globalCustomerAcctId`, make/model/year, issue, status, `submittedAt`, and an optional `Section10A` outcome block.

Written once per intake submission by `IntakeOrchestrationService`, best-effort — a failure is swallowed and does not roll back the request. Also read back for vehicle prefill.

This is Spec X-2. Nothing else reads it, and that is correct. It exists so the record is there later. The `Section10A` block on it is archived scope; the entry itself is not.

### Location — `locations`

`slug`, address, phone, `intakeConfig`, `enabledCapabilities[]`, `packetConfig`. The slug is the public intake URL segment and is unique per tenant.

`packetConfig` (Spec B-6 / C-6, issue #435) is embedded: `enabled` (bool, default true), `recipients[]` (0–10 email addresses — validated by `PacketConfigValidator`), `disabledRecipients[]` (issue #439 — `{ email, reason, disabledAtUtc }`; addresses parked after a hard bounce, an address is never in both lists), `attachPdf` (default true), `includePhotos` (default true), `pasteBlockCharacterCap` (default 1000, range 100–5000), `statusLinkTtlDays` (default 30, range 1–30), `logoUrl` (optional absolute http(s) URL). Defaults are chosen so a location only needs a recipient address set. Read and written through the existing `api/locations` endpoints (`disabledRecipients` is response-only — carried across a settings save by `LocationService`, and re-enabled by re-adding its address to `recipients`); `Dealership.ServiceEmail` stays dealership-level and unread.

**Coverage:** `pasteBlockCharacterCap` is consumed by the paste-block generator (#436); `enabled`, `recipients`, `attachPdf`, and `includePhotos` are consumed by the packet email send (#437, `PacketGenerationService` → `PacketEmailComposer`), which is now idempotent per `(serviceRequestId, packetVersion)` and retried three times with exponential backoff then a `LogCritical` alert (#438, state on `ServiceRequest.packetEmailDelivery`). `disabledRecipients` is written by `LocationService.DisableRecipientForBounceAsync` (#439) and consumed by the packet email send via the active `recipients` list it drains from. **Gap:** `statusLinkTtlDays` and `logoUrl` have no consumer yet; #439's disable path is built at the service layer but nothing signals a bounce into it yet. `recipients` is bounded at 0–10 rather than the Spec's 1–10 so defaults stay usable before configuration.

### TenantConfig — `tenant-configs`

Id `{tenantId}_config`. `accessGate` (`loginsEnabled`, `disabledReason`, `disabledMessage`, `supportContactEmail`, `disabledAtUtc`) and `availableCapabilities[]`. Read by `TenantAccessGateMiddleware`. The gate is set by the platform-admin tool (`ITenantConfigService.SetAccessGateAsync`, Spec P-4): disabling records `disabledReason` and `disabledAtUtc`, enabling clears both.

### Tenant — `dealerships`

`id == tenantId`, `type = 'tenant'`: `name`, `billingEmail`, `status` (`Pilot` / `Active` / `Churned` — the commercial state, independent of the access gate), `plan` (`mobile` / `location`), `notes`. It is the list of tenants the admin tool reads and holds the billing details for hand-sent invoices. Written only by the platform-admin tool (#563), which also gives the tenant's first documents fixed ids — `dlr_{name}` for the dealership, `loc_{name}_1` for the first location — so a re-submitted provisioning run finds them instead of duplicating them. Read by id single-partition; listed through the one cross-partition query noted under Containers.

### Supporting

`Dealership` and `Tenant` share the `dealerships` container. `slug-lookups` resolves a public slug to a tenant and location without a cross-partition query; `dealershipName` and `locationName` are denormalized onto it for the intake page. A slug is reserved with a create-only write, so a taken slug is a 409 even when two creates race (#563). `lookup-sets` holds controlled vocabulary, partitioned by `/category`.

---

## Controlled vocabulary

At launch the Spec keeps **one** vocabulary: `issue-category`, 13 codes finalised in issue #452 (Slides, Electrical, Plumbing, HVAC, Generator, LPGas, Appliances, Roof, Awning, Chassis, Exterior, Interior, Other). The four technician-side vocabularies — component type, failure mode, repair action, part number — are archived (issue #454). They were never populated at intake; they filled in after a technician closed a job, and that workflow no longer exists.

`lookup-sets` is structured to hold all five, but the seeder (`RVS.Data.Cosmos.Seed`) emits only `issue-category` as of issue #454 — it is the one set that needs seeding and maintenance. The `IssueCategory` set is generated from `RVS.Domain.Validation.IssueCategoryVocabulary`, which is also the list the rule-based categorization fallbacks and `IssueCategoryCapabilityMap` key against; a value outside it is coerced to `Other` before storage.

---

## Blob Storage

Single container `rvs-attachments` on a `Standard_LRS` StorageV2 account, Hot tier, `allowBlobPublicAccess: false`, shared-key access disabled.

Access is entirely SAS-based and time-limited: the browser requests an upload URL, `PUT`s directly to blob with `x-ms-blob-type: BlockBlob`, then confirms. Read access is a per-request SAS, generated on demand and never persisted. Binaries never transit the API — **except** generated packet PDFs, which the API renders in-process (`#434`) and writes server-side to the same container under the `packets/{tenantId}/{serviceRequestId}/v{n}.pdf` prefix; the path and version are recorded on `ServiceRequest.packetGeneration`.

Accepted upload types: jpeg, png, mp4, m4a, wav, pdf. Cap is 10 files, 25 MB each.

CORS on the blob service allows GET/HEAD/PUT from the Static Web App custom domains only.

---

## Table Storage

One table, `intakeRedirectHits`, on the same storage account as the attachment container (`modules/storage-account.bicep`, issue #599). Append-only: the application never updates or deletes a row, and retention is a storage-lifecycle concern rather than application code.

| | |
|---|---|
| PartitionKey | `locationId` — or the literal `unresolved` when the slug did not resolve to a location. Every read is a single-partition query, the same discipline the Cosmos repositories keep |
| RowKey | `{inverted ticks:D19}-{guid:N}`. Table Storage sorts row keys ascending as strings, so inverting the tick count puts the newest hit first and makes "the last N days" a prefix range rather than a scan; the guid suffix keeps hits recorded in the same tick from colliding, which link-preview bursts make a real case |
| Columns | `TenantId`, `Slug`, `Source`, `OccurredAtUtc`, `IsLikelyBot`, `UserAgent` (truncated to 256 chars) |

**Why not Cosmos.** Redirect hits are a high-volume write path read occasionally, and most of them never convert — messaging clients fetch the URL to build a link preview the moment it is composed, possibly once per send. Cosmos would charge request units on every one of those writes to serve a query somebody runs once a month. Here the cost is storage.

**No customer identity.** No IP address, no cookie, no token. The only client-supplied value stored is a truncated User-Agent, kept so the `IsLikelyBot` classification can be re-evaluated later if the preview-fetcher landscape shifts.

Auth is the app's managed identity with **Storage Table Data Contributor**, granted in the same module as the blob roles. When `TableStorage:Endpoint` is unset the API registers `NoOpIntakeRedirectHitRepository` instead: redirects still work and `ServiceRequest.intakeSource` is still recorded, only the conversion denominator is lost.

---

## Descope notes

- `rv-warranty-rules` has no repository and no reader. It is OEM/warranty reference data for an archived capability.
- `ServiceRequest.serviceEvent`, `.messages[]`, and the scheduling/assignment fields are dead weight in the document. They cost storage and read RUs on every fetch.
- Removing fields from Cosmos documents is a migration, not a code edit. Sequence it deliberately; nothing forces it before B ships.
