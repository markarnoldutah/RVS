# Auth0 Portal Configuration Checklist

**Updated:** September 14, 2026
**Source:** [`../RVS_Identity.md`](../RVS_Identity.md), [`../Infra/Auth0/README.md`](../Infra/Auth0/README.md)

The RVS API, permissions, roles, applications, grants, connections and Post-Login Action are **scripted**: they are declared in [`../Infra/Auth0/baseline/`](../Infra/Auth0/baseline/) and applied with `auth0-apply.sh`. Don't configure them in the dashboard; the next plan reports the edit as a difference and the next apply reverts it.

This checklist covers only what the scripts can't do. The identity model itself is in [`../RVS_Identity.md`](../RVS_Identity.md).

---

## 1. Create the tenant (new environments only)

Only needed when an environment gets its own tenant. Today development, staging and production share `dev-2jhzz8xmjggh26pm.us.auth0.com` (#610).

Tenant creation has no Management API endpoint. Create it in the Auth0 dashboard, then add a `tenants/<name>.env` file from `_template.env`.

---

## 2. Create the Management API application (once per tenant)

The scripts authenticate as a Machine to Machine application.

1. **Applications → Applications → Create Application.** Name it `rvs-config-automation`, choose **Machine to Machine**, and select **Auth0 Management API**.
2. Authorize these scopes (names occasionally change between releases; the Authorize screen shows a description next to each):

   | Needed for | Scopes |
   |---|---|
   | Plan and export | `read:resource_servers` `read:roles` `read:clients` `read:client_grants` `read:connections` `read:actions` `read:triggers` `read:tenant_settings` `read:prompts` `read:attack_protection` |
   | Apply | `create:resource_servers` `update:resource_servers` `create:roles` `update:roles` `create:clients` `update:clients` `create:client_grants` `update:client_grants` `update:connections` `create:actions` `update:actions` `update:triggers` |

   Grant only the plan scopes until you need to apply. No `delete:*`, user or `read:client_keys` scopes are needed.
3. Store its credentials in the vault named by `AUTH0_MGMT_KEYVAULT` in the tenant file. Enter the client secret at a hidden prompt so it doesn't land in shell history:

   ```bash
   KV=kv-rvs-staging-wus3   # the tenant file's AUTH0_MGMT_KEYVAULT
   az keyvault secret set --vault-name $KV --name Auth0Mgmt--Domain   --value <tenant>.us.auth0.com
   az keyvault secret set --vault-name $KV --name Auth0Mgmt--ClientId --value <CLIENT_ID>
   read -rs S && az keyvault secret set --vault-name $KV --name Auth0Mgmt--ClientSecret --value "$S" >/dev/null; unset S
   ```

---

## 3. Point the apps at the tenant

After `auth0-apply.sh <tenant> --apply`, the plan output lists the Manager application's client ID. Set it, with the authority `https://<tenant domain>/`, in the environment's `RVS.Blazor.Manager/wwwroot/appsettings.<Environment>.json`. Set the same values in that environment's `auth0*` Bicep parameters, which feed the API's `Auth0--*` Key Vault secrets. The plan reports any appsettings file that disagrees with the tenant.

---

## 4. Users and `app_metadata`

Users aren't scripted. Create each dealer user under **User Management → Users**, assign one role, and set `app_metadata`:

```json
{
  "tenantId": "org_blue_compass_rv",
  "orgName": "Blue Compass RV",
  "locationIds": ["loc_blue_compass_slc"],
  "regionTag": "west"
}
```

- **`tenantId`** — required; the Cosmos partition key and the isolation boundary. The Post-Login Action denies login without it.
- **`orgName`** — required; display name for the corporation. The Action denies login without it.
- **`locationIds`** — locations the user can access; omit or leave empty for corporate-wide roles (`dealer:corporate-admin`, `dealer:owner`).
- **`regionTag`** — optional; only for `dealer:regional-manager` users.

The user also needs at least one role, or the Action denies login.

**While environments share one tenant, test accounts must use test-only `tenantId` values.** A test account with a real dealer's `tenantId` can reach that dealer's production data.

The rest of onboarding (Tenant, Dealership, Location, slug lookup and TenantConfig documents) is in "Tenant provisioning" in `RVS_Identity.md`.

---

## Not used

**Auth0 Organizations.** Tenant scoping is permanently via `app_metadata`, which keeps RVS on the Free plan with no organization cap. The trade-offs are no per-tenant identity provider and no branded login.
