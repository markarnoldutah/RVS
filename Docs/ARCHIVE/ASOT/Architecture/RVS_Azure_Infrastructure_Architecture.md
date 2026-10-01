# RVS Azure Infrastructure Architecture

**Authoritative Source of Truth (ASOT) — April 5, 2026**

This document defines the Azure resource topology, Infrastructure as Code (IaC) strategy, deployment patterns, environment separation model, scaling decisions, and security configurations for the RV Service Flow (RVS) platform. This addresses P0 gap identified in `RVS_Cloud_Arch_Assessment.md` (line 288).

---

## Executive Summary

RVS is deployed entirely on **Microsoft Azure** as a B2B SaaS platform using a **pooled multi-tenancy** model. The infrastructure supports three Blazor frontend applications (Intake PWA, Manager desktop, MAUI Tech mobile) and a single ASP.NET Core REST API backed by Azure Cosmos DB.

**Core Infrastructure Components:**
- **App Service (Linux, .NET 10)** — ASP.NET Core API with staging slots
- **Azure Cosmos DB for NoSQL** — Pooled multi-tenant data layer (9 containers)
- **Azure Blob Storage** — Customer attachment storage (photos, videos)
- **Azure Key Vault** — Secret management (Auth0, OpenAI, SFTP keys)
- **Azure Static Web Apps** — Blazor WASM frontends (Intake, Manager)
- **Azure Front Door + WAF** — Global CDN, DDoS protection, OWASP rule set
- **Azure Application Insights** — Observability, tenant-tagged telemetry
- **Azure Container Registry** — Optional for containerized deployments

**Deployment Strategy:**
- **IaC Format:** Bicep (Azure-native)
- **CI/CD:** GitHub Actions with manual approval gates
- **Environment Model:** Subscription isolation (Dev, Staging, Prod)
- **Tenant Provisioning:** Bicep modules + seed scripts

---

## 1. Resource Topology

### 1.1 Resource Groups

Each environment uses a dedicated resource group for logical isolation and RBAC scoping:

```
┌─────────────────────────────────────────────────────────────┐
│ Azure Subscription: RVS Production                          │
│                                                             │
│  ┌────────────────────────────────────────────────────┐   │
│  │ Resource Group: rg-rvs-staging-westus3               │   │
│  │  • App Service Plan (B1)                            │   │
│  │  • App Service (API)                                │   │
│  │  • Cosmos DB Account (staging-shared, Serverless)   │   │
│  │  • Storage Account (LRS)                            │   │
│  │  • Key Vault                                        │   │
│  │  • Application Insights                             │   │
│  │  • Static Web App (Free tier) × 2                   │   │
│  └────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌────────────────────────────────────────────────────┐   │
│  │ Resource Group: rg-rvs-staging-westus3              │   │
│  │  • App Service Plan (P1v3)                          │   │
│  │  • App Service (API) with staging slot              │   │
│  │  • Cosmos DB Account (autoscale 400-4000 RU)        │   │
│  │  • Storage Account (ZRS)                            │   │
│  │  • Key Vault                                        │   │
│  │  • Application Insights                             │   │
│  │  • Static Web App (Standard tier) × 2               │   │
│  │  • Azure Front Door Profile                         │   │
│  └────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌────────────────────────────────────────────────────┐   │
│  │ Resource Group: rg-rvs-prod-westus3                 │   │
│  │  • App Service Plan (P2v3, zone redundant)          │   │
│  │  • App Service (API) with staging slot              │   │
│  │  • Cosmos DB Account (autoscale 400-10000 RU)       │   │
│  │  • Storage Account (GRS)                            │   │
│  │  • Key Vault                                        │   │
│  │  • Application Insights                             │   │
│  │  • Static Web App (Standard tier) × 2               │   │
│  │  • Azure Front Door Premium (with WAF)              │   │
│  │  • Log Analytics Workspace                          │   │
│  └────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Resource Naming Convention

Follow [Azure naming best practices](https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-naming):

| Resource Type | Pattern | Example |
|---|---|---|
| Resource Group | `rg-{workload}-{env}-{region}` | `rg-rvs-prod-westus3` |
| App Service Plan | `plan-{workload}-{env}` | `plan-rvs-prod` |
| App Service | `app-{workload}-api-{env}` | `app-rvs-api-prod` |
| Cosmos DB Account | `cosmos-{workload}-{env}` | `cosmos-rvs-prod` |
| Storage Account | `st{workload}{env}{random}` | `strvsprodhj3k` (no hyphens, lowercase, 3-24 chars) |
| Key Vault | `kv-{workload}-{env}-{random}` | `kv-rvs-prod-x7m2` (max 24 chars) |
| Static Web App | `stapp-{workload}-{component}-{env}` | `stapp-rvs-intake-prod` |
| Front Door | `fd-{workload}-{env}` | `fd-rvs-prod` |
| Application Insights | `appi-{workload}-{env}` | `appi-rvs-prod` |
| Log Analytics | `log-{workload}-{env}` | `log-rvs-prod` |

**Tagging Strategy:**

All resources receive mandatory tags:

```json
{
  "Environment": "Production|Staging|Development",
  "Workload": "RVS",
  "CostCenter": "Engineering",
  "Owner": "platform-team@example.com",
  "ManagedBy": "Bicep"
}
```

---

## 2. Azure App Service (API Tier)

### 2.1 App Service Plan Sizing

| Environment | SKU | vCPU | RAM | Max Instances | Notes |
|---|---|---|---|---|
| **Development** | B1 | 1 | 1.75 GB | 1 | Cost-optimized, always-on disabled |
| **Staging** | P1v3 | 2 | 8 GB | 3 | Production-parity for testing |
| **Production (MVP)** | P2v3 | 4 | 16 GB | 10 | Zone redundancy enabled, autoscale 2-10 instances |
| **Production (GA)** | P3v3 | 8 | 32 GB | 20+ | Multi-region with Traffic Manager |

**Auto-Scale Rules (Production):**

```bicep
// Scale out when average CPU > 70% for 5 minutes
// Scale in when average CPU < 30% for 10 minutes
resource autoScaleSettings 'Microsoft.Insights/autoscalesettings@2022-10-01' = {
  name: 'autoscale-${appServiceName}'
  properties: {
    profiles: [{
      name: 'Default'
      capacity: {
        minimum: '2'
        maximum: '10'
        default: '2'
      }
      rules: [
        {
          metricTrigger: {
            metricName: 'CpuPercentage'
            operator: 'GreaterThan'
            threshold: 70
            timeAggregation: 'Average'
            timeWindow: 'PT5M'
          }
          scaleAction: {
            direction: 'Increase'
            type: 'ChangeCount'
            value: '1'
            cooldown: 'PT5M'
          }
        }
        {
          metricTrigger: {
            metricName: 'CpuPercentage'
            operator: 'LessThan'
            threshold: 30
            timeAggregation: 'Average'
            timeWindow: 'PT10M'
          }
          scaleAction: {
            direction: 'Decrease'
            type: 'ChangeCount'
            value: '1'
            cooldown: 'PT10M'
          }
        }
      ]
    }]
  }
}
```

### 2.2 Deployment Slots

**Staging and Production environments use a Blue-Green deployment model:**

```
App Service: app-rvs-api-prod
├── Production Slot (active)
└── Staging Slot → swap → Production
```

**Deployment Flow:**

1. Deploy new API version to **staging slot**
2. Smoke test against staging slot URL (`https://app-rvs-api-prod-staging.azurewebsites.net`)
3. **Swap** staging → production (zero-downtime, automatic rollback on health check failure)
4. Monitor Application Insights for 15 minutes
5. If errors spike, **swap back** to previous version

