# RVS — Infrastructure

**Version:** 1.3 · September 12, 2026
**Scope:** Azure resources and CI/CD as declared. The Bicep in `Infra/Bicep.IaC/` is the source of truth; this document explains it. Where they disagree, the Bicep is right.

`main.bicep` targets subscription scope. Primary region **westus3**, Whisper **northcentralus**, Static Web Apps **westus2**, ACS global.

---

## What deploys, and what gates it

**Unconditional — deployed by all four parameter sets:**

| Resource | Module |
|---|---|
| Resource groups `rg-rvs-{env}-westus3`, `rg-rvs-{env}-ncus` | inline |
| Azure OpenAI account + `gpt-4o` deployment | `openai.bicep` |
| Azure OpenAI account + `whisper` deployment (ncus) | `openai-whisper.bicep` |

**Flag-gated:**

| Resource | Module | Gate |
|---|---|---|
| RG `rg-rvs-{env}-westus2` | inline | `deploySwa` |
| App Service Plan + Web App (+ staging slot) | `app-service.bicep` | `deployAppService`; slot only when SKU is `S1` |
| Web App appsettings | `app-service-config.bicep` | `deployAppService` |
| Log Analytics workspace | `log-analytics.bicep` | `deployObservability` |
| App Insights + availability webtest | `app-insights.bicep` | `deployObservability`; webtest also needs `deployAvailabilityTest && deployAppService` |
| Ops action group + 5 packet-pipeline alert rules | `monitor-alerts.bicep` | `deployObservability` |
| Key Vault + role assignments | `key-vault.bicep` | `deployKeyVault` |
| Cosmos account, database, 10 containers | `cosmos-db.bicep` | `deployCosmosDb` |
| Storage account, CORS, `rvs-attachments`, 4 role assignments | `storage-account.bicep` | `deployStorageAccount` |
| ACS + Email Service + managed domain + 2 role assignments (+ a `CustomerManaged` sending domain when `acsCustomEmailDomain` is set) | `communication-services.bicep` | `deployAcs`; role assignments only when an App Service principal is supplied; custom domain in staging and prod (`#532`) |
| Two Static Web Apps + custom domains | `static-web-app.bicep` | `deploySwa` |
| DNS zones + record sets | `dns.bicep` | `deploySwa && deployDns` |
| DNS Zone Contributor grants | `dns-zone-contributor.bicep` | `deploySwa && deployDns && env=='prod' && principals supplied` |
| Key Vault secrets | `*-keyvault-secrets.bicep` | `deployKeyVault` plus the paired resource flag |

---

## Environments

All three parameter files set every deploy flag to `true`, `cosmosCapacityMode='Serverless'`, `storageAllowSharedKeyAccess=false`, `swaSkuName='Standard'`, `swaLocation='westus2'`. The differences are small:

| | `staging` | `prod` | `prod_basic` |
|---|---|---|---|
| App Service SKU | `B1` | `S1` | `B1` |
| Staging slot | no | yes | no |
| OpenAI capacity | 10 | 30 | 30 |
| Whisper capacity | 1 | 2 | 2 |
| Storage CORS origins | set | set | default |
| DNS RBAC principals | — | set | — |
| ACS custom sending domain | `mail.staging.rvintake.com` | `mail.rvintake.com` | `mail.rvintake.com` |

Every file is deployable as committed. Prod is a single file with no phases; the two things it leaves to a human are the one-time registration of the `rvintake.com` apex with the Intake SWA (a token Azure mints at registration time) and the data-plane verification + quota bump + warming of the `mail.rvintake.com` sending domain (`#532`) — both sequences are in `Infra/Bicep.IaC/README.md` "Deploy Production" (steps 2 and 4). Redeploys never touch either. Staging's `mail.staging.rvintake.com` needs the same verification, but no quota request and no warming.

Two things to know before using these:

