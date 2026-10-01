---
goal: Deploy all Azure dev environment infrastructure for RVS (excluding Front Door/WAF and ACR)
---

# Introduction

This plan defines the complete Bicep IaC implementation for the RVS development environment in Azure. It covers all resources needed to run the RVS platform (API, data tier, observability, secrets, and frontend hosting) using Azure Verified Modules (AVM) where available. The existing `Docs/infra/` directory already contains an OpenAI module, naming-tags helper, and parameter files — this plan extends it to cover the full resource topology defined in `Docs/ASOT/RVS_Azure_Infrastructure_Architecture.md`.

**Excluded by design (per requirements):**
- Azure Front Door + WAF (not needed for dev)
- Azure Container Registry (not needed)

---

## Existing Assets

The following files already exist in `Docs/infra/` and will be reused or extended:

| File | Purpose | Action |
|---|---|---|
| `modules/naming-tags.bicep` | Standard naming + tagging helper | Reuse as-is; add `westus3` to region map |
| `modules/openai.bicep` | Azure OpenAI GPT-4o (raw resource) | Keep; integrate into new orchestration |
| `modules/openai-keyvault-secrets.bicep` | Stores OpenAI secrets in KV | Keep; wire to new Key Vault module |
| `parameters/dev.bicepparam` | Dev OpenAI params | Extend with all new parameters |
| `parameters/prod.bicepparam` | Prod OpenAI params | Extend (future, out of scope for this plan) |
| `main.bicep` | Orchestration (OpenAI only) | Rewrite to orchestrate all resources |

---

## Resources

### logAnalyticsWorkspace

```yaml
name: logAnalyticsWorkspace
kind: AVM
avmModule: br/public:avm/res/operational-insights/workspace:0.15.0

purpose: Centralized log ingestion for Application Insights and diagnostic logs
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Workspace name following naming convention
      example: law-rvs-obs-dev-wus3-s01-001
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: retentionInDays
      type: int
      description: Data retention period
      default: 90
    - name: dailyQuotaGb
      type: string
      description: Daily ingestion cap in GB
      default: '5'
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Log Analytics Workspace resource ID
  - name: logAnalyticsWorkspaceId
    type: string
    description: Workspace customer ID (GUID)
  - name: name
    type: string
    description: Workspace name

references:
  docs: https://learn.microsoft.com/azure/azure-monitor/logs/log-analytics-workspace-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/operational-insights/workspace
```

### applicationInsights

```yaml
name: applicationInsights
kind: AVM
avmModule: br/public:avm/res/insights/component:0.7.1

purpose: Application performance monitoring and telemetry with tenant-tagged custom dimensions
dependsOn: [logAnalyticsWorkspace]

parameters:
  required:
    - name: name
      type: string
      description: Application Insights resource name
      example: appi-rvs-api-dev-wus3-s01-001
    - name: workspaceResourceId
      type: string
      description: Log Analytics Workspace resource ID for workspace-based mode
      example: logAnalyticsWorkspace.outputs.resourceId
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: applicationType
      type: string
      description: Application type
      default: web
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Application Insights resource ID
  - name: connectionString
    type: string
    description: Connection string for SDK configuration
  - name: instrumentationKey
    type: string
    description: Instrumentation key (legacy)
  - name: name
    type: string
    description: Resource name

references:
  docs: https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/insights/component
```

### keyVault

```yaml
name: keyVault
kind: AVM
avmModule: br/public:avm/res/key-vault/vault:0.13.3

purpose: Centralized secret management for Auth0, OpenAI, and App Insights connection strings. Azure Communication Services (email + SMS) authenticates via managed identity — no secret required.
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Key Vault name (max 24 chars)
      example: kv-rvs-shared-dev-wus3-s01
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: enableRbacAuthorization
      type: bool
      description: Use Azure RBAC for data plane access
      default: true
    - name: enableSoftDelete
      type: bool
      description: Enable soft delete
      default: true
    - name: softDeleteRetentionInDays
      type: int
      description: Soft delete retention period
      default: 90
    - name: enablePurgeProtection
      type: bool
      description: Enable purge protection (prod only)
      default: false
    - name: networkAcls
      type: object
      description: Network ACL rules
      default: "{ defaultAction: 'Allow' }"
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Key Vault resource ID
  - name: name
    type: string
    description: Key Vault name
  - name: uri
    type: string
    description: Key Vault URI

references:
  docs: https://learn.microsoft.com/azure/key-vault/general/overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/key-vault/vault
```