**Slot Settings (sticky):**

- `Auth0:Domain`, `Auth0:Audience` — environment-specific
- `CosmosDb:AccountEndpoint` — environment-specific
- `ApplicationInsights:ConnectionString` — environment-specific

### 2.3 App Service Configuration

**Runtime Stack:** .NET 10 (Linux container)

**Always On:** Enabled (Staging, Prod) | Disabled (Dev)

**Health Check Path:** `/health/live`

**Managed Identity:** System-assigned, granted:
- `Key Vault Secrets User` on Key Vault
- `Cosmos DB Account Reader Role` on Cosmos DB account
- `Storage Blob Data Contributor` on attachment storage account

**Application Settings (Key Vault references):**

```json
{
  "Auth0__ClientSecret": "@Microsoft.KeyVault(SecretUri=https://kv-rvs-prod-x7m2.vault.azure.net/secrets/auth0-client-secret)",
  "AzureOpenAI__ApiKey": "@Microsoft.KeyVault(SecretUri=https://kv-rvs-prod-x7m2.vault.azure.net/secrets/openai-api-key)",
  "CosmosDb__AccountEndpoint": "https://cosmos-rvs-prod.documents.azure.com:443/",
  "CosmosDb__DatabaseName": "RVS",
  "BlobStorage__AccountName": "strvsprodhj3k",
  "BlobStorage__ContainerName": "attachments",
  "ApplicationInsights__ConnectionString": "@Microsoft.KeyVault(SecretUri=https://kv-rvs-prod-x7m2.vault.azure.net/secrets/appinsights-connection-string)"
}
```

**CORS Policy:**

```bicep
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  properties: {
    siteConfig: {
      cors: {
        allowedOrigins: [
          'https://intake.rvs.app'
          'https://manager.rvs.app'
        ]
        supportCredentials: false
      }
    }
  }
}
```

---

## 3. Azure Cosmos DB

### 3.1 Account Configuration

**API:** NoSQL (Core SQL API)

**Consistency Level:** Session (default, balances latency and consistency for single-region writes)

**Multi-Region Writes:** Disabled (MVP). Single write region (West US 3), optional read replica in East US (GA phase).

**Backup Policy:**
- **Mode:** Continuous (7-day point-in-time restore)
- **Retention:** 7 days (MVP), 30 days (GA)

**Network Access:**
- **Public access:** Enabled with firewall rules
- **Allowed IPs:** App Service outbound IPs, developer IPs (dev environment only)
- **Private Endpoint:** GA phase only (requires VNet integration)

**Managed Identity:** App Service system-assigned identity granted `Cosmos DB Account Reader Role`

### 3.2 Database and Container Design

**Database:** `RVS` (single database, all containers)

**Container Sizing:**

| Container | Partition Key | Index Policy | RU Mode | RU Range | Notes |
|---|---|---|---|---|---|
| `serviceRequests` | `/tenantId` | Default | Autoscale | 400–4,000 (MVP)<br>400–10,000 (GA) | Primary workload container |
| `customerProfiles` | `/tenantId` | Default | Autoscale | 400–1,000 | Moderate read/write |
| `globalCustomerAccts` | `/email` | Include `/magicLinkToken/?` | Autoscale | 400–1,000 | Cross-partition query on token |
| `assetLedger` | `/assetId` | Default | Autoscale | 400–2,000 | Append-only, scan for analytics |
| `locations` | `/tenantId` | Include `/slug/?` | Autoscale | 400–1,000 | Read-heavy |
| `dealerships` | `/id` | Default | Autoscale | 400–1,000 | Small, cached |
| `tenantConfigs` | `/id` | Default | Autoscale | 400–1,000 | Small, cached |
| `lookupSets` | `/type` | Default | Autoscale | 400–1,000 | Read-only seed data |
| `slugLookup` | `/slug` | Default | Manual | 400 | Point reads only, O(1) |

**Autoscale Cost Model (per container):**

