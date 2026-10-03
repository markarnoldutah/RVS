# RVS — Infrastructure

**Version:** 1.4 · October 2, 2026
**Scope:** Azure resources and CI/CD as declared. The Bicep in `Infra/Bicep.IaC/` is the source of truth; this document explains it. Where they disagree, the Bicep is right.

`main.bicep` targets subscription scope. Primary region **westus3**, Whisper **northcentralus**, Static Web Apps **westus2**. Email (SendGrid) and SMS (Twilio) are outside Azure.

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
| Ops action group + 5 packet-pipeline alert rules + daily-cap alert | `monitor-alerts.bicep` | `deployObservability` |
| Availability-failing + telemetry-dark alerts (`#602`) | `monitor-alerts.bicep` | `deployObservability`, and only while the availability webtest exists |
| Key Vault + role assignments | `key-vault.bicep` | `deployKeyVault` |
| Cosmos account, database, 11 containers | `cosmos-db.bicep` | `deployCosmosDb` |
| Storage account, CORS, `rvs-attachments`, `intakeRedirectHits` table, 7 role assignments | `storage-account.bicep` | `deployStorageAccount` |
| Two Static Web Apps + custom domains | `static-web-app.bicep` | `deploySwa` |
| DNS zones + record sets | `dns.bicep` | `deploySwa && deployDns` |
| DNS Zone Contributor grants | `dns-zone-contributor.bicep` | `deploySwa && deployDns && env=='prod' && principals supplied` |
| Key Vault secrets | `*-keyvault-secrets.bicep` | `deployKeyVault` plus the paired resource flag |

---

## Environments

Both parameter files set every deploy flag to `true`, `cosmosCapacityMode='Serverless'`, `storageAllowSharedKeyAccess=false`, `swaSkuName='Standard'`, `swaLocation='westus2'`. The differences are small:

| | `staging` | `prod` |
|---|---|---|
| App Service SKU | `B1` | `B1` |
| Staging slot | no | no |
| OpenAI capacity | 10 | 30 |
| Whisper capacity | 1 | 2 |
| Storage CORS origins | set | set |
| DNS RBAC principals | — | set |
| Email sending domain (SendGrid) | `mail-staging.rvintake.com` | `mail.rvintake.com` |
| Intake host | `staging.rvintake.com` | `rvintake.com` (apex) |
| Manager host | `manager-staging.rvintake.com` | `manager.rvintake.com` |
| Redirect host | `go-staging.rvintake.com` | `go.rvintake.com` |
| API origin | `api-staging.rvserviceflow.com` | `api.rvserviceflow.com` |

Moving prod to `S1` (Always On, staging slot) is a one-value change — README "SKU Upgrade Paths".

Every file is deployable as committed. Prod is a single file with no phases; the things it leaves to a human are the one-time registration of the `rvintake.com` apex with the Intake SWA (a token Azure mints at registration time), the hostname binding and managed certificate for the `go` and `api` hosts (`#599`, `#633`), and the SendGrid domain authentication + warming of the `mail.rvintake.com` sending domain — both sequences are in `Infra/Bicep.IaC/README.md` "Deploy Production" (steps 2 and 4). Redeploys never touch either. Staging's `mail-staging.rvintake.com` needs the same authentication, but no warming. The SendGrid and Twilio keys are set in Key Vault by hand (`deployment-cmds.azcli` §4e).

Two things to know before using these:

- **Prod is live** (first deployed 2026-09-12). The northcentralus Whisper quota is 3 units subscription-wide, and `gpt-5` in westus3 allows 3 units per model; for both, `staging = 1` + `prod = 2` fits exactly with no headroom, so raising either value needs a quota increase first.
- **No parameter file sets the Auth0 values.** `auth0Domain`, `auth0Audience`, `auth0ClientId`, `auth0ClientSecret` are empty, so `auth0-keyvault-secrets.bicep` is skipped on a param-file-only deploy and the vault's existing `Auth0--*` secrets are left as they are. Pass the values on the CLI only to create or change those secrets.

---

## SKUs and configuration

