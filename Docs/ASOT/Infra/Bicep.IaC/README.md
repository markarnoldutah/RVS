# RVS – Azure Infrastructure (Bicep)

Infrastructure as Code for the RVS Azure platform. Supports **independent deployment** of staging and production environments via separate parameter files.

## Environment Model

| Environment | Purpose | Notes |
|---|---|---|
| **Staging** | Pre-production validation | Full cloud resources |
| **Production** | Live service | Deployed only when explicitly triggered |

---

## Resources Provisioned

| Resource | Module | Staging | Production |
|---|---|---|---|
| App Service (API) | `app-service.bicep` | Free F1 ($0/mo) | Basic B1 (~$12/mo) or Standard S1 (~$58/mo) + staging slot |
| Cosmos DB | `cosmos-db.bicep` | Serverless | Serverless |
| Blob + Table Storage | `storage-account.bicep` | Standard LRS | Standard LRS |
| Key Vault | `key-vault.bicep` | RBAC model | RBAC model |
| Log Analytics | `log-analytics.bicep` | Per env | Per env |
| Application Insights | `app-insights.bicep` | /health test | /health test |
| Monitor alerts + ops action group | `monitor-alerts.bicep` | Packet-pipeline alerts | Packet-pipeline alerts |
| OpenAI (GPT-4o) | `openai.bicep` | 10 K TPM | 30 K TPM |
| OpenAI (Whisper) | `openai-whisper.bicep` | 1 K TPM | 1 K TPM |
| Communication Services | `communication-services.bicep` | Email + SMS | Email + SMS |
| Static Web App (Intake) | `static-web-app.bicep` | Standard | Standard |
| Static Web App (Manager) | `static-web-app.bicep` | Standard | Standard |
| DNS (corporate + intake zones) | `dns.bicep` (×2) | Shared zones | Shared zones |

---

## Prerequisites

| Tool | Minimum Version | Install |
|------|-----------------|---------|
| Azure CLI | 2.61+ | [Install](https://learn.microsoft.com/cli/azure/install-azure-cli) |
| Bicep CLI | 0.28+ | `az bicep upgrade` |
| Azure subscription | — | Resource providers registered |
| Permissions | Contributor + Cognitive Services Contributor | — |

Register resource providers if needed:

```bash
az provider register --namespace Microsoft.CognitiveServices
az provider register --namespace Microsoft.Communication
az provider register --namespace Microsoft.DocumentDB
az provider register --namespace Microsoft.Web
az provider register --namespace Microsoft.KeyVault
az provider register --namespace Microsoft.OperationalInsights
az provider register --namespace Microsoft.Insights
```

---

## Repository Layout

```text
Docs/ASOT/Infra/Bicep.IaC/
├── main.bicep                              # Orchestration template (subscription scope)
├── rg-scaffold.bicep                       # Pre-provision all resource groups
├── bicepconfig.json                        # Linter rules
├── verify-alert-rules.sh                   # Fires each packet-pipeline alert with a synthetic record (#732)
├── modules/
│   ├── app-service.bicep                   # App Service Plan + Web App (Managed Identity)
│   ├── app-service-config.bicep            # Post-deploy app settings (App Insights, Key Vault)
│   ├── app-insights.bicep                  # Application Insights + /health availability test
│   ├── monitor-alerts.bicep                # Ops action group + packet-pipeline critical/warn alert rules (#494)
│   ├── cosmos-db.bicep                     # Cosmos DB account + 10 containers with index policies
│   ├── key-vault.bicep                     # Key Vault (RBAC access model)
│   ├── log-analytics.bicep                 # Log Analytics Workspace
│   ├── naming-tags.bicep                   # Shared naming & tagging helper
│   ├── openai.bicep                        # Azure OpenAI + GPT-4o deployment
│   ├── openai-whisper.bicep                # Azure OpenAI + Whisper STT deployment
│   ├── openai-keyvault-secrets.bicep       # Stores OpenAI secrets in Key Vault
│   ├── communication-services.bicep        # Azure Communication Services (Email + SMS)
│   ├── acs-keyvault-secrets.bicep          # Stores ACS secrets in Key Vault
│   ├── storage-account.bicep               # Storage account + rvs-attachments container
│   ├── static-web-app.bicep                # Azure Static Web App (resource only — no bindings)
│   ├── swa-custom-domain.bicep             # SWA custom-domain binding, ordered after its DNS record
│   └── dns.bicep                           # DNS zone + CNAME / A / ALIAS-A / TXT record sets (called once per zone)
├── parameters/
│   ├── staging.bicepparam                  # Staging parameter values (full)
│   └── prod.bicepparam                     # Production — the one prod file, deployable as committed
└── README.md                               # This file
```

---

## Quick-Start Deployment

### Deploy Staging

> **Note (PowerShell on Windows):** Use the PowerShell block below.
> Inline `$(date +%Y%m%d%H%M)` in a double-quoted string is not evaluated by PowerShell;
> the literal `%` format specifiers are passed to Azure and cause an `InvalidDoubleEncodedRequestUri` error.

**PowerShell**
```powershell
az login
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam `
  --name "rvs-staging-$ts"
```

**bash / zsh**
```bash
az login
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam \
  --name "rvs-staging-${TS}"
```

### Deploy Production

One parameter file, `prod.bicepparam`, deployable as committed — there is no
phase 1 / phase 2 and nothing to fill in afterwards. Bicep stands up every
resource, binds `manager.rvintake.com`, and writes an **ALIAS** A record
at the `rvintake.com` apex that tracks the Intake SWA (no pinned IP). The one
thing it does not do is *register* the apex with the SWA: Azure mints the
ownership token at registration time, so that is a one-time step you do by
hand after the first deploy (step 2). Redeploys never touch it.

Prerequisites: the six resource groups exist (`rg-scaffold.bicep`).
`deployDns = true` creates or re-asserts both DNS zones. Both `rvintake.com`
and `rvserviceflow.com` are already delegated to Azure DNS at the registrar
and resolving (confirmed 2026-09-11), so no registrar step is needed. If a
zone is ever recreated, Azure may assign different nameservers — re-check
before relying on any record Bicep writes:

```bash
dig NS rvintake.com +short          # expect ns*-0*.azure-dns.* (x4)
dig NS rvserviceflow.com +short     # same check for the corporate zone
```

If a check returns something else, copy the zone's current nameservers
(`az network dns zone show -g rg-rvs-prod-westus3 -n <zone> --query nameServers`)
to the registrar's NS records.

**Step 0 — pre-flight (read-only).** See the change set before you commit
to it. On a first bring-up expect a wall of `Create`; on a redeploy expect
`NoChange` almost everywhere.

> **Whisper quota — northcentralus is capped at 3 units, subscription-wide.**
> `staging.bicepparam` now takes `whisperCapacity = 1` and `prod.bicepparam`
> takes `2`, so the two environments fit the cap exactly (1 + 2 = 3). This
> leaves no headroom: raising either value, or adding a third environment in
> that region, needs a quota increase first (Portal → Azure OpenAI → Quotas →
> *Whisper* / *North Central US*). If staging is still deployed at its old
> `whisperCapacity = 3`, redeploy staging before the prod pre-flight — the
> what-if fails `InsufficientQuota` until staging releases those 2 units.

```bash
az deployment sub what-if \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam
```

**Step 1 — deploy.** On the first deploy, pass the Auth0 values on the
command line (they are deliberately not in the parameter file — see *Key
Vault Configuration*), or omit them and write the secrets to the vault by
hand afterwards. On a redeploy they are optional: the Auth0 secrets module
only runs when `auth0Domain` is set, so omitting them leaves the vault's
existing `Auth0--*` secrets untouched. Pass them only to change those secrets.

```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam \
  --parameters auth0Domain='https://<tenant>.us.auth0.com/' \
               auth0Audience='https://api.rvserviceflow.com' \
               auth0ClientId='<client id>' \
               auth0ClientSecret='<client secret>' \
  --name "rvs-prod-${TS}"
```

```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam `
  --parameters auth0Domain='https://<tenant>.us.auth0.com/' auth0Audience='https://api.rvserviceflow.com' auth0ClientId='<client id>' auth0ClientSecret='<client secret>' `
  --name "rvs-prod-$ts"
```

When it finishes: `manager.rvintake.com` is bound and serving;
`rvintake.com` resolves (the ALIAS record is in place) but the SWA does not
yet accept that hostname, so the apex returns an Azure placeholder page —
and `https://` fails with a cert-name mismatch — until step 2. The
deployment's `intakeApexAction` output repeats this reminder (it is a
non-empty string only for prod).