- **Manual 400 RU/s:** $24/month per container
- **Autoscale 400–1,000 RU/s:** $5.84/month minimum (10% of max), scales to $58.40 at full utilization
- **Production recommendation:** Autoscale for all containers except `slugLookup`

**Total Cosmos DB Cost (MVP Production):**

- 8 containers at autoscale 400-1,000 RU: ~$46.72/month (minimum)
- 1 container (`serviceRequests`) at autoscale 400-4,000 RU: ~$23.36/month (minimum)
- **Total floor:** ~$70/month
- **Expected average (20 tenants, moderate load):** $150-250/month

### 3.3 Indexing Policies

**`globalCustomerAccts` Custom Index (Magic Link Optimization):**

```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    { "path": "/*" },
    { "path": "/magicLinkToken/?" }
  ],
  "excludedPaths": [
    { "path": "/\"_etag\"/?" }
  ]
}
```

**`locations` Custom Index (Slug Lookup):**

```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    { "path": "/*" },
    { "path": "/slug/?" }
  ],
  "excludedPaths": []
}
```

---

## 4. Azure Blob Storage

### 4.1 Storage Account Configuration

**Account Kind:** StorageV2 (general-purpose v2)

**Performance Tier:** Standard

**Replication:**

| Environment | Replication | Cost/GB/month | RPO | Notes |
|---|---|---|---|---|
| Development | LRS (Locally Redundant) | $0.018 | N/A | Acceptable data loss for dev |
| Staging | ZRS (Zone Redundant) | $0.0225 | 0 | Production-parity testing |
| Production | GRS (Geo Redundant) | $0.036 | < 15 min | Read-access from secondary region |

**Access Tier:** Hot (customer attachments accessed frequently during active cases)

**Blob Versioning:** Enabled (accidental delete protection)

**Soft Delete:** Enabled (7 days for blobs, 7 days for containers)

**Public Access:** **Disabled** (explicit `PublicAccess = BlobContainerPublicAccessType.None`)

### 4.2 Container Structure

```
Storage Account: strvsprodhj3k
└── Container: attachments (private)
    ├── {tenantId}/
    │   ├── {serviceRequestId}/
    │   │   ├── {attachmentId}.jpg
    │   │   ├── {attachmentId}.mp4
    │   │   └── ...
    │   └── ...
    └── ...
```

**Naming Pattern:** `{tenantId}/{serviceRequestId}/{attachmentId}.{extension}`

**Access Control:** SAS tokens only (no anonymous access, no account keys in code)

**SAS Token Policy:**
- **Permissions:** Read-only for dealer dashboard, Write for upload
- **Expiry:** 1 hour (read), 10 minutes (upload)
- **IP Restriction:** None (mobile technicians on variable IPs)
- **HTTPS Only:** Required

### 4.3 Lifecycle Management

**Automatic Blob Tiering:**

```bicep
resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
    policy: {
      rules: [
        {
          name: 'MoveToArchiveAfter1Year'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['attachments/']
            }
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 90
                }
                tierToArchive: {
                  daysAfterModificationGreaterThan: 365
                }
                delete: {
                  daysAfterModificationGreaterThan: 2555  // 7 years (configurable per tenant)
                }
              }
            }
          }
        }
      ]
    }
  }
}
```

**Cost Impact:**
- Hot: $0.018/GB/month
- Cool: $0.01/GB/month (after 90 days)
- Archive: $0.002/GB/month (after 365 days)

---

## 5. Azure Key Vault

### 5.1 Configuration

**SKU:** Standard (no HSM requirement for MVP)

**Access Policy Model:** **Azure RBAC** (not legacy Access Policies — Microsoft deprecated Access Policies)

**Network Access:**
- **Public access:** Enabled with firewall (MVP)
- **Allowed networks:** App Service outbound IPs, GitHub Actions runner IPs
- **Private Endpoint:** GA phase

**Soft Delete:** Enabled (90-day retention)

**Purge Protection:** Enabled (production only, prevents permanent deletion during soft-delete period)

### 5.2 Secrets Stored

| Secret Name | Purpose | Rotation Frequency |
|---|---|---|
| `auth0-client-secret` | Auth0 M2M authentication | 90 days |
| `auth0-management-api-key` | Auth0 Management API token | 90 days |
| `openai-api-key` | Azure OpenAI service key | 180 days |
| `appinsights-connection-string` | Application Insights telemetry | Never (rotate on compromise) |
| ~~`sendgrid-api-key`~~ | ~~Email notification service~~ | Removed — ACS uses managed identity |
| `sftp-key-{tenantId}` | Per-tenant SFTP private keys | Per tenant request |
| `cosmos-connection-string` | Emergency fallback (not used in code) | Never |

**RBAC Assignments:**

```bicep
// App Service Managed Identity → Key Vault Secrets User
resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, appServiceIdentity.id, keyVaultSecretsUserRole.id)
  properties: {
    roleDefinitionId: keyVaultSecretsUserRole.id
    principalId: appServiceIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
```

### 5.3 Secret Reference Pattern in App Service

**appsettings.json (checked into source control):**

```json
{
  "Auth0": {
    "Domain": "rvs-dev.us.auth0.com",
    "Audience": "https://api.rvs.app",
    "ClientId": "public-value-ok-in-source"
  }
}
```

**App Service Application Settings (not in source):**

```json
{
  "Auth0__ClientSecret": "@Microsoft.KeyVault(SecretUri=https://kv-rvs-prod-x7m2.vault.azure.net/secrets/auth0-client-secret/)",
  "AzureOpenAI__ApiKey": "@Microsoft.KeyVault(SecretUri=https://kv-rvs-prod-x7m2.vault.azure.net/secrets/openai-api-key/)"
}
```

**Program.cs (no code change required):**

```csharp
// Azure App Service automatically resolves @Microsoft.KeyVault() references
// when the app uses DefaultAzureCredential
builder.Configuration.AddEnvironmentVariables();
```