**App Service** — Linux, `DOTNETCORE|10.0`, `httpsOnly`, TLS 1.2, FTPS disabled, HTTP/2 on. `healthCheckPath=/health` on anything above F1. `alwaysOn` and deployment slots only on S1.

**Cosmos** — Standard offer, Session consistency, `EnableServerless`, single region westus3, not zone-redundant, continuous backup, TLS 1.2, system-assigned identity. `publicNetworkAccess: Enabled`, `disableLocalAuth: false` — consumed by key, not RBAC.

**Storage** — StorageV2, `Standard_LRS`, Hot, TLS 1.2, `allowBlobPublicAccess: false`, network ACL default `Allow`. Two data services on the one account: the `rvs-attachments` blob container, and the `intakeRedirectHits` table (`#599`) holding the append-only `go.rvintake.com` redirect hit log, partitioned by location. The table carries no CORS — nothing in a browser talks to it — and its access grant is **Storage Table Data Contributor** on the same three principals as the blob roles (app identity, staging slot, the dev Entra group). Shape and rationale in `RVS_DataModel.md`.

**Azure OpenAI** — both accounts `kind: OpenAI`, SKU `S0`, custom subdomain, system-assigned identity. `gpt-4o` version `2024-11-20`; Whisper model `whisper` version `001`. Whisper is in northcentralus because Whisper 001 Standard is not offered in westus3.

**Optional assessment-only deployment (`#584`).** `openai.bicep`'s `additionalDeployments` array can add further model deployments on the primary account beyond `gpt-4o`, keyed by `assessmentModelName` in `main.bicep` — staging and prod both set this to `gpt-5` (`assessmentDeploymentCapacity` 30 / 40 K TPM, each raised by 10 in `#783` so step-6 question calls don't compete with packet assessments. A text-only call reserves ~3.6K tokens against TPM up front and a call with photos (`#772`, up to 5) ~7.6–9.3K; a call bigger than the whole limit is refused every time. Capacity costs nothing on a pay-per-token deployment; the ceiling is the shared DataZoneStandard gpt-5 quota in westus3, 300K TPM with 70K used), used only by the packet preliminary assessment via the `AzureOpenAi--AssessmentDeploymentName` Key Vault secret; categorization and issue-text refinement stay on `gpt-4o` via `TextDeploymentName`. `gpt-5` deploys under SKU `DataZoneStandard` (US), not the `Standard` regional SKU `gpt-4o` uses — that SKU isn't offered for it. Blank `assessmentModelName` and redeploy to revert the assessment call to `gpt-4o` with no application-code change. The two deployments take different request shapes: when `AssessmentDeploymentName` is set the API sends the reasoning-model shape (`max_completion_tokens` 2000, `reasoning_effort` low, no `temperature`, api-version `2025-04-01-preview`); when it is blank it sends the `gpt-4o` shape. Each model rejects the other's with a 400, which lands silently in the rule-based fallback.

**Step-6 questions on the same deployment (`#783`).** `questionsUseAssessmentDeployment` (default `false`; `true` in both parameter files) writes the assessment deployment's name to the `AzureOpenAi--QuestionsDeploymentName` Key Vault secret, and `questionsReasoningEffort` (`minimal` or `low`) to `AzureOpenAi--QuestionsReasoningEffort`. When the name is set, only diagnostic question generation moves to `gpt-5`, with the reasoning-model shape (`max_completion_tokens` 3000, ~4K reserved per call) on its own HTTP client: 12 s per attempt, one retry, 15 s total, after which the customer gets the question bank. Category suggestion stays on `gpt-4o`. Set it back to `false` and redeploy to revert, no code change.

**Messaging — SendGrid (email) and Twilio (SMS), not Azure.** Azure Communication Services carried both until Oct 2 2026. It was removed before go-live because Microsoft retires it on Sep 30 2028, with email and SMS both on the retirement list. Neither replacement is an Azure resource, so Bicep owns only the sending domain's DNS and the app settings; accounts, numbers and keys are console and vault work (`Infra/Bicep.IaC/deployment-cmds.azcli` §4e). The one-time ACS teardown is §4e (4) there.