### storageAccount

```yaml
name: storageAccount
kind: AVM
avmModule: br/public:avm/res/storage/storage-account:0.32.0

purpose: Customer attachment storage (photos, videos) with tenant-prefixed blob containers
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Storage account name (no hyphens, lowercase, 3-24 chars)
      example: strvsdatdevwus3s01001
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: kind
      type: string
      description: Storage account kind
      default: StorageV2
    - name: skuName
      type: string
      description: Replication SKU (Standard_LRS for dev, Standard_ZRS staging, Standard_GRS prod)
      default: Standard_LRS
    - name: accessTier
      type: string
      description: Default access tier for blob data
      default: Hot
    - name: allowBlobPublicAccess
      type: bool
      description: Disable public blob access
      default: false
    - name: minimumTlsVersion
      type: string
      description: Minimum TLS version
      default: TLS1_2
    - name: blobServices
      type: object
      description: Blob service configuration including containers, versioning, soft delete
      default: "See implementation"
    - name: managementPolicies
      type: array
      description: Lifecycle management rules for tiering and deletion
      default: "See implementation"
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Storage account resource ID
  - name: name
    type: string
    description: Storage account name
  - name: primaryBlobEndpoint
    type: string
    description: Primary blob service endpoint URL

references:
  docs: https://learn.microsoft.com/azure/storage/common/storage-account-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/storage/storage-account
```

### cosmosDbAccount

```yaml
name: cosmosDbAccount
kind: AVM
avmModule: br/public:avm/res/document-db/database-account:0.19.1

purpose: Pooled multi-tenant NoSQL data layer with 9 containers partitioned by tenantId
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Cosmos DB account name
      example: cosmos-rvs-data-dev-wus3-s01-001
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: locations
      type: array
      description: Geo-replication locations
      default: "[{ locationName: location, failoverPriority: 0, isZoneRedundant: false }]"
    - name: defaultConsistencyLevel
      type: string
      description: Default consistency level
      default: Session
    - name: enableAutomaticFailover
      type: bool
      description: Enable automatic failover
      default: false
    - name: backupPolicyContinuousTier
      type: string
      description: Continuous backup tier
      default: Continuous7Days
    - name: sqlDatabases
      type: array
      description: SQL databases and containers with partition keys and indexing policies
      default: "See implementation — 1 database 'RVS' with 9 containers"
    - name: networkRestrictions
      type: object
      description: Firewall and network rules
      default: "{ publicNetworkAccess: 'Enabled' }"
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Cosmos DB account resource ID
  - name: name
    type: string
    description: Cosmos DB account name
  - name: endpoint
    type: string
    description: Cosmos DB account endpoint URL

references:
  docs: https://learn.microsoft.com/azure/cosmos-db/nosql/how-to-create-account
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/document-db/database-account
```

### appServicePlan

```yaml
name: appServicePlan
kind: AVM
avmModule: br/public:avm/res/web/serverfarm:0.7.0

purpose: Linux App Service Plan hosting the ASP.NET Core API
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: App Service Plan name
      example: plan-rvs-api-dev-wus3-s01-001
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: kind
      type: string
      description: Plan kind
      default: Linux
    - name: reserved
      type: bool
      description: Required true for Linux plans
      default: true
    - name: skuName
      type: string
      description: SKU name (B1 dev, P1v3 staging, P2v3 prod)
      default: B1
    - name: skuCapacity
      type: int
      description: Number of instances
      default: 1
    - name: zoneRedundant
      type: bool
      description: Zone redundancy (prod only)
      default: false
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: App Service Plan resource ID
  - name: name
    type: string
    description: App Service Plan name

references:
  docs: https://learn.microsoft.com/azure/app-service/overview-hosting-plans
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/serverfarm
```

### appService