- **Prod has not been deployed yet** (as of 2026-09-10 the prod resource groups hold only the two DNS zones, which the staging deploy created). The first prod run is the README sequence, start to finish. The northcentralus Whisper quota is 3 units subscription-wide; `staging = 1` + `prod = 2` fits it exactly with no headroom, so if staging is still deployed at its old `whisperCapacity = 3` it must be redeployed before the prod pre-flight will pass.
- **No parameter file sets the Auth0 values.** `auth0Domain`, `auth0Audience`, `auth0ClientId`, `auth0ClientSecret` are empty, so `auth0-keyvault-secrets.bicep` never runs from a param file. Those secrets must be passed on the CLI or written to the vault by hand.

---

## SKUs and configuration

**App Service** — Linux, `DOTNETCORE|10.0`, `httpsOnly`, TLS 1.2, FTPS disabled, HTTP/2 on. `healthCheckPath=/health` on anything above F1. `alwaysOn` and deployment slots only on S1.

**Cosmos** — Standard offer, Session consistency, `EnableServerless`, single region westus3, not zone-redundant, continuous backup, TLS 1.2, system-assigned identity. `publicNetworkAccess: Enabled`, `disableLocalAuth: false` — consumed by key, not RBAC.

**Storage** — StorageV2, `Standard_LRS`, Hot, TLS 1.2, `allowBlobPublicAccess: false`, network ACL default `Allow`.

**Azure OpenAI** — both accounts `kind: OpenAI`, SKU `S0`, custom subdomain, system-assigned identity. `gpt-4o` version `2024-11-20`; Whisper model `whisper` version `001`. Whisper is in northcentralus because Whisper 001 Standard is not offered in westus3.

**Optional assessment-only deployment (`#584`).** `openai.bicep`'s `additionalDeployments` array can add further model deployments on the primary account beyond `gpt-4o`, keyed by `assessmentModelName` in `main.bicep` — staging and prod both set this to `gpt-5` (`assessmentDeploymentCapacity` 1 / 2 K TPM), used only by the packet preliminary assessment via the `AzureOpenAi--AssessmentDeploymentName` Key Vault secret; categorization and issue-text refinement stay on `gpt-4o` via `TextDeploymentName`. `gpt-5` deploys under SKU `DataZoneStandard` (US), not the `Standard` regional SKU `gpt-4o` uses — that SKU isn't offered for it. Blank `assessmentModelName` and redeploy to revert the assessment call to `gpt-4o` with no application-code change.

**ACS** — location `global`, data location United States, engagement tracking disabled. Email Service with an Azure-managed domain in every environment; staging and prod each also link a `CustomerManaged` sending subdomain — `mail.staging.rvintake.com` and `mail.rvintake.com` (`acsCustomEmailDomain`, `#532`) — and send the packet email From it.

**ACS Email quotas and the managed-domain ceiling (`#521`, `#532`).** An Azure-managed domain is a trial tier, not a small custom one — Microsoft's published limits:

| | Azure-managed domain (fallback only) | Verified custom domain (staging, prod) |
|---|---|---|
| Send rate | **5 emails/min, 10 emails/hour** | 30/min, 100/hour out of the box |
| Raisable via support? | **No** | Yes, up to 1–2 M/hour |
| Request size incl. attachments | 10 MB (≈7.5 MB raw, base64 inflates ~33%) | 10 MB, up to 30 MB on request |

Two consequences worth stating plainly:

- **On the managed domain, ten packets an hour is the hard ceiling and no support ticket lifts it.** Higher quotas are available only for verified custom domains. Neither deployed environment sends from the managed domain; it stays linked only as a fallback.
- **Prod deploys the custom sending subdomain via Bicep (`#532`).** `communication-services.bicep` provisions the `CustomerManaged` domain and emits the records it needs; `main.bicep` writes **SPF** (`… -all`), **DKIM** + **DKIM2** (CNAME), a domain-ownership TXT, and a **DMARC** `p=none` record with `rua` reporting into the `rvintake.com` zone. Verification (`initiate-verification`), the quota-increase request (72 h lead, bounce rate < 1 %), and 2–3 weeks of domain warming on Jay Lyons's real traffic (`#525`) before any other shop's mailbox sees a packet are the manual follow-up — README "Deploy Production" step 4, surfaced by the `acsCustomDomainAction` output. The in-room deliverability check for later pilots is `FS-7` in `RVS_Plan.md`.