**Step 2 — register the apex (once, ~5 minutes).** Portal path, which does
the whole TXT-token handshake for you:

1. Portal → `stapp-rvs-intake-prod` (in `rg-rvs-prod-westus2`) → *Settings* →
   **Custom domains** → **+ Add** → **Custom Domain on Azure DNS**.
2. Pick the `rvintake.com` zone, leave the hostname as the apex, **Add**.
3. Azure writes the TXT ownership record into the zone, confirms the ALIAS
   record, and validates. Watch *Status* on the row go `Validating` → `Ready`.

If the portal objects that an `@` A record already exists in the zone (it
should accept it — Bicep wrote the same ALIAS the portal would), delete it and
retry; the next Bicep redeploy re-creates it as a no-op:

```bash
az network dns record-set a delete -g rg-rvs-prod-westus3 -z rvintake.com -n @ --yes
```

CLI path, equivalent (note `hostname set` — there is no `hostname add`):

```bash
# 1. Register — --no-wait, because the operation blocks until the TXT validates
az staticwebapp hostname set -n stapp-rvs-intake-prod -g rg-rvs-prod-westus2 \
  --hostname rvintake.com --validation-method dns-txt-token --no-wait

# 2. Read the token Azure minted (this is the ONLY value `hostname show` returns —
#    it does not return IP addresses; none are needed with the ALIAS record)
TOKEN=$(az staticwebapp hostname show -n stapp-rvs-intake-prod -g rg-rvs-prod-westus2 \
  --hostname rvintake.com --query validationToken -o tsv)

# 3. Publish it
az network dns record-set txt add-record -g rg-rvs-prod-westus3 -z rvintake.com \
  -n @ -v "$TOKEN"

# 4. Wait for Ready (re-run until it flips; minutes, not the 72 h the docs quote,
#    because the zone is on Azure DNS)
az staticwebapp hostname show -n stapp-rvs-intake-prod -g rg-rvs-prod-westus2 \
  --hostname rvintake.com --query '{status:status,error:errorMessage}' -o json
```

**Step 3 — verify.**

```bash
dig +short rvintake.com A                       # resolves via the ALIAS
dig +short manager.rvintake.com CNAME      # → <name>.azurestaticapps.net
az staticwebapp hostname list -n stapp-rvs-intake-prod  -g rg-rvs-prod-westus2 -o table
az staticwebapp hostname list -n stapp-rvs-manager-prod -g rg-rvs-prod-westus2 -o table
curl -sSI https://rvintake.com | head -1        # 200 once the app is deployed by CI
curl -sS  https://<api hostname>/health
```

The SWAs serve Azure's placeholder until `deploy-production.yml` pushes the
apps; that workflow needs the two deployment tokens from this deployment's
outputs stored as GitHub secrets — see *SWA deployment tokens* in
`deployment-cmds.azcli` §6.

**Step 4 — verify and warm the custom sending domain (`#532`).** Bicep has
already provisioned the `CustomerManaged` ACS domain `mail.rvintake.com` and
written its SPF / DKIM / DMARC records into the `rvintake.com` zone. What is
left is the data-plane verification, linking the verified domain, and the
warming window — none expressible in Bicep. The `acsCustomDomainAction` deployment output
repeats this. Commands are in `deployment-cmds.azcli` §4e (2)(c).

1. **Confirm the zone records match ACS.** `az communication email domain show --domain-name mail.rvintake.com --email-service-name <acs>-email -g rg-rvs-prod-westus3 --query verificationRecords`, and spot-check each against the `rvintake.com` zone. They should already agree — Bicep wrote them from the same source.

   **Query `verificationRecords`, not `properties.verificationRecords`.** This command flattens `properties.*` to the top level, so the longer path matches nothing and prints an *empty result rather than an error* — it reads as "the domain has no records" when in fact the query is wrong. `verificationStates` flattens the same way. Same trap as `az webapp config hostname list` further below. Hit for real, 2026-09-17.
2. **Initiate verification** for each record type (`Domain`, `SPF`, `DKIM`, `DKIM2`):
   ```bash
   for t in Domain SPF DKIM DKIM2; do
     az communication email domain initiate-verification \
       --domain-name mail.rvintake.com \
       --email-service-name <acs>-email -g rg-rvs-prod-westus3 \
       --verification-type $t
   done
   ```
   Then poll until every entry in `verificationStates` is `Verified` (minutes, since the zone is on Azure DNS). Sends From `mail.rvintake.com` fail until then; the Azure-managed domain stays linked as a fallback.

   **Link the verified domain.** ACS rejects linking an unverified domain, so a brand-new custom domain's first deploy runs with `acsCustomDomainVerified = false` — that deploy only creates the domain and writes its DNS records. Once Domain, SPF, DKIM and DKIM2 all read `Verified`, set `acsCustomDomainVerified = true` in the parameter file and redeploy (step 1). `prod.bicepparam` and `staging.bicepparam` already carry `true` for their linked domains; **never set it back to `false`** — the next deploy would unlink the domain. Confirm with `az communication show -n <acs> -g rg-rvs-prod-westus3 --query linkedDomains` (the domain's own `linkedAccount` field reads `null` even when linked).