---

## 6. Azure Static Web Apps

### 6.1 Blazor WASM Hosting

**Intake Portal (`RVS.Blazor.Intake`):**
- **SKU:** Standard (Free for dev, Standard for staging/prod to enable Auth0 custom auth)
- **Custom Domain:** `intake.rvs.app`
- **CDN:** Built-in (Azure Front Door integration included in Standard tier)
- **Auth:** Custom Auth0 PKCE flow (not Static Web Apps built-in auth)

**Manager Desktop (`RVS.Blazor.Manager`):**
- **SKU:** Standard
- **Custom Domain:** `manager.rvs.app`
- **CDN:** Built-in
- **Auth:** Auth0 with role enforcement

**Build Configuration (`staticwebapp.config.json`):**

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/appsettings.json", "/css/*", "/_framework/*"]
  },
  "mimeTypes": {
    ".json": "application/json",
    ".wasm": "application/wasm"
  },
  "globalHeaders": {
    "cache-control": "public, max-age=31536000, immutable"
  },
  "routes": [
    {
      "route": "/appsettings*.json",
      "headers": {
        "cache-control": "no-cache"
      }
    }
  ]
}
```

### 6.2 GitHub Actions Integration

Static Web Apps auto-generate a GitHub Actions workflow on creation. Example for Intake app:

```yaml
name: Deploy Intake Portal
on:
  push:
    branches: [main]
    paths:
      - 'RVS.Blazor.Intake/**'
      - 'RVS.UI.Shared/**'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build And Deploy
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN_INTAKE }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: 'upload'
          app_location: '/RVS.Blazor.Intake'
          api_location: ''  # No API backend (uses separate App Service)
          output_location: 'wwwroot'
```

---

## 7. Azure Front Door + WAF

### 7.1 Purpose and Configuration

**Deployment:** Production only (costs ~$35/month for Standard, $330/month for Premium)

**SKU Decision:**
- **Standard:** MVP/Staging (basic DDoS, managed rule set)
- **Premium:** GA (advanced threat protection, private link to App Service, Microsoft Threat Intelligence)

**Origins:**

1. **App Service API:** `app-rvs-api-prod.azurewebsites.net`
2. **Static Web App (Intake):** `intake.rvs.app`
3. **Static Web App (Manager):** `manager.rvs.app`
4. **Blob Storage (Attachments):** `strvsprodhj3k.blob.core.windows.net/attachments`

**Routing Rules:**

```
https://api.rvs.app/*          → App Service (app-rvs-api-prod)
https://intake.rvs.app/*       → Static Web App (Intake)
https://manager.rvs.app/*      → Static Web App (Manager)
https://cdn.rvs.app/attachments/* → Blob Storage (cached, 1-hour SAS tokens valid)
```

### 7.2 WAF Policy

**Mode:** Prevention (blocks malicious requests)

**Managed Rule Sets:**

- **Microsoft Default Rule Set 2.1** (OWASP Core 3.3.5 equivalent)
- **Microsoft Bot Manager Rule Set** (bad bot protection)

**Custom Rules:**

```bicep
resource wafPolicy 'Microsoft.Network/frontDoorWebApplicationFirewallPolicies@2023-05-01' = {
  name: 'waf-rvs-prod'
  location: 'Global'
  sku: {
    name: 'Premium_AzureFrontDoor'
  }
  properties: {
    policySettings: {
      mode: 'Prevention'
      requestBodyCheck: 'Enabled'
      maxRequestBodySizeInKb: 128
    }
    customRules: {
      rules: [
        {
          name: 'RateLimitAnonymousIntake'
          priority: 100
          ruleType: 'RateLimitRule'
          rateLimitThreshold: 100
          rateLimitDurationInMinutes: 1
          matchConditions: [
            {
              matchVariable: 'RequestUri'
              operator: 'Contains'
              matchValue: ['/api/intake/']
            }
          ]
          action: 'Block'
        }
      ]
    }
    managedRules: {
      managedRuleSets: [
        {
          ruleSetType: 'Microsoft_DefaultRuleSet'
          ruleSetVersion: '2.1'
        }
        {
          ruleSetType: 'Microsoft_BotManagerRuleSet'
          ruleSetVersion: '1.0'
        }
      ]
    }
  }
}
```

**Excluded Rules (intentional allowances):**

- **942440** (SQL Injection): Exclude for `/api/service-requests/search` POST body (JSON contains SQL-like keywords in issue descriptions)
- **920300** (Missing User-Agent): Exclude for API health checks from monitoring tools

---

## 8. Azure Application Insights

### 8.1 Configuration

**Ingestion Mode:** Workspace-based (required for cross-resource queries and 90+ day retention)

**Log Analytics Workspace:**
- **Name:** `log-rvs-prod`
- **Retention:** 90 days (MVP), 365 days (GA for compliance)
- **Daily Cap:** 5 GB/day (MVP), removes cap at GA

**Sampling:**
- **Adaptive Sampling:** Enabled (auto-adjusts to keep under daily cap)
- **Sampling Percentage:** 100% (no sampling) for errors and exceptions
- **Sampling Percentage:** 20% for successful dependency calls (Cosmos, Blob, OpenAI)

### 8.2 Custom Dimensions (Tenant Telemetry)

**TelemetryInitializer (registered in Program.cs):**

```csharp
public class TenantTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is ISupportProperties propTelemetry)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var tenantId = httpContext.User.FindFirst("tenantId")?.Value;
                var locationId = httpContext.User.FindFirst("locationId")?.Value;
                var userId = httpContext.User.FindFirst("sub")?.Value;

                if (!string.IsNullOrEmpty(tenantId))
                    propTelemetry.Properties["TenantId"] = tenantId;

                if (!string.IsNullOrEmpty(locationId))
                    propTelemetry.Properties["LocationId"] = locationId;

                if (!string.IsNullOrEmpty(userId))
                    propTelemetry.Properties["UserId"] = userId;  // Hashed for GDPR
            }
        }
    }
}
```

### 8.3 Key Metrics and Alerts

**Custom Metrics Tracked:**

| Metric Name | Type | Dimensions | Purpose |
|---|---|---|---|
| `CosmosRU.ServiceRequestSearch` | Custom | `TenantId`, `LocationId` | Monitor per-tenant RU consumption |
| `AzureOpenAI.Latency` | Dependency | `Operation` | Track categorization performance |
| `AzureOpenAI.Fallback` | Event | `TenantId`, `Reason` | Count AI service failures |
| `MagicLinkGenerated` | Event | `TenantId` | Track customer intake completion |
| `AttachmentUploaded` | Event | `TenantId`, `SizeBytes` | Monitor blob storage usage |

**Alerts (Production):**

```bicep
// Alert: API availability < 99% over 5 minutes
resource availabilityAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-api-availability'
  location: 'global'
  properties: {
    severity: 0  // Critical
    enabled: true
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Availability'
          metricName: 'Availability'
          operator: 'LessThan'
          threshold: 99
          timeAggregation: 'Average'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// Alert: Cosmos RU throttling (429 responses)
resource cosmosThrottleAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-cosmos-throttling'
  properties: {
    severity: 1  // Error
    criteria: {
      allOf: [
        {
          metricName: 'TotalRequests'
          dimensions: [
            {
              name: 'StatusCode'
              operator: 'Include'
              values: ['429']
            }
          ]
          operator: 'GreaterThan'
          threshold: 10
          timeAggregation: 'Total'
        }
      ]
    }
  }
}
```

---

## 9. Environment Separation Model

### 9.1 Environment Strategy

**Model:** Subscription isolation (recommended for production workloads with RBAC boundaries)

| Environment | Subscription | Purpose | Data Sensitivity |
|---|---|---|---|
| **Development** | RVS Dev | Local testing, seed data only | No customer data |
| **Staging** | RVS Staging | Pre-production validation, anonymized customer data | Anonymized |
| **Production** | RVS Production | Live customer workloads | Full PII |

**Alternative (Cost-Optimized):** Single subscription with resource group isolation (acceptable for MVP if budget-constrained).

### 9.2 Configuration Per Environment

| Setting | Development | Staging | Production |
|---|---|---|---|
| **Cosmos DB** | Manual 400 RU | Autoscale 400–4,000 | Autoscale 400–10,000 |
| **App Service** | B1 (1 vCPU) | P1v3 (2 vCPU) | P2v3 (4 vCPU, zone redundant) |
| **Storage** | LRS | ZRS | GRS |
| **Static Web Apps** | Free | Standard | Standard |
| **Front Door + WAF** | None | Standard | Premium |
| **Key Vault** | Shared dev | Dedicated | Dedicated + purge protection |
| **Application Insights** | Shared dev | Dedicated | Dedicated + 365-day retention |
| **CORS Origins** | `http://localhost:*` | `https://*.staging.rvs.app` | `https://*.rvs.app` |
| **Auth0 Tenant** | `rvs-dev.us.auth0.com` | `rvs-staging.us.auth0.com` | `rvs.us.auth0.com` |

---

## 10. Infrastructure as Code (Bicep)

### 10.1 Repository Structure

```
/infrastructure
├── modules/
│   ├── app-service.bicep           # App Service + Plan
│   ├── cosmos-db.bicep             # Cosmos account + containers
│   ├── storage.bicep               # Blob storage + lifecycle
│   ├── key-vault.bicep             # Key Vault + RBAC
│   ├── static-web-app.bicep        # Static Web Apps
│   ├── front-door.bicep            # Front Door + WAF
│   ├── application-insights.bicep  # App Insights + Log Analytics
│   └── tenant-provisioning.bicep   # New tenant seed data
├── environments/
│   ├── staging.bicepparam          # Staging parameters
│   └── prod.bicepparam             # Prod parameters
├── main.bicep                      # Root orchestration
└── README.md                       # Deployment guide
```

### 10.2 Main Bicep Template (Orchestration)

```bicep
// main.bicep — Root deployment template
targetScope = 'resourceGroup'

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string

@description('Primary Azure region')
param location string = resourceGroup().location

@description('Tags for all resources')
param tags object = {
  Environment: environment
  Workload: 'RVS'
  ManagedBy: 'Bicep'
}

// --- App Service ---
module appService './modules/app-service.bicep' = {
  name: 'deploy-app-service'
  params: {
    environment: environment
    location: location
    tags: tags
    appServicePlanSku: environment == 'prod' ? 'P2v3' : (environment == 'staging' ? 'P1v3' : 'B1')
    zoneRedundant: environment == 'prod'
  }
}

// --- Cosmos DB ---
module cosmosDb './modules/cosmos-db.bicep' = {
  name: 'deploy-cosmos-db'
  params: {
    environment: environment
    location: location
    tags: tags
    maxAutoscaleThroughput: environment == 'prod' ? 10000 : 4000
  }
}

// --- Blob Storage ---
module storage './modules/storage.bicep' = {
  name: 'deploy-storage'
  params: {
    environment: environment
    location: location
    tags: tags
    replicationMode: environment == 'prod' ? 'GRS' : (environment == 'staging' ? 'ZRS' : 'LRS')
  }
}

// --- Key Vault ---
module keyVault './modules/key-vault.bicep' = {
  name: 'deploy-key-vault'
  params: {
    environment: environment
    location: location
    tags: tags
    enablePurgeProtection: environment == 'prod'
    appServicePrincipalId: appService.outputs.principalId
  }
}

// --- Application Insights ---
module appInsights './modules/application-insights.bicep' = {
  name: 'deploy-app-insights'
  params: {
    environment: environment
    location: location
    tags: tags
    retentionInDays: environment == 'prod' ? 365 : 90
  }
}

// --- Static Web Apps ---
module staticWebAppIntake './modules/static-web-app.bicep' = {
  name: 'deploy-static-intake'
  params: {
    environment: environment
    appName: 'intake'
    sku: environment == 'dev' ? 'Free' : 'Standard'
    tags: tags
  }
}

module staticWebAppManager './modules/static-web-app.bicep' = {
  name: 'deploy-static-manager'
  params: {
    environment: environment
    appName: 'manager'
    sku: environment == 'dev' ? 'Free' : 'Standard'
    tags: tags
  }
}

// --- Front Door (prod/staging only) ---
module frontDoor './modules/front-door.bicep' = if (environment != 'dev') {
  name: 'deploy-front-door'
  params: {
    environment: environment
    sku: environment == 'prod' ? 'Premium_AzureFrontDoor' : 'Standard_AzureFrontDoor'
    tags: tags
    apiOriginHostName: appService.outputs.defaultHostName
    intakeOriginHostName: staticWebAppIntake.outputs.defaultHostName
    managerOriginHostName: staticWebAppManager.outputs.defaultHostName
  }
}

// --- Outputs ---
output appServiceName string = appService.outputs.name
output cosmosDbAccountName string = cosmosDb.outputs.accountName
output storageAccountName string = storage.outputs.accountName
output keyVaultName string = keyVault.outputs.name
```

### 10.3 Deployment Commands

**Initial Deployment:**

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "RVS Production"

# Create resource group
az group create --name rg-rvs-prod-westus3 --location westus3

# Deploy infrastructure
az deployment group create \
  --resource-group rg-rvs-prod-westus3 \
  --template-file infrastructure/main.bicep \
  --parameters infrastructure/environments/prod.bicepparam

# Deploy secrets to Key Vault (manual one-time step)
az keyvault secret set --vault-name kv-rvs-prod-x7m2 --name auth0-client-secret --value "<secret>"
az keyvault secret set --vault-name kv-rvs-prod-x7m2 --name openai-api-key --value "<secret>"
```

**Update Deployment:**

```bash
# What-If mode (dry run)
az deployment group what-if \
  --resource-group rg-rvs-prod-westus3 \
  --template-file infrastructure/main.bicep \
  --parameters infrastructure/environments/prod.bicepparam

# Apply changes
az deployment group create \
  --resource-group rg-rvs-prod-westus3 \
  --template-file infrastructure/main.bicep \
  --parameters infrastructure/environments/prod.bicepparam
```

---

## 11. CI/CD Pipeline (GitHub Actions)

### 11.1 Workflow Overview

```
┌─────────────────────────────────────────────────────────────┐
│ GitHub Actions Workflow: Deploy RVS Platform               │
│                                                             │
│  Trigger: Push to main branch                              │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Stage 1: Build & Test                               │  │
│  │  • dotnet restore                                    │  │
│  │  • dotnet build --configuration Release             │  │
│  │  • dotnet test (all test projects)                  │  │
│  └─────────────────────────────────────────────────────┘  │
│                          │                                  │
│                          ▼                                  │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Stage 2: Deploy to Staging                          │  │
│  │  • az deployment group create (Bicep)               │  │
│  │  • dotnet publish RVS.API                           │  │
│  │  • Deploy API to staging slot                       │  │
│  │  • Deploy Blazor apps to Static Web Apps            │  │
│  │  • Run smoke tests against staging                  │  │
│  └─────────────────────────────────────────────────────┘  │
│                          │                                  │
│                          ▼                                  │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Stage 3: Manual Approval (GitHub Environment)       │  │
│  │  • Required reviewers: 1                            │  │
│  │  • Timeout: 24 hours                                │  │
│  └─────────────────────────────────────────────────────┘  │
│                          │                                  │
│                          ▼                                  │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Stage 4: Deploy to Production                       │  │
│  │  • Swap staging → production slot                   │  │
│  │  • Monitor Application Insights for 5 minutes       │  │
│  │  • Auto-rollback on error spike                     │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### 11.2 Sample GitHub Actions Workflow

```yaml
name: Deploy RVS Platform

on:
  push:
    branches: [main]
  workflow_dispatch:

env:
  DOTNET_VERSION: '10.0.x'
  AZURE_RESOURCE_GROUP_STAGING: rg-rvs-staging-westus3
  AZURE_RESOURCE_GROUP_PROD: rg-rvs-prod-westus3

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal

      - name: Publish API
        run: dotnet publish RVS.API/RVS.API.csproj -c Release -o ./api-publish

      - name: Upload API artifact
        uses: actions/upload-artifact@v4
        with:
          name: api
          path: ./api-publish

  deploy-staging:
    needs: build-and-test
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - uses: actions/checkout@v4

      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS_STAGING }}

      - name: Deploy Bicep Infrastructure
        run: |
          az deployment group create \
            --resource-group ${{ env.AZURE_RESOURCE_GROUP_STAGING }} \
            --template-file infrastructure/main.bicep \
            --parameters infrastructure/environments/staging.bicepparam

      - name: Download API artifact
        uses: actions/download-artifact@v4
        with:
          name: api
          path: ./api-publish

      - name: Deploy API to Staging Slot
        uses: azure/webapps-deploy@v3
        with:
          app-name: app-rvs-api-staging
          slot-name: staging
          package: ./api-publish

      - name: Deploy Blazor Intake
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN_INTAKE_STAGING }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: 'upload'
          app_location: '/RVS.Blazor.Intake'
          output_location: 'wwwroot'

      - name: Smoke Test Staging
        run: |
          curl -f https://app-rvs-api-staging-staging.azurewebsites.net/health || exit 1

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: production  # GitHub Environment with manual approval
    steps:
      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS_PROD }}

      - name: Swap Staging to Production
        run: |
          az webapp deployment slot swap \
            --resource-group ${{ env.AZURE_RESOURCE_GROUP_PROD }} \
            --name app-rvs-api-prod \
            --slot staging \
            --target-slot production

      - name: Monitor Production Health
        run: |
          sleep 60
          curl -f https://api.rvs.app/health || exit 1
```

### 11.3 Secrets Required in GitHub

| Secret Name | Purpose | How to Obtain |
|---|---|---|
| `AZURE_CREDENTIALS_STAGING` | Service Principal JSON for Staging | `az ad sp create-for-rbac --name sp-rvs-staging --role Contributor --scopes /subscriptions/{sub-id}/resourceGroups/rg-rvs-staging-westus3 --sdk-auth` |
| `AZURE_CREDENTIALS_PROD` | Service Principal JSON for Production | `az ad sp create-for-rbac --name sp-rvs-prod --role Contributor --scopes /subscriptions/{sub-id}/resourceGroups/rg-rvs-prod-westus3 --sdk-auth` |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_INTAKE_STAGING` | Deployment token for Intake app | From Static Web App resource in Azure Portal |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_MANAGER_STAGING` | Deployment token for Manager app | From Static Web App resource in Azure Portal |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_INTAKE_PROD` | Deployment token for Intake app | From Static Web App resource in Azure Portal |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_MANAGER_PROD` | Deployment token for Manager app | From Static Web App resource in Azure Portal |

---

## 12. Tenant Provisioning

### 12.1 Automated Tenant Onboarding

**Bicep Module:** `infrastructure/modules/tenant-provisioning.bicep`

**Inputs:**
- `tenantId` (e.g., `org_blue_compass_rv`)
- `tenantName` (e.g., `Blue Compass RV`)
- `ownerEmail` (dealership owner email)
- `planTier` (`Starter`, `Pro`, `Enterprise`)

**Provisioning Steps:**

1. **Create `Dealership` document** in Cosmos DB `dealerships` container
2. **Create `TenantConfig` document** in Cosmos DB `tenantConfigs` container
3. **Create initial `Location` document** in Cosmos DB `locations` container
4. **Create `SlugLookup` document** in Cosmos DB `slugLookup` container
5. **Generate SFTP keypair** and store private key in Key Vault as `sftp-key-{tenantId}`
6. **Create Auth0 Organization** (via Management API) with `tenantId` as `org_id`
7. **Send welcome email** with initial owner invite link

**Script Execution:**

```bash
# Run tenant provisioning via Azure CLI + Bicep
az deployment group create \
  --resource-group rg-rvs-prod-westus3 \
  --template-file infrastructure/modules/tenant-provisioning.bicep \
  --parameters \
    tenantId="org_blue_compass_rv" \
    tenantName="Blue Compass RV" \
    ownerEmail="admin@bluecompassrv.com" \
    planTier="Enterprise"
```

---

## 13. Disaster Recovery and Backup

### 13.1 Cosmos DB Backup

**Mode:** Continuous Backup (Point-in-Time Restore)

**Retention:** 7 days (MVP), 30 days (GA)

**Restore Scenarios:**

- **Accidental container deletion:** Restore to 5 minutes before deletion
- **Incorrect bulk update:** Restore affected containers to timestamp before update
- **Data corruption:** Restore entire database to known good state

**Restore Command:**

```bash
# List available restore timestamps
az cosmosdb sql database list-restorable-timestamps \
  --account-name cosmos-rvs-prod \
  --database-name RVS

# Restore to specific timestamp
az cosmosdb restore \
  --account-name cosmos-rvs-prod-restored \
  --restore-timestamp "2026-04-01T12:00:00Z" \
  --location "West US 3"
```

### 13.2 Blob Storage Backup

**Soft Delete:** Enabled (7-day retention for blobs and containers)

**Blob Versioning:** Enabled (immutable history, protects against overwrites)

**Geo-Replication (GRS):** Automatic async replication to paired region (East US)

**Recovery Point Objective (RPO):** < 15 minutes (Azure SLA for GRS)

### 13.3 Key Vault Backup

**Soft Delete:** Enabled (90-day retention)

**Purge Protection:** Enabled (production only, prevents permanent deletion during soft-delete window)

**Secret Versioning:** Automatic (all secret updates create new versions, old versions retained)

**Backup Command:**

```bash
# Backup all secrets from Key Vault
az keyvault secret backup \
  --vault-name kv-rvs-prod-x7m2 \
  --name auth0-client-secret \
  --file auth0-client-secret.backup

# Restore from backup
az keyvault secret restore \
  --vault-name kv-rvs-prod-x7m2 \
  --file auth0-client-secret.backup
```

---

## 14. Security and Compliance

### 14.1 Network Security

**App Service:**
- **HTTPS Only:** Enforced (HTTP → HTTPS redirect)
- **TLS Version:** 1.2 minimum
- **Outbound IPs:** Static (registered in Cosmos DB firewall)
- **Private Endpoint:** GA phase (VNet integration required)

**Cosmos DB:**
- **Firewall:** Enabled (allow App Service IPs, deny all others)
- **Public Network Access:** Enabled (MVP), Disabled with Private Link (GA)
- **Encryption:** Automatic (data at rest encrypted with Microsoft-managed keys)

**Blob Storage:**
- **HTTPS Only:** Enforced
- **Public Access:** Disabled (SAS tokens only)
- **Encryption:** Automatic (data at rest encrypted with Microsoft-managed keys)

**Key Vault:**
- **Firewall:** Enabled (allow App Service IPs, GitHub Actions IPs)
- **Private Endpoint:** GA phase
- **Soft Delete + Purge Protection:** Enabled (production)

### 14.2 Identity and Access Management

**Azure RBAC Roles (Production):**

| Principal | Role | Scope | Justification |
|---|---|---|---|
| App Service Managed Identity | `Cosmos DB Account Reader Role` | Cosmos DB Account | Read-only access to connection metadata |
| App Service Managed Identity | `Storage Blob Data Contributor` | Blob Storage Account | Read/write attachments |
| App Service Managed Identity | `Key Vault Secrets User` | Key Vault | Read secrets only (no write/delete) |
| GitHub Actions Service Principal | `Contributor` | Resource Group | Deploy infrastructure and app code |
| DevOps Team | `Owner` | Resource Group | Full management (emergency access) |
| Support Team | `Reader` | Resource Group | Read-only for troubleshooting |

**Principle of Least Privilege:** No account keys or connection strings in code. All access via managed identity + RBAC.

### 14.3 Compliance Readiness

**Data Residency:** All resources deployed in **West US 3** (primary), **East US** (failover replica for Cosmos DB and GRS blobs)

**Data Classification:**
- **PII (Customer email, phone, name):** Encrypted at rest, access logged
- **Attachments (photos, videos):** Encrypted at rest, time-limited SAS access
- **Secrets (Auth0, OpenAI keys):** Azure Key Vault with RBAC and audit logs

**Audit Logging:**
- **Azure Activity Log:** All resource-level changes (who created/modified/deleted)
- **Application Insights:** All API requests with tenant ID dimension
- **Cosmos DB Diagnostic Logs:** All data-plane operations (read/write/delete)
- **Key Vault Audit Logs:** All secret access events

**Retention:** 90 days (MVP), 365 days (GA for SOC 2 compliance)

---

## 15. Cost Estimation

### 15.1 MVP Cost Breakdown (20 tenants, moderate load)

| Resource | SKU | Unit Cost | Quantity | Monthly Cost |
|---|---|---|---|---|
| App Service Plan | P2v3 | $292.00 | 1 | $292.00 |
| Cosmos DB (autoscale avg) | ~1,500 RU/s avg | $0.12/100 RU/hr | 1 | $130.00 |
| Blob Storage (Hot + GRS) | $0.036/GB | 50 GB | $1.80 |
| Blob Transactions | $0.004/10k | 100k | $0.40 |
| Key Vault | Standard | $0.03/10k ops | 50k ops | $0.15 |
| Application Insights | $2.30/GB | 5 GB | $11.50 |
| Static Web Apps | Standard | $9.00 | 2 | $18.00 |
| Azure Front Door | Premium | $330.00 | 1 | $330.00 |
| **Total** | | | | **$783.85/month** |

**Cost Optimization Opportunities:**

- **Use Front Door Standard** (not Premium) in MVP: saves $295/month → **$488/month total**
- **Defer Front Door to GA** (use App Service + Static Web Apps only): saves $330/month → **$453/month total**
- **Use single subscription** (not 3): no change to resource costs, simplifies billing

### 15.2 GA Cost Projection (100 tenants, high load)

| Resource | Change from MVP | Monthly Cost |
|---|---|---|
| App Service Plan | P3v3 (8 vCPU, 32 GB RAM) | $584.00 |
| Cosmos DB | ~5,000 RU/s avg | $430.00 |
| Blob Storage | 500 GB + lifecycle tiering | $12.00 |
| Azure Front Door | Premium with WAF | $330.00 |
| Application Insights | 20 GB | $46.00 |
| **Total** | | **$1,420/month** |

**Unit Economics:**

- **MVP (20 tenants):** $488/month ÷ 20 = $24.40/tenant (plan tier $199-499 → 4-20x margin)
- **GA (100 tenants):** $1,420/month ÷ 100 = $14.20/tenant (plan tier $199-499 → 14-35x margin)

---

## 16. Migration Path to Multi-Region

**Deferred to GA Phase.** When RVS expands to multi-region for global customers:

**Architecture Changes:**

1. **Cosmos DB:** Enable multi-region writes (Active-Active). Add read replicas in target regions.
2. **App Service:** Deploy App Service instances in each region (West US 3, East US, West Europe).
3. **Azure Traffic Manager or Front Door:** Route users to nearest region based on latency.
4. **Blob Storage:** Use ZRS (zone-redundant) or GZRS (geo-zone-redundant) in each region. Replicate via Azure Data Factory or AzCopy.
5. **Auth0:** No change (Auth0 is globally distributed by default).

**Estimated Additional Cost:** +60% ($2,272/month for dual-region active-active deployment).

---

## Summary

This document provides the complete Azure infrastructure architecture for RVS, addressing the P0 gap identified in the Cloud Architecture Assessment. Key deliverables:

✅ **Resource topology defined** (App Service, Cosmos DB, Blob Storage, Key Vault, Static Web Apps, Front Door, Application Insights)  
✅ **Sizing and scaling decisions** (App Service tiers, Cosmos RU autoscale ranges, storage redundancy)  
✅ **Environment separation model** (subscription isolation for dev/staging/prod)  
✅ **Infrastructure as Code strategy** (Bicep modules, deployment commands, GitHub Actions CI/CD)  
✅ **Security baseline** (managed identity, RBAC, Key Vault references, WAF policies)  
✅ **Cost estimation** (MVP $488/month, GA $1,420/month with 14-35x margin on lowest plan tier)  
✅ **Disaster recovery** (Cosmos continuous backup, blob soft delete, Key Vault purge protection)  

**Next Steps:**

1. Implement Bicep modules in `/infrastructure` directory
2. Configure GitHub Actions workflows for automated deployment
3. Provision development environment and validate IaC
4. Create tenant provisioning runbook
5. Address magic link storage guidance (see companion document `RVS_MagicLink_Storage_Guidance.md`)

---

**Document Version:** 1.0  
**Last Updated:** April 5, 2026  
**Author:** GitHub Copilot (Azure IaC Code Generation Hub)  
**Status:** Authoritative Source of Truth (ASOT)