```yaml
name: appService
kind: AVM
avmModule: br/public:avm/res/web/site:0.22.0

purpose: ASP.NET Core 10 REST API with system-assigned managed identity and Key Vault references
dependsOn: [appServicePlan, applicationInsights, keyVault]

parameters:
  required:
    - name: name
      type: string
      description: App Service name
      example: app-rvs-api-dev-wus3-s01-001
    - name: kind
      type: string
      description: App kind
      example: app,linux
    - name: serverFarmResourceId
      type: string
      description: App Service Plan resource ID
      example: appServicePlan.outputs.resourceId
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: managedIdentities
      type: object
      description: Managed identity configuration
      default: "{ systemAssigned: true }"
    - name: siteConfig
      type: object
      description: Site configuration including runtime, health check, CORS
      default: "See implementation"
    - name: httpsOnly
      type: bool
      description: HTTPS only
      default: true
    - name: configs
      type: array
      description: App settings and connection strings
      default: "See implementation"
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: App Service resource ID
  - name: name
    type: string
    description: App Service name
  - name: defaultHostname
    type: string
    description: Default hostname
  - name: systemAssignedMIPrincipalId
    type: string
    description: System-assigned managed identity principal ID

references:
  docs: https://learn.microsoft.com/azure/app-service/overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/site
```

### staticWebAppIntake

```yaml
name: staticWebAppIntake
kind: AVM
avmModule: br/public:avm/res/web/static-site:0.9.3

purpose: Blazor WASM hosting for the customer Intake Portal (RVS.Blazor.Intake)
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Static Web App name
      example: stapp-rvs-intake-dev-wus3-s01-001
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: sku
      type: string
      description: SKU tier (Free for dev, Standard for staging/prod)
      default: Free
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Static Web App resource ID
  - name: name
    type: string
    description: Static Web App name
  - name: defaultHostname
    type: string
    description: Default hostname for CDN/CORS configuration

references:
  docs: https://learn.microsoft.com/azure/static-web-apps/overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/static-site
```

### staticWebAppManager

```yaml
name: staticWebAppManager
kind: AVM
avmModule: br/public:avm/res/web/static-site:0.9.3

purpose: Blazor WASM hosting for the dealer Manager Desktop (RVS.Blazor.Manager)
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Static Web App name
      example: stapp-rvs-manager-dev-wus3-s01-001
  optional:
    - name: location
      type: string
      description: Azure region
      default: resourceGroup().location
    - name: sku
      type: string
      description: SKU tier (Free for dev, Standard for staging/prod)
      default: Free
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Static Web App resource ID
  - name: name
    type: string
    description: Static Web App name
  - name: defaultHostname
    type: string
    description: Default hostname

references:
  docs: https://learn.microsoft.com/azure/static-web-apps/overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/static-site
```

### openAiAccount (existing)

```yaml
name: openAiAccount
kind: Raw
type: Microsoft.CognitiveServices/accounts@2024-10-01

purpose: Azure OpenAI GPT-4o Vision for VIN extraction (already implemented)
dependsOn: []

parameters:
  required:
    - name: resourceName
      type: string
      description: OpenAI account name
      example: oai-rvs-ai-dev-wus3-s01-001
  optional:
    - name: location
      type: string
      description: Azure region
      default: westus3
    - name: deploymentCapacity
      type: int
      description: TPM capacity in thousands
      default: 1

outputs:
  - name: endpoint
    type: string
    description: OpenAI endpoint URL
  - name: name
    type: string
    description: OpenAI resource name
  - name: deploymentName
    type: string
    description: Model deployment name
  - name: principalId
    type: string
    description: System-assigned MI principal ID

references:
  docs: https://learn.microsoft.com/azure/ai-services/openai/overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/cognitive-services/account (0.14.2 available — future migration candidate)
```

### rbacRoleAssignments

