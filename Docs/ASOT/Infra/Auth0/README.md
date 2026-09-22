# Auth0 configuration

Scripts that capture and change the RVS part of an Auth0 tenant. Anything declared in `baseline/` is owned by these scripts: a dashboard edit to it shows up as a difference on the next plan and is overwritten by the next apply.

Development, staging and production currently share **one** tenant, `dev-2jhzz8xmjggh26pm.us.auth0.com` (#610). A change applied to it reaches all three environments at once. The tenant also hosts unrelated products' APIs, applications and Actions, which these scripts leave alone.

## Files

| Path | What it is |
| --- | --- |
| `tenants/<name>.env` | One file per Auth0 tenant: domain, where its Management API credentials live, and the environment-specific URLs. Today only `shared.env`; `_template.env` is the starting point for more. |
| `baseline/` | Desired state for what RVS owns: the RVS API and its permissions, roles, the Manager and Swagger applications (with their API grants and connections), and the Post-Login Action source. Environment-agnostic; URLs come from the tenant file. |
| `auth0-apply.sh` | Compares a tenant with `baseline/` and, with `--apply`, makes it match. |
| `auth0-export.sh` | Read-only snapshot of a whole tenant into `snapshots/<name>/`. For inspecting a tenant or finding drift outside the baseline; not a source of truth. |
| `lib.sh` | Helpers shared by both scripts. |

`snapshots/` is gitignored. This repository is public, and a snapshot includes other products' configuration and the tenant's security settings.

## Prerequisites

- `az`, logged in, with read access to the tenant's Management API secrets (`Auth0Mgmt--Domain`, `--ClientId`, `--ClientSecret`; the vault is named in the tenant file)
- `curl` and `jq`
- bash 3.2 or later (the macOS default works)

The Management API app and its Key Vault secrets are created by hand, once per tenant: see [`../../Auth0/Auth0-Portal-Configuration-Checklist.md`](../../Auth0/Auth0-Portal-Configuration-Checklist.md). Planning needs only read scopes. Applying needs write scopes as well; the full list is in the header of `auth0-apply.sh`.

## Making a change

1. On a branch, edit `baseline/` or the tenant file.
2. Plan: `./auth0-apply.sh shared`. It prints a diff per resource and exits `2` when there are differences, `0` when there are none.
   A setting it checks but never changes (below) also exits `2`, with a line saying `--apply` won't fix it.
3. Once the change is reviewed: `./auth0-apply.sh shared --apply`. It plans again against live state, asks you to type the tenant domain, then applies.
4. Plan again and expect `No differences`.

Common edits:

- **New permission:** add it to `scopes` in `baseline/resource-server.json` and to the roles that need it in `baseline/roles.json`. The Manager's grant (`"scope": "all"`) picks it up automatically. It also needs a policy in `RVS.API/Program.cs`.
- **New Manager origin:** add it to `AUTH0_MANAGER_ORIGINS` in the tenant file. The callback, logout and web-origin URLs are all derived from it.
- **Renaming an application in the dashboard:** change `name` in its `baseline/clients/<name>.json` in the same change. Applications are matched by name, so after a dashboard rename the plan reports `+ create application <old name>`,
  and `--apply` would create a duplicate with a new client ID. The Manager was renamed to "RV Intake Manager" this way.
- **Action change:** edit `baseline/actions/<name>.js`. Apply updates the Action, waits for the build, deploys it, and makes sure it is bound to its trigger.

## What the scripts never do

- Delete a resource. Removing a role or application from `baseline/` leaves it in the tenant; delete it in the dashboard. Within a managed resource the baseline is authoritative: a permission not in the baseline is removed from the RVS API, RVS roles and the Manager grant.
- Touch users. Onboarding users (with `app_metadata`) is covered in the checklist and in `RVS_Identity.md`.
- Manage tenant-wide settings (session lifetimes, attack protection, Universal Login prompts). While the tenant is shared with other products, most of those aren't RVS's to set. The exceptions RVS does set — the custom domain, the Friendly Name, Universal Login branding and the email provider — are deliberate, done by hand, and written up in [`../../Auth0/Auth0-Portal-Configuration-Checklist.md`](../../Auth0/Auth0-Portal-Configuration-Checklist.md) §6–§8. Each one reaches the other products in this tenant as well.

  The apply script **checks** most of them against the tenant file and reports drift. It never changes them, so a dashboard edit by anyone sharing the tenant shows up on the next plan:

  | Setting | Expected value | Scope | If it drifts |
  | --- | --- | --- | --- |
  | Friendly Name | `AUTH0_EXPECT_FRIENDLY_NAME` | `read:tenant_settings` | The login card names something else |
  | Custom domain: ready, and the tenant default | the host in `AUTH0_APP_AUTHORITY` | `read:custom_domains` | `/admin` set-password links fall back to the canonical host |
  | Email provider and From address | `AUTH0_EXPECT_EMAIL_PROVIDER`, `AUTH0_EXPECT_EMAIL_FROM` | `read:email_provider` | Reset mail comes from `no-reply@auth0user.net`, or someone else's sender |

  An unset value skips its check. A missing scope skips its check with a warning, so a plan works before the scopes are granted. Branding isn't checked yet; there is nothing set to compare against.
- Edit Manager `appsettings.*.json` or Key Vault. The apply script checks that the appsettings files listed in the tenant file point at this tenant and application, and reports any mismatch. The authority it expects is `AUTH0_APP_AUTHORITY`, falling back to `https://$AUTH0_DOMAIN/`. Those differ once the tenant has a custom domain: the apps move to it, `AUTH0_DOMAIN` stays on the canonical `.us.auth0.com` host for the Management API.

## Splitting environments later

1. Create the tenant and its Management API app, and store the app's credentials in that environment's vault (checklist).
2. Copy `tenants/_template.env` to, e.g., `tenants/prod.env` and fill it in.
3. `./auth0-apply.sh prod --apply` creates the API, roles, applications, grants, connections and Action.
4. Update `RVS.Blazor.Manager/wwwroot/appsettings.Production.json` with the new authority and client ID (the plan reports both), and the `auth0*` Bicep parameters for that environment.
5. Remove that environment's URLs from `shared.env` and apply there.

## Known state worth knowing

- The `dealer:technician` role and the `analytics:read` permission are archived scope. They stay in the baseline because they exist live; remove them as part of the descope. `service-requests:update-service-event` was removed from the baseline in #457; the next apply drops it from the RVS API and every RVS role.
- The Swagger SPA Client's grant to the RVS API has no permissions, and the API only issues permissions an application is granted. Tokens from Swagger UI therefore carry none.