Prod's warmed custom domain is what carries the local cluster (`#527`).

**Each environment keeps its own ACS resource and sending subdomain (`#532`).** Staging sends From `mail.staging.rvintake.com` on `acs-rvs-notify-staging-wus3-s01-001`, and local development borrows that resource too. One prod resource shared by every environment was considered and rejected, for three reasons:

- **Bounce budget.** ACS tracks failures, the suppression list and send quota per resource and domain. Staging fails many sends, because every seeded packet recipient is an undeliverable `.example.com` address. On a shared resource those failures would count against `mail.rvintake.com` while it warms and while the quota request, which needs bounces under 1 %, is pending.
- **Access to prod.** ACS has no narrower send role, so a shared resource would give staging's app identity, and every developer, Contributor on prod's ACS.
- **No saving.** A second ACS resource has no standing charge.

`mail.staging` is a sibling of `mail`, not a child of it. Mailbox providers still weigh a subdomain's behaviour partly against its parent `rvintake.com`, so staging stays harmless by behaviour: staging mail that reaches a real inbox goes only to mailboxes the team controls.

**Outbound SMS sending number — toll-free, not per-location 10DLC (issue #600).** The advisor-initiated intake invite (A-14) sends by SMS. Per-location 10DLC numbers were considered, for the trust benefit of a local area code, and rejected in favor of one shared toll-free number: the customer is already on a live call with the advisor when the text lands, which collapses the cold-outreach trust gap 10DLC solves, and toll-free avoids a per-location provisioning/reconciliation tail — notably, STOP/opt-out handling is *per-number*, so a customer opting out from one location's number stays reachable from another's, a compliance gap 10DLC would force an explicit answer to. ACS 10DLC brand/campaign registration is also still **preview**, with no SLA and an explicit Microsoft "not recommended for production" notice, against toll-free's GA path — a second reason to default there even though 10DLC registers faster (days, versus 5–8 weeks official for toll-free and real-world reports of ~4 months). The outbound sending number still resolves per-location in config/data rather than being hardcoded, so a future *cold* outreach use case (no live call to lean on) can move to local numbers without a schema change; every location resolves to the same toll-free number today. Submitting a toll-free verification application in parallel is cheap (~$2/mo) and worth doing regardless, as a hedge against the 10DLC preview surface changing.

**Key Vault** — standard SKU, RBAC authorization, 90-day soft delete, purge protection on, public access enabled.

**Static Web Apps** — Standard, staging environments enabled, config file updates allowed, enterprise CDN off.

**Observability** — Log Analytics `PerGB2018`, 30-day retention. Workspace-based App Insights. Availability test pings from three US locations every 300 s, expecting HTTP 200 with an SSL check.

**Alerts (`#494`).** `monitor-alerts.bicep` deploys an ops action group `ag-rvs-ops-{env}-wus3` and five `scheduledQueryRules` (`kind: LogAlert`) scoped to the App Insights component, one per packet-pipeline health event. Four are Severity 1, evaluated every 5 minutes over a 5-minute window — `439002` `AllRecipientsBounced` (a location's last packet recipient hard-bounced), `438001` `PacketEmailDeliveryExhausted`, `434001` `PacketGenerationExhausted`, `521001` `PacketEmailOversized` (email sent without its PDF — a render-size regression, page-worthy from day one). The fifth, `439001` `RecipientHardBounced` (one recipient disabled, others still deliver), is Severity 3 on a 6-hour window evaluated hourly — a digest, not a page. Each query is `union traces, exceptions | where tostring(customDimensions.EventId) == "<id>"` (434001 logs with an exception, so it lands in `exceptions`) and projects `TenantId` plus `LocationId` / `ServiceRequestId` as split dimensions, so the alert payload identifies the tenant and the offending location or request. Action-group receivers are **not** in the parameter files — `opsAlertEmailReceivers` is empty and set on the deploy or in the portal, the same handling as the Auth0 values; the `opsAlertReceiverAction` output flags an empty group. On-call runbook and the end-to-end verification query are in `Infra/Bicep.IaC/README.md` "Monitoring & alerts".

---

## Identity and secrets

System-assigned managed identities on the Web App, its staging slot, the Cosmos account, and both OpenAI accounts.

Role assignments:

- **Key Vault Secrets User** → app + slot, at vault scope
- **Storage Blob Data Contributor** and **Storage Blob Delegator** → app + slot, at account scope
- **Contributor** → app + slot, at ACS resource scope (ACS has no granular email-send data-plane role; the API sends the packet email via managed identity)
- **DNS Zone Contributor** → supplied principals, at zone scope

There is no RBAC grant to Cosmos or OpenAI. Both are consumed by key, read from Key Vault.

Secrets written by the `*-keyvault-secrets` modules: `AzureOpenAi--*` (endpoint, key, vision/text/whisper deployment names, Whisper endpoint and key), `CosmosDb--Endpoint/Key/DatabaseId`, `BlobStorage--Endpoint`, `AzureCommunicationServices--Endpoint/ConnectionString`, `ApplicationInsights--ConnectionString`, `Auth0--*`.

`app-service-config.bicep` sets four app settings — `ASPNETCORE_ENVIRONMENT`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, `KeyVault__VaultUri`, and (when `deployAcs`) `AzureCommunicationServices__Email__FromAddress` = `DoNotReply@<ACS sender domain>`, so the packet-email sender tracks the deployed ACS resource instead of a hardcoded value. The sender domain is `mail.rvintake.com` when `acsCustomEmailDomain` is set (prod, `#532`) and the Azure-managed domain otherwise; `main.bicep` chooses between `communicationServices.outputs.customFromSenderDomain` and `.azureManagedMailFrom`. Everything else is pulled by the Key Vault configuration provider at startup using the managed identity. Locally, development uses `appsettings.Development.json` plus `dotnet user-secrets`, and Blob and ACS use `AzureCliCredential` directly to avoid the managed-identity probe timeout. The local API borrows staging's storage account and staging's ACS resource, sending From `DoNotReply@mail.staging.rvintake.com`, and never touches prod's.

---

## Networking and DNS

Two public DNS zones — `rvserviceflow.com` (Manager) and `rvintake.com` (Intake) — both living in `rg-rvs-prod-westus3`. Subdomains bind to the Static Web Apps by CNAME delegation, with the binding (`swa-custom-domain.bicep`) ordered after the record it validates against. The production apex is an ALIAS A record that tracks the Intake SWA resource — no IP is pinned — plus a one-time out-of-band TXT-token registration that Bicep does not declare. The `rvintake.com` zone also carries the ACS custom-sending-domain records for `mail.rvintake.com` (prod) and `mail.staging.rvintake.com` (staging) (`#532`) — SPF, DKIM + DKIM2, a domain-ownership TXT, and a DMARC `p=none` record — written by the `dnsIntake` module from `communicationServices` outputs. `dns-zone-contributor.bicep` grants the staging deployer zone-scoped rights so it can write records into prod-owned zones.

**There are no VNets, no private endpoints, and no private DNS zones anywhere.** Cosmos, Key Vault, Storage, Log Analytics, App Insights and both Azure OpenAI accounts (GPT-4o + Whisper) are all reachable publicly, with Storage, Key Vault and the OpenAI accounts' network ACLs defaulting to `Allow` (`bypass: AzureServices`). Blob CORS permits GET/HEAD/PUT from the Static Web App custom domains only.

---

## CI/CD

Four workflows in `.github/workflows/`, detailed runbook in its README.

| Workflow | Trigger | Does |
|---|---|---|
| `build-test.yml` | push / PR, any branch | Restore, build `RVS.slnx` Release, run all three test projects with coverage, upload TRX + cobertura. Never deploys |
| `deploy-staging.yml` | push to `main` | Diffs `HEAD~1..HEAD` to detect which apps changed, builds and tests everything, publishes only what changed. API via Azure OIDC + `webapps-deploy`; each SWA via its `*_SWA_TOKEN_STAGING` |
| `deploy-production.yml` | `workflow_dispatch` only | Resolves the latest successful staging run on `main` and re-downloads **its** artifacts. Never rebuilds. Rewrites `blazor-environment` Staging→Production in `staticwebapp.config.json`, then deploys |
| `copilot-setup-steps.yml` | — | Agent environment bootstrap |

Auth: the API uses Azure OIDC federated credentials, no long-lived secrets. Static Web Apps use deployment tokens held as environment secrets.

**No workflow deploys the Bicep.** Infrastructure is applied by hand with `az deployment sub create`, per `Infra/Bicep.IaC/deployment-cmds.azcli`.

---

## Known defects

| Defect | Detail |
|---|---|
| `build-mobile.yml` | Builds `RVS.MAUI.Tech` on `mobile-v*` tags. That project is not in the repo and the offline mobile app is archived. Delete the workflow |
| `deployment-cmds.azcli` | References a `parameters/dev.bicepparam` that does not exist. It also carries a manual `Stripe--WebhookSecret` vault write — harmless, but premature: billing is build item 7 and nothing reads that secret yet |
| ACS send quota on the managed domain caps delivery at 10 packets/hour (`#521`) | An Azure-managed Email domain is limited to 5 emails/min and 10/hour **with no support path to raise it**. Neither environment sends from it any more: staging deploys its own verified `mail.staging.rvintake.com` (verification only, no quota request). Prod deploys the verified custom domain `mail.rvintake.com` (`#532`, now in Bicep), against which the quota increase *can* be requested — a 72 h lead, and 2–3 weeks of warming on top. Not a code defect; `#521`'s size handling is built. Still gates the local-cluster launch (`#527`) until the prod domain is verified and warmed |

---

## Descope candidates

Conservative — flagged, not assumed.

**Safe to remove now:** `build-mobile.yml`. It builds a project that does not exist, for a capability that is archived.

**Deferred, not archived:** the Stripe pieces. Billing is build item 7, so leave the `Stripe--WebhookSecret` guidance in place — just don't run it yet.

**Keep, despite the descope:** ACS. SMS and email ride the same resource and email is now the core delivery mechanism. Dropping two-way SMS removes code paths, not infrastructure. Also keep the Manager Static Web App and the Auth0 secrets — a thin manager app is still in scope.

**Flag before the next infra deploy (issue #467):** the Whisper account and the `rg-rvs-{env}-ncus` resource group are **unconditional** — they deploy in every environment with no flag. Voice capture is in scope (issue #429, closes Q8), so the account stays, but it belongs behind a `deployWhisper` flag — defaulted on — rather than left unconditional, so the per-environment spend is a deliberate choice and the decision stays reversible. The same treatment applies to the gpt-4o account, which is also unflagged and is needed for VIN vision extraction and issue refinement.

**Verify against the packet flow first:** the `rv-warranty-rules` container (no reader) and the cross-tenant reach of `global-customer-accounts`. See `RVS_DataModel.md`.

---

## Deploying

```bash
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam
```

Auth0 values are not in the parameter files — pass them with `--parameters auth0Domain=... auth0Audience=...` or write the secrets to the vault directly. Full command set and the prod phase sequence are in `Infra/Bicep.IaC/deployment-cmds.azcli`; service-principal setup is in `Infra/Bicep.IaC/PROD_DEPLOYER_SP_SETUP.md`; naming rules are in `Infra/Bicep.IaC/Azure_Resource_Naming_Conventions.md`.
