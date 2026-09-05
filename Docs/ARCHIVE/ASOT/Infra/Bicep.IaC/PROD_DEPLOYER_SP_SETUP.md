# Production Deployer Service Principal — Setup Runbook

This runbook walks you through provisioning the GitHub Actions OIDC service
principal that the **production** deploy workflow uses, and confirms the value
that goes into `dnsZoneContributorPrincipalIds` in
[parameters/prod_phase1.bicepparam](parameters/prod_phase1.bicepparam) and
[parameters/prod_phase2.bicepparam](parameters/prod_phase2.bicepparam).

> **Companion doc:** [.github/workflows/README.md](../../../../.github/workflows/README.md)
> covers the SWA tokens, federated credentials, and GitHub environment variables
> for the deploy workflows themselves. This doc is the **DNS Zone Contributor**
> angle: granting the deployer the minimum rights it needs to write DNS records
> into the prod-owned zones.

---

## TL;DR

You have two reasonable choices:

| Option | When to pick it | DNS principal entries |
|---|---|---|
| **A. Reuse the existing staging SP** (`github-actions-rvs-deployment`, app id `de5714f8-924a-4761-b278-9e7a94f2d116`) | Single-engineer or small-team setup; you trust one SP to deploy both envs | Just **one** entry — already populated in the param files |
| **B. Create a separate production SP** | Multi-team / SOC2 / blast-radius isolation; you want prod creds rotated independently | **Two** entries — staging SP + new prod SP |

The current param files reflect **Option A**. Steps below cover both.

---

## Option A — Reuse the staging SP for production

### A.1 Add a federated credential for the production environment

The existing app registration is wired for `repo:…:environment:staging`. Add a
second federated credential so the same SP can also assume tokens issued by the
GitHub `production` environment.

```bash
APP_ID="de5714f8-924a-4761-b278-9e7a94f2d116"

az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "github-actions-production",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:markarnoldutah/RVS:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

Verify both subjects are present:

```bash
az ad app federated-credential list --id "$APP_ID" \
  --query '[].{name:name, subject:subject}' -o table
```

You should see:

```
Name                       Subject
-------------------------  ------------------------------------------------
github-actions-staging     repo:markarnoldutah/RVS:environment:staging
github-actions-production  repo:markarnoldutah/RVS:environment:production
```

### A.2 Grant Contributor on the production resource groups

```bash
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

# Primary RG (API, Cosmos, KV, Storage, ACS, OpenAI, App Insights)
az role assignment create \
  --assignee "$APP_ID" \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-rvs-prod-westus3"

# Whisper RG (northcentralus)
az role assignment create \
  --assignee "$APP_ID" \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-rvs-prod-ncus"

# Static Web Apps RG (westus2)
az role assignment create \
  --assignee "$APP_ID" \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-rvs-prod-westus2"
```

> The DNS zones live inside `rg-rvs-prod-westus3`, so RG-wide Contributor on
> the primary RG already lets the SP touch them. The
> `dnsZoneContributorPrincipalIds` mechanism in the param files is **only
> needed when a less-privileged principal** (a different env's deployer) needs
> zone access without RG-wide rights. Under Option A the prod SP is
> Contributor on the RG already — but we still grant DNS Zone Contributor at
> the zone scope so the assignment exists for any future prod deployer that is
> *not* an RG Contributor.

### A.3 Create the GitHub `production` environment

In **GitHub → Settings → Environments → New environment** (name: `production`):

- **Required reviewers**: enable for manual approval (recommended).
- **Variables**:
  - `AZURE_CLIENT_ID` = `de5714f8-924a-4761-b278-9e7a94f2d116`
  - `AZURE_TENANT_ID` = `<your tenant id>` (`az account show --query tenantId -o tsv`)
  - `AZURE_SUBSCRIPTION_ID` = `<your subscription id>`
  - `API_APP_SERVICE_NAME` = `app-rvs-api-prod-wus3`
- **Secrets** (from `az staticwebapp secrets list` after phase 1):
  - `INTAKE_SWA_TOKEN_PRODUCTION`
  - `MANAGER_SWA_TOKEN_PRODUCTION`

### A.4 `dnsZoneContributorPrincipalIds` — already correct

The current param file content is already correct for Option A:

```bicep
param dnsZoneContributorPrincipalIds = [
  // Display Name:  github-actions-rvs-deployment
  // APP_ID: de5714f8-924a-4761-b278-9e7a94f2d116
  '9b1e460a-6bed-418a-9d65-1bbed1f768a9'
]
```

No change needed. Proceed to phase 1 deploy.

---

## Option B — Create a separate production SP

### B.1 Create the new app registration + service principal

```bash
PROD_APP_NAME="github-actions-rvs-deployment-prod"