3. **Check DMARC resolves and reports are authorized:** `dig +short TXT _dmarc.mail.rvintake.com` → `v=DMARC1; p=none; rua=mailto:support@arnolddigitalsolutions.com; adkim=r; aspf=r`. The reporting address is on another domain, so the authorization records at the registrar must exist too, or receivers drop the reports. Follow the `dmarcReportAuthorizationAction` output and see [DMARC aggregate reports](#dmarc-aggregate-reports-608) below.
4. **Send quota — no request at bring-up.** Once the domain verifies, ACS applies 30 emails/min, 100/hour automatically, which covers the pilot. Request an increase only when sustained volume approaches that ceiling (`#603` tracks the alert): Portal → ACS → **Email** → **Domains** → `mail.rvintake.com` → quota request. Approval takes **up to 72 hours** and requires a sustained bounce rate **under 1 %**, so file when the alert fires, not at the hard cap.
5. **Warm the domain.** Let Jay Lyons's real intake traffic (P1, `#525`) send through `mail.rvintake.com` for **2–3 weeks** before any *other* shop's mailbox receives a packet. Ramp volume gradually; watch the ACS delivery / bounce metrics and the DMARC `rua` reports. Sustained ACS failures above ~1 % risk throttling.
6. **In-room deliverability check (second pilot onward — `FS-7` in `RVS_Plan.md`).** When onboarding a shop after Jay: send a test packet while you are with the service manager, confirm it lands in the inbox and not Junk, and have them mark the sender safe on the spot. Not needed for the first pilot.

**Step 5 — bind the `go.<zone>` redirect host (`#599`, once per environment, ~10 minutes).**
See the standalone section below; it applies to staging too, where the label is
`go-staging` instead of `go`.

**Step 2b — hand the apex token to Bicep (once, `#652`).** The apex TXT
record-set now also carries the apex SPF string, and a deploy replaces that
whole set. Copy the token from step 2 into `intakeApexValidationToken` in
`prod.bicepparam` **before the next deploy**, so the deploy re-writes the token
alongside SPF instead of deleting it. Copy it from `$TOKEN` or from the zone
(`az network dns record-set txt show -g rg-rvs-prod-westus3 -z rvintake.com -n @`).
Don't re-query `hostname show` for it: its `validationToken` reads blank once
the apex is `Ready`. A prod what-if that shows only `+ "v=spf1 -all"` on
`TXT/@` confirms the parameter matches the zone. While the parameter is blank the deploy
leaves the apex TXT set alone, and the apex has no SPF. See *Intake apex mail
posture* below.

**Redeploys** are step 1 again, with the Auth0 values optional. ARM
incremental mode leaves the apex binding alone (the template does not declare
it) and re-asserts the apex TXT set, token included, from
`intakeApexValidationToken`. It leaves the `go` hostname binding and its managed
certificate alone for the same reason, and re-asserts the ACS domain, its link
and its SPF/DKIM/DMARC records as no-ops while
`acsCustomDomainVerified = true` — redeploys never re-trigger verification.

### Bind the `go.<zone>` redirect host (`#599`)

`go.rvintake.com` (prod) and `go-staging.rvintake.com` (staging) front the
**API**, not the Intake SWA: the redirect writes to the hit log, and a static
host could serve a redirect but could not count it. `Spec A-13` routes every
distribution path — QR sticker, texted link, printed card — through it.

Bicep writes both DNS records: the CNAME to the Web App's default hostname and
the `asuid.<label>` ownership TXT, whose value comes from the site's own
`customDomainVerificationId`. What it does **not** declare is the hostname
binding and the managed certificate, for the same reason it does not declare
the Intake apex binding: the binding waits on DNS to validate and the
certificate waits on the binding, so a first bring-up from one template
deadlocks on records that template has not written yet.

Run this once per environment, after a deploy that has written the records.
Substitute the API app name, its resource group, and the label/zone.

```bash
APP=app-rvs-api-prod-wus3              # staging: app-rvs-api-staging-wus3
RG=rg-rvs-prod-westus3                 # the API's resource group
GO_HOST=go.rvintake.com                # staging: go-staging.rvintake.com
# NOT "HOST" — zsh defines HOST as a built-in holding the local machine name,
# so a line pasted into a fresh zsh resolves it to your laptop and openssl
# reports "Could not find certificate from <stdin>". Hit for real, 2026-09-16.

# 1. Confirm DNS is in place (Bicep wrote both; these should already answer)
dig +short CNAME "$GO_HOST"               # -> <app>.azurewebsites.net
dig +short TXT  "asuid.${GO_HOST%%.*}.rvintake.com"

# 2. Bind the hostname (SNI SSL comes with the certificate in step 3)
az webapp config hostname add --webapp-name "$APP" -g "$RG" --hostname "$GO_HOST"

# 3. Issue and bind a free App Service managed certificate
az webapp config ssl create --name "$APP" -g "$RG" --hostname "$GO_HOST"

# Read the thumbprint off the certificate RESOURCE, not `ssl list`. A freshly
# created managed certificate has a null serverFarmId, and `az webapp config
# ssl list` filters those out — it returns [] even though the certificate
# exists and is valid. Verified on staging, 2026-09-16.
THUMB=$(az resource show --resource-type Microsoft.Web/certificates \
  -n "$GO_HOST" -g "$RG" --query properties.thumbprint -o tsv)
echo "$THUMB"   # must be non-empty before the bind

az webapp config ssl bind --name "$APP" -g "$RG" \
  --certificate-thumbprint "$THUMB" --ssl-type SNI

# 4. Confirm the binding took. These fields are FLATTENED to the top level —
#    querying properties.sslState returns null and means nothing.
az webapp config hostname list --webapp-name "$APP" -g "$RG" \
  --query "[?name=='$GO_HOST'].{host:name, sslState:sslState, thumb:thumbprint}" -o json
# expect sslState "SniEnabled" and the thumbprint from step 3

# 5. Verify end to end. The SNI binding takes a minute or two to reach the
#    front ends; until it does, TLS serves the *.azurewebsites.net wildcard and
#    curl fails with "no alternative certificate subject name matches". That is
#    propagation, not a misconfiguration — re-run until the CN matches.
echo | openssl s_client -connect "$GO_HOST:443" -servername "$GO_HOST" 2>/dev/null \
  | openssl x509 -noout -subject          # expect CN=$GO_HOST

curl -sSI "https://$GO_HOST/<some-location-slug>?src=qr" | head -5
```

Then set `Intake:RedirectBaseUrl` for that environment to
`https://$HOST` (it is already committed in `appsettings.json` for prod and
`appsettings.Staging.json` for staging). **Leave it unset in an environment
whose host is not yet bound** — the API then falls back to the Intake host and
hands out working but untagged links, rather than links to a host that does not
answer. Managed certificates renew automatically; redeploys leave the binding
alone.

### Corporate domain mail posture (`rvserviceflow.com`)

The corporate zone neither sends nor receives mail. The packet email goes out From `mail.rvintake.com`; there are no mailboxes on `rvserviceflow.com`. Bicep now says so explicitly, in the `dnsApi` module:

| Record | Value | Why |
| --- | --- | --- |
| `MX @` | `0 .` | RFC 7505 null MX — "accepts no mail", so senders fail fast instead of retrying for days |
| `TXT @` | `v=spf1 -all` | no host is authorised to send as this domain |
| `TXT _dmarc` | `v=DMARC1; p=reject; adkim=s; aspf=s` | act on failures rather than merely observing them |

**Why this matters for a domain nobody mails.** Without SPF and DMARC, anyone can forge `From: someone@rvserviceflow.com` and a receiver has nothing to check it against. The domain is guessable — it appears in the JWT claim namespace, in the Auth0 audience, and in earlier versions of the pilot agreement — so "nobody knows it exists" was never the protection.

**No `rua` on that DMARC record, deliberately.** A reporting address on `rvintake.com` would be cross-organizational-domain and would need its own RFC 7489 §7.1 authorization record in the intake zone. A policy-only DMARC record is valid, needs no such record, and there is nothing sending here to report on.

**If corporate mailboxes are ever added here**, replace the null MX entry with real exchangers and relax SPF in the same change — a null MX with live mailboxes fails every inbound message.

> **History.** This zone carried an MX pointing at `mail.yourmailprovider.com` — a placeholder registered to Domains By Proxy, not ours — until 2026-09-17. It survived indefinitely because `dns.bicep` had no MX support, so no deploy ever asserted otherwise. That is the argument for declaring record types you do not use: an undeclared record in an IaC-managed zone is invisible to the template forever.

### Intake apex mail posture (`rvintake.com`, `#652`)

The apex sends no mail. The packet email goes out From `mail.rvintake.com` (prod) and `mail-staging.rvintake.com` (staging), and each has its own `_dmarc` record. The prod deploy (`dnsIntake` module) publishes:

| Record | Value | Why |
| --- | --- | --- |
| `TXT @` | `v=spf1 -all`, alongside the SWA apex validation token | no host is authorised to send as the apex |
| `TXT _dmarc` | `v=DMARC1; p=reject; sp=reject; adkim=s; aspf=s; rua=mailto:<dmarcReportingAddress>` | act on failures, at the apex and on every subdomain without its own record; report forgery attempts (`#608`) |
| `MX @` | `0 .` | RFC 7505 null MX: `rvintake.com` accepts no mail (`#608`) |

**Before adding a sending subdomain, read this.** DMARC falls back to the organizational domain's policy for any subdomain that has no record of its own, and `sp=reject` means a new sender (a second ACS domain, a transactional or marketing provider) with no `_dmarc` record has all of its mail rejected from the first message. The sending side sees no useful error, and nobody changed a record. Give it its own `_dmarc` record in the same change that starts it sending. `sp=reject` was chosen over `sp=none` because `mail.rvintake.com` is expected to stay the only sender, and `sp=none` would leave every subdomain spoofable. The reasoning is also in `main.bicep`, next to the record.

**The token has to be in the parameter file.** `intakeApexValidationToken` in `prod.bicepparam` carries the token Azure minted when the apex was registered (Deploy Production, step 2). If it is blank, the deploy does not declare the apex TXT set at all. That means no SPF, but it also means the deploy never deletes a token it cannot re-write. If the apex is re-registered, update the parameter before the next deploy. Staging never writes `@` or `_dmarc`.

**Null MX, because the reports go elsewhere (`#608`).** DMARC reports go to `support@arnolddigitalsolutions.com`, so nothing needs to receive mail at `rvintake.com`. The null MX says so and makes senders fail at once. Customer replies to `DoNotReply@mail.rvintake.com` are unaffected: MX is looked up for the exact host, and `mail.rvintake.com` never had one. If mailboxes are ever added at `rvintake.com`, replace the null MX with real exchangers in the same change.

Verify after a prod deploy:

```bash
NS=ns1-08.azure-dns.com        # rvintake.com's own NS set, NOT rvserviceflow.com's
dig @$NS +short TXT rvintake.com                     # token AND "v=spf1 -all"
dig @$NS +short TXT _dmarc.rvintake.com              # p=reject; sp=reject; ... rua=mailto:support@arnolddigitalsolutions.com
dig @$NS +short MX rvintake.com                      # 0 .
dig @$NS +short TXT _dmarc.mail.rvintake.com         # p=none; rua=mailto:support@arnolddigitalsolutions.com
dig @$NS +short TXT _dmarc.mail-staging.rvintake.com # p=none; rua=mailto:support@arnolddigitalsolutions.com
```