**Email: one SendGrid account, one sending subdomain per environment.** Staging sends From `DoNotReply@mail-staging.rvintake.com`, prod From `DoNotReply@mail.rvintake.com` (`mailSendingDomain`). Each subdomain is authenticated in SendGrid separately (automated security), so staging's failed sends — every seeded packet recipient is an undeliverable `.example.com` address — never count against prod's sender reputation. `main.bicep` writes the three CNAMEs SendGrid mints for each (`sendGridDnsRecords`: return path `em1234.<sub>`, DKIM `s1`/`s2._domainkey.<sub>`) and a **DMARC** `p=none` record whose `rua` reports go to `support@arnolddigitalsolutions.com` (`#608`). That address is on another domain, so it needs RFC 7489 §7.1 authorization TXT records in the `arnolddigitalsolutions.com` zone, at its registrar; the deploy's `dmarcReportAuthorizationAction` output names them. One restricted API key per environment (Mail Send only) sits in Key Vault as `SendGrid--ApiKey`. Click, open and subscription tracking are off per message and account-wide: click tracking would rewrite the packet's photo SAS links. A packet email is held to 10 MB including base64 attachments (budget 9.5 MB); SendGrid accepts 30 MB, but recipient mailboxes are the binding limit. Two to three weeks of warming on Jay Lyons's real traffic (`#525`) before any other shop's mailbox sees a packet is still the plan; the in-room deliverability check for later pilots is `FS-7` in `RVS_Plan.md`.

`mail-staging` is a sibling of `mail`, not a child of it. Mailbox providers still weigh a subdomain's behaviour partly against its parent `rvintake.com`, so staging stays harmless by behaviour: staging mail that reaches a real inbox goes only to mailboxes the team controls.

