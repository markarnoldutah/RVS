# SWA Preview Environments

Both Intake and Manager SWAs use **named pre-production environments** (Standard tier feature, up to 10 per SWA) for branch testing. Names are fixed so Auth0 callback URLs stay explicit (no wildcards).

## Reserved environment names

| Env | Hostname pattern | Trigger |
|---|---|---|
| `pr-preview` | `<host>-pr-preview.azurestaticapps.net` | `pull_request` (auto, via `.github/workflows/pr-preview-swa.yml`) |
| `qa` | `<host>-qa.azurestaticapps.net` | `workflow_dispatch` (future) |
| `demo` | `<host>-demo.azurestaticapps.net` | `workflow_dispatch` (future) |

`<host>` is the SWA's `defaultHostname` (without the `.azurestaticapps.net` suffix), obtainable via:

```bash
az staticwebapp show -n <swa-name> --query defaultHostname -o tsv
```

Current staging hosts:

| App | Default hostname |
|---|---|
| Intake | `intake-staging.azurestaticapps.net` |
| Manager | `manager-staging.azurestaticapps.net` |

> Some Azure regions return a hostname with a region segment (e.g. `<host>-pr-preview.westus2.azurestaticapps.net`). After the first preview deploy, confirm the exact form printed by the `Azure/static-web-apps-deploy@v1` action and update the Auth0 entries below to match.

## How it works

1. A PR targeting `main` that touches `RVS.Blazor.Intake/**`, `RVS.Blazor.Manager/**`, `RVS.UI.Shared/**`, or `RVS.Domain/**` triggers the workflow.
2. Each SWA is published and deployed to the `pr-preview` named environment using the existing `INTAKE_SWA_TOKEN_STAGING` / `MANAGER_SWA_TOKEN_STAGING` secrets.
3. When the PR is closed (merged or abandoned) the workflow runs with `action: close`, which tears down the named environment.

## Auth0 registration (Manager only)

**Intake has no authentication**, so it requires no Auth0 changes — preview hostnames just work.

**Manager** uses Auth0 and needs each named env hostname registered in the **staging Auth0 tenant** on the Manager SPA application. Because env names are fixed, hostnames are stable and can be registered explicitly — no wildcards required.

For the three reserved envs, add these entries on the Manager SPA:

### Allowed Callback URLs

- `https://manager-staging.azurestaticapps.net/authentication/login-callback`
- `https://manager-staging-pr-preview.azurestaticapps.net/authentication/login-callback`
- `https://manager-staging-qa.azurestaticapps.net/authentication/login-callback`
- `https://manager-staging-demo.azurestaticapps.net/authentication/login-callback`

### Allowed Logout URLs

- `https://manager-staging.azurestaticapps.net/`
- `https://manager-staging.azurestaticapps.net/authentication/logout-callback`
- `https://manager-staging-pr-preview.azurestaticapps.net/`
- `https://manager-staging-pr-preview.azurestaticapps.net/authentication/logout-callback`
- `https://manager-staging-qa.azurestaticapps.net/`
- `https://manager-staging-qa.azurestaticapps.net/authentication/logout-callback`
- `https://manager-staging-demo.azurestaticapps.net/`
- `https://manager-staging-demo.azurestaticapps.net/authentication/logout-callback`

### Allowed Web Origins / Allowed Origins (CORS)

- `https://manager-staging.azurestaticapps.net`
- `https://manager-staging-pr-preview.azurestaticapps.net`
- `https://manager-staging-qa.azurestaticapps.net`
- `https://manager-staging-demo.azurestaticapps.net`

> Also ensure the **staging API's CORS allow-list** includes the same four origins, otherwise the browser will block API calls from preview envs even though Auth0 succeeds.

## Tradeoffs

- Only **one PR at a time** can occupy the `pr-preview` slot per SWA. The concurrency group in the workflow serializes runs so the last push wins.
- All previews point at the **shared staging API** (no per-branch API isolation until App Service is upgraded to S1 with deployment slots — see `Revised_Arch.md`).
- If parallel PR testing is needed before then, add additional named environments (e.g. `pr-preview-2`) and corresponding Auth0 registrations.