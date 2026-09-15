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

This application is for the configuration scripts only. The provisioning tool uses its own application with user scopes — see §5.

---

## 3. Point the apps at the tenant

After `auth0-apply.sh <tenant> --apply`, the plan output lists the Manager application's client ID. Set it, with the authority `https://<tenant domain>/`, in the environment's `RVS.Blazor.Manager/wwwroot/appsettings.<Environment>.json`. Set the same values in that environment's `auth0*` Bicep parameters, which feed the API's `Auth0--*` Key Vault secrets. The plan reports any appsettings file that disagrees with the tenant.

---

## 4. Users and `app_metadata`

**Use the provisioning tool** — `/admin` in the Manager app (Spec P-1 … P-8, #563). It creates the user, sets `app_metadata`, assigns the role and returns a set-password link, and it writes the tenant's Cosmos documents in the same submission. The manual steps below are a fallback for when the tool is unavailable.

Create each dealer user under **User Management → Users**, assign one role, and set `app_metadata`:

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

A manually created user still needs the tenant's Cosmos documents (Tenant, TenantConfig, Dealership, Location, slug lookup). The tool writes them; see "Tenant provisioning" in `RVS_Identity.md`.

---

## 5. Set up the provisioning tool (application and admin once per tenant; secrets once per environment)

The `/admin` pages create users through the Management API, so they need their own application and an allowlisted admin account.

1. **Provisioner application.** **Applications → Applications → Create Application.** Name it `RVS API Provisioner`, choose **Machine to Machine**, and select **Auth0 Management API**. Authorize exactly these scopes and nothing else:

   `read:users` `create:users` `update:users` `update:users_app_metadata` `read:roles` `create:role_members` `create:user_tickets`

   This is a different application from `rvs-config-automation` (§2). Never swap their credentials: the configuration application can rewrite the tenant, and the provisioner can create users.

2. **Admin account.** On the admin's user page: assign the `platform:admin` role, set `app_metadata` to the following (the Post-Login Action denies login without both fields), and enrol MFA. Copy the user id (`auth0|…`).

   ```json
   { "tenantId": "org_rvs_platform", "orgName": "RVS" }
   ```

3. **Key Vault secrets**, in each environment's API vault. Pass them as the optional `auth0Provisioner*` and `adminAllowedUserId` Bicep parameters, or set them directly:

   ```bash
   KV=kv-rvs-staging-wus3   # the API's vault for this environment
   az keyvault secret set --vault-name $KV --name Auth0Provisioner--Domain   --value <tenant>.us.auth0.com
   az keyvault secret set --vault-name $KV --name Auth0Provisioner--ClientId --value <PROVISIONER_CLIENT_ID>
   read -rs S && az keyvault secret set --vault-name $KV --name Auth0Provisioner--ClientSecret --value "$S" >/dev/null; unset S
   az keyvault secret set --vault-name $KV --name Admin--AllowedUserIds--0 --value 'auth0|<admin user id>'
   ```

   The names are `Auth0Provisioner--*`, **not** `Auth0Mgmt--*`. The staging vault already holds `Auth0Mgmt--*` for §2's application, and the API loads every secret in its vault. Restart the API after setting them; it reads Key Vault at startup.

Without the provisioner secrets the tool still opens, but every create reports the identity step as `failed` with "Auth0 provisioning is not configured". Without `Admin--AllowedUserIds--0` every admin endpoint returns 403.

**While environments share one tenant**, users created from staging are real users in the tenant production signs in against. Use `+staging` email aliases and test-only tenant ids.

---

## Not used

**Auth0 Organizations.** Tenant scoping is permanently via `app_metadata`, which keeps RVS on the Free plan with no organization cap. The trade-offs are no per-tenant identity provider and no branded login.
