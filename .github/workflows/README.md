# CI/CD Workflow Runbook

## Architecture Overview

```
build-test.yml          PR gate — build + test, throwaway artifacts
        │
        ▼ (merge to main)
deploy-staging.yml      Build, test, publish artifacts, deploy to staging
        │
        ▼ (manual trigger)
deploy-production.yml   Promote exact staging artifacts to production (never rebuilds)
```

### Principles

- **build-test.yml** runs on every push/PR. It is the merge gate. It never deploys.
- **deploy-staging.yml** is the single place that builds deployable artifacts. It runs on
  push to `main`, detects which apps changed, builds the full solution, runs all tests,
  then publishes and deploys only the changed apps.
- **deploy-production.yml** never rebuilds from source. It promotes the exact binaries that
  staging already validated — either via App Service slot swap (API) or by downloading
  the staging run's uploaded artifacts (SWAs).

---

## Auth Model Per Resource Type

| Resource          | Auth mechanism     | Stored as                  | Needed in           |
| ----------------- | ------------------ | -------------------------- | ------------------- |
| API (App Service) | Azure OIDC (RBAC)  | GitHub environment **vars** | staging, production |
| Intake SWA        | SWA deployment token | GitHub environment **secrets** | staging, production |
| Manager SWA       | SWA deployment token | GitHub environment **secrets** | staging, production |

- **OIDC** = no long-lived secrets. GitHub requests a short-lived token from Azure AD each run.
  Requires an Entra ID app registration with federated credentials.
- **SWA tokens** = long-lived API keys retrieved once from Azure and stored as GitHub secrets.
  No OIDC or Azure login required for SWA deployments.

---

## GitHub Environment Setup

### Staging (already configured ✅)

#### Variables (Settings → Environments → staging → Variables)

| Variable                | Value                                    |
| ----------------------- | ---------------------------------------- |
| `AZURE_CLIENT_ID`       | App registration client ID (GUID)        |
| `AZURE_TENANT_ID`       | Entra ID tenant ID (GUID)                |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID (GUID)             |
| `API_APP_SERVICE_NAME`  | App Service name (e.g. `rvs-api-staging`) |

#### Secrets (Settings → Environments → staging → Secrets)

| Secret                       | Value                        |
| ---------------------------- | ---------------------------- |
| `INTAKE_SWA_TOKEN_STAGING`   | Intake SWA deployment token  |
| `MANAGER_SWA_TOKEN_STAGING`  | Manager SWA deployment token |

---

### Production (configure when resources are provisioned)

#### Variables (Settings → Environments → production → Variables)

| Variable                | Value                                        |
| ----------------------- | -------------------------------------------- |
| `AZURE_CLIENT_ID`       | Same app registration or a production-only one |
| `AZURE_TENANT_ID`       | Same tenant ID                                |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID (may differ from staging)     |
| `API_APP_SERVICE_NAME`  | App Service name (e.g. `rvs-api-production`)  |
| `AZURE_RESOURCE_GROUP`  | RG containing the API App Service             |

> **Note**: `AZURE_RESOURCE_GROUP` is only needed in production because the slot swap uses
> `az webapp deployment slot swap` (which requires `--resource-group`). The staging deploy
> uses `azure/webapps-deploy@v3` which resolves the resource group automatically.

#### Secrets (Settings → Environments → production → Secrets)

| Secret                          | Value                        |
| ------------------------------- | ---------------------------- |
| `INTAKE_SWA_TOKEN_PRODUCTION`   | Intake SWA deployment token  |
| `MANAGER_SWA_TOKEN_PRODUCTION`  | Manager SWA deployment token |

---

## Step-by-Step: Onboard Production Resources

Run these commands after provisioning the production API App Service, Intake SWA, and
Manager SWA in Azure.

### 1. Set Shell Variables