```yaml
name: rbacRoleAssignments
kind: Raw
type: Microsoft.Authorization/roleAssignments@2022-04-01

purpose: RBAC bindings connecting App Service managed identity to Key Vault, Storage, and Cosmos DB
dependsOn: [appService, keyVault, storageAccount, cosmosDbAccount]

parameters:
  required:
    - name: appServicePrincipalId
      type: string
      description: App Service system-assigned managed identity principal ID
      example: appService.outputs.systemAssignedMIPrincipalId
    - name: keyVaultResourceId
      type: string
      description: Key Vault resource ID for scoping
      example: keyVault.outputs.resourceId
    - name: storageAccountResourceId
      type: string
      description: Storage account resource ID for scoping
      example: storageAccount.outputs.resourceId
    - name: cosmosDbAccountResourceId
      type: string
      description: Cosmos DB account resource ID for scoping
      example: cosmosDbAccount.outputs.resourceId

outputs:
  - name: none
    type: n/a
    description: Role assignments do not produce outputs

references:
  docs: https://learn.microsoft.com/azure/role-based-access-control/role-assignments-bicep
  avm: N/A (raw resources — AVM modules support inline roleAssignments parameter)
```

---

# Implementation Plan

The plan implements 8 Bicep modules and updates the orchestration template in dependency order. Each phase produces independently testable modules. Deployment is a single `az deployment group create` invocation — phases represent authoring order, not separate deployments.

**Key dependencies:**
- Application Insights requires Log Analytics Workspace resource ID
- App Service requires App Service Plan resource ID
- RBAC assignments require App Service managed identity principal ID + target resource IDs
- OpenAI Key Vault secrets require Key Vault name (already implemented)

**Naming convention:** All resources use the `naming-tags.bicep` helper module with the pattern `<type>-rvs-<workload>-<env>-<region>-<stamp>-<instance>`. The `westus3` → `wus3` region code must be added to the helper.

---

## Phase 1 — Foundation (Naming Update)

**Objective:** Extend the existing naming-tags module to support `westus3` and validate it handles all resource types needed.

- IMPLEMENT-GOAL-001: Update naming-tags module and validate naming for all resource types

| Task | Description | Action |
|---|---|---|
| TASK-001 | Add `westus3: 'wus3'` to `regionCodeByLocation` map in `naming-tags.bicep` | Edit `Docs/infra/modules/naming-tags.bicep` — add entry to `regionCodeByLocation` variable |
| TASK-002 | Validate naming output for each resource type prefix against the naming conventions doc | Manual verification against `Docs/infra/Azure_Resource_Naming_Conventions.md` |

---

## Phase 2 — Observability (Log Analytics + Application Insights)

**Objective:** Deploy the observability stack first so all subsequent resources can emit diagnostic data from initial deployment.

- IMPLEMENT-GOAL-002: Create Log Analytics Workspace and Application Insights modules

| Task | Description | Action |
|---|---|---|
| TASK-003 | Create `modules/log-analytics.bicep` using AVM `operational-insights/workspace:0.15.0` | New file `Docs/infra/modules/log-analytics.bicep` — AVM module reference with parameters: name, location, retentionInDays (90), dailyQuotaGb ('5'), tags |
| TASK-004 | Create `modules/app-insights.bicep` using AVM `insights/component:0.7.1` | New file `Docs/infra/modules/app-insights.bicep` — AVM module reference with parameters: name, location, workspaceResourceId, applicationType ('web'), tags |
| TASK-005 | Validate both modules compile with `az bicep build` | Run `az bicep build --file modules/log-analytics.bicep` and `az bicep build --file modules/app-insights.bicep` |

---

## Phase 3 — Data Tier (Cosmos DB + Storage)

**Objective:** Deploy the persistent data layer — Cosmos DB with all 9 containers and Blob Storage with the `attachments` container.

- IMPLEMENT-GOAL-003: Create Cosmos DB and Storage Account modules