### DMARC aggregate reports (`#608`)

All three DMARC records in `rvintake.com` send aggregate (`rua`) reports to `dmarcReportingAddress`: `_dmarc.mail`, `_dmarc.mail-staging` and, in prod, `_dmarc` at the apex. That address is **`support@arnolddigitalsolutions.com`**, a monitored mailbox on the filing entity's own domain and the same contact address the Intake footer shows. `rvserviceflow.com` has no `rua`; see its section above.

**The authorization records are manual and live outside Azure.** The reporting address is on a different organizational domain from the records that name it. Under RFC 7489 §7.1, a receiver first looks up `<policy-domain>._report._dmarc.<rua-domain>` for a `TXT "v=DMARC1"`, and a conforming one **drops the report if that record is missing**. The `arnolddigitalsolutions.com` zone is hosted at its registrar, not in Azure DNS, so Bicep cannot declare these records. Instead, each deploy prints the exact names it depends on in the `dmarcReportAuthorizationAction` output. Add them once at the registrar:

| Host (FQDN) | Type | Value | Needed by |
| --- | --- | --- | --- |
| `mail.rvintake.com._report._dmarc.arnolddigitalsolutions.com` | TXT | `v=DMARC1` | prod sending domain |
| `rvintake.com._report._dmarc.arnolddigitalsolutions.com` | TXT | `v=DMARC1` | prod apex |
| `mail-staging.rvintake.com._report._dmarc.arnolddigitalsolutions.com` | TXT | `v=DMARC1` | staging sending domain |

Most registrar UIs want the host **relative to the zone**, so enter `mail.rvintake.com._report._dmarc` and so on without the trailing `.arnolddigitalsolutions.com`. The UI appends the zone name. Enter the full name and the record lands at `…arnolddigitalsolutions.com.arnolddigitalsolutions.com` and authorizes nothing.

**Use one record per policy domain, not a `*._report._dmarc` wildcard.** A wildcard would let any domain on the internet send its reports to this mailbox.

**If the address or a sending domain changes,** the list changes with it. Redeploy, read `dmarcReportAuthorizationAction` again, and add the new names before removing the old ones.

Verify (any resolver; this zone is not on Azure DNS):

```bash
for d in mail.rvintake.com rvintake.com mail-staging.rvintake.com; do
  printf '%-28s ' "$d"; dig +short TXT "$d._report._dmarc.arnolddigitalsolutions.com"
done                                                  # each: "v=DMARC1"
dig +short MX arnolddigitalsolutions.com              # the mailbox's real exchanger
```

**What to expect.** Receivers send reports about once a day, as zipped XML attachments, and only for days when mail from the domain reached them. The first `mail.rvintake.com` report arrives a day or two after the first packet email a large receiver (Google, Microsoft, Yahoo) accepts. Check that each `<record>` has `<dkim>pass</dkim>` and `<spf>pass</spf>` under `<policy_evaluated>`. DMARC passes if either one aligns, but `#608` asks for both. If SPF fails alignment while DKIM passes, look at the envelope-from (`<identifiers>` / `<auth_results><spf><domain>`) before blaming the SPF record. A run of clean reports through warming is the evidence for any later move from `p=none` to `quarantine`/`reject`. That move is out of scope for `#608`.

> **History.** `rua` pointed at `dmarc-reports@rvserviceflow.com` until 2026-09-17. It had no authorization record, and that domain's only MX was a third party's placeholder. It then pointed at `dmarc-reports@rvintake.com`, which bounced because `rvintake.com` had no MX. `#608` gave reports a real destination.

### Bind the `api.<zone>` host (`#633`)

`api.rvserviceflow.com` (prod) and `api-staging.rvserviceflow.com` (staging)
are the origin the browser apps call. **This is the one host that stays on the
corporate domain** — an XHR target is seen in devtools and a CSP, not on a
sticker. The `go.<zone>` redirect fronts the same Web App from the intake zone,
because a link on a QR code very much is customer-facing.

Until `#633` the name existed only as the Auth0 resource-server identifier — an
opaque audience string with no DNS behind it — while both Blazor apps called
the `*.azurewebsites.net` default hostname. **The audience value is unrelated
and does not change**; that it matches this hostname is a convenience, not a
coupling. Changing it would invalidate every issued token and grant.

Bicep writes the CNAME and the `asuid.<label>` ownership TXT. It does not
declare the hostname binding or the certificate, for the same reason as the
`go.<zone>` host above: the binding waits on DNS and the certificate waits on
the binding.

Run once per environment, after a deploy that has written the records.

```bash
APP=app-rvs-api-prod-wus3              # staging: app-rvs-api-staging-wus3
RG=rg-rvs-prod-westus3                 # the API's resource group
API_HOST=api.rvserviceflow.com         # staging: api-staging.rvserviceflow.com
# NOT "HOST" — zsh defines HOST as a built-in holding the local machine name,
# so a line pasted into a fresh zsh resolves it to your laptop and openssl
# reports "Could not find certificate from <stdin>".

# 1. Confirm DNS is in place (Bicep wrote both)
dig +short CNAME "$API_HOST"                          # -> <app>.azurewebsites.net
dig +short TXT  "asuid.${API_HOST%%.*}.rvserviceflow.com"

# 2. Bind the hostname
az webapp config hostname add --webapp-name "$APP" -g "$RG" --hostname "$API_HOST"

# 3. Issue and bind a free App Service managed certificate
az webapp config ssl create --name "$APP" -g "$RG" --hostname "$API_HOST"

# Read the thumbprint off the certificate RESOURCE, not `ssl list`. A freshly
# created managed certificate has a null serverFarmId, and `az webapp config
# ssl list` filters those out — it returns [] even though the certificate
# exists and is valid.
THUMB=$(az resource show --resource-type Microsoft.Web/certificates \
  -n "$API_HOST" -g "$RG" --query properties.thumbprint -o tsv)
echo "$THUMB"   # must be non-empty before the bind

az webapp config ssl bind --name "$APP" -g "$RG" \
  --certificate-thumbprint "$THUMB" --ssl-type SNI

# 4. Confirm. These fields are FLATTENED to the top level — querying
#    properties.sslState returns null and means nothing.
az webapp config hostname list --webapp-name "$APP" -g "$RG" \
  --query "[?name=='$API_HOST'].{host:name, sslState:sslState, thumb:thumbprint}" -o json

# 5. Verify end to end. The SNI binding takes a minute or two to reach the front
#    ends; until it does, TLS serves the *.azurewebsites.net wildcard and curl
#    fails with "no alternative certificate subject name matches". That is
#    propagation, not a misconfiguration — re-run until the CN matches.
echo | openssl s_client -connect "$API_HOST:443" -servername "$API_HOST" 2>/dev/null \
  | openssl x509 -noout -subject          # expect CN=$API_HOST

curl -sS "https://$API_HOST/health"
```

The App Service now answers on two custom hostnames — this one and
`go.<intake zone>` — plus its default. That is intended.

Once **both** environments answer, drop the two `*.azurewebsites.net` entries
from `connect-src` in
[`RVS.Blazor.Manager/wwwroot/staticwebapp.config.json`](../../../../RVS.Blazor.Manager/wwwroot/staticwebapp.config.json).
They are kept there during the cutover so the apps keep working if a binding
lags; leaving them permanently would mean the CSP still permits an origin
nothing should be using.

### Retire the old Manager host (`#632`)

> **Done — both environments, September 17 2026.** `manager.rvserviceflow.com` and
> `manager-staging.rvserviceflow.com` are unbound and their CNAMEs deleted; neither
> resolves. The corporate zone now holds only the API origin records. Kept below as the
> record of what was run, and as the pattern for retiring any future host.

The Manager SWA moved from `manager.rvserviceflow.com` to
`manager.rvintake.com` (`manager-staging.*` in staging). Bicep writes the new
CNAME and binds the new hostname on its own — but **removing the old record
from the template does not delete it from Azure.** Deployments run in
incremental mode, which leaves a record set nobody declares any more exactly
where it is, still resolving, still bound, still holding a certificate.

So the teardown is explicit, once per environment, and only **after** the new
host is confirmed serving:

