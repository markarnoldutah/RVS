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
| `global-customer-accounts` | `/email` | — (id derived from the email instead, #679) | Yes |
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
| Workflow | `status`, `priority`, `boardSequence` | `boardSequence` orders cards within a column on the Kanban board, which is kept (#456 closed `not_planned`, Plan decision log Sep 21 2026) — core, not archived |
| Issue | issue text, `issueCategory`, `technicianSummary` | Core. `technicianSummary` is the closest thing to a paste block today |
| Disposition | `disposition` — `reasonCode`, `disposedAtUtc`, `disposedByUserId`; nullable | Core (issue #445, `Spec C-4`). Present only while `status` is `Cancelled`: set with it by the disposition endpoint, dropped when the status moves off `Cancelled`. `reasonCode` is one of `DispositionReasons.All` — `Duplicate`, `Spam`, `WrongLocation`, `CustomerWithdrew`. Manager-only, never on the customer status payload. Existing documents have no field, which reads as `null` |
| Customer status note | `customerStatusNote` — `text`, `updatedAtUtc`, `updatedByUserId`; nullable, one per request, overwritten on edit | Core (issue #500, `Spec C-9`). Manager-authored, one-directional; rendered on the customer status page next to the status. `CustomerStatusNoteValidator` caps `text` at 280 chars and rejects `< > ` plus control characters (ordinary punctuation is allowed — it is a human sentence); the text is never written to application logs (same rule as issue text, `Spec X-7`) |
| Customer | embedded `customerSnapshot` — name, email, phone, `preferredContact` (`Phone`/`Text`/`Email`, captured at intake per `#472`; null for pre-existing requests) | Core. `preferredContact` chooses the confirmation channel, and the profile's opt-outs veto it (see *Notification opt-outs* below, `#662`) |
| Asset | `assetInfo` — VIN, make, model, year | Core |
| Attachments | `attachments[]` | Core |
| Diagnostics | `diagnosticResponses[]` | Core — this is the packet's most valuable block |
| AI | `aiEnrichment` metadata | Core |
| Channel | `intakeSource` — normalised `src` from the `go.rvintake.com` redirect (`textrepl`, `quickreply`, `qr`, `mgrapp`, `print`, or an ad-hoc tag) | Core (issue #599, `Spec A-13`). The authoritative source-of-job record: raw redirect hits say how many links were fetched, this says which channel produced a request. Never blank on a new request — an absent `src` is stored as `print` — and `null` only on requests created before the field existed. Exposed on the detail DTO; grouped by `GET api/locations/{id}/intake-sources`. A submission that redeems an A-14 invite is stored as `advisor` whatever `src` it carried |
| Advisor invite | `intakeInviteId`, `advisorUserId` | Core (issue #664, `Spec A-14`). Set together, only when the submission carried an unexpired, unredeemed invite for this location; `intakeInviteId` is the invite's id (the token hash). `null` for every other request, including one whose invite had expired or been used, which still keeps its `src`. Exposed on the detail DTO |
| Several issues per visit | `submissionId`, `submissionPosition`, `submissionCount` | Core (issue #806, `Spec A-17`). A submission that reports several issues creates one request per issue; every sibling carries its first request's id as `submissionId`, its 1-based position, and the count. All three are unset (`null`, `0`, `0`) for a single-issue submission, so it stores exactly what it did before. `IServiceRequestRepository.GetBySubmissionIdAsync` reads a submission back, single-partition. Exposed on the detail and summary DTOs |
| Preliminary assessment | `preliminaryAssessment` — `probableCause`, `possibleFixes[]`, `likelyParts[]`, `confidence` (`high` / `medium` / `low` / `abstain`), `provider`, `generatedAtUtc`, `photoFindings` (nullable: `dataPlates[]` — `component`, `manufacturer?`, `modelNumber?`, `serialNumber?`, `attachmentId`; `faultCodes[]` — `component`, `code`, `meaning?`, `attachmentId`; `observations[]` — `text`, `attachmentId`) | Core (issue #507). Null until the first packet generation, which fills it once; regenerations reuse it. Rendered in the packet's Preliminary assessment section; on `abstain` the cause, fixes and parts are empty and nothing structured renders. `photoFindings` (issue #772) is what the model read off the photos: ≤ 5 per list, every `attachmentId` one of the request's attachments, and kept on `abstain`. Not exposed on any API DTO |
| Packet | `packetGeneration` — `status`, `attemptCount`, `lastAttemptAtUtc`, `lastError`, `generatedAtUtc`, `packetVersion`, `pdfBlobPath`, `alertRaised`, `expectedAttachmentCount` | Core (issue #434). Async packet-generation state; `lastError` never holds customer issue text (`Spec X-7`); `MaxAttempts` = 3 then a `LogCritical` alert. The packet's "short reference code" is **not stored** — it is derived at compose time as the first hyphen-delimited segment of `id`, upper-cased (`#472`). `expectedAttachmentCount` (issue #516) is how many attachments intake declared it was about to upload; generation defers while `attachments` is short of it and the 2-minute window from `createdAtUtc` is open, so a packet is never rendered before the customer's photos land. `0` for every non-intake origin. Preserved across `ResetForRegeneration` |
| Packet delivery | `packetEmailDelivery` — `status`, `attemptCount`, `lastAttemptAtUtc`, `deliveredPacketVersion`, `deliveredAtUtc`, `lastError`, `alertRaised` | Core (issue #438). Idempotent, retried packet-email state; delivery is skipped when `deliveredPacketVersion` already equals the current `packetGeneration.packetVersion` (idempotency per `(serviceRequestId, packetVersion)`, `Spec B-4`); `MaxAttempts` = 3 with exponential backoff, then a `LogCritical` alert; `lastError` never holds customer issue text (`Spec X-7`) |
| Job type | `jobType` | Core (issue #843, `Spec C-11`). Nullable, operator-set at triage; one of `JobTypes` (`Remote`, `OnSite`, `InShop`, `Inspection`, `Install`, `Other`), anything else rejected with 400. Absent on older documents, which read as null (*Not triaged*); no backfill. Never on the packet, the status page or the ledger entry |
| Scheduling | `scheduledDateUtc`, `assignedTechnicianId`, `requiredSkills` | **Kept** — #459 closed `not_planned` on 2026-09-22. Scheduled date and technician are editable from the detail drawer; `requiredSkills` is carried but has no editor. `scheduledDateUtc` is in scope again (Spec C-12, #841) and is reshaped into a start, a time zone and a time-is-set flag by #844 |
| Scheduling — removed | `assignedBayId` | **Gone** (#713). Removed from the entity, the update/search/detail DTOs, the mapper, the Cosmos search filter and the queue's search panel, once the only surface that wrote it — the retired `ServiceRequestEdit` page — was deleted. Nothing ever populated it in practice, so no document migration was run: any pre-existing document simply keeps an ignored `assignedBayId` property until its next write drops it |
| Messaging | `messages[]` | **Archived** — defined, referenced nowhere in the codebase |

### CustomerProfile — `customer-profiles`

Per-tenant customer record. Contact fields, email/SMS opt-out flags, `assetsOwned[]`, `serviceRequestIds[]`, aggregate counts.

`phone` keeps what the customer typed; `phoneE164` is the same number normalised, or null when it does not normalise, and it is the only form a lookup can match (#665). It is indexed in both `cosmos-db.bicep` and the seeder. `smsKeywordAtUtc` records when the last inbound keyword RVS acted on was *sent*, and an event at or before it is ignored — webhooks can repeat and arrive in no fixed order, so without it a stale `STOP` could undo a later `START`. Twilio's inbound webhook carries no timestamp, so the value is the time RVS received it.

### GlobalCustomerAcct — `global-customer-accounts`

Cross-tenant, partitioned by email. Contact, `linkedProfiles[]`, `auth0UserId`, and `magicLinkToken` / expiry.

### Customer identity: email is the key, phone is not

A customer is identified by email alone, normalised by trimming and lowercasing (#679). There is one `GlobalCustomerAcct` per email and one `CustomerProfile` per email per tenant. Intake reads by email first and creates only on a miss, and each create is guarded because two first submissions can race (a double-tapped Submit, two tabs):

- **`CustomerProfile`**: the `[/tenantId, /email]` unique key rejects the second create.
- **`GlobalCustomerAcct`**: the container has no unique key, and one can't be added to an existing container. Instead a new account's id is `GlobalCustomerAcct.IdForEmail(email)` (`gca_` + SHA-256 hex of the normalised email), so the second create collides on id. Accounts created before #679 keep their GUID ids. Lookups always go by email, so both kinds resolve.

Both repositories turn a Cosmos 409 into `ConflictException`. Intake then re-reads by email and continues with the record that won, applying this submission's phone and opt-outs to it. It fails with a 409 only if the winner can't be read back.

**Phone is deliberately not unique.** Households share numbers, and one person can be a customer of several dealers. `phoneE164` is a lookup key for an inbound STOP, which reaches every profile with the number (#665). It is never an identity. Phone is overwritten on each submission.

**Contact checks at submission (#679).** `POST api/intake/{slug}/service-requests` applies the same rules as the intake wizard and returns **422** with a field error for each failure (`Customer.Email`, `Customer.Phone`, `Customer.PreferredContact`):

- **Email:** `EmailValidator`, a structural check with one `@`, a dotted domain and no blocked characters.
- **Phone:** `PhoneValidator`: required whatever the preference, at least 10 digits, at most 40 characters.

Both validators live in `RVS.Domain/Validation`, and the wizard calls the same ones, so the two sides can't drift. The phone rule is deliberately looser than `PhoneNumberNormalizer`: a number that doesn't normalise is still one the shop can call. It is kept as typed, with `phoneE164` left null, and it only drops out of SMS.

### Notification opt-outs

`smsOptOut` / `emailOptOut` (each with an `…AtUtc` stamp, set on first opt-out and cleared on opt-in) live on `CustomerProfile`, **not** on `ServiceRequest`. **Intake only sets them (#673):** a ticked box sets the flag and stamps `…AtUtc` if unset (`CustomerProfile.ApplyIntakeOptOuts`); an unticked box leaves the stored value alone, because the form never shows it. Only an inbound `START` / `UNSTOP` clears `smsOptOut`; nothing clears `emailOptOut` yet. `GlobalCustomerAcct` does not carry them: #673 stopped writing them there, and the properties themselves were later removed, because nothing read that copy. An account written before then keeps the stale fields until intake next replaces the document, and nothing reads them. They are a **hard veto** over `customerSnapshot.preferredContact` (`Spec A-2`, `#577` / `#662`): RVS never sends on an opted-out channel, whatever the preference says.

- **An opted-out channel is never the preference — within one submission.** `NotificationPreferenceValidator` (Domain) rejects `Text` + `smsOptOut` and `Email` + `emailOptOut` as ticked in that request. A stored opt-out is not checked: the customer can't see it, so the submission is accepted and routing falls back. The intake wizard disables the vetoed radio and clears a conflicting selection; `POST api/intake/{slug}/service-requests` returns **422** for a hand-built request that pairs them. `Phone` is always allowed.
- **Routing.** Intake passes the profile's opt-outs *after* the write, not the submission's boxes. `NotificationOrchestrator` sends exactly one confirmation: SMS when the preference is `Text` and SMS is permitted (enabled, phone present, not opted out); otherwise email when permitted, with a Warning logged when a `Text` preference fell back; otherwise SMS when permitted; otherwise nothing, logged at Warning. `Phone` and a null preference confirm by email.

This document is what powers the customer status page. Under the reduced scope its cross-tenant graph (`linkedProfiles`) exists to serve a multi-dealer customer history that the product no longer promises. The token fields are load-bearing. Per the X-5 decision (issue #427, closes Q7), `magicLinkToken` becomes `magicLinkTokenHash` (SHA-256, raw token never stored), TTL drops to ≤ 30 days with sliding renewal, and the status token stays per-customer while C-7 action links are per-request/per-action; see `RVS_Architecture.md` and `RVS_Identity.md`.

### IntakeInvite — `intake-invites`

An advisor-initiated intake invite (`Spec A-14`, issue #663). `id` is `InviteToken.Hash(token)`: lowercase hex SHA-256 of a 32-byte, base64url token (`RVS.Domain/Security/InviteToken.cs`). The raw token is never stored; it exists only in the texted or emailed link (or the self-entry URL returned once, on create). Making the hash the id is what makes redemption a point read once `slug-lookups` has given the tenant.

| Field | Meaning |
|---|---|
| `locationId`, `advisorUserId` | Where the invite opens, and who sent it. The resulting request is attributed to the advisor (#664) |
| `firstName`, `phone`, `email` | Prefill for the intake form. `phone` is E.164, required for a texted invite; `email` is trimmed and lower-cased like `CustomerProfile.email`, required for an emailed one (#693). Either is optional otherwise, and validated when given |
| `channel` | `sms` or `email` (`IntakeInviteChannel`, #693). Absent on invites written before #693, which read as `sms`; meaningless for self-entry |
| `isSelfEntry` | *Fill it in myself*: minted for the advisor, never sent |
| `consentCapturedAtUtc` | When the advisor confirmed the caller's verbal consent to the text or email. Separate from `sentAtUtc` and from delivery, never cleared; `null` only for self-entry |
| `sentAtUtc` | When Twilio or SendGrid accepted the text or email; `null` for self-entry or a send that never reached the provider |
| `expiresAtUtc` | `createdAtUtc` + `IntakeInvites:ExpiryHours` (72) |
| `redeemedAtUtc`, `serviceRequestId` | Set on intake **submission**, not on open (#664): link previews fetch the URL, and redeeming on open would spend the invite before the customer tapped it. Written after the service request is created; a failed write is logged and the submission stands. `IntakeInvite.IsRedeemableAt` (unredeemed and `expiresAtUtc` in the future) gates both prefill and attribution |
| `providerMessageId` | The Twilio Message SID, which status callbacks are matched back by (#665), or the SendGrid message id for an emailed invite, which nothing reads yet. Was `acsMessageId` until Oct 2 2026; no data was migrated (prod held test tenants only) |
| `deliveryStatus` | `pending` → `queued` / `failed`; then `delivered` / `failed` from SMS delivery reports. An emailed invite stops at `queued` or `failed`. `notSent` for self-entry |

**No TTL, and never deleted.** The consent fields are the opt-in evidence for toll-free verification and for any complaint, so the document outlives the invite. `expiresAtUtc` retires the token, not the record.

The opt-out check before a text reads `customer-profiles` in the tenant's partition for `smsOptOut = true` and compares each stored phone after E.164 normalisation, because stored phones are as the customer typed them. Before an email it reads the tenant's profile for that address with `GetByEmailAsync` (stored lower-cased, so one lookup) and refuses on `emailOptOut`. It does not read `global-customer-accounts` (partitioned by email, so that would be cross-partition).

**Inbound keywords write across tenants (#665).** `STOP` and its synonyms set `smsOptOut`; `START` and `UNSTOP` clear it, and a keyword is the only thing that clears it, since intake sets an opt-out but never clears one (#673). The keyword arrives with a phone number and no tenant, and the toll-free sending number is shared, so `ListByPhoneE164AcrossTenantsAsync` matches `phoneE164` in **every** tenant's partition and each matching profile is updated. Scoping it to one tenant would leave the other dealers texting into a carrier block. Keyword traffic is rare and the result is bounded by how many dealers know one customer. A number matching no profile is a no-op: the carrier still enforces its own block. `HELP` writes nothing at all — Twilio answers it with a fixed reply and it is neither consent nor a revocation, so it needs no profile and does not move `smsKeywordAtUtc`. Status callbacks match an invite through `GetByProviderMessageIdAcrossTenantsAsync` on the indexed `providerMessageId`, for the same reason — a callback carries no tenant.

### AssetLedgerEntry — `asset-ledger`

Append-only. `assetId`, `tenantId`, `serviceRequestId`, `globalCustomerAcctId`, make/model/year, issue, status and `submittedAt`.

`assetId` is the vehicle-history key from `VehicleHistoryKey.For` (Spec A-3): the VIN, or `MANUFACTURER:SERIAL` (e.g. `LANCE:152263`) for a rig with only a serial number (issue #808). The same key is used for `CustomerProfile.assetsOwned[].assetId`. `ServiceRequest.assetInfo.assetId` is not a key: it holds the VIN or serial number as entered. A serial number with no usable manufacturer gets no key, so no entry or ownership record. Entries written under #807 are not backfilled.

Written once per intake submission by `IntakeOrchestrationService`, best-effort — a failure is swallowed and does not roll back the request. Written, never read. Its one reader, A-7's token-based vehicle prefill, was removed when A-7 was cancelled (#815). That is still correct under X-2, which exists so the record is there later.

This is Spec X-2. Nothing else reads it, and that is correct. It exists so the record is there later. The optional `section10A` outcome block it used to carry was archived scope and was removed in #457; the entry itself stays.

### Location — `locations`

`slug`, address, phone, `timeZoneId`, `intakeConfig`, `enabledCapabilities[]`, `packetConfig`. The slug is the public intake URL segment and is unique per tenant.

`timeZoneId` (issue #506) is an optional IANA id such as `America/Denver`. It exists so the service packet's `Received` line reads in the time the service advisor actually took the request; `null` — every location created before #506 — leaves that line in UTC. Validated by `TimeZoneValidator` (blank is legal; otherwise a zone the host resolves, or one of the eleven in `DealershipTimeZones`, whose curated short spellings the packet renders). Set from the Manager app's location drawer; an empty string on update clears it, `null` leaves it unchanged.

`packetConfig` (Spec B-6 / C-6, issue #435) is embedded: `enabled` (bool, default true), `recipients[]` (0–10 email addresses — validated by `PacketConfigValidator`), `attachPdf` (default true), `includePhotos` (default true), `pasteBlockCharacterCap` (default 2500, range 100–5000), `statusLinkTtlDays` (default 30, range 1–30). A legacy `logoUrl` here is read only to migrate it into `branding` (below) and is never written back. Defaults are chosen so a location only needs a recipient address set. Read and written through the existing `api/locations` endpoints; `Dealership.ServiceEmail` stays dealership-level and unread.

**Coverage:** `pasteBlockCharacterCap` is consumed by the paste-block generator (#436); `enabled`, `recipients`, `attachPdf`, and `includePhotos` are consumed by the packet email send (#437, `PacketGenerationService` → `PacketEmailComposer`), which is now idempotent per `(serviceRequestId, packetVersion)` and retried three times with exponential backoff then a `LogCritical` alert (#438, state on `ServiceRequest.packetEmailDelivery`). **Gap:** `statusLinkTtlDays` has no consumer yet; `recipients` is bounded at 0–10 rather than the Spec's 1–10 so defaults stay usable before configuration.

`branding` (Spec A-16, issue #470) is embedded: `logoUrl` (optional absolute **https** URL, ≤ 2048 chars), `headerColor` (optional `#RRGGBB`, upper-cased on write) and `accentColor` (optional `#RRGGBB`, upper-cased on write and stored as entered; a colour under 4.5:1 against white is darkened when rendered, not rejected — `LocationBrandingValidator.EffectiveAccent`), all validated by `LocationBrandingValidator`. Empty — the default — means RV Intake's own mark and Denim header. **Migration:** before #470 the logo lived at `packetConfig.logoUrl` (#435). `Location` folds it into `branding.logoUrl` on deserialisation (`[OnDeserialized]`) when branding has none and the value is https, and never writes `packetConfig.logoUrl` back, so a location's next save completes the move; an `http` legacy value is dropped rather than carried into a field that would then fail validation. No backfill script: prod holds test tenants only. Read and written through `api/locations` (a null `branding` on update leaves it unchanged; a non-null one replaces it); returned to the anonymous intake form on `IntakeConfigResponseDto.branding`. **Coverage:** `logoUrl` is consumed by the Intake header and Step 0 and by the packet (`PacketGenerationService` fetches it for the PDF); `headerColor` by the Intake header; `accentColor` by the Intake theme. `Dealership.logoUrl` is separate, older and still unread.

`dealerQuestions` (Spec A-18, issue #785) is a list of 0–2 strings, each ≤ 200 characters, non-blank and distinct ignoring case — validated by `DealerQuestionsValidator`; the mapper trims them and drops blank entries. Empty by default, and a document written before #785 reads as empty. Read and written through `api/locations` (a null `dealerQuestions` on update leaves them unchanged; a non-null list replaces them); returned to the anonymous intake form on `IntakeConfigResponseDto.dealerQuestions`, where Step 6 appends them to the AI's questions. Answers land in the request's existing `diagnosticResponses`, keyed by question text like the AI's; nothing marks which questions were the dealer's.

### TenantConfig — `tenant-configs`

Id `{tenantId}_config`. `accessGate` (`loginsEnabled`, `disabledReason`, `disabledMessage`, `supportContactEmail`, `disabledAtUtc`) and `availableCapabilities[]`. Read by `TenantAccessGateMiddleware`. The gate is set by the platform-admin tool (`ITenantConfigService.SetAccessGateAsync`, Spec P-4): disabling records `disabledReason` and `disabledAtUtc`, enabling clears both.

### Tenant — `dealerships`

`id == tenantId`, `type = 'tenant'`: `name`, `billingEmail`, `status` (`Pilot` / `Active` / `Churned` — the commercial state, independent of the access gate), `plan` (`mobile` / `location`), `notes`. It is the list of tenants the admin tool reads and holds the billing details for hand-sent invoices. Written only by the platform-admin tool (#563), which also gives the tenant's first documents fixed ids — `dlr_{name}` for the dealership, `loc_{name}_1` for the first location — so a re-submitted provisioning run finds them instead of duplicating them. Read by id single-partition; listed through the one cross-partition query noted under Containers.

### Supporting

`Dealership` and `Tenant` share the `dealerships` container. `slug-lookups` resolves a public slug to a tenant and location without a cross-partition query; `dealershipName` and `locationName` are denormalized onto it for the intake page. A slug is reserved with a create-only write, so a taken slug is a 409 even when two creates race (#563). `lookup-sets` holds controlled vocabulary, partitioned by `/category`.

---

## Controlled vocabulary

The Spec keeps **two** vocabularies. One is `issue-category`, 13 codes finalised in issue #452 (Slides, Electrical, Plumbing, HVAC, Generator, LPGas, Appliances, Roof, Awning, Chassis, Exterior, Interior, Other). The four technician-side vocabularies — component type, failure mode, repair action, part number — are archived (issue #454). They were never populated at intake; they filled in after a technician closed a job, and that workflow no longer exists. The other is `job-types` (issue #843, `Spec C-11`): six codes, set by the operator rather than the customer.

`lookup-sets` is structured to hold all five, but the seeder (`RVS.Data.Cosmos.Seed`) emits only `issue-categories` and `job-types`. The `JobType` set is generated from `RVS.Domain.Validation.JobTypes`, which the API also validates against. The `IssueCategory` set is generated from `RVS.Domain.Validation.IssueCategoryVocabulary`, which is also the list the rule-based categorization fallbacks and `IssueCategoryCapabilityMap` key against; a value outside it is coerced to `Other` before storage.

---

## Blob Storage

Single container `rvs-attachments` on a `Standard_LRS` StorageV2 account, Hot tier, `allowBlobPublicAccess: false`, shared-key access disabled.

Access is entirely SAS-based and time-limited: the browser requests an upload URL, `PUT`s directly to blob with `x-ms-blob-type: BlockBlob`, then confirms. Read access is a per-request SAS, generated on demand and never persisted. Binaries never transit the API — **except** generated packet PDFs, which the API renders in-process (`#434`) and writes server-side to the same container under the `packets/{tenantId}/{serviceRequestId}/v{n}.pdf` prefix; the path and version are recorded on `ServiceRequest.packetGeneration`.

Accepted upload types: jpeg, png, mp4, m4a, wav, pdf. Cap is 10 files, 25 MB each.

CORS on the blob service allows GET/HEAD/PUT from the Static Web App custom domains only.

---

## Table Storage

Two tables, `intakeRedirectHits` (issue #599) and `intakeFormStarts` (issue #839), on the same storage account as the attachment container (`modules/storage-account.bicep`). Append-only: the application never updates or deletes a row, and retention is a storage-lifecycle concern rather than application code.

### `intakeRedirectHits`

| | |
|---|---|
| PartitionKey | `locationId` — or the literal `unresolved` when the slug did not resolve to a location. Every read is a single-partition query, the same discipline the Cosmos repositories keep |
| RowKey | `{inverted ticks:D19}-{guid:N}`. Table Storage sorts row keys ascending as strings, so inverting the tick count puts the newest hit first and makes "the last N days" a prefix range rather than a scan; the guid suffix keeps hits recorded in the same tick from colliding, which link-preview bursts make a real case |
| Columns | `TenantId`, `Slug`, `Source`, `OccurredAtUtc`, `IsLikelyBot`, `UserAgent` (truncated to 256 chars) |

**Why not Cosmos.** Redirect hits are a high-volume write path read occasionally, and most of them never convert — messaging clients fetch the URL to build a link preview the moment it is composed, possibly once per send. Cosmos would charge request units on every one of those writes to serve a query somebody runs once a month. Here the cost is storage.

**No customer identity.** No IP address, no cookie, no token. The only client-supplied value stored is a truncated User-Agent, kept so the `IsLikelyBot` classification can be re-evaluated later if the preview-fetcher landscape shifts.

Auth is the app's managed identity with **Storage Table Data Contributor**, granted in the same module as the blob roles. When `TableStorage:Endpoint` is unset the API registers `NoOpIntakeRedirectHitRepository` instead: redirects still work and `ServiceRequest.intakeSource` is still recorded, only the conversion denominator is lost.

### `intakeFormStarts`

One row per Intake visit that reaches Step 1 with the form showing: the denominator of the completion rate (Spec A-13, "Completion rate"). Written by `POST api/intake/{slug}/starts` → `IntakeFormStartService` → `AzTableIntakeFormStartRepository`, which follows the redirect-hit repository: same keys, same create-on-404 retry, `NoOpIntakeFormStartRepository` when `TableStorage:Endpoint` is unset. The endpoint answers `204` whatever it is sent.

| | |
|---|---|
| PartitionKey | `locationId`. Only resolved, unexpired slugs are recorded, so there is no `unresolved` partition |
| RowKey | `{inverted ticks:D19}-{guid:N}`, as for redirect hits |
| Columns | `TenantId`, `Slug`, `Source` (normalised `src`, so completion rate splits by channel), `SessionId`, `OccurredAtUtc`, `IsLikelyBot`, `UserAgent` (truncated to 256 chars) |

**`SessionId`** is a random per-tab id the Intake app generates on the first Step 1 render and keeps in `sessionStorage` with the wizard state, so a refresh or Back into Step 1 is the same visit; a cleared session (after a submission, or a link to another location) is a new one. Count distinct `SessionId`, not rows: if storage is blocked, a refresh can write a second row with a new id, and nothing stops a client posting twice. The API drops any id that is not 8–64 letters, digits, `-` or `_`. Like the redirect hits, a row holds no customer identity.

---

## Descope notes

- `rv-warranty-rules` has no repository and no reader. It is OEM/warranty reference data for an archived capability.
- `ServiceRequest.messages[]`, `assignedTechnicianId` and `requiredSkills` are dead weight in the document (`scheduledDateUtc` is not; Spec C-12). They cost storage and read RUs on every fetch.
- `ServiceRequest.serviceEvent` and `AssetLedgerEntry.section10A` (technician outcome capture) were removed from the entities in #457. A document written before then may still carry the property: the Newtonsoft serializer ignores unknown members on read, so it is harmless, and a service request drops it on its next write. Ledger entries are never rewritten, so theirs stays. Re-running the seeder upserts the seed documents without either. Production had no tenants when this shipped, so nothing there carries it.
- `GlobalCustomerAcct.allKnownAssetIds` was removed in #815 with A-7's token-based prefill, its only reader. An account written before then keeps the field until intake next replaces the document; the Newtonsoft serializer ignores it on read. No backfill: production had test tenants only.
- Removing fields from Cosmos documents is a migration, not a code edit. Sequence it deliberately; nothing forces it before B ships.