| Task | Description | Action |
|---|---|---|
| TASK-006 | Create `modules/cosmos-db.bicep` using AVM `document-db/database-account:0.19.1` | New file `Docs/infra/modules/cosmos-db.bicep` — AVM module with NoSQL API, Session consistency, Continuous7Days backup, single `RVS` database containing 9 containers: `serviceRequests` (/tenantId), `customerProfiles` (/tenantId), `globalCustomerAccts` (/email), `assetLedger` (/assetId), `locations` (/tenantId), `dealerships` (/id), `tenantConfigs` (/id), `lookupSets` (/type), `slugLookup` (/slug). Dev uses manual 400 RU/s for all containers. Include custom indexing policies for `globalCustomerAccts` (magicLinkToken) and `locations` (slug). |
| TASK-007 | Create `modules/storage.bicep` using AVM `storage/storage-account:0.32.0` | New file `Docs/infra/modules/storage.bicep` — AVM module with StorageV2, Standard_LRS (dev), Hot access tier, public access disabled, blob versioning enabled, soft delete 7 days for blobs and containers, `attachments` container with private access. Include lifecycle management policy: cool after 90 days, archive after 365 days, delete after 2555 days. |
| TASK-008 | Validate Cosmos DB container definitions compile and match architecture spec | Cross-reference container list against Section 3.2 of architecture doc |

---

## Phase 4 — Secrets (Key Vault)

**Objective:** Deploy Key Vault with Azure RBAC mode. RBAC assignments are deferred to Phase 6 after the App Service managed identity exists.

- IMPLEMENT-GOAL-004: Create Key Vault module

| Task | Description | Action |
|---|---|---|
| TASK-009 | Create `modules/key-vault.bicep` using AVM `key-vault/vault:0.13.3` | New file `Docs/infra/modules/key-vault.bicep` — AVM module with RBAC authorization enabled, soft delete (90 days), purge protection disabled (dev), Standard SKU, public network access allowed (dev). Key Vault name max 24 chars — use compact naming: `kv-rvs-shared-dev-wus3-s01`. |
| TASK-010 | Verify OpenAI Key Vault secrets module wires to new Key Vault | Confirm `modules/openai-keyvault-secrets.bicep` accepts the Key Vault name from the new module output |

---

## Phase 5 — Compute (App Service Plan + App Service)

**Objective:** Deploy the API hosting tier with system-assigned managed identity, health check, CORS, and Application Insights integration.

- IMPLEMENT-GOAL-005: Create App Service Plan and App Service modules

| Task | Description | Action |
|---|---|---|
| TASK-011 | Create `modules/app-service-plan.bicep` using AVM `web/serverfarm:0.7.0` | New file `Docs/infra/modules/app-service-plan.bicep` — AVM module with Linux kind, reserved=true, B1 SKU (dev), single instance, zone redundancy disabled |
| TASK-012 | Create `modules/app-service.bicep` using AVM `web/site:0.22.0` | New file `Docs/infra/modules/app-service.bicep` — AVM module with kind='app,linux', .NET 10 Linux runtime stack (`DOTNETCORE\|10.0`), system-assigned managed identity, HTTPS only, health check path `/health/live`, always-on disabled (dev), CORS origins `['http://localhost:5000', 'http://localhost:5001']`, Application Insights connection via `applicationInsightResourceId` parameter, minimum TLS 1.2. App settings: `CosmosDb__AccountEndpoint`, `CosmosDb__DatabaseName`, `BlobStorage__AccountName`, `BlobStorage__ContainerName`. Key Vault reference settings: `Auth0__ClientSecret`, `AzureOpenAI__ApiKey`, `ApplicationInsights__ConnectionString`. |
| TASK-013 | Validate managed identity output is available for RBAC phase | Confirm AVM web/site module outputs `systemAssignedMIPrincipalId` |

---

## Phase 6 — RBAC Bindings

**Objective:** Create role assignments connecting the App Service managed identity to Key Vault, Storage, and Cosmos DB using least-privilege roles.

- IMPLEMENT-GOAL-006: Create RBAC role assignments module

| Task | Description | Action |
|---|---|---|
| TASK-014 | Create `modules/rbac-assignments.bicep` as a raw Bicep module | New file `Docs/infra/modules/rbac-assignments.bicep` — three `Microsoft.Authorization/roleAssignments@2022-04-01` resources: (1) Key Vault Secrets User (`4633458b-17de-408a-b874-0445c86b69e6`) scoped to Key Vault, (2) Storage Blob Data Contributor (`ba92f5b4-2d11-453d-a403-e96b0029c9fe`) scoped to Storage Account, (3) Cosmos DB Account Reader Role (`fbdf93bf-df7d-467e-a4d2-9458aa1360c8`) scoped to Cosmos DB Account. All assigned to the App Service managed identity principal ID. |
| TASK-015 | Use `guid()` function for deterministic role assignment names | Ensure each role assignment name uses `guid(targetResourceId, principalId, roleDefinitionId)` for idempotent deployments |