```bash
ENV=staging                                   # or prod
SUFFIX=-staging                               # or "" for prod

# 1. Unbind the hostname from the SWA (releases its managed certificate)
az staticwebapp hostname delete \
  -n "stapp-rvs-manager-$ENV" -g "rg-rvs-$ENV-westus2" \
  --hostname "manager$SUFFIX.rvserviceflow.com"

# 2. Delete the now-orphaned CNAME from the corporate zone
az network dns record-set cname delete \
  -g rg-rvs-prod-westus3 -z rvserviceflow.com -n "manager$SUFFIX"

# 3. Confirm it is gone — must return nothing, not a redirect
dig +short "manager$SUFFIX.rvserviceflow.com"
```

Do **not** delete the `rvserviceflow.com` zone itself. It stays under IaC (the
`dnsApi` module declares it) because the API origin host binds there (`#633`)
and its no-mail posture (null MX, SPF, DMARC) has to stay asserted.

### Pre-Provision Resource Groups (all environments)

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/rg-scaffold.bicep `
  --name "rvs-rg-scaffold-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/rg-scaffold.bicep \
  --name "rvs-rg-scaffold-${TS}"
```

---

## SKU Upgrade Paths

All upgrades are performed by changing a single parameter value and redeploying.

### App Service: B1 → S1

Change `appServiceSkuName` from `'B1'` to `'S1'`:

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam `
  --parameters appServiceSkuName='S1' `
  --name "rvs-staging-sku-upgrade-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam \
  --parameters appServiceSkuName='S1' \
  --name "rvs-staging-sku-upgrade-${TS}"
```

**What changes:** Always On enabled, deployment slots available, autoscale possible.

### Cosmos DB: Serverless → Provisioned Autoscale

> **Note:** Serverless → Provisioned requires account recreation. Back up data first.

Change `cosmosCapacityMode` from `'Serverless'` to `'Provisioned'` and optionally set `cosmosAutoscaleMaxThroughput`:

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam `
  --parameters cosmosCapacityMode='Provisioned' cosmosAutoscaleMaxThroughput=4000 `
  --name "rvs-prod-cosmos-upgrade-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam \
  --parameters cosmosCapacityMode='Provisioned' cosmosAutoscaleMaxThroughput=4000 \
  --name "rvs-prod-cosmos-upgrade-${TS}"
```

**What changes:** Autoscale throughput (400–4000 RU/s default), provisioned billing.

### SWA: Free → Standard

Change `swaSkuName` from `'Free'` to `'Standard'`:

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam `
  --parameters swaSkuName='Standard' `
  --name "rvs-staging-swa-upgrade-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam \
  --parameters swaSkuName='Standard' \
  --name "rvs-staging-swa-upgrade-${TS}"
```

---

## Cross-Environment DNS RBAC

Both DNS zones (`rvserviceflow.com`, `rvintake.com`) live in the **prod** RG
(`rg-rvs-prod-westus3`) by design — apex zones are global. The staging
deployment writes CNAME records into those zones (`manager-staging` and
`staging`, both now in `rvintake.com` — #632), so the staging GitHub Actions
service principal needs write access to the zones. `rvserviceflow.com` carries
no customer-facing records; it is kept under IaC for the API origin host (#633)
and its no-mail posture.

We grant **DNS Zone Contributor** (`befefa01-2a29-4197-83a8-272ff33ce314`)
**at the zone scope only** — never at the prod RG scope.

**One-time setup:**

1. Get the staging deployer SP object id:
   ```bash
   az ad sp show --id <STAGING_APP_REG_APPID> --query id -o tsv
   ```
2. Add it to `dnsZoneContributorPrincipalIds` in `prod.bicepparam`.
3. Re-run the prod deploy. The role assignments are created at zone scope.

The staging principal can now upsert record sets in both zones but holds
no other rights on the prod RG.

---

## Key Vault Configuration

The Key Vault module uses **RBAC authorization** (not access policies). The API managed identity is automatically granted the **Key Vault Secrets User** role (get + list) when both `deployKeyVault` and `deployAppService` are `true`.

### Secrets to store manually after deployment

| Secret Name | Source | Purpose |
|---|---|---|
| `OpenAi--ApiKey` | Azure Portal → OpenAI resource → Keys | Stored automatically when Key Vault is deployed |
| `AzureCommunicationServices--ConnectionString` | ACS resource → Keys | Stored automatically when ACS + Key Vault are deployed |
| `Stripe--WebhookSecret` | Stripe Dashboard → Webhooks | **Manual** — add after Stripe configuration |

### Add Stripe webhook secret

```bash
az keyvault secret set \
  --vault-name kv-rvs-staging-wus3 \
  --name "Stripe--WebhookSecret" \
  --value "<YOUR_STRIPE_WEBHOOK_SECRET>"