PROD_APP_ID=$(az ad app create \
  --display-name "$PROD_APP_NAME" \
  --query appId -o tsv)

az ad sp create --id "$PROD_APP_ID"

PROD_SP_OBJECT_ID=$(az ad sp show --id "$PROD_APP_ID" --query id -o tsv)

echo "PROD_APP_ID=$PROD_APP_ID"
echo "PROD_SP_OBJECT_ID=$PROD_SP_OBJECT_ID"
```

Save both values — `PROD_APP_ID` goes in GitHub variables, `PROD_SP_OBJECT_ID`
goes in the Bicep param files.

### B.2 Add the federated credential for the `production` environment

```bash
az ad app federated-credential create \
  --id "$PROD_APP_ID" \
  --parameters '{
    "name": "github-actions-production",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:markarnoldutah/RVS:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

### B.3 Grant Contributor on the production resource groups

```bash
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

for RG in rg-rvs-prod-westus3 rg-rvs-prod-ncus rg-rvs-prod-westus2; do
  az role assignment create \
    --assignee "$PROD_APP_ID" \
    --role "Contributor" \
    --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RG"
done
```

### B.4 Create the GitHub `production` environment

Same as Option A.3, except `AZURE_CLIENT_ID` = `$PROD_APP_ID` (the new one).

### B.5 Update `dnsZoneContributorPrincipalIds` to include both SPs

The staging SP needs zone access (its `staging.bicepparam` writes the
`manager-staging` and `staging` CNAMEs into the prod-owned zones). The new prod
SP gets RG-wide Contributor in B.3, so a zone-scoped grant is technically
redundant — but include it for explicit-intent and so the assignment exists if
the prod SP is ever downgraded off RG Contributor.

Edit both [prod_phase1.bicepparam](parameters/prod_phase1.bicepparam) and
[prod_phase2.bicepparam](parameters/prod_phase2.bicepparam):

```bicep
param dnsZoneContributorPrincipalIds = [
  // Display Name:  github-actions-rvs-deployment   (staging SP)
  // APP_ID: de5714f8-924a-4761-b278-9e7a94f2d116
  '9b1e460a-6bed-418a-9d65-1bbed1f768a9'

  // Display Name:  github-actions-rvs-deployment-prod
  // APP_ID: <PROD_APP_ID from B.1>
  '<PROD_SP_OBJECT_ID from B.1>'
]
```

---

## Verification (both options)

After running `prod_phase1.bicepparam`, confirm the role assignments landed at
**zone scope** (not RG scope):

```bash
ZONE_ID=$(az network dns zone show \
  --name rvintake.com \
  --resource-group rg-rvs-prod-westus3 \
  --query id -o tsv)

az role assignment list \
  --scope "$ZONE_ID" \
  --query '[].{principalName:principalName, role:roleDefinitionName}' -o table
```

Expect to see `DNS Zone Contributor` for each principal id you listed in
`dnsZoneContributorPrincipalIds`.

Repeat for `rvserviceflow.com`.

---

## Why zone-scoped (and not RG-scoped)

The DNS module is invoked from both staging and prod deploys against the
**prod** RG (`rg-rvs-prod-westus3`), because apex zones are global and we
intentionally co-locate them. Granting the staging deployer RG-wide
Contributor on the prod RG would let it touch prod App Service, Cosmos,
Key Vault, Storage, ACS, OpenAI — far more than the CNAME write it actually
needs. Zone-scoped DNS Zone Contributor caps the blast radius to the two
record sets the staging deploy actually upserts.