```sh
az login
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

# Your existing app registration from staging setup
APP_ID="<paste-your-app-registration-client-id>"

# Production resource names (update to match your naming)
PROD_API_RG="rg-rvs-westus3-production"
PROD_API_NAME="rvs-api-production"
PROD_SWA_RG="rg-rvs-westus2-production"
PROD_INTAKE_SWA="rvs-intake-production"
PROD_MANAGER_SWA="rvs-manager-production"
```

### 2. Add OIDC Federated Credential for Production

Skip if you already created this during staging setup.

```sh
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "github-actions-production",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:markarnoldutah/RVS:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

### 3. Grant Contributor on Production Resource Groups

```sh
# API resource group (westus3)
az role assignment create \
  --assignee "$APP_ID" \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$PROD_API_RG"

# SWA resource group (westus2) — only if service principal needs to manage SWA resources
# Not required for SWA deployment (tokens handle that), but useful for future az CLI calls
# az role assignment create \
#   --assignee "$APP_ID" \
#   --role "Contributor" \
#   --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$PROD_SWA_RG"
```

### 4. Retrieve SWA Deployment Tokens

```sh
echo "=== Intake SWA Token ==="
az staticwebapp secrets list \
  --name "$PROD_INTAKE_SWA" \
  --resource-group "$PROD_SWA_RG" \
  --query "properties.apiKey" \
  --output tsv

echo "=== Manager SWA Token ==="
az staticwebapp secrets list \
  --name "$PROD_MANAGER_SWA" \
  --resource-group "$PROD_SWA_RG" \
  --query "properties.apiKey" \
  --output tsv
```

### 5. Create the GitHub Production Environment

1. Go to **Settings → Environments → New environment** → name: `production`
2. Recommended: enable **Required reviewers** for manual approval before production deploys
3. Add **Variables** (from Section "Production Variables" above):
   - `AZURE_CLIENT_ID` = `$APP_ID`
   - `AZURE_TENANT_ID` = `$TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID` = `$SUBSCRIPTION_ID`
   - `API_APP_SERVICE_NAME` = `$PROD_API_NAME`
   - `AZURE_RESOURCE_GROUP` = `$PROD_API_RG`
4. Add **Secrets**:
   - `INTAKE_SWA_TOKEN_PRODUCTION` = paste token from step 4
   - `MANAGER_SWA_TOKEN_PRODUCTION` = paste token from step 4

### 6. Verify

```sh
# Confirm federated credentials
az ad app federated-credential list --id "$APP_ID" \
  --query '[].{name:name, subject:subject}' -o table

# Should show:
# github-actions-staging      repo:markarnoldutah/RVS:environment:staging
# github-actions-production   repo:markarnoldutah/RVS:environment:production

# Confirm role assignments
az role assignment list --assignee "$APP_ID" \
  --query '[].{role:roleDefinitionName, scope:scope}' -o table
```

---

## Rotating SWA Tokens

If a token is compromised or rotated in Azure:

```sh
# Reset the token in Azure (generates a new one)
az staticwebapp secrets reset-api-key \
  --name "<swa-name>" \
  --resource-group "<rg-name>"

# Retrieve the new token
az staticwebapp secrets list \
  --name "<swa-name>" \
  --resource-group "<rg-name>" \
  --query "properties.apiKey" \
  --output tsv

# Update the corresponding GitHub secret
```

---

## Troubleshooting

| Symptom | Cause | Fix |
| ------- | ----- | --- |
| `Login failed: Not all values are present` | GitHub environment variables not set | Add vars to the correct environment in Settings → Environments |
| `AADSTS700024: Client assertion is not within its valid time range` | Clock skew or wrong federated credential subject | Verify the `subject` matches `repo:ORG/REPO:environment:ENV_NAME` exactly |
| `Artifact not found` in production | Staging run didn't upload that artifact (app wasn't changed) | Re-run staging with changes, or deploy only the apps that changed |
| `Artifact not found` + correct staging run | Artifact expired (>30 day retention) | Re-run `deploy-staging.yml` to regenerate artifacts |
| SWA deploy `401 Unauthorized` | Token is wrong or was rotated | Re-retrieve token and update GitHub secret |