```

---

## Cosmos DB Containers

The `cosmos-db.bicep` module creates 10 containers with optimized index policies matching the seed tool:

| # | Container | Partition Key | Unique Keys | Composite Indexes |
|---|---|---|---|---|
| 1 | `service-requests` | `/tenantId` | — | status+createdAtUtc, locationId+status |
| 2 | `customer-profiles` | `/tenantId` | tenantId+email | — |
| 3 | `global-customer-accounts` | `/email` | — | — |
| 4 | `asset-ledger` | `/assetId` | assetId+serviceRequestId | — |
| 5 | `dealerships` | `/tenantId` | — | type+name |
| 6 | `locations` | `/tenantId` | tenantId+slug | — |
| 7 | `slug-lookups` | `/slug` | — | — |
| 8 | `tenant-configs` | `/tenantId` | — | — |
| 9 | `lookup-sets` | `/category` | — | category+name |
| 10 | `rv-warranty-rules` | `/manufacturer` | — | — |

---

## Blob and Table Storage

The `storage-account.bicep` module creates:

- **Storage account**: Standard LRS, TLS 1.2, no public blob access
- **`rvs-attachments` container**: `PublicAccess = None` — holds intake file attachments and, since #434, generated packet PDFs under the `packets/` prefix
- **`intakeRedirectHits` table** (`#599`): the append-only `go.rvintake.com` redirect hit log, partitioned by location. No CORS — nothing in a browser talks to it. Table names are alphanumeric only, which is why this one is camelCase where the Cosmos containers are kebab-case
- **CORS rules**: Configured per environment for browser-based SAS uploads
- **Role assignments**: Storage Blob Data Contributor + Blob Delegator, and Storage Table Data Contributor, for the API managed identity (and the staging deployment slot's identity when present)

### Developer / manual blob access (`devBlobAccessPrincipalId`)

The running app authenticates with its managed identity. A **local API run uses `AzureCliCredential`**, i.e. the developer's own Entra identity, so that identity needs the same two data-plane roles on the storage account it talks to (packet generation is the first dev path that exercises Blob — a blank `BlobStorage:Endpoint` also makes `BlobServiceClient` throw at startup).

`devBlobAccessPrincipalId` (main → `storage-account.bicep`) takes the **object ID of an Entra group** (`sg-rvs-dev-blob`); the module grants it Storage Blob Data Contributor + Storage Blob Delegator + Storage Table Data Contributor (`#599`), scoped to that storage account only, with `principalType: 'Group'`. Add or remove developers via **group membership** — no redeploy.

- **Set it only in non-prod parameter files.** `staging.bicepparam` carries a `TODO` placeholder plus the `az ad group create` / `member add` / `show` commands. Deploy succeeds with it left as `''`.
- **Prod leaves it unset** (explicit comment in the prod param files). Humans get prod blob data access **just-in-time** (PIM-eligible activation) or via break-glass, never standing.
- Granting the role assignment requires the deploying principal to hold *User Access Administrator* / *Owner* on the scope.

---

## Application Insights

The `app-insights.bicep` module creates:

- **Workspace-based Application Insights** linked to Log Analytics
- **Standard availability test** on `/health` (URL ping), only when
  `deployAvailabilityTest = true` — **off in both environments today** (#674).
  Frequency (`availabilityTestFrequencySeconds`, 300 or 900) and locations
  (`availabilityTestLocations`) are parameters, because each location is billed
  per run. See [Turning the availability test on](#turning-the-availability-test-on).

**Server-side logs without App Insights (#602).** `app-service.bicep` turns on
App Service file-system logging, so the API's console log (Information and up
in staging, Warning and up in prod) is kept for 3 days, capped at 35 MB, whatever
App Insights is doing. Read it live, or pull the files:

```bash
az webapp log tail     -n app-rvs-api-<env>-wus3 -g rg-rvs-<env>-westus3
az webapp log download -n app-rvs-api-<env>-wus3 -g rg-rvs-<env>-westus3 --log-file api-logs.zip
```

---

## Monitoring & alerts

`monitor-alerts.bicep` (gated by `deployObservability`, deployed in staging and
prod) turns the packet-pipeline `LogCritical` events — which until now only
landed in Application Insights — into Azure Monitor alerts (#494).

**Ops action group** — `ag-rvs-ops-<env>-wus3` (short name `rvs-ops-stg` /
`rvs-ops-prod`). Receivers come from the `opsAlertEmailReceivers` parameter,
committed as a real default in both param files (#639) — do **not** clear it
back to `[]` and add a receiver in the portal instead. The Action Groups
resource provider does a full-replace PUT on `emailReceivers`; a template that
declares (or omits) the property as empty deletes whatever is live, portal-added
receivers included. To change the receiver, edit the param file or override on
the deploy:

```bash
az deployment sub create ... \
  --parameters opsAlertEmailReceivers='[{"name":"oncall","email":"ops@yourco.com"}]'
```

If the parameter is ever left empty, the rules still evaluate and fire but
notify nobody — the `opsAlertReceiverAction` deployment output flags this.

**Alert rules** — log-search rules (`Microsoft.Insights/scheduledQueryRules`,
`kind: LogAlert`) scoped to the App Insights component. Each query is
`union traces, exceptions | where tostring(customDimensions.EventId) == "<id>"`
and projects the structured log properties as **split dimensions**, so the
alert payload carries `TenantId` plus the offending `LocationId` /
`ServiceRequestId`. `union traces, exceptions` because 434001 logs with an
exception argument and so lands in `exceptions`, not `traces`.

| Rule | EventId | Tier | Cadence / window | Dimensions |
|---|---|---|---|---|
| `sqr-rvs-packet-recipients-bounced-<env>-wus3` | 439002 `AllRecipientsBounced` | Sev 1 — page | 5 min / 5 min | `LocationId`, `TenantId` |
| `sqr-rvs-packet-email-delivery-exhausted-<env>-wus3` | 438001 `PacketEmailDeliveryExhausted` | Sev 1 — page | 5 min / 5 min | `ServiceRequestId`, `TenantId` |
| `sqr-rvs-packet-generation-exhausted-<env>-wus3` | 434001 `PacketGenerationExhausted` | Sev 1 — page | 5 min / 5 min | `ServiceRequestId`, `TenantId` |
| `sqr-rvs-packet-email-oversized-<env>-wus3` | 521001 `PacketEmailOversized` | Sev 1 — page | 5 min / 5 min | `ServiceRequestId`, `TenantId` |
| `sqr-rvs-packet-recipient-bounced-warn-<env>-wus3` | 439001 `RecipientHardBounced` | Sev 3 — digest | 1 h / 6 h | `LocationId`, `TenantId` |
| `sqr-rvs-law-daily-cap-reached-<env>-wus3` | — (workspace `OverQuota`) | Sev 2 | 15 min / 1 h | — |
| `ma-rvs-api-availability-<env>-wus3` ¹ | — (`/health` test failing) | Sev 1 — page | 5 min / one test interval | — |
| `sqr-rvs-api-telemetry-dark-<env>-wus3` ¹ | — (pings pass, no `requests`) | Sev 2 | 15 min / 1 h | — |

¹ Only exists while `deployAvailabilityTest = true`; off in both environments
today. See [Turning the availability test on](#turning-the-availability-test-on).

**The alerts cannot see their own blindness on their own.** Every rule above
reads the workspace, so anything that stops telemetry reaching it silences them
all, with nothing to say why (#602). Two rules cover the two ways that happens:

- **Workspace stops ingesting** — the daily cap. `sqr-rvs-law-daily-cap-reached`
  reads `_LogOperation`, which the cap does not block.
- **The app stops sending** — the SDK dies while the app keeps serving. The
  availability service writes `availabilityResults` itself, independently of
  the app, and each passing ping makes a request the SDK should record.
  `sqr-rvs-api-telemetry-dark` fires when at least two pings passed in the last
  hour and `requests` is empty. A daily-cap stop empties both tables at once, so
  it does not fire this rule; the cap alert covers it.

Without the availability test there is nothing to compare against: on an idle
B1 worker with no Always On, "no requests for an hour" is the normal state.
**So while the test is off, a repeat of #602 is caught by nothing.**

5 minutes is the practical near-real-time floor for log-search alerts; the four
Sev 1 rules use it. **439001** (one recipient disabled, others still receive
packets) is deliberately lower: it is not a delivery failure, so it does not
page — a Sev 3 rule on a 6-hour window evaluated hourly reads as a digest. Fix
the address before it becomes the last active one (439002).

**521001** (`PacketEmailOversized`) pages from day one. Since #566 validates the
email size budget at startup, it can no longer be reached by a misconfigured
budget — it now fires only when the packet PDF has grown past its ~1.5–3 MB norm
(a renderer regression, or an oversized embedded asset such as a per-location
logo). The email still delivered and any photos that fit were still attached, so
nothing else catches the missing PDF. Treat any occurrence as a bug to chase.
The sibling `LogWarning` for the ordinary photo-heavy case (some attachments
trimmed, no `EventId`) is informational and is not alerted.

### Runbook — on-call

- **439002 `AllRecipientsBounced`** — packets for that location are going
  nowhere. In the manager app (or Cosmos), open the location's packet settings
  and **fix or replace the bounced address in `recipients`, then save**.
  `LocationService.UpdateAsync` reconciles the disabled list — re-enabling a
  bounced address is just "add it back to `recipients` and save". The alert
  auto-resolves once no 439002 recurs within the window. Payload carries
  `LocationId` + `TenantId`.
- **438001 / 434001** — a service request has no packet delivered / generated
  after 3 attempts. Payload carries `ServiceRequestId` + `TenantId`. Check the
  correlated traces/exceptions for the failure, then trigger regeneration
  (`POST /api/.../packet:regenerate`, per `PacketGenerationService`).
- **521001 `PacketEmailOversized`** — the email went out without its PDF.
  Not a delivery incident; it is a rendering-size regression. Pull the packet
  for that `ServiceRequestId` from the manager app, check the PDF size and what
  inflated it (embedded image resampling, a per-location logo, the HTML body),
  and file a bug.
- **439001 `RecipientHardBounced`** (digest) — one address on a location was
  disabled; delivery still works via the others. Replace it at leisure before
  the location hits 439002.

Verify the pipeline end to end (payload really carries the dimensions):

```kusto
union traces, exceptions
| where tostring(customDimensions.EventId) in ("439002","438001","434001","521001","439001")
| project timestamp, customDimensions.EventId, SeverityLevel,
          LocationId = tostring(customDimensions.LocationId),
          ServiceRequestId = tostring(customDimensions.ServiceRequestId),
          TenantId = tostring(customDimensions.TenantId)
| order by timestamp desc
```

### Verifying the alert rules

Deploy-time checks only prove the rules are *valid*. They do not prove the rules
*fire* (#602, #732). Re-run both steps after anything that could break the
chain: a change to `monitor-alerts.bicep` or the action group, an App Insights
SDK upgrade, or a telemetry outage like #602. Before you start, confirm that
telemetry is flowing. Step 1 of [Runbook — telemetry gone dark](#runbook--telemetry-gone-dark)
should show `AppTraces` rows from the last hour.

**1. Synthetic: all five rules, end to end.** From this folder:

```bash
./verify-alert-rules.sh staging
```

The script posts one record per alerted EventId to the component's ingestion
endpoint, shaped as the app's exporter writes them. That means `EventId`,
`EventName` and the template arguments go into `customDimensions`, and 434001
goes in as an exception. The IDs are obviously fake (`ten_alertcheck`,
`loc_alertcheck_*`, `sr_alertcheck_*`). It then prints the KQL and the `az rest`
command for checking the result. Pass when:

- all five rows are in `traces` / `exceptions`;
- the four Sev 1 rules fire within ~15 minutes, and
  `recipient-bounced-warn` fires on its next hourly run;
- the ops mailbox gets one email per rule, and each names `TenantId` plus the
  `LocationId` or `ServiceRequestId`;
- every alert auto-resolves after one clean window.

Against `prod` the same script pages the prod on-call.

**2. Real: prove the app emits that shape (staging only).** Step 1 bypasses the
app. The one alerted event staging can produce on demand is 438001, so break
email sending on purpose:

```bash
rg=rg-rvs-staging-westus3; app=app-rvs-api-staging-wus3
orig=$(az webapp config appsettings list -g $rg -n $app \
  --query "[?name=='AzureCommunicationServices__Email__FromAddress'].value" -o tsv); echo "$orig"
az webapp config appsettings set -g $rg -n $app \
  --settings AzureCommunicationServices__Email__FromAddress=DoNotReply@unlinked.invalid
# Submit one intake at staging.rvintake.com. ACS rejects each send, the three
# delivery attempts fail (backoff RetryBaseDelay × 1, × 2), and the third logs 438001.
az webapp config appsettings set -g $rg -n $app \
  --settings AzureCommunicationServices__Email__FromAddress="$orig"
```

Changing the setting restarts the app, so the first request cold-starts. The
email alert should name the real `ServiceRequestId` and tenant. The next Bicep
deploy would also restore the sender, but do not wait for it: until you restore
it, staging sends no email at all.

What neither step can show from live traffic:

- **439001 / 439002.** Nothing calls `LocationService.DisableRecipientForBounceAsync`
  yet, because the inbound bounce signal was left out of #494. Both rules are
  covered by step 1 only. The log calls use the same `LocationId`/`TenantId`
  template shape as 438001, so step 2 proves that shape transitively.
- **434001 / 521001.** Neither can be triggered safely: generation has
  rule-based fallbacks, and startup validation keeps the email budget out of
  reach. Step 1 covers both, and 434001's `exceptions` path with it.

### Turning the availability test on

Off in both environments (#674): each location is billed per run. Turning it on
creates the test **and** the two alerts that read it, so a failure pages the ops
action group and dark telemetry is caught. Do it per environment, in its own
`.bicepparam`, and redeploy:

```bicep
param deployAvailabilityTest = true
param availabilityTestFrequencySeconds = 900   // 300 or 900
param availabilityTestLocations = [
  'us-ca-sjc-azr'
]
```

Scale it with how much a missed outage would cost, not all at once:

| Stage | Frequency | Locations | Runs / month | Detects an outage within |
|---|---|---|---|---|
| Staging, or prod pre-traffic | 900 s | 1 (`us-ca-sjc-azr`) | ~2.9K | ~15–20 min |
| Prod, first paying shops (G-3) | 900 s | 1 | ~2.9K | ~15–20 min |
| Prod, several shops depending on it | 300 s | 3 (`us-ca-sjc-azr`, `us-tx-sn1-azr`, `us-va-ash-azr`) | ~26K | ~5–10 min |

- With **one location**, one failed run pages. With **several**, the alert waits
  for all but one to fail, so a single location's own trouble does not page.
- The dark-telemetry rule needs **two passing pings an hour**. Every row in the
  table gives at least four, so any of them is enough.
- Neither setting affects the Log Analytics daily cap in a way that matters:
  each ping adds one `availabilityResults` row and one `requests` row.

To turn it off again, set `deployAvailabilityTest = false`. The redeploy removes
nothing on its own, because incremental mode leaves existing resources in place.
Delete the test and both rules by hand:

```bash
az monitor app-insights web-test delete -g rg-rvs-<env>-westus3 -n avail-appi-rvs-api-<env>-wus3
az monitor metrics alert delete        -g rg-rvs-<env>-westus3 -n ma-rvs-api-availability-<env>-wus3
az monitor scheduled-query delete      -g rg-rvs-<env>-westus3 -n sqr-rvs-api-telemetry-dark-<env>-wus3
```

### Runbook — telemetry gone dark

`sqr-rvs-api-telemetry-dark` fired, or you noticed App Insights is empty while
the app works (#602). Run each step in the workspace's **Logs** blade unless
it says otherwise.

1. **Which side stopped?** Compare what the platform wrote with what the app wrote:

   ```kusto
   union withsource = Table AppAvailabilityResults, AppRequests, AppTraces, AppExceptions, AppDependencies
   | where TimeGenerated > ago(2d)
   | summarize Rows = count(), Last = max(TimeGenerated) by Table
   ```

   - `AppAvailabilityResults` keeps arriving while the app tables stop: **the app
     stopped sending**. Go to step 3.
   - Everything stops at the same instant: **the workspace stopped ingesting**.
     Go to step 2.

2. **Workspace side.** Check for the cap, and for ingestion errors:

   ```kusto
   _LogOperation
   | where TimeGenerated > ago(2d)
   | project TimeGenerated, Category, Operation, Level, Detail
   ```

   Also check both caps. The workspace cap is `logAnalyticsDailyCapGb`. The App
   Insights component has its own legacy cap that Bicep does not set, so it
   should be the 100 GB default:

   ```bash
   az monitor log-analytics workspace show -g rg-rvs-<env>-westus3 -n law-rvs-obs-<env>-wus3 --query workspaceCapping
   az monitor app-insights component billing show -g rg-rvs-<env>-westus3 --app appi-rvs-api-<env>-wus3
   ```

3. **App side.** Read the file-system log from around the cutoff (see
   [Application Insights](#application-insights)), looking for a startup after
   the last good row and for any exception during it. A platform recycle is not
   in the activity log. Look in the portal instead: App Service → *Diagnose and
   solve problems* → *Web App Restarted*. Changes that someone made do appear in
   the activity log:

   ```bash
   az monitor activity-log list -g rg-rvs-<env>-westus3 \
     --start-time <cutoff − 1h> --end-time <cutoff + 1h> \
     --query "[].{t:eventTimestamp, op:operationName.localizedValue, status:status.value, caller:caller}" -o table
   ```

   The SDK's exporter reports its own failures through OpenTelemetry
   self-diagnostics, not `ILogger`, so they are not in the console log. To
   capture them, upload an `OTEL_DIAGNOSTICS.json` to the app's content root
   (`/home/site/wwwroot`) containing
   `{"LogDirectory": "/home/LogFiles", "FileSize": 1024, "LogLevel": "Warning"}`,
   then restart. Remove it afterwards.

4. **Recover.** `az webapp restart`, then request `/health` a few times and
   confirm new `AppRequests` rows within about 5 minutes. If restarting brings
   telemetry back, the next recycle might stop it again. To test that, let the
   worker idle for more than 20 minutes and request `/health` once, so the app
   cold-starts, then check again.

---

## Communication Services — Email

`communication-services.bicep` provisions the ACS account, an Email Service, and
an **Azure-managed email domain** (`<guid>.azurecomm.net`). In **staging and prod**
it also provisions a **CustomerManaged sending subdomain** — `mail-staging.rvintake.com`
and `mail.rvintake.com` respectively (`acsCustomEmailDomain`, issue `#532`) — and
links both domains to that environment's account.
`AcsEmailNotificationService` sends the service-department packet email (`#437`)
and authenticates with the API **managed identity** (`DefaultAzureCredential`),
so two things must line up with the deployed resource — both now handled by Bicep:

| Concern | How Bicep handles it |
|---|---|
| **Send permission** | `communication-services.bicep` grants the API managed identity (and the S1 staging-slot identity) **Contributor scoped to the ACS resource** — ACS has no granular data-plane "email sender" role yet. Wired from `main.bicep` (`apiPrincipalId` / `stagingSlotPrincipalId`) exactly like the Key Vault / Storage grants. |
| **Sender address** | `app-service-config.bicep` sets the app setting `AzureCommunicationServices__Email__FromAddress` to `DoNotReply@<sender domain>` — the custom verified subdomain (`mail.rvintake.com`) when `acsCustomEmailDomain` is set, otherwise the Azure-managed domain. `main.bicep` picks between `communicationServices.outputs.customFromSenderDomain` and `.azureManagedMailFrom`. The hardcoded `DoNotReply@<guid>.azurecomm.net` was removed from `RVS.API/appsettings.json` so a stale value cannot shadow it. |

A plain [idempotent redeploy](#quick-start-deployment) applies both. RBAC
propagation can take a few minutes.

**Local development** sends through **staging's** ACS resource, never prod's.
`RVS.API/appsettings.Development.json` sets the staging endpoint and the From
address `DoNotReply@mail-staging.rvintake.com`, and the API authenticates with
`AzureCliCredential` (the same shortcut Blob uses). Your `az login` identity
therefore needs **Contributor** on `acs-rvs-notify-staging-wus3-s01-001`;
subscription Owner already covers it. Local intake runs send real email.

### Custom sending domains — `mail.rvintake.com`, `mail-staging.rvintake.com` (`#532`)

The Azure-managed `*.azurecomm.net` domain caps at **5 emails/min, 10/hour with
no support path to raise it** (`#521`) and carries no sender reputation — a spam
quarantine at a pilot shop is the one failure the "no IT involvement" pitch
cannot survive. Prod therefore sends from a dedicated verified subdomain.

Staging sends from its own verified subdomain on its **own ACS resource**, never
prod's. ACS tracks failures, the suppression list and send quota per resource and
domain, and staging fails many sends (seeded recipients are `.example.com`), so
sharing prod's resource would spend prod's bounce budget while it warms.
`mail.staging` is a sibling of `mail`, not a child of it. Mailbox providers still
weigh any subdomain partly against `rvintake.com`, so the staging subdomain is
kept harmless by behaviour: staging mail that reaches a real inbox goes only to
mailboxes we control.

**What Bicep does** (any env whose params set `acsCustomEmailDomain` — staging and prod):

- Provisions a `CustomerManaged` `Microsoft.Communication/emailServices/domains`
  named after `acsCustomEmailDomain`, and adds it to the account's `linkedDomains`
  only when `acsCustomDomainVerified = true` (ACS rejects linking an unverified domain).
- Writes the records ACS requires into the `rvintake.com` zone via `dnsIntake`:
  **domain-ownership** TXT, **SPF** TXT (`v=spf1 include:… -all` — ACS fails
  verification on `~all`), and **DKIM** + **DKIM2** CNAMEs. Values come from
  `communicationServices.outputs.customDomainVerificationRecords`; `main.bicep`
  builds the record-set names from the subdomain label (`mail` / `mail.staging`),
  because ACS returns DKIM names as a bare selector and Domain/SPF names as the
  full FQDN — neither is zone-relative. There is nothing to transcribe.
- Publishes **DMARC** at `_dmarc.mail` (prod) / `_dmarc.mail-staging` (staging) — `v=DMARC1; p=none; rua=mailto:<dmarcReportingAddress>`
  — authored in `main.bicep` (not taken from ACS) so the policy stays `p=none`
  and the reporting mailbox is ours. `p=none` makes alignment failures visible
  in the aggregate reports without dropping mail while the domain is cold. The
  reports only arrive if the registrar-side authorization records exist; see
  [DMARC aggregate reports](#dmarc-aggregate-reports-608).

**What stays manual** (surfaced by the `acsCustomDomainAction` output — see
"Deploy Production" step 4): `az communication email domain initiate-verification`
for each record type, the follow-up deploy with `acsCustomDomainVerified = true`
that links the verified domain, and **domain warming** (prod only). The send-quota
increase is not part of bring-up — the default 30/min, 100/hour covers the pilot,
and the request is filed when volume warrants it (`#603`).

### Manual steps after the deploy (not expressible in Bicep)

Commands for each are in `deployment-cmds.azcli` §4e. Summary:

1. **Verify the sending domain.** Staging and prod send from a custom domain: run "Deploy Production" step 4 (1)–(3) against that environment's domain, ACS resource and resource group — sends From it fail until every record shows `Verified`. The Azure-managed fallback domain verifies automatically but can lag (`az communication email domain show` → `provisioningState = Succeeded`); sends from it fail with `DomainNotLinked` until it completes.
2. **Read back the real sender domain** (`properties.fromSenderDomain`) and confirm the deployed `AzureCommunicationServices__Email__FromAddress` app setting is `DoNotReply@<that domain>`.
3. **Confirm the RBAC grant landed** (`az role assignment list --scope <acs-resource-id>`). If not (older Bicep, or propagation), assign **Contributor** on the ACS resource by hand — §4e (1).
4. **Check the ACS email send quota.** A verified custom domain starts at 30/min, 100/hour — enough for staging and for the prod pilot, so there is no request to file at bring-up. Prod raises it against `mail.rvintake.com` when the `#603` volume alert fires — "Deploy Production" step 4. An environment left on the Azure-managed domain is capped at 10/hour, and no request lifts that.
5. **Set a real recipient on a staging Location.** Seed data uses RFC 2606 `.example.com` addresses that hard-bounce. Point at least one location's `packetConfig.recipients` at a mailbox you control — `PUT /api/dealers/{dealerId}/locations/{locationId}` or directly in Cosmos. `packetConfig.enabled` defaults to `true`. Only ever use mailboxes you control in staging: it is the rule that keeps `mail-staging.rvintake.com` from affecting `rvintake.com`'s reputation.
6. **Bind the custom hostnames on the API Web App.** `go.<intake zone>` (`#599`) and `api.<corporate zone>` (`#633`). Bicep writes both sets of DNS records but can declare neither binding — see "Bind the `go.<zone>` redirect host" and "Bind the `api.<zone>` host" above. The `apiHostBindingAction` deployment output repeats this. Until the `api.` host answers, leave the Blazor apps' `ApiBaseUrl` on the `*.azurewebsites.net` name.
7. **Run the end-to-end check.** Complete a staging intake; App Insights should show `ACS packet email send initiated …` from `AcsEmailNotificationService`. A failure logs `Packet email dispatch failed …` from `PacketGenerationService` and is otherwise swallowed (packet generation still reports `Succeeded`; retry/idempotency is `#438`). Confirm the mail arrives with the PDF + photo attachments, subject `[RVS] {category} — {year} {make} {model} — {customer last name}`.

> **Prod sends from `mail.rvintake.com` and staging from `mail-staging.rvintake.com`,
> not the managed domain** — provisioned by Bicep (`acsCustomEmailDomain`), verified
> by hand, and warmed in prod only. See "Custom sending domains" above and "Deploy
> Production" step 4.

---

## Estimated Monthly Costs

| Environment | Estimated Monthly Cost | Notes |
|---|---|---|
| **Staging** | ~$50–70 | B1 App Service, Serverless Cosmos, Standard SWAs |
| **Production** | ~$60–120 | Same SKUs; higher OpenAI TPM allocation |
| **Total** | ~$110–190 | Two independent cloud environments |

---

## Post-Deployment: Auth0 Configuration

Auth0 is configured outside of Bicep (external SaaS), and almost all of it is
scripted rather than clicked: the API, permissions, roles, applications, grants,
connections and the Post-Login Action are declared in `Infra/Auth0/baseline/` and
applied with `auth0-apply.sh`. See **`Docs/ASOT/Auth0/Auth0-Portal-Configuration-Checklist.md`**
for the portal-only remainder, and `Docs/ASOT/RVS_Identity.md` for the model.

Three earlier statements here were wrong and are worth naming, because each is
the opposite of how it works:

- **One tenant serves development, staging and production** — `dev-2jhzz8xmjggh26pm.us.auth0.com`
  (#610). A second tenant needs a paid plan, so the split is deferred. Not "never
  share tenants across environments"; sharing is the current, deliberate design,
  with the risks recorded in `RVS_Identity.md`.
- **RVS does not use Auth0 Organizations.** Tenant context is `app_metadata.tenantId`,
  injected into the access token by a Post-Login Action. There is no Organization
  per customer and there is not meant to be.
- **The audience is not the App Service hostname.** It is the fixed opaque string
  `https://api.rvserviceflow.com`. Since #633 a host of the same name also exists;
  they are not coupled, and changing the audience invalidates every issued token.

### DNS this template writes for Auth0

One record, once the Auth0 custom domain exists: `login.rvintake.com` in the
`dnsIntake` module, from `auth0CustomDomainCnameTarget` (#627). It is the only
record here that is **not** per-environment — one custom domain serves all three
environments, so both parameter files write the same name and value. The
parameter is empty until the domain is created in the Auth0 dashboard, and the
record is a no-op until then. Checklist §6.3.

---

## Architecture Decision Records

| Decision | Rationale |
|---|---|
| **App Service B1** | Cost-optimized for MVP; no Always On accepted. Upgrade path to S1. |
| **Cosmos DB Serverless** | Pay-per-request; no minimum cost. Upgrade when RU cost exceeds ~$100/month. |
| **SWA Standard** | Required for custom auth (Auth0) and custom domains. |
| **Key Vault RBAC** | Modern best practice; no access policies to manage. |
| **Separate parameter files** | Independent staging and prod deployment; prod not deployed until needed. |
| **Feature flags (deploy*)** | Granular control over which resources are provisioned per environment. |