**Note:** AVM modules for Key Vault, Storage, and Cosmos DB also support inline `roleAssignments` parameters. An alternative approach is to pass role assignments directly to each AVM module instead of a separate RBAC module. The separate module approach is chosen here for clarity and auditability.

---

## Phase 7 — Frontends (Static Web Apps)

**Objective:** Deploy two Static Web Apps for the Blazor WASM frontends on Free tier (dev).

- IMPLEMENT-GOAL-007: Create Static Web App module

| Task | Description | Action |
|---|---|---|
| TASK-016 | Create `modules/static-web-app.bicep` using AVM `web/static-site:0.9.3` | New file `Docs/infra/modules/static-web-app.bicep` — AVM module with parameterized name and SKU (Free for dev). This is a single reusable module invoked twice (Intake + Manager) from the orchestration template. |
| TASK-017 | Validate Free tier supports Blazor WASM deployment | Confirm SWA Free tier allows custom `staticwebapp.config.json` and navigation fallback |

---

## Phase 8 — Orchestration (main.bicep + Parameters)

**Objective:** Rewrite `main.bicep` to orchestrate all modules in dependency order and update `dev.bicepparam` with all required parameters.

- IMPLEMENT-GOAL-008: Rewrite orchestration and parameter files

| Task | Description | Action |
|---|---|---|
| TASK-018 | Rewrite `main.bicep` to import and wire all modules | Replace `Docs/infra/main.bicep` — add parameters for environment, location, and all environment-specific SKU/capacity overrides. Module deployment order: (1) logAnalytics, (2) appInsights → logAnalytics.outputs.resourceId, (3) keyVault, (4) cosmosDb, (5) storage, (6) appServicePlan, (7) appService → plan.outputs.resourceId + appInsights.outputs.resourceId, (8) rbacAssignments → appService.outputs.systemAssignedMIPrincipalId + kv/storage/cosmos resourceIds, (9) openAi (existing), (10) openAiKvSecrets → keyVault.outputs.name (conditional), (11) staticWebAppIntake, (12) staticWebAppManager. Output all resource names and the API default hostname. |
| TASK-019 | Update `parameters/dev.bicepparam` with all new parameters | Extend `Docs/infra/parameters/dev.bicepparam`: `environmentName='dev'`, `location='westus3'`, `appServicePlanSku='B1'`, `cosmosDbMaxThroughput=400` (manual), `storageReplication='Standard_LRS'`, `staticWebAppSku='Free'`, `openAiCapacity=1`, `enablePurgeProtection=false`, `logRetentionDays=90`. |
| TASK-020 | Validate full template compiles with `az bicep build` | Run `az bicep build --file main.bicep` to verify all module references resolve |
| TASK-021 | Test deployment with `what-if` mode | Run `az deployment group what-if --resource-group rg-rvs-api-dev-wus3-s01-001 --template-file ../Bicep.IaC/main.bicep --parameters ../Bicep.IaC/parameters/dev.bicepparam` |

---

## High-Level Design

```
┌─────────────────────────────────────────────────────────────────────┐
│  Resource Group: rg-rvs-api-dev-wus3-s01-001                       │
│                                                                     │
│  ┌──────────────┐    ┌──────────────────┐                          │
│  │ Log Analytics │◄───│ Application      │                          │
│  │ Workspace     │    │ Insights         │                          │
│  │ (law-rvs-*)   │    │ (appi-rvs-*)     │                          │
│  └──────────────┘    └────────┬─────────┘                          │
│                               │                                     │
│  ┌──────────────┐    ┌───────┴──────────┐    ┌──────────────┐     │
│  │ Key Vault     │    │ App Service      │    │ Cosmos DB    │     │
│  │ (kv-rvs-*)    │◄──RBAC──│ (app-rvs-*)  │──RBAC──►│ (cosmos-*)  │     │
│  │ Secrets User  │    │ .NET 10 Linux    │    │ 9 containers │     │
│  └──────────────┘    │ Managed Identity  │    └──────────────┘     │
│                       └───────┬──────────┘                          │
│  ┌──────────────┐            │RBAC                                  │
│  │ Storage Acct  │◄───────────┘                                     │
│  │ (st-rvs-*)    │    Blob Data Contributor                         │
│  │ attachments   │                                                  │
│  └──────────────┘                                                   │
│                                                                     │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐         │
│  │ Static Web   │    │ Static Web   │    │ Azure OpenAI │         │
│  │ App (Intake)  │    │ App (Manager) │    │ GPT-4o       │         │
│  │ Free tier     │    │ Free tier     │    │ (existing)   │         │
│  └──────────────┘    └──────────────┘    └──────────────┘         │
│                                                                     │
│  ┌──────────────┐                                                   │
│  │ App Service   │                                                   │
│  │ Plan (B1)     │                                                   │
│  └──────────────┘                                                   │
└─────────────────────────────────────────────────────────────────────┘
```