**SMS: one Twilio subaccount per environment, each with one toll-free number in one Messaging Service (issue #600).** The number resolves per location through `ISmsSenderNumberResolver` rather than being hardcoded; every location resolves to the environment's one number today. Sends go through the Messaging Service, which applies **Advanced Opt-Out**: Twilio refuses a send to a number that texted STOP (error 21610) and answers STOP, START and HELP itself, with the HELP text from `RVS.API/Integrations/InboundSmsReplyContent.cs`. RVS sends no keyword reply of its own.

| Environment | Number | Verification | `smsEnabled` |
| --- | --- | --- | --- |
| Staging | not yet bought | not submitted (#659) | `false` |
| Prod | not yet bought | not submitted (#659) | `false` |

The ACS numbers (`+18662319618` staging, `+18332398230` prod) were released on Oct 2 2026; their ACS verification was never completed and is abandoned.

**Verification is per number**, and unverified toll-free traffic is blocked outright. The package — company details, program description, opt-in evidence hosted at `rvintake.com/compliance/`, samples — is `Infra/TollFreeEvidence/README.md`; it carries over from the ACS application unchanged apart from the provider-specific fields.

**Filing entity (#680).** The verification application is filed by **Arnold Digital Solutions**, the entity that signs dealer agreements and owns the billing. Its contact email is `support@arnolddigitalsolutions.com`, on the entity's own domain rather than free webmail. `rvintake.com` corroborates both: every Intake page's footer names the entity and shows that address, and links the privacy policy (`/privacy`), terms (`/terms`) and texting terms (`/sms-terms`). They come from `RVS.Blazor.Intake/SiteIdentity.cs`; if the application's company name or contact email changes, change that file to match (Spec A-15).

**How the number reaches the API (#661).** It is bought in the Twilio console, so Bicep cannot derive it. Each `.bicepparam` carries `smsFromPhoneNumber` and `twilioMessagingServiceSid`, and `app-service-config.bicep` injects them as `Sms__FromPhoneNumber` and `Twilio__MessagingServiceSid`. `smsEnabled` is injected as `Sms__Enabled` in every environment, including when it is `false`, and is checked before the Twilio credentials, so a vault that already holds them sends nothing. Flip it only after that environment's number shows verified (G-4). The API refuses to start with SMS enabled and no valid E.164 number.

**Inbound texts and status callbacks reach the API as Twilio webhooks (#665).** The Messaging Service posts incoming messages to `POST https://{api host}/api/events/twilio-sms/inbound` and status updates to `…/status`; each send also names the status URL. Both endpoints are anonymous, so every request must carry a valid `X-Twilio-Signature` (HMAC-SHA1 with the subaccount's auth token, Key Vault `Twilio--AuthToken`). The API checks it against `Twilio__WebhookBaseUrl` — the public `api.` origin Bicep injects — rather than the request URL, because App Service terminates TLS in front of the app and Twilio signs the exact URL it called. With no auth token or base URL the endpoints answer 503; a bad signature, 403. Inbound STOP / START / UNSTOP are mirrored into every tenant's `CustomerProfile` for that number; Twilio's `OptOutType` parameter is preferred over the raw body because it is what Twilio actually enforced.

**Rotation.** Set the new value in Key Vault, then `az webapp restart`: the API reads Key Vault at startup only. Rotating the auth token in Twilio invalidates webhook signatures until the API restarts with the new one, so do the two back to back. Auth0's identity mail uses its own SendGrid key (Auth0 checklist §8), so rotating the API's key cannot break password-reset mail.

**Key Vault** — standard SKU, RBAC authorization, enabled for template deployment (so `.bicepparam` files can read secrets with `az.getSecret`, #678), 90-day soft delete, purge protection on, public access enabled.

**Static Web Apps** — Standard, staging environments enabled, config file updates allowed, enterprise CDN off.

**Observability** — Log Analytics `PerGB2018`, 30-day retention. Workspace-based App Insights. The availability test is **off in both environments** (`#674`). When on, it pings `/health` from `availabilityTestLocations` every `availabilityTestFrequencySeconds` (committed: one location, 900 s), expecting HTTP 200 with an SSL check. App Service file-system logging is on (`#602`): the console log is kept 3 days / 35 MB, a server-side log path that does not depend on App Insights.

**Alerts (`#494`).** `monitor-alerts.bicep` deploys an ops action group `ag-rvs-ops-{env}-wus3` and five `scheduledQueryRules` (`kind: LogAlert`) scoped to the App Insights component, one per packet-pipeline health event. Four are Severity 1, evaluated every 5 minutes over a 5-minute window — `439002` `AllRecipientsBounced` (a location's last packet recipient hard-bounced), `438001` `PacketEmailDeliveryExhausted`, `434001` `PacketGenerationExhausted`, `521001` `PacketEmailOversized` (email sent without its PDF — a render-size regression, page-worthy from day one). The fifth, `439001` `RecipientHardBounced` (one recipient disabled, others still deliver), is Severity 3 on a 6-hour window evaluated hourly — a digest, not a page. Each query is `union traces, exceptions | where tostring(customDimensions.EventId) == "<id>"` (434001 logs with an exception, so it lands in `exceptions`) and projects `TenantId` plus `LocationId` / `ServiceRequestId` as split dimensions, so the alert payload identifies the tenant and the offending location or request. Action-group receivers are committed in both parameter files (`opsAlertEmailReceivers`, `#639`), because the Action Groups provider does a full-replace PUT and would delete a portal-added receiver on the next deploy; the `opsAlertReceiverAction` output flags an empty group. A sixth rule fires when the Log Analytics daily cap is reached. Two more (`#602`) exist only alongside the availability test: `ma-rvs-api-availability-{env}-wus3` pages when the test fails, and `sqr-rvs-api-telemetry-dark-{env}-wus3` fires when pings pass but `requests` is empty for an hour — the app is serving and its telemetry is not arriving, so every rule above is blind. With the test off, nothing detects that. On-call runbook and the end-to-end verification query are in `Infra/Bicep.IaC/README.md` "Monitoring & alerts".

---

## Identity and secrets

System-assigned managed identities on the Web App, its staging slot, the Cosmos account, and both OpenAI accounts.

Role assignments:

- **Key Vault Secrets User** → app + slot, at vault scope
- **Storage Blob Data Contributor** and **Storage Blob Delegator** → app + slot, at account scope
- **DNS Zone Contributor** → supplied principals, at zone scope

There is no RBAC grant to Cosmos or OpenAI. Both are consumed by key, read from Key Vault.

Secrets written by the `*-keyvault-secrets` modules: `AzureOpenAi--*` (endpoint, key, vision/text/whisper deployment names, Whisper endpoint and key), `CosmosDb--Endpoint/Key/DatabaseId`, `BlobStorage--Endpoint`, `TableStorage--Endpoint` (`#599`), `ApplicationInsights--ConnectionString`, `Auth0--*`. Set by hand: `SendGrid--ApiKey` and `Twilio--AccountSid/ApiKeySid/ApiKeySecret/AuthToken`.

`app-service-config.bicep` sets `ASPNETCORE_ENVIRONMENT`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, `KeyVault__VaultUri`, and the messaging settings: `Email__FromAddress` = `DoNotReply@<mailSendingDomain>`, `Sms__Enabled`, `Sms__FromPhoneNumber`, `Twilio__MessagingServiceSid` and `Twilio__WebhookBaseUrl`. Everything else is pulled by the Key Vault configuration provider at startup using the managed identity. Locally, development uses `appsettings.Development.json` plus `dotnet user-secrets`, and Blob uses `AzureCliCredential` directly to avoid the managed-identity probe timeout. The local API borrows staging's storage account; it sends email only if staging's SendGrid key is in user-secrets, From `DoNotReply@mail-staging.rvintake.com`, and never touches prod's.

---

## Networking and DNS

Two public DNS zones — `rvintake.com` and `rvserviceflow.com` — both living in `rg-rvs-prod-westus3`. Since `#634` the split is by audience, not by app: **`rvintake.com` carries every hostname a human reads** — the Intake apex, the Manager app, the `go` redirect and the email sending domain — while **`rvserviceflow.com` is corporate-only**, holding the API origin (`#633`), which nobody types, and a no-mail posture (null MX, SPF `-all`, DMARC `p=reject`). `manager.rvserviceflow.com` and `manager-staging.rvserviceflow.com` were retired on September 17 2026 and no longer resolve. Subdomains bind to the Static Web Apps by CNAME delegation, with the binding (`swa-custom-domain.bicep`) ordered after the record it validates against. The production apex is an ALIAS A record that tracks the Intake SWA resource — no IP is pinned — plus a one-time out-of-band TXT-token registration. Bicep re-writes that token afterwards from `intakeApexValidationToken`, because the token shares the apex TXT record-set with the apex SPF string. The apex publishes `v=spf1 -all`, a null MX (`#608`) and `_dmarc` `p=reject; sp=reject` with a `rua` (`#652`, `#608`), so a new sending subdomain under `rvintake.com` needs its own `_dmarc` record or all of its mail is rejected. See README "Intake apex mail posture". The `rvintake.com` zone also carries the sending-domain records for `mail.rvintake.com` (prod) and `mail-staging.rvintake.com` (staging) — SendGrid's three domain-authentication CNAMEs (from `sendGridDnsRecords`) and a DMARC `p=none` record — written by the `dnsIntake` module, plus the `go` redirect host's CNAME and `asuid` TXT (`#599`, below). `dns-zone-contributor.bicep` grants the staging deployer zone-scoped rights so it can write records into prod-owned zones.

The zone also carries `login.rvintake.com`, the Auth0 Universal Login host (`#627`, `#634`) — live since September 18 2026, serving an Auth0-managed certificate, and the `iss` of every token. `main.bicep`'s `auth0CnameRecords` writes it from `auth0CustomDomainCnameTarget`, a value Auth0 mints once when the custom domain is added in its dashboard — the same out-of-band pattern as the SendGrid CNAMEs above, and the reason the parameter is a hand-entered string rather than something the template derives. It is also the one record here with no `-staging` sibling: the Auth0 Free plan includes exactly one custom domain and all three environments share one tenant (`#610`), so both parameter files write the same name and value. Steps are in `Docs/ASOT/Auth0/Auth0-Portal-Configuration-Checklist.md` §6.

**`go.rvintake.com` — the channel-tagging redirect (`Spec A-13`, `#599`).** A sibling label in the Intake zone, `go` in prod and `go-staging` in staging, pointed at the **API** App Service rather than the Intake SWA — the redirect has to write to the hit log, and a static host could serve a redirect but could not count it. Bicep writes both records it can know: the CNAME to the Web App's default hostname, and the `asuid.<label>` ownership TXT, whose value the site itself supplies (`customDomainVerificationId`).

What Bicep does **not** declare, for the same reason it does not declare the Intake apex binding: the hostname binding waits on DNS to validate and the App Service managed certificate waits on the binding, so a first bring-up from a single template would deadlock on records that template has not written yet. Binding the hostname and issuing the certificate are one-time out-of-band steps — `Infra/Bicep.IaC/README.md`, "Bind the go.<zone> redirect host". Redeploys never touch them. Until that is done in an environment, `Intake:RedirectBaseUrl` should stay unset there: the API then falls back to the Intake host and hands out working but untagged links rather than links to a host that does not answer.

**`api.rvserviceflow.com` — the API origin (`#633`).** `api` in prod, `api-<env>` in staging, in the **corporate** zone. This is the one hostname that deliberately stayed off the intake brand: an XHR origin is seen in devtools and in a CSP, not on a sticker. Both Blazor apps call it, and it fronts the same App Service the `go` host does — so that Web App carries two custom hostnames, one per zone, which is the intended split rather than an accident.

Before `#633` the name existed only as the Auth0 resource-server identifier, an opaque audience string with no DNS behind it, while the apps called the `*.azurewebsites.net` default hostname. **The audience value is unrelated and did not change**; that it matches this hostname is a convenience, not a coupling — changing it would invalidate every issued token and grant. Bicep writes the CNAME and the `asuid` TXT; the hostname binding and managed certificate are out-of-band for the same reason as the `go` host, per README "Bind the `api.<zone>` host". Both environments were bound on September 17 2026.

**There are no VNets, no private endpoints, and no private DNS zones anywhere.** Cosmos, Key Vault, Storage, Log Analytics, App Insights and both Azure OpenAI accounts (GPT-4o + Whisper) are all reachable publicly, with Storage, Key Vault and the OpenAI accounts' network ACLs defaulting to `Allow` (`bypass: AzureServices`). Blob CORS permits GET/HEAD/PUT from the Static Web App custom domains only.

---

## CI/CD

Four workflows in `.github/workflows/`, detailed runbook in its README.

| Workflow | Trigger | Does |
|---|---|---|
| `build-test.yml` | push / PR, any branch | Restore, build `RVS.slnx` Release, run all three test projects with coverage, upload TRX + cobertura. Never deploys |
| `deploy-staging.yml` | push to `main` | Diffs `HEAD~1..HEAD` to detect which apps changed, builds and tests everything, publishes only what changed. API via Azure OIDC + `webapps-deploy`; each SWA via its `*_SWA_TOKEN_STAGING` |
| `deploy-production.yml` | `workflow_dispatch` only | For each selected app, resolves the newest successful staging run on `main` that built it and re-downloads that artifact. Never rebuilds. The Blazor apps choose their environment from the hostname in `index.html`, so the artifacts deploy unmodified |
| `copilot-setup-steps.yml` | — | Agent environment bootstrap |

Auth: the API uses Azure OIDC federated credentials, no long-lived secrets. Static Web Apps use deployment tokens held as environment secrets.

**No workflow deploys the Bicep.** Infrastructure is applied by hand with `az deployment sub create`, per `Infra/Bicep.IaC/deployment-cmds.azcli`.

---

## Known defects

| Defect | Detail |
|---|---|
| `deployment-cmds.azcli` | References a `parameters/dev.bicepparam` that does not exist. It also carries a manual `Stripe--WebhookSecret` vault write — harmless, but premature: billing is build item 7 and nothing reads that secret yet |
| ACS still deployed until the one-time teardown runs | Removing the ACS modules from Bicep does not delete the resources (incremental deploys). Until `deployment-cmds.azcli` §4e (4) has run in both environments, the ACS resources, Event Grid topics, role assignments, Key Vault secrets and `azurecomm` DNS records still exist. They cost nothing without numbers, but they are clutter that reads as live |

---

## Descope candidates

Conservative — flagged, not assumed.

**Deferred, not archived:** the Stripe pieces. Billing is build item 7, so leave the `Stripe--WebhookSecret` guidance in place — just don't run it yet.

**Keep, despite the descope:** the Manager Static Web App and the Auth0 secrets — a thin manager app is still in scope.

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
