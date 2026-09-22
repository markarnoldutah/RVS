# Auth0 Portal Configuration Checklist

**Updated:** September 17, 2026
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
   | Plan: tenant-wide checks | `read:custom_domains` `read:email_provider` — optional; without them the plan skips those checks with a warning |
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

After `auth0-apply.sh <tenant> --apply`, the plan output lists the Manager application's client ID. Set it, with the authority `https://<tenant domain>/`, in the environment's `RVS.Blazor.Manager/wwwroot/appsettings.<Environment>.json`. Set the same values in that environment's `auth0*` Bicep parameters, which feed the API's `Auth0--*` Key Vault secrets.
The plan reports any appsettings file that disagrees with the tenant.

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

   `read:users` `create:users` `update:users` `delete:users` `update:users_app_metadata` `read:roles` `read:role_members` `create:role_members` `delete:role_members` `create:user_tickets`

   `delete:users`, `read:role_members` and `delete:role_members` arrived with the Users page (#647). An application authorised before then needs them added under **APIs → Auth0 Management API → Machine To Machine Applications**; until it has them, listing, editing and deleting users fail with a 403 from Auth0 and adding a user that already exists fails at the role step.

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

`rvintake.com`, not `rvserviceflow.com` (#634): every hostname a human types, clicks or reads is on the intake brand, and the corporate domain keeps the API origin and the JWT claim namespace. Sign-in is the most visible surface there is.
It also means login, the Manager app and the ACS sending domain all agree, so §8 has no brand mismatch to paper over — an earlier draft proposed a second ACS sending domain for exactly that, and moving login here deleted the problem instead of funding it.

**Status: nothing in §6–§8 has been applied.** As of 2026-09-17 both apps authenticate against `dev-2jhzz8xmjggh26pm.us.auth0.com`, no Auth0 custom domain exists, and `auth0CustomDomainCnameTarget` in `main.bicep` is empty. §6.1–§6.4 and §7.1 can be done in daylight and change nothing for users; §6.5, §7.2 and §8 are the ones with consequences.

**Read this before you start.** The tenant is shared two ways, and the two consequences are not equally serious.

- Development, staging and production all use it (#610). One custom domain serves all three; the Free plan includes exactly one, so there is no per-environment login domain. `login.rvintake.com` is the only hostname in the whole infrastructure with no `-staging` sibling — every `*DnsPrefix` parameter in `main.bicep` is environment-suffixed, and `auth0LoginDnsPrefix` deliberately is not.
- The tenant also hosts unrelated products' applications ([`../Infra/Auth0/README.md`](../Infra/Auth0/README.md)). **Adding a custom domain does not move them.** The canonical `dev-2jhzz8xmjggh26pm.us.auth0.com` host keeps serving after the custom domain is live, and each application follows the authority its own configuration names — so only the apps cut over in §6.5 move.
  Auth0 stamps `iss` from whichever host handled the token request, which is why both keep working side by side.

  What *is* unavoidably tenant-wide is **§7 branding** and **§8's email provider**: those two change what the other products' users see and who their mail comes from. Confirm that's acceptable before running §7 and §8. §6 on its own is additive and reversible.

  The one thing that does not straddle the two hosts is a session. A user signed in at the canonical domain is not signed in at the custom domain. That, not the domain, is why everyone is logged out at cutover.

### 6.1 Verify billing

The Free plan includes one custom domain but requires a card on file to activate it. **Settings → Billing** and add one. Auth0's wording: the card is "for verification purposes and fraud prevention" and "will not be charged". Nothing else in §6–§8 costs money — the only paid item anywhere here is removing §7's watermark.

### 6.2 Create the domain in Auth0

**Branding → Custom Domains → Add Domain.**

- Domain: `login.rvintake.com`. A subdomain, not the apex. Auth0's own guidance prefers a root domain — but only to make passkeys work across sibling apps, which RVS does not use — and the `rvintake.com` apex is the Intake SWA anyway, so the login host has to be a label under it. Ignore that recommendation here.
- Certificate: **Auth0-managed**. Self-managed means you own renewal, for no benefit on a single host.

Auth0 then shows one verification record — type, host and value. Copy it **verbatim**; the value contains a per-tenant token.

### 6.3 Publish the DNS record through Bicep, not the portal

`rvintake.com` is IaC in this repo. A record created by hand in the Azure portal is reverted by the next infrastructure deploy.

The record is **already declared** — [`main.bicep`](../Infra/Bicep.IaC/main.bicep) carries `auth0CnameRecords`, appended to the `dnsIntake` module's `cnameRecords`, and it evaluates to an empty list until you supply the target. So this step is one string:

```bicep
param auth0CustomDomainCnameTarget string = 'dev-2jhzz8xmjggh26pm-cd-<hash>.edge.tenants.us.auth0.com'
```

Set it as the **parameter default in `main.bicep`**, not in a `.bicepparam`. It is tenant-wide and environment-independent: both environments' deploys upsert the same record with the same value, which is the one case in this template where that is correct rather than a bug. Putting it in one `.bicepparam` would leave the other environment's deploy silently deleting nothing and writing nothing.

If Auth0 asks for a **TXT** record rather than a CNAME, build the same shape (`{ name: auth0LoginDnsPrefix, values: [ '...' ] }`) and append it to that module's `txtRecords` concat instead — see [`modules/dns.bicep`](../Infra/Bicep.IaC/modules/dns.bicep).

Then deploy either environment's parameter file (the record is identical from both) and wait for propagation:

```bash
# Preview first. Expect exactly one Create — the login CNAME — and zero Deletes.
# Every "Modify" on a DNS record is a what-if artefact: it cannot resolve
# reference() at preview time, so runtime-derived records render as
# "current literal => unresolved expression" and resolve back to themselves.
az deployment sub what-if \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam

az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam

dig +short CNAME login.rvintake.com     # must return the Auth0 target before you click Verify
```

Don't pass `opsAlertEmailReceivers` on the command line. Older runbook snippets do; since #639 both parameter files carry it, and the Action Groups resource provider does a full-replace PUT — so a partial CLI override is a way to silently drop receivers, not a way to set them.

This mirrors how the ACS email domain is verified (#532): Azure mints the token, so the value cannot be pre-written in source and lands by hand once.

### 6.4 Verify and enable

Back in **Branding → Custom Domains**, click **Verify**. Status goes to **Ready** once the record resolves and Auth0 finishes issuing the certificate — allow a few minutes for the certificate.

Then turn on **Enable custom domains for email and phone notifications** — on the same screen in current dashboards, under **Settings → Custom Domains** in older ones. Without it, the link inside a password-reset email still points at the canonical tenant domain, which defeats most of the point at the one moment the user is most likely to read the address bar.
Easy to miss, and nothing fails visibly when it is off.

**Also set it as the tenant's default custom domain** — the dashboard offers this as **Switch to custom** once the domain is Ready. Only a verified domain can be made default, which is why this belongs here and not earlier.

The default domain governs Auth0-generated notification links (password reset, email verification, welcome, SMS) *and* **Management API requests that trigger a notification without the optional `auth0-custom-domain` header**. With no default set, Auth0 falls back to the canonical tenant domain for all of them.

That second clause is the one that decides it. [`Auth0ManagementProvisioner.cs`](../../../RVS.API/Integrations/Auth0ManagementProvisioner.cs) calls `POST api/v2/tickets/password-change` and sends no such header — the string appears nowhere in the codebase. That ticket is the **set-password link a brand-new dealer clicks during onboarding** (#563, Spec P-1…P-8).
Left on the canonical domain, the first URL a customer ever receives from RVS points at `dev-2jhzz8xmjggh26pm.us.auth0.com` — the precise failure §6 exists to prevent, at the moment there is least trust to spend. The alternative is passing the header on every such call, which is a code change; the default setting is free and covers every flow at once.

**Shared-tenant consequence, stated plainly:** the other products in this tenant get `login.rvintake.com` in their notification links too. Narrower than it sounds — interactive login stays per-application, as the top of §6 explains — but it is a real RVS-brand leak, and each of those products can override per request with the `auth0-custom-domain` header if it ever matters.

Decide it together with §8 rather than on its own. Once Auth0's email provider points at the RVS ACS resource, those same products' identity mail arrives *from* `DoNotReply@mail.rvintake.com`; a From address is a larger leak than a link. If the §8 footprint is acceptable, this is strictly smaller.
If it is not, the answer is to split the tenant (#610), not to leave reset links on a raw `.auth0.com` host.

**Verify both empirically, not by inspection.** The toggle and the default interact, and neither shows an error when wrong. Trigger a real password reset and read the link; then, separately, provision a user through `/admin` and read *that* link.
They travel different code paths — the reset is Auth0's own notification, the ticket is the Management API path above — so one working does not prove the other.

Leaving §6 here is a valid stopping point. The custom domain is live and unused; no user sees any difference until §6.5.

### 6.5 Cut the applications over

Nothing so far has changed how anyone signs in — the canonical domain is still serving, and §6.1–§6.4 only added a second host. This step is the actual cutover. Every signed-in user is logged out, across all three environments at once, because a session does not carry between the two hosts. Do it at a quiet hour.

Four places pin the issuer:

| Where | What to change |
| --- | --- |
| Key Vault `Auth0--Domain`, per environment | the new domain; restart the API (it reads Key Vault at startup) |
| [`RVS.Blazor.Manager/wwwroot/appsettings.{Development,Staging,Production}.json`](../../../RVS.Blazor.Manager/wwwroot/) | `Auth0:Authority` |
| [`RVS.Blazor.Manager/wwwroot/staticwebapp.config.json`](../../../RVS.Blazor.Manager/wwwroot/staticwebapp.config.json) | the CSP `connect-src` **and** `frame-src` entries — miss either and login fails silently in the browser |
| [`../RVS_Identity.md`](../RVS_Identity.md) | the stated tenant domain |

**Order.** Key Vault and the API restart first, then the Manager deploy. There is a window either way — an old-issuer token hitting a new-authority API is a 401, and so is the reverse — and this order makes that window "nobody can call the API for a minute", which is indistinguishable from the logout everyone is getting anyway.

**0. Sign out of the Manager app first**, in every browser and installed PWA where you answered "yes" to the keep-signed-in prompt.
[`js/session-persist.js`](../../../RVS.Blazor.Manager/wwwroot/js/session-persist.js) mirrors the OIDC user record into `localStorage` under `rvs.persist.oidc.user:<authority>:<clientId>` — keyed by authority.
After the cutover the library reads a different key, so the old mirror is never restored and never cleaned up: it strands a live, rotating refresh token for the canonical issuer in `localStorage` on that device, indefinitely.
Signing out clears the mirror on the way through. Nothing breaks if you skip this — login works fine — which is exactly why it is easy to leave behind.

The service worker needs no such care: [`service-worker.js`](../../../RVS.Blazor.Manager/wwwroot/service-worker.js) is deliberately network-only with no cache, so there is no stale `appsettings` to evict after the deploy.

**1. Key Vault, both vaults.** `Auth0--Domain` is the JWT authority ([`RVS.API/Program.cs`](../../../RVS.API/Program.cs) — `options.Authority`).
Do **not** touch `Auth0Mgmt--Domain` or `Auth0Provisioner--Domain`: both are Management API clients and the Management API stays on the canonical host.
`Auth0Mgmt--*` exists only in the **staging** vault — that is correct, not a gap: `shared.env` points `AUTH0_MGMT_KEYVAULT` there for the shared tenant, so a `secret show` against the prod vault returning `SecretNotFound` is the expected answer.

```bash
for KV in kv-rvs-staging-wus3 kv-rvs-prod-wus3; do
  az keyvault secret set --vault-name $KV --name Auth0--Domain \
    --value https://login.rvintake.com >/dev/null
  az keyvault secret set --vault-name $KV --name Auth0--AuthorizationUrl \
    --value https://login.rvintake.com/authorize >/dev/null
  az keyvault secret set --vault-name $KV --name Auth0--TokenUrl \
    --value https://login.rvintake.com/oauth/token >/dev/null
done

az webapp restart -n app-rvs-api-staging-wus3 -g rg-rvs-staging-westus3
az webapp restart -n app-rvs-api-prod-wus3    -g rg-rvs-prod-westus3
```

**Set all three, and note that the prod pair is currently malformed.** `main.bicep` builds them as `'${auth0Domain}oauth/token'`, which assumes `auth0Domain` ends in a slash. Prod was deployed without one, so the prod vault holds:

```text
Auth0--AuthorizationUrl   https://dev-2jhzz8xmjggh26pm.us.auth0.comauthorize
Auth0--TokenUrl           https://dev-2jhzz8xmjggh26pm.us.auth0.comoauth/token
```

Staging's are correct.
The commands above overwrite both with well-formed values, which is the cheapest moment to fix it — but if you ever redeploy with the `auth0Domain` parameter, pass it **with** a trailing slash or the malformation comes back.

`Auth0--AuthorizationUrl` and `Auth0--TokenUrl` are written by [`modules/auth0-keyvault-secrets.bicep`](../Infra/Bicep.IaC/modules/auth0-keyvault-secrets.bicep) and **nothing currently reads them** — Swagger derives both from `Auth0:Domain` at request time, via `.TrimEnd('/')`, which is why the prod malformation has never surfaced.
Set them anyway so a later `auth0Domain` redeploy doesn't reintroduce the canonical host, and so the next person reading the vault isn't misled.

The alternative to the loop above is a redeploy with `--parameters auth0Domain=https://login.rvintake.com/ …`, which rewrites all three. That needs every other `auth0*` parameter supplied in the same command (the module is skipped entirely when `auth0Domain` is empty), so the `az keyvault secret set` route is usually less work.

**2. Manager appsettings, all three files.** All three currently read `https://dev-2jhzz8xmjggh26pm.us.auth0.com/`:

```bash
sed -i '' 's|https://dev-2jhzz8xmjggh26pm.us.auth0.com/|https://login.rvintake.com/|' \
  RVS.Blazor.Manager/wwwroot/appsettings.{Development,Staging,Production}.json
```

Development included: it points at the same shared tenant, so leaving it behind means local sign-in uses a different issuer than the API it is calling.

**3. The Manager CSP.** Both directives, in [`staticwebapp.config.json`](../../../RVS.Blazor.Manager/wwwroot/staticwebapp.config.json):

```text
connect-src … https://login.rvintake.com …
frame-src  'self' https://login.rvintake.com
```

Leave the canonical host in both for one deploy. It costs nothing, and it is what lets you roll back by editing appsettings alone rather than shipping another CSP change under pressure.

**4. `AUTH0_APP_AUTHORITY`.** Uncomment it in [`../Infra/Auth0/tenants/shared.env`](../Infra/Auth0/tenants/shared.env) in the same commit:

```text
AUTH0_APP_AUTHORITY=https://login.rvintake.com/
```

`auth0-apply.sh` cross-checks each Manager appsettings file against this value, falling back to `https://$AUTH0_DOMAIN/`. Once the apps move while `AUTH0_DOMAIN` stays canonical — as the next paragraph requires — the fallback reports a false mismatch on all three files, on every run, forever. `AUTH0_APP_AUTHORITY` is what the apps use; `AUTH0_DOMAIN` is what the Management API uses.
They are the same value until a custom domain exists, and different afterwards.

**Leave `AUTH0_DOMAIN` on the canonical `.us.auth0.com` host.** The configuration scripts talk to the Management API, which does not move, and `lib.sh` fails the run if the tenant file and the vault's `Auth0Mgmt--Domain` disagree.

**5. Verify, in this order.** Each step fails differently, so don't collapse them:

```bash
curl -s https://login.rvintake.com/.well-known/openid-configuration | python3 -m json.tool | head -5
# "issuer": "https://login.rvintake.com/" — if this 404s, §6.4 is not actually Ready
```

Then in a browser, on `https://manager-staging.rvintake.com`: sign in (the address bar must read `login.rvintake.com` on the Auth0 screen), load a service request (proves the API accepts the new issuer), and trigger a password reset to confirm the link in the mail points at `login.rvintake.com` rather than the canonical host — that last one is what §6.4's notification toggle buys you.
Repeat on `https://manager.rvintake.com`.

A blank Auth0 screen with nothing in the network tab is the CSP. A successful login followed by 401s on every API call is a vault secret that didn't take or an API that wasn't restarted.

**To roll back:** point `Auth0--Domain` and the appsettings authorities at the canonical domain again and restart the API. The custom domain can stay defined and Ready in Auth0 while unused — that is the whole reason §6.1–§6.4 are safe to run ahead of this step.

---

## 7. Universal Login branding (once per tenant)

**Genuinely tenant-wide, unlike §6.** The custom domain is additive — the other products in this tenant keep using the canonical host until they choose to move. Branding is not: it changes the login, password-reset and MFA screens for every application in the tenant, including theirs, immediately.
There is no per-application override, and the per-*customer* branded login that would give you one needs Organizations, which RVS permanently does not use (see "Not used"). Confirm that's acceptable before you touch it.

### 7.1 The logo does not exist yet — decide this first

§7.2 asks for a public HTTPS logo URL. **Issue #702 landed the logo kit, so there is now a real asset** — the previous placeholder problem (both apps shipped byte-identical 32×32 PNGs named `icon-192.png` and `icon-512.png`, which the login page would have rendered as a smudge scaled up) is gone.

Use `https://manager.rvintake.com/icon-512.png`. It is the "RV Intake" badge from the kit — a genuine 512×512, cream `RV` on an Ink `#2F4C6B` rounded square — and Auth0 renders it at roughly 150×150, so there is headroom on retina. The Manager SWA is public and CDN-backed and serves it today; no `staticwebapp.config.json` change is needed.

Verify the exact URL with `curl` before pasting it into Auth0. A misspelled path does **not** 404: `navigationFallback` rewrites it to `index.html` and it answers **200 `text/html`**. Auth0 fetches server-side, stores what it gets, and shows a blank logo with no error — so a typo looks identical to a broken Auth0.

```bash
curl -sI https://manager.rvintake.com/icon-512.png | head -3   # expect 200 and image/png, not text/html
```

If a wordmark is ever wanted instead of the badge, `RVS.UI.Shared/wwwroot/brand/wordmark-stacked.svg` is the square lockup — but it carries live `<text>`, so it needs Space Grotesk converted to outlines before anything outside the app renders it correctly. The badge has no text and no such caveat, which is why it is the recommendation here.

Do not point the URL at `manager.rvserviceflow.com`. It was retired on 2026-09-17 and does not resolve; because Auth0 fetches it server-side the failure surfaces as a silently missing logo, not an error you would notice.

### 7.2 Set the branding

1. **Confirm the New Universal Login experience.** **Branding → Universal Login**. The no-code customization below applies to the new experience; Classic is templated differently.
2. **Branding → Universal Login → Customization**, and set:

   | Field | Value | Source |
   | --- | --- | --- |
   | Logo | the URL decided in §7.1 | — |
   | Primary color | `#C1502E` | Rust — `RvsBrand.Accent`, the action colour the Manager app's buttons use |
   | Page background | `#FAF8F3` | `RvsBrand.PaperNeutral` — Manager's `PaletteLight.Background`, a barely-tinted paper deliberately not competing with the button |

   **These changed with issue #702**, when the brand moved from Material Indigo to Denim & Rust. The old values were `#3F51B5` and `#FAFAFA`; a tenant still carrying them hands off to an app in a completely different palette, which is the specific failure this section exists to avoid. Read the current values from [`RVS.UI.Shared/Theme/RvsBrand.cs`](../../../RVS.UI.Shared/Theme/RvsBrand.cs) — the one authoritative copy — rather than from this table if the two ever disagree.

   Note the split: the login **button** takes Rust because it is an action, while the Ink `#2F4C6B` that dominates the app bar is structure and does not belong in either field here.

3. **Leave the watermark alone for now.** Free-plan tenants show a "Powered by Auth0" badge below the widget and it cannot be removed on Free — removing it means the paid Essentials tier (~$35/mo). It reads as "they didn't roll their own auth", not as a phishing signal. Revisit once there are paying customers.

These settings apply automatically to the login, password-reset and MFA screens; there is nothing separate to configure for those. Check the password-reset screen anyway — it is the one a dealer sees under stress, and it is the one whose *link* depends on §6.4's notification toggle.

Manager also carries a dark palette, and both apps carry a separate high-contrast accessibility theme (`PaletteDark` and `RvsHighContrastPalette` in `RVS.UI.Shared/Theme/`). The no-code customization editor takes **one** palette, so don't try to mirror either — the values above are the whole surface.

The login page also cannot reach the app's self-hosted Space Grotesk, so it renders in Auth0's default face. That is expected and not worth fixing on the Free plan; the colour and the logo are what carry continuity across the hand-off.

Don't try to script any of this. The configuration scripts deliberately don't manage tenant-wide settings — an edit here is not something `auth0-apply.sh` will report or revert.

---

## 8. Email provider — custom mail server (once per tenant)

This is the one that matters more than the watermark. Auth0's built-in sender sends from **`no-reply@auth0user.net`**, is capped at 10 messages/minute, and Auth0 documents it as not for production. A password-reset email arriving at a dealership from an unrecognised third-party domain is exactly the shape corporate mail filters flag.

Unlike the watermark, this is **not** gated by the Auth0 plan — it's gated by plugging in your own provider. Tenant-wide, so §7's caveat applies: the other products in this tenant start sending their password-reset mail from an RVS address too.

### 8.1 Use Auth0's native Azure Communication Services integration

**An earlier draft of this section proposed generic SMTP against the ACS relay and flagged that it could not be confirmed. Skip that.** Auth0 ships a first-class **Azure Communication Services** email provider that authenticates with an ACS connection string over HTTPS — no SMTP, no Entra app registration, no role assignment, no SMTP-username resource.
Auth0 now recommends it over SMTP for Azure, partly because Exchange Online's SMTP basic auth retires in April 2026.

RVS already has everything it needs: a verified custom sending domain and a `DoNotReply` sender on both ACS resources (#532, #643).

**Decide which ACS resource, once.** The email provider is tenant-wide and takes one connection string, and dev/staging/prod share this tenant — so one ACS resource sends identity mail for all three. **Use production.** Staging's domain is throttled and internal, and a real dealer resetting a real password must not receive mail from a `-staging` host.

| | Value |
| --- | --- |
| ACS resource | `acs-rvs-notify-prod-wus3-s01-001` (`rg-rvs-prod-westus3`) |
| Sending domain | `mail.rvintake.com` — linked and verified |
| From address | `DoNotReply@mail.rvintake.com` — the `donotreply` sender username exists on that domain |

Consequence to accept deliberately: a developer resetting a password locally gets mail from the production sending domain. That is the right trade — the alternative sends production dealers staging mail — but it means identity mail does not appear in staging's ACS metrics at all. Look for it in prod.

**Steps.**

1. Get the connection string. It is already in the prod vault, so there is no need to open the ACS Keys blade:

   ```bash
   az keyvault secret show --vault-name kv-rvs-prod-wus3 \
     --name AzureCommunicationServices--ConnectionString --query value -o tsv
   ```

   Shaped `endpoint=https://…;accesskey=…`.

2. **Branding → Email Provider.** Enable **Use my own email provider**, select **Azure Communication Services**, paste the connection string, and set **From** to `DoNotReply@mail.rvintake.com`.

   Auth0's documentation is explicit that ACS "only supports a raw email address" in that field: `DoNotReply@mail.rvintake.com`, not `RV Intake <DoNotReply@mail.rvintake.com>`. Don't assume a display name is harmlessly ignored — the friendly name belongs in the template instead (§8.3).

3. **Save**, then **Send Test Email**. If nothing arrives within a few minutes, read the Auth0 tenant logs before touching Azure — a rejected connection string shows up there, not in ACS.

**Two things worth knowing before you paste it.**

- **That connection string is a production ACS access key, and you are copying it into the Auth0 tenant.** It is a shared secret held by a third party from then on, alongside whatever other products use this tenant. Nothing else in RVS does that — every other Azure credential stays in Key Vault behind Managed Identity.
  It is the accepted cost of the integration, but it belongs in the risk register, not in a footnote.
- **Rotating the ACS key silently breaks identity mail.** A rotation updates Key Vault and the API picks it up on restart; Auth0 holds its own copy and does not. The failure is password-reset emails quietly not arriving — the worst failure mode to discover by accident. Whoever owns key rotation needs to know Auth0 is now a second consumer of that key.

One prerequisite in Auth0's docs can be ignored: "allow inbound connections from Auth0 IP addresses" is aimed at self-hosted SMTP. ACS is a public HTTPS endpoint with no inbound network ACL in this deployment, so there is nothing to allowlist — don't go looking for the setting.

### 8.2 Fallbacks, if 8.1 does not take

**Generic SMTP against the ACS relay.** The relay is real — host `smtp.azurecomm.net`, port 587, STARTTLS.
Getting to it needs three things §8.1 does not: an Entra app registration; the **Communication and Email Service Owner** role on the ACS resource (or a custom role with `Microsoft.Communication/CommunicationServices` read+write plus `Microsoft.Communication/EmailServices` write); and an `smtpUsernames` child resource linking the app to a username.
The login string is then `<smtp username>.<Entra app id>.<Entra tenant id>` with the app's client secret as the password — roughly 110 characters, which has tripped username-length limits in other clients. All of that work buys you nothing over §8.1. It is here only so nobody re-derives it.

**A dedicated vendor.** SendGrid, Amazon SES, Mailgun and Postmark are first-class integrations in **Branding → Email Provider**. Well-trodden, at the cost of another vendor and another sending domain to warm up, monitor and keep out of spam folders — and RVS already did that work for `mail.rvintake.com`.

### 8.3 After a provider is configured

- **Branding → Email Templates** unlocks. Custom templates and a custom From are only editable once a provider exists. At minimum, set the templates' From and put the brand name in the body, since §8.1's raw-address constraint means the envelope alone shows no friendly name.
- Re-check §6.4's notification toggle. Without it the link inside the reset email points at the canonical tenant domain, which undoes most of §6 at the one moment the user is most likely to look at the address bar.
- Send one real password reset to an external mailbox (not a company one) and read the headers: SPF and DKIM should pass on `mail.rvintake.com`, the same records #532 and #643 set up for the packet email. Identity mail and packet mail share a sending reputation from here on — a deliverability problem in one is a problem in both.

---

## Not used

**Auth0 Organizations.** Tenant scoping is permanently via `app_metadata`, which keeps RVS on the Free plan with no organization cap. The trade-offs are no per-tenant identity provider and no *per-customer* branded login — tenant-wide branding is still available and is covered in §7.