**Network Architecture (Dev — simplified, no VNet):**

```
┌─────────────────────────────────────────────────────────────┐
│  Internet                                                    │
│                                                              │
│  Developer Browser ──► Static Web Apps (Intake/Manager)     │
│       │                     │                                │
│       │                     ▼                                │
│       └──────────────► App Service (API)                    │
│                          │    │    │                         │
│                          ▼    ▼    ▼                         │
│                     Cosmos  Storage  Key Vault               │
│                       DB     Blob     (secrets)              │
│                                                              │
│  All traffic over public endpoints (HTTPS only)             │
│  Access controlled via:                                      │
│    • Managed Identity + RBAC (API → data services)          │
│    • Auth0 PKCE (Browser → API)                             │
│    • SAS tokens (API → Blob signed URLs)                    │
│    • Firewall rules (where supported)                       │
└─────────────────────────────────────────────────────────────┘
```

---

## Dev Environment Configuration Summary

| Resource | SKU / Config | Monthly Cost (est.) |
|---|---|---|
| Log Analytics Workspace | Per-GB, 90-day retention, 5 GB/day cap | ~$11.50 |
| Application Insights | Workspace-based | Included in LAW |
| Key Vault | Standard, RBAC, no purge protection | ~$0.15 |
| Storage Account | StorageV2, Standard_LRS, Hot | ~$2.00 |
| Cosmos DB | Manual 400 RU/s × 9 containers | ~$216.00 |
| App Service Plan | B1 (1 vCPU, 1.75 GB) | ~$13.14 |
| App Service | Linux, .NET 10, always-on disabled | Included in plan |
| Static Web Apps × 2 | Free tier | $0.00 |
| Azure OpenAI | S0, 1K TPM (pay-per-use) | ~$0.00 |
| **Total** | | **~$242.79/month** |

**Cost note:** Cosmos DB dominates dev cost at manual 400 RU/s per container. Consider using shared database-level throughput (400 RU/s shared across all 9 containers) to reduce to ~$24/month. This is a recommended optimization flagged for TASK-006.

---

## File Manifest (New + Modified)

| Action | File Path |
|---|---|
| Modify | `Docs/infra/modules/naming-tags.bicep` |
| Create | `Docs/infra/modules/log-analytics.bicep` |
| Create | `Docs/infra/modules/app-insights.bicep` |
| Create | `Docs/infra/modules/cosmos-db.bicep` |
| Create | `Docs/infra/modules/storage.bicep` |
| Create | `Docs/infra/modules/key-vault.bicep` |
| Create | `Docs/infra/modules/app-service-plan.bicep` |
| Create | `Docs/infra/modules/app-service.bicep` |
| Create | `Docs/infra/modules/rbac-assignments.bicep` |
| Create | `Docs/infra/modules/static-web-app.bicep` |
| Rewrite | `Docs/infra/main.bicep` |
| Rewrite | `Docs/infra/parameters/dev.bicepparam` |
| Keep | `Docs/infra/modules/openai.bicep` |
| Keep | `Docs/infra/modules/openai-keyvault-secrets.bicep` |
| Keep | `Docs/infra/parameters/prod.bicepparam` (extend later) |
| Keep | `Docs/infra/README.md` (update after implementation) |
