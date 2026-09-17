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
  "tenantId": "ten_blue_compass_rv",
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
   { "tenantId": "ten_rvs_platform", "orgName": "RVS" }
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

## 6. Custom domain (once per tenant)

Moves the login URL from `dev-2jhzz8xmjggh26pm.us.auth0.com` to `login.rvintake.com`. A raw `.auth0.com` address in the browser bar is the biggest "this looks sketchy" tell for a service advisor signing in.

`rvintake.com`, not `rvserviceflow.com`: every hostname a human types, clicks or reads is on the intake brand, and the corporate domain keeps the API origin and the JWT claim namespace. Sign-in is the most visible surface there is. This also settles §8's sending-domain question before it is asked — see the note there.

**Read this before you start.** A custom domain is a *tenant-wide* setting, and this tenant is shared two ways:

- Development, staging and production all use it (#610). One custom domain serves all three; the Free plan includes exactly one, so there is no per-environment login domain.
- The tenant also hosts unrelated products' applications ([`../Infra/Auth0/README.md`](../Infra/Auth0/README.md)). Those applications' login pages move to `login.rvintake.com` too. Decide that's acceptable before enabling it.

### 6.1 Verify billing

The Free plan includes one custom domain but requires a card on file to activate it. **Settings → Billing** and add one. There is no charge.

### 6.2 Create the domain in Auth0

**Branding → Custom Domains → Add Domain.**

- Domain: `login.rvintake.com`. Use a subdomain — Auth0 does not support an apex custom domain. That constraint decides it here regardless: the `rvintake.com` apex is the Intake SWA, so the login host has to be a label under it.
- Certificate: **Auth0-managed**. Self-managed means you own renewal.

Auth0 then shows one verification record — type, host and value. Copy it **verbatim**; the value contains a per-tenant token.

### 6.3 Publish the DNS record through Bicep, not the portal

`rvintake.com` is IaC in this repo. A record created by hand in the Azure portal is reverted by the next infrastructure deploy.

The record goes in the **`dnsIntake`** module in [`../Infra/Bicep.IaC/main.bicep`](../Infra/Bicep.IaC/main.bicep) — the zone is `rvintake.com`, not the corporate zone. Its `cnameRecords` is a `concat(...)` of per-concern lists rather than a literal array, so add the login record as its own variable next to `redirectCnameRecords` and append it:

```bicep
// Auth0 custom domain (§6 of the Auth0 portal checklist). Tenant-wide and
// environment-independent — the value is a token Auth0 mints once, so it is
// written here by hand rather than derived. Both environments' deploys upsert
// the same record with the same value, which is why this is not env-guarded.
var auth0CnameRecords = [
  {
    name: 'login'
    target: '<value Auth0 showed>'   // e.g. <tenant>-cd-<hash>.edge.tenants.us.auth0.com
  }
]
```

then add `auth0CnameRecords` to the `concat(...)` in `dnsIntake`'s `cnameRecords`.

If Auth0 gave you a TXT record instead, build the same shape (`{ name: 'login', values: [ '...' ] }`) and append it to that module's `txtRecords` concat — see [`modules/dns.bicep`](../Infra/Bicep.IaC/modules/dns.bicep).

This mirrors how the ACS email domain is verified (#532): Azure mints the token, so the value cannot be pre-written in source and lands in the param/record by hand once.

Deploy the DNS resource group, then wait for propagation.

### 6.4 Verify and enable

Back in **Branding → Custom Domains**, click **Verify**. Status goes to **Ready** once the record resolves and Auth0 finishes issuing the certificate — allow a few minutes for the certificate.

Then turn on **Enable custom domains for email and phone notifications** on the same screen. Without it, the link inside a password-reset email still points at the raw tenant domain, which defeats most of the point. Easy to miss.

### 6.5 Cut the applications over

The issuer in every token changes from `https://dev-2jhzz8xmjggh26pm.us.auth0.com/` to `https://login.rvintake.com/`. Four places pin it:

| Where | What to change |
| --- | --- |
| Key Vault `Auth0--Domain`, per environment | the new domain; restart the API (it reads Key Vault at startup) |
| [`RVS.Blazor.Manager/wwwroot/appsettings.{Development,Staging,Production}.json`](../../../RVS.Blazor.Manager/wwwroot/) | `Auth0:Authority` |
| [`RVS.Blazor.Manager/wwwroot/staticwebapp.config.json`](../../../RVS.Blazor.Manager/wwwroot/staticwebapp.config.json) | the CSP `connect-src` **and** `frame-src` entries — miss either and login fails silently in the browser |
| [`../RVS_Identity.md`](../RVS_Identity.md) | the stated tenant domain |

**Leave `AUTH0_DOMAIN` in [`../Infra/Auth0/tenants/shared.env`](../Infra/Auth0/tenants/shared.env) on the canonical `.us.auth0.com` domain.** The configuration scripts talk to the Management API, which stays on the canonical domain, and `lib.sh` fails the run if the tenant file and the vault's `Auth0Mgmt--Domain` disagree.

**Set `AUTH0_APP_AUTHORITY` in the same file first, or every later plan run is noise.** `auth0-apply.sh` cross-checks each Manager appsettings file against `https://$AUTH0_DOMAIN/`. Once the apps point at `https://login.rvintake.com/` while `AUTH0_DOMAIN` stays canonical — as the paragraph above requires — that check reports a false mismatch on all three files, on every run, forever. `AUTH0_APP_AUTHORITY` is what the apps use; `AUTH0_DOMAIN` is what the Management API uses. They are the same value until a custom domain exists, and different afterwards.

Because the tenant is shared, this cutover hits all three environments at once, and every signed-in user is logged out when the issuer changes. Do it at a quiet hour.

To roll back: point `Auth0--Domain` and the appsettings authorities at the canonical domain again. The custom domain can stay defined in Auth0 while unused.

---

## 7. Universal Login branding (once per tenant)

**Tenant-wide, on a shared tenant** — the same caveat as §6. The other products in this tenant get the RVS logo and colors on their login pages. Confirm that's acceptable first.

1. **Confirm the New Universal Login experience.** **Branding → Universal Login**. The no-code customization below applies to the new experience; Classic is templated differently.
2. **Branding → Universal Login → Customization**, and set:
   - **Logo** — a public HTTPS URL, reachable anonymously (Auth0's servers fetch it, not the browser alone). Roughly square, ~150×150. Host it as a static asset on the Manager SWA (`https://manager.rvintake.com/...`) so it is already public and CDN-backed.
   - **Primary color** — `#1565C0`, the MudBlazor theme primary used across both Blazor apps.
   - **Page background** — keep neutral; avoid a second brand color competing with the button.
3. **Leave the watermark alone for now.** Free-plan tenants show a "Powered by Auth0" badge below the widget and it cannot be removed on Free — removing it means the paid Essentials tier (~$35/mo). It reads as "they didn't roll their own auth", not as a phishing signal. Revisit once there are paying customers.

These settings apply automatically to the login, password-reset and MFA screens; there is nothing separate to configure for those.

Don't try to script this. The configuration scripts deliberately don't manage tenant-wide settings.

---

## 8. Email provider — custom mail server (once per tenant)

This is the one that matters more than the watermark. Auth0's built-in sender sends from **`no-reply@auth0user.net`**, is capped at 10 messages/minute, and Auth0 documents it as not for production. A password-reset email arriving at a dealership from an unrecognised third-party domain is exactly the shape corporate mail filters flag.

Unlike the watermark, this is **not** gated by the Auth0 plan — it's gated by plugging in your own provider. Tenant-wide, so the shared-tenant caveat from §6 applies again.

### Option A — reuse the existing ACS (try this first)

RVS already sends the packet email through Azure Communication Services with a verified custom sending domain — `mail.rvintake.com` in production, `mail.staging.rvintake.com` in staging, sending as `DoNotReply@mail.<domain>`. ACS offers an SMTP relay, and Auth0's provider list includes a generic SMTP option, so the two should meet without standing up SendGrid or SES.

1. Create SMTP credentials for the ACS resource (an Entra application authorized against the Communication Service, exposed as an SMTP username/password).
2. **Branding → Email Provider → SMTP** in Auth0. Host `smtp.azurecomm.net`, port 587, STARTTLS, the credentials from step 1.
3. Set the From address and send the built-in test message.

Two things to decide before you commit to it:

- **Brand is already aligned — nothing to decide here.** The sending domain is `mail.rvintake.com`, the Manager app is `manager.rvintake.com`, and §6 puts login on `login.rvintake.com`. A dealer resetting a password sees one brand end to end. An earlier draft of this section proposed adding `mail.rvserviceflow.com` as a second ACS domain to match a `rvserviceflow.com` login host; that host no longer exists, so the second domain isn't needed. Worth knowing why it would have been expensive: `main.bicep` requires the ACS custom domain to be a subdomain of `intakeZoneName`, so it would have been a template change, not a parameter — plus a full #532 record set and the out-of-band verification dance.
- **Verify ACS SMTP relay is available** for this resource and region before building on it. I could not confirm it live from this machine. If it isn't workable, fall back to Option B rather than writing a custom Action.

### Option B — a dedicated email vendor

SendGrid, Amazon SES, Mailgun and Postmark are first-class integrations in **Branding → Email Provider**. Well-trodden and quick, at the cost of another vendor and another sending domain to warm up and monitor.

### After a provider is configured

- **Branding → Email Templates** unlocks. Custom templates and a custom From address are only editable once a provider exists. Set the From to your own domain and put the RVS logo in the body.
- Re-check that §6.4's notification toggle is on, so the link inside the reset email points at `login.rvintake.com`.

---

## Not used

**Auth0 Organizations.** Tenant scoping is permanently via `app_metadata`, which keeps RVS on the Free plan with no organization cap. The trade-offs are no per-tenant identity provider and no *per-customer* branded login — tenant-wide branding is still available and is covered in §7.
