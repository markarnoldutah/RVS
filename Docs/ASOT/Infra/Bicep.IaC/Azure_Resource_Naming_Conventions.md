# RVS Azure Resource Naming Conventions

## Purpose

This standard defines how Azure resources are named across RVS environments.
It is based on Azure Cloud Adoption Framework naming guidance and SaaS Well-Architected design principles.

## Business Model Context

RVS is treated as a multitenant SaaS platform.

- Primary assumption: **B2B SaaS** with tenant-level isolation options.
- Secondary support: **B2C-like scale** through deployment stamps and shared platform resources.

The naming model must make it easy to identify environment, region, platform stamp, and ownership while avoiding tenant data leakage in names.

## Naming Design Principles

- Use only lowercase letters, numbers, and hyphens unless Azure resource rules require otherwise.
- Keep names deterministic and immutable. If mutable business metadata is needed, use tags instead.
- Include only stable identifiers in names: app, workload, environment, region, stamp, and instance.
- Use short Azure-approved abbreviations for resource type prefixes.
- Include environment in every deployable resource name.
- Include region when the resource can be regional or when cross-region operations are expected.
- Include stamp to support horizontal scaling and noisy-neighbor isolation.
- Never include secrets, customer names, email addresses, or PII in resource names.
- Prefer three-digit instance counters for repeatable assets (`001`, `002`).

## Canonical Pattern

Use this pattern unless a resource type has stricter rules:

`<type>-<app>-<workload>-<env>-<region>-<stamp>-<instance>`

Where:

- `type`: Azure resource abbreviation (for example `rg`, `kv`, `vnet`).
- `app`: platform identifier. For this repo use `rvs`.
- `workload`: bounded context or service name (for example `api`, `intake`, `manager`, `data`, `shared`).
- `env`: `dev`, `test`, `stage`, `prod`.
- `region`: short region code (for example `wus2`, `eus2`, `weu`).
- `stamp`: deployment stamp or scale unit (`s01`, `s02`).
- `instance`: numeric sequence (`001`, `002`).

## Components and Allowed Values

### Environment Codes

- `dev`: developer/shared development
- `test`: integration and automated validation
- `stage`: pre-production
- `prod`: production

### Region Codes

Use one short code per Azure region and keep it consistent:

- `wus2`: westus2
- `eus2`: eastus2
- `weu`: westeurope

If new regions are added, update this list once and reuse it everywhere.

### Stamp Codes

- `s01` to `s99`
- `s00` can be reserved for global/shared platform resources when needed

## Required Abbreviations

| Resource Type | Prefix | Example |
|---|---|---|
| Resource Group | `rg` | `rg-rvs-api-dev-wus2-s01-001` |
| Virtual Network | `vnet` | `vnet-rvs-shared-prod-wus2-s01-001` |
| Subnet | `snet` | `snet-rvs-api-prod-wus2-s01-001` |
| Network Security Group | `nsg` | `nsg-rvs-api-prod-wus2-s01-001` |
| Route Table | `rt` | `rt-rvs-shared-prod-wus2-s01-001` |
| User Assigned Managed Identity | `uami` | `uami-rvs-api-prod-wus2-s01-001` |
| Key Vault | `kv` | `kv-rvs-shared-prod-wus2-s01-001` |
| Log Analytics Workspace | `law` | `law-rvs-obs-prod-wus2-s01-001` |
| Application Insights | `appi` | `appi-rvs-api-prod-wus2-s01-001` |
| Azure OpenAI | `oai` | `oai-rvs-ai-prod-wus2-s01-001` |
| AI Search | `srch` | `srch-rvs-ai-prod-wus2-s01-001` |
| Cosmos DB Account | `cosmos` | `cosmos-rvs-data-prod-wus2-s01-001` |
| Service Bus Namespace | `sbns` | `sbns-rvs-int-prod-wus2-s01-001` |
| Container Apps Environment | `cae` | `cae-rvs-app-prod-wus2-s01-001` |
| Container App | `ca` | `ca-rvs-api-prod-wus2-s01-001` |
| Azure Container Registry | `acr` | `acr-rvs-shared-prod-wus2-s01-001` |

## Special Rules for Global-Name Resources

Some Azure resources require globally unique names and do not allow hyphens or have strict character limits.

Use compact pattern:

`<type><app><workload><env><region><stamp><instance>`

Examples:

- Storage account: `strvsdataprodwus2s01001`
- ACR (if compact mode required): `acrrvssharedprodwus2s01001`
- Web app/function app when DNS constraints apply: keep names short and deterministic, for example `app-rvs-api-prod-wus2-s01-001`

When a resource type has stricter platform rules, platform rules win.

## SaaS Tenant Naming Guidance

- Do not include raw tenant names in shared infrastructure resources.
- Prefer stamp and workload encoding over customer identifiers.
- For dedicated tenant resources (enterprise tier), use tenant surrogate IDs only, for example `t0142`.
- Dedicated pattern:
  - `<type>-rvs-<workload>-<env>-<region>-<tenantid>-<instance>`
  - Example: `rg-rvs-api-prod-wus2-t0142-001`
- Keep tenant IDs opaque and non-PII.

## Tagging Requirements (Required with Naming)

Naming alone is not enough for governance and cost allocation.

Every resource must include at least these tags:

- `application = rvs`
- `environment = dev|test|stage|prod`
- `workload = api|intake|manager|shared|data|ai|integration`
- `region = westus2|eastus2|...`
- `stamp = s01|s02|...`
- `owner = team-or-service`
- `costCenter = <code>`
- `tenantModel = shared|dedicated`

## Example Name Set by Environment

### Dev

- `rg-rvs-api-dev-wus2-s01-001`
- `vnet-rvs-shared-dev-wus2-s01-001`
- `kv-rvs-shared-dev-wus2-s01-001`
- `oai-rvs-ai-dev-wus2-s01-001`

### Prod

- `rg-rvs-api-prod-wus2-s01-001`
- `law-rvs-obs-prod-wus2-s01-001`
- `appi-rvs-api-prod-wus2-s01-001`
- `cosmos-rvs-data-prod-wus2-s01-001`

## Validation and Enforcement

- Enforce naming in Bicep/Terraform modules with parameter validation.
- Add CI checks for naming regex and required tags.
- Reject non-compliant names during pull request validation.

Suggested baseline regex for hyphen-allowed resource names:

```text
^[a-z0-9]+(-[a-z0-9]+){5,7}$
```

Adjust per-resource constraints (length, allowed characters, uniqueness scope).

## References

- Azure naming guidance: [Define your naming convention](https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-naming)
- SaaS architecture guide: [SaaS and multitenant solution architecture](https://learn.microsoft.com/azure/architecture/guide/saas-multitenant-solution-architecture/)
- SaaS design principles: [Design principles of SaaS workloads on Azure](https://learn.microsoft.com/azure/well-architected/saas/design-principles)
- Azure resource abbreviations: [Recommended abbreviations for Azure resource types](https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations)