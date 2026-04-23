# SWA Preview Environments

Both Intake and Manager SWAs use **named pre-production environments** (Standard tier feature, up to 10 per SWA) for branch testing. Names are fixed so Auth0 callback URLs stay explicit (no wildcards).

## Reserved environment names

| Env | Hostname pattern | Trigger |
|---|---|---|
| `pr-preview` | `<host>-pr-preview.westus2.azurestaticapps.net` | `pull_request` (auto, via `.github/workflows/pr-preview-swa.yml`) |
| `qa` | `<host>-qa.westus2.azurestaticapps.net` | `workflow_dispatch` (future) |
| `demo` | `<host>-demo.westus2.azurestaticapps.net` | `workflow_dispatch` (future) |

`<host>` is the SWA's `defaultHostname`, obtainable via:

```bash
az staticwebapp show -n <swa-name> --query defaultHostname -o tsv
```

SWA resource names follow the naming convention defined in `Docs/ASOT/Infra/Bicep.IaC/main.bicep`:

| App | Resource name |
|---|---|
| Intake | `stapp-rvs-intake-<env>` (e.g. `stapp-rvs-intake-staging`) |
| Manager | `stapp-rvs-manager-<env>` (e.g. `stapp-rvs-manager-staging`) |

## How it works

1. A PR targeting `main` that touches `RVS.Blazor.Intake/**`, `RVS.Blazor.Manager/**`, `RVS.UI.Shared/**`, or `RVS.Domain/**` triggers the workflow.
2. Each SWA is published and deployed to the `pr-preview` named environment using the existing `INTAKE_SWA_TOKEN_STAGING` / `MANAGER_SWA_TOKEN_STAGING` secrets.
3. When the PR is closed (merged or abandoned) the workflow runs with `action: close`, which tears down the named environment.

## Auth0 registration

Because environment names are fixed, the preview hostnames are stable and can be registered explicitly in the Auth0 staging tenant — no wildcard callback URLs required.

Add the following to the Auth0 staging application's **Allowed Callback URLs** and **Allowed Logout URLs**:

- `https://stapp-rvs-intake-staging-pr-preview.westus2.azurestaticapps.net`
- `https://stapp-rvs-manager-staging-pr-preview.westus2.azurestaticapps.net`

## Tradeoffs

- Only **one PR at a time** can occupy the `pr-preview` slot per SWA. The concurrency group in the workflow serializes runs so the last push wins.
- If parallel PR testing is needed in the future, add additional named environments (e.g. `pr-preview-2`) and corresponding secrets/Auth0 registrations.
