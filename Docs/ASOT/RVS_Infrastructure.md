# RVS — Infrastructure

**Version:** 1.0 · September 4, 2026
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
| Key Vault + role assignments | `key-vault.bicep` | `deployKeyVault` |
| Cosmos account, database, 10 containers | `cosmos-db.bicep` | `deployCosmosDb` |
| Storage account, CORS, `rvs-attachments`, 4 role assignments | `storage-account.bicep` | `deployStorageAccount` |
| ACS + Email Service + managed domain + 2 role assignments | `communication-services.bicep` | `deployAcs`; role assignments only when an App Service principal is supplied |
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

Every file is deployable as committed. Prod is a single file with no phases; the only thing it leaves to a human is the one-time registration of the `rvintake.com` apex with the Intake SWA, which needs a token Azure mints at registration time — the sequence is in `Infra/Bicep.IaC/README.md` "Deploy Production". Redeploys never touch it.

Two things to know before using these:

- **Prod has not been deployed yet** (as of 2026-09-10 the prod resource groups hold only the two DNS zones, which the staging deploy created). The first prod run is the README sequence, start to finish. The northcentralus Whisper quota is 3 units subscription-wide; `staging = 1` + `prod = 2` fits it exactly with no headroom, so if staging is still deployed at its old `whisperCapacity = 3` it must be redeployed before the prod pre-flight will pass.
- **No parameter file sets the Auth0 values.** `auth0Domain`, `auth0Audience`, `auth0ClientId`, `auth0ClientSecret` are empty, so `auth0-keyvault-secrets.bicep` never runs from a param file. Those secrets must be passed on the CLI or written to the vault by hand.

---

## SKUs and configuration

**App Service** — Linux, `DOTNETCORE|10.0`, `httpsOnly`, TLS 1.2, FTPS disabled, HTTP/2 on. `healthCheckPath=/health` on anything above F1. `alwaysOn` and deployment slots only on S1.

**Cosmos** — Standard offer, Session consistency, `EnableServerless`, single region westus3, not zone-redundant, continuous backup, TLS 1.2, system-assigned identity. `publicNetworkAccess: Enabled`, `disableLocalAuth: false` — consumed by key, not RBAC.

**Storage** — StorageV2, `Standard_LRS`, Hot, TLS 1.2, `allowBlobPublicAccess: false`, network ACL default `Allow`.

**Azure OpenAI** — both accounts `kind: OpenAI`, SKU `S0`, custom subdomain, system-assigned identity. `gpt-4o` version `2024-11-20`; Whisper model `whisper` version `001`. Whisper is in northcentralus because Whisper 001 Standard is not offered in westus3.

**ACS** — location `global`, data location United States, Email Service with an Azure-managed domain, engagement tracking disabled.

**Key Vault** — standard SKU, RBAC authorization, 90-day soft delete, purge protection on, public access enabled.

**Static Web Apps** — Standard, staging environments enabled, config file updates allowed, enterprise CDN off.

**Observability** — Log Analytics `PerGB2018`, 30-day retention. Workspace-based App Insights. Availability test pings from three US locations every 300 s, expecting HTTP 200 with an SSL check.

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

`app-service-config.bicep` sets four app settings — `ASPNETCORE_ENVIRONMENT`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, `KeyVault__VaultUri`, and (when `deployAcs`) `AzureCommunicationServices__Email__FromAddress` = `DoNotReply@<ACS managed domain>`, so the packet-email sender tracks the deployed ACS resource instead of a hardcoded value. Everything else is pulled by the Key Vault configuration provider at startup using the managed identity. Locally, development uses `appsettings.Development.json` plus `dotnet user-secrets`, and Blob uses `AzureCliCredential` directly to avoid the managed-identity probe timeout.

---

## Networking and DNS

Two public DNS zones — `rvserviceflow.com` (Manager) and `rvintake.com` (Intake) — both living in `rg-rvs-prod-westus3`. Subdomains bind to the Static Web Apps by CNAME delegation, with the binding (`swa-custom-domain.bicep`) ordered after the record it validates against. The production apex is an ALIAS A record that tracks the Intake SWA resource — no IP is pinned — plus a one-time out-of-band TXT-token registration that Bicep does not declare. `dns-zone-contributor.bicep` grants the staging deployer zone-scoped rights so it can write records into prod-owned zones.

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
