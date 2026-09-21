# RVS — Identity and Authorization

**Version:** 1.0 · September 4, 2026
**Scope:** Auth0 configuration and the API authorization model, as built.

Only the Manager app authenticates. **The intake app is anonymous and must stay that way** — no auth packages, no token handler, no `AuthorizeRouteView`. Customers are never Auth0 users; they reach their own data through an anonymous, rate-limited token endpoint.

---

## Tenant model

Auth0 tenant `dev-2jhzz8xmjggh26pm.us.auth0.com`, API audience `https://api.rvserviceflow.com`, custom claim namespace `https://rvserviceflow.com/`.

**Sign-in is served from the custom domain `login.rvintake.com`** (#627, #634), and that is the `iss` of every token: both apps' `Auth0:Authority` and both vaults' `Auth0--Domain` name it. The tenant above is still the tenant — it is where the Management API lives, so `AUTH0_DOMAIN` in `Infra/Auth0/tenants/shared.env` and the `Auth0Mgmt--*` / `Auth0Provisioner--*` secrets all stay on it, and `AUTH0_APP_AUTHORITY` carries the browser-facing value instead. The canonical host also keeps serving: Auth0 stamps `iss` from whichever host handled the request, which is what makes the two coexist and makes a rollback a configuration change rather than a migration.

> Both of those are **opaque identifiers**, not addresses to resolve. Since `#633` the API is also *served* at `api.rvserviceflow.com`, and since `#634` the claim namespace is the only other thing on that domain — but neither is coupled to the hostname. Changing the audience invalidates every issued token and every grant; changing the namespace breaks claim extraction in seven places. Moving a hostname is not a reason to touch either. The user-facing brand moved to `rvintake.com`; these did not, and should not.

**One Auth0 tenant serves development, staging and production** (#610). A second tenant needs a paid Auth0 plan, so the split is deferred until usage justifies the cost. The accepted risks: any Auth0 change reaches every environment at once, and a test account carrying a real dealer's `tenantId` can sign in to the production Manager app and see that dealer's data. Test accounts must use test-only `tenantId` values. The tenant also hosts unrelated products; RVS owns only what `Infra/Auth0/baseline/` declares.

Configuration changes go through `Infra/Auth0/auth0-apply.sh` (plan, review, then `--apply`), not the dashboard. See `Infra/Auth0/README.md`, including how to split into per-environment tenants later.

**RVS does not use Auth0 Organizations.** Tenant context lives in each user's `app_metadata` and is injected into the access token by a Post-Login Action (`Infra/Auth0/baseline/actions/add-metadata-to-accesstoken-claims.js`). The Action denies login when the user has no `tenantId`, no role, or no `orgName`. This runs on the Auth0 Free plan with no organization cap, and keeps tenant-scoping logic in `ClaimsService` rather than in Auth0.

`app_metadata` carries `tenantId`, `orgName`, and optionally `locationIds` and `regionTag`. `app_metadata.tenantId` is the value used everywhere downstream: the Cosmos partition key, the blob path prefix, and the isolation boundary. Its values are conventionally shaped like `ten_blue_compass_rv`, but they are ordinary strings — **not** Auth0 organization identifiers. The `ten_` prefix is deliberate: real Auth0 Organization ids start with `org_`, and a tenant id should never be mistaken for one.

Consequences worth knowing: there is no per-tenant identity provider, and no **per-customer** branded login — both would require Organizations. Tenant-*wide* branding is available and is not affected by that choice; it is step §7 of the portal checklist. Staff accounts are created by RVS through the provisioning tool (see "Tenant provisioning" below), not by dealers.

---

## Roles

Roles are global Auth0 roles. A user at Corporation A cannot see Corporation B because their `tenantId` differs, not because of any Auth0 membership boundary.

| Role | Scope | Under new scope |
|---|---|---|
| `platform:admin` | Cross-tenant | RVS internal staff. Grants `platform:tenants:manage`, which the provisioning tool requires together with the `Admin:AllowedUserIds` allowlist (#563) |
| `dealer:owner` | Tenant | Keep |
| `dealer:corporate-admin` | Tenant | Keep |
| `dealer:regional-manager` | Tenant + region | Thin — multi-location coordination is archived |
| `dealer:manager` | Location | Keep — the primary user of the manager app |
| `dealer:advisor` | Location | Keep |
| `dealer:readonly` | Location | Keep |
| `dealer:technician` | Location | **Archived** — there is no technician workflow |

At the reduced scope the role set is larger than the product needs. Collapsing it is cheap and safe to defer; the permission model below is what actually gates anything. The provisioning tool offers only `dealer:owner`, `dealer:manager`, `dealer:advisor` and `dealer:readonly`.

---

## Permissions

Authorization is **per-permission, never per-role**. Policies are declared in `RVS.API/Program.cs` and each requires a single `permissions` claim value.

| Policy | Claim | Notes |
|---|---|---|
| `CanReadServiceRequests` | `service-requests:read` | |
| `CanSearchServiceRequests` | `service-requests:search` | |
| `CanCreateServiceRequests` | `service-requests:create` | |
| `CanUpdateServiceRequests` | `service-requests:update` | |
| `CanUpdateServiceEvent` | `service-requests:update-service-event` | **Archived** — technician outcome capture |
| `CanDeleteServiceRequests` | `service-requests:delete` | |
| `CanUploadAttachments` | `attachments:upload` | |
| `CanReadAttachments` | `attachments:read` | |
| `CanDeleteAttachments` | `attachments:delete` | |
| `CanReadDealerships` | `dealerships:read` | |
| `CanUpdateDealerships` | `dealerships:update` | |
| `CanReadLocations` | `locations:read` | |
| `CanCreateLocations` | `locations:create` | |
| `CanUpdateLocations` | `locations:update` | |
| `CanReadAnalytics` | `analytics:read` | **Archived** — analytics is out of scope |
| `CanManageTenantConfig` | `tenants:config:read` / `create` / `update` | Any one of the three satisfies it |
| `CanReadLookups` | `lookups:read` | |
| `CanSendIntakeInvites` | `intake-invites:send` | Advisor intake invites (Spec A-14, #663). Held by every role that can create service requests — `dealer:advisor`, `dealer:manager`, `dealer:owner`, `dealer:corporate-admin`, `dealer:regional-manager`, `platform:admin` (#666). The manager app cannot read `permissions` (Auth0 RBAC writes them to the access token only, never the id token), so the Send intake link button is always shown and a 403 is explained in the dialog |
| `PlatformAdmin` | `platform:tenants:manage` | **Plus** the caller's `sub` on `Admin:AllowedUserIds` (`PlatformAdminAllowlistHandler`). Guards `api/admin/tenants` (#563) |

`PlatformAdmin` is the one policy with a second requirement. The permission alone is not enough because the Auth0 tenant is shared across environments and products: a stray `platform:admin` assignment must not open the provisioning tool.

Packet delivery (Spec B) will need new permissions — at minimum resend and per-location packet configuration. Neither exists yet.

---

## Claims and user context

`ClaimsService` (scoped) owns the claim-type constants and all extraction. `GetTenantIdOrThrow()` throws `UnauthorizedAccessException` — surfacing as 401 — when the tenant claim is absent. Every controller action opens with it, except `AdminTenantsController`, which takes the tenant from the route because its caller is RVS staff acting on another tenant.

Services never touch `HttpContext`. `IUserContextAccessor` lives in Domain and exposes `UserId` and `TenantId`; `HttpUserContextAccessor` in the API reads from `IHttpContextAccessor` and is registered scoped. Audit fields on every entity come from `_userContext.UserId`.

---

## Manager app authentication

Auth0 OIDC with PKCE. `AddOidcAuthentication`, `ResponseType = "code"`, scopes `openid profile email offline_access`, audience `https://api.rvserviceflow.com`. The bearer token is attached by `AuthorizationMessageHandler` on the named `RVS.API` client.

A custom `RefreshingAccessTokenProvider` decorates `IAccessTokenProvider` and exchanges refresh tokens directly against Auth0's `/oauth/token`, rather than relying on iframe silent renewal. The app-wide `FallbackPolicy` requires an authenticated user; only `/` and `/authentication/{action}` are anonymous.

**Persistent session (Spec C-7, issue #498).** The framework keeps the OIDC user record — including the rotating refresh token — in `sessionStorage` and offers no option to change the store, so a closed tab or installed PWA used to mean a fresh login. `wwwroot/js/session-persist.js` loads before `AuthenticationService.js`, restores that one record from a `localStorage` mirror on startup, and mirrors every later write or removal (sign-in, refresh, sign-out).

Persistence is **opt-in per device**. After sign-in, `KeepSignedInPrompt` asks once, "Keep me signed in on this device?", and warns against shared computers; the answer is stored in `localStorage` (`rvs.keepSignedIn`). Until the user says yes, nothing is mirrored and the session dies with the tab, as before; saying no removes any mirror left over from earlier. Refresh tokens are rotating, **30 days absolute and 7 days idle** (`Infra/Auth0/baseline/clients/rvs-blazor-manager.json`). On sign-out, `LoginDisplay` revokes the refresh token at Auth0's `/oauth/revoke` (`RefreshTokenRevocationClient`) and clears the mirror before the framework's own sign-out, so a token copied earlier stops working.

**Auth0's own session follows the same answer.** Auth0 keeps a separate session cookie on its domain, and the Blazor authentication library silently signs in against it on page load, so the app's opt-in alone would not stop Auth0 signing the next person into a shared computer. `KeepSignedInPolicy` (`RVS.UI.Shared`) therefore adds `prompt=login` to every authorize request unless the device opted in. An interactive sign-in then always asks for the password, and the silent sign-in fails fast: Auth0 rejects a request carrying both `prompt=none` and `prompt=login` with `invalid_request` (verified against the tenant). Token renewal still works through the refresh token. Sign-out goes through Auth0's `/oidc/logout` (advertised as `end_session_endpoint`), which ends the Auth0 session — but only if `{origin}/authentication/logout-callback` is an Allowed Logout URL, because Auth0 matches that list exactly and returns 400 otherwise.

The remaining trade-off is that an opted-in refresh token can be read by script on the manager origin. The Content-Security-Policy in `wwwroot/staticwebapp.config.json` limits which scripts can run there and where they can send data: no inline script, and scripts only from the app itself and the Application Insights CDN. It must allow **same-origin framing** (`frame-src 'self'`, `frame-ancestors 'self'`, `X-Frame-Options: SAMEORIGIN`): the authentication library's silent sign-in runs in a hidden iframe that starts at Auth0's `/authorize` and is redirected back to the app's own `/authentication/login-callback`, which must load in that frame to report its result. With `frame-src` limited to Auth0 and framing denied, the redirect is blocked (`net::ERR_BLOCKED_BY_CSP`) and every sign-in and sign-out stalls for the library's silent-request timeout. Only other origins are refused, so clickjacking protection is unchanged. Rotation with reuse detection further bounds a stolen token. Packet-email deep links (`/sr/{id}`, `/sr/{id}?action=…`) rely on this session — the status write happens only on an explicit confirm tap through the authenticated update endpoint, so there is no anonymous write surface and a scanner fetching the link changes nothing.

---

## Anonymous token surfaces

Two endpoints serve unauthenticated users, both rate-limited **per caller IP** (fixed window, partitioned on the `X-Forwarded-For` client address with the socket address as fallback — see `RVS.API/RateLimiting/ClientIpResolver.cs`): the intake endpoints (`IntakeEndpoint`, 20/min) and the customer status feed (`StatusEndpoint`, 10/min). Over-limit callers get `429`.

Spec X-5 requires that every anonymous token be ≥128 bits of entropy, **stored hashed**, TTL-bounded, rate-limited per IP, access-audited, and read-only. C-7 status writes go through the authenticated manager app, not an anonymous token (#498).

**The current implementation only partly meets this** — the per-IP rate limiting is in place (issue #442), but tokens are still stored unhashed on `GlobalCustomerAcct.magicLinkToken` with a 90-day expiry. The model is now decided (issue #427, closes Q7): SHA-256-hashed storage with the raw token never persisted; the **status token stays per-customer** (TTL ≤ 30 days, sliding renewal on use) and is read-only. **C-7 status changes are not on this token model** — issue #498 replaced the anonymous action-link design with authenticated deep links into the manager app (see "Manager app authentication" above), so there is no anonymous write surface. The prior ARCHIVE decision (`Docs/ARCHIVE/ASOT/RVS_MagicLink_Storage_Guidance.md`) that argued unhashed storage was adequate stays overturned by #427's decision on hashing. Implementation and migration are tracked in #440 / #441. See `RVS_Architecture.md` and Q7 in `../RVS_Plan.md`.

---

## Tenant provisioning

Tenants are provisioned with the platform-admin tool (Spec P-1 … P-8, issue #563): the hidden `/admin` pages of the manager app, backed by `api/admin/tenants` and `TenantProvisioningService`. One submission creates, in order, the `Tenant`, the `TenantConfig` (logins enabled), the `Dealership`, the first `Location` with its `slug-lookups` entry, and the first Auth0 user. Cosmos writes run first, with fixed ids (`ten_{name}`, `dlr_{name}`, `loc_{name}_1`); Auth0 runs last. Every step checks for what an earlier attempt left behind, so re-submitting after a partial failure finishes the job without duplicates, and the response reports each step as `created`, `already existed`, `failed` or `skipped`. Adding users and locations, re-issuing set-password links and toggling the access gate are separate actions on the same pages.

**Who can use it.** The `PlatformAdmin` policy needs the `platform:tenants:manage` permission **and** a caller `sub` on `Admin:AllowedUserIds` (Key Vault `Admin--AllowedUserIds--0`, `--1`, …); either alone is a 403. The admin account carries `app_metadata {tenantId: "ten_rvs_platform", orgName: "RVS"}` because the Post-Login Action requires both. `ten_rvs_platform` is reserved and cannot be provisioned. MFA is required on the account. The manager app does no permission checks of its own — the ID token carries no permissions — so a non-admin who opens `/admin` gets the API's 403 and sees Access Denied.

**How users are created.** `Auth0ManagementProvisioner` calls the Management API as a dedicated Machine-to-Machine application, "RVS API Provisioner", authorised for only `read:users create:users update:users update:users_app_metadata read:roles create:role_members create:user_tickets`. Its credentials are `Auth0Provisioner:Domain`, `ClientId` and `ClientSecret` (Key Vault `Auth0Provisioner--*`). They are deliberately **not** the `Auth0Mgmt--*` secrets: those belong to `rvs-config-automation`, the far broader application the `Infra/Auth0` scripts use, and they sit in the staging vault that the staging API loads in full. When the provisioner settings are absent, `UnconfiguredIdentityProvisioner` is registered and every call throws, so the identity step reports `failed` instead of silently succeeding.

A new user gets a random password nobody sees and a set-password ticket: 7 days, email marked verified, returning to the manager app. An email that already exists on `Username-Password-Authentication` is updated only when its `app_metadata.tenantId` matches. Any other user with that email — another tenant's, or one with no RVS tenant at all, which the shared Auth0 tenant makes possible — is a 409. Passwords and ticket URLs are never logged. Admin writes are audit-logged with the admin's user id and the tenant id (`EventId` 563001–563007), without email addresses.

**Shared-tenant risk.** Provisioning from staging creates real users in the Auth0 tenant production signs in against. Use `+staging` email aliases and test-only tenant ids. Splitting the tenant is `FS-9` in `../RVS_Plan.md`.

`TenantAccessGateMiddleware` reads `TenantConfig.accessGate` from Cosmos `tenant-configs` through `ITenantConfigService.GetAccessGateAsync` and returns 403 for a disabled tenant. It allowlists `/api/tenants/config`, `/api/intake/`, `/api/status/`, `/health` and `/swagger`. The gate and the commercial `Tenant.status` are independent; neither sets the other.

The one-time setup the tool needs — the provisioner application, the admin account, the Key Vault secrets — is §5 of `Auth0/Auth0-Portal-Configuration-Checklist.md`. That checklist's manual user steps (§4) remain as a fallback.
