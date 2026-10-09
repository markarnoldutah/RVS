# RVS — Ownership Boundaries

**Version:** 1.0 · October 9, 2026
**Status:** Action plan. Not yet started.

The goal: **RV Intake can be handed to a new owner as a single unit.** Every account, tenant, domain and contract it depends on should belong to RV Intake and nothing else, so that a sale is a handover, not a migration project.

This is cheapest to do now. Prod holds only test tenants, the G-5 wipe already plans a redeploy and recreates all users, and no customer has signed yet. Each item below gets harder once there are paying shops: real users in Auth0, live data in Azure, and signed contracts.

Source: a working session on October 8, 2026 about separating RV Intake from RigRoom, adapted here to what RV Intake actually runs.

---

## 1. The principle: tenant, not subscription

Azure has two separate hierarchies:

- **Billing:** billing account → subscription → resources. This decides **who pays**.
- **Identity:** every subscription trusts exactly one **Entra tenant**. This decides **who can sign in and what identities exist**.

Moving the billing to a buyer is painless; resources keep running. Moving a subscription to a buyer's **tenant** (a "directory change") is what hurts:

- Every RBAC role assignment is deleted.
- Managed identities must be re-enabled or recreated, and get new principal IDs.
- Key Vaults must be re-pointed to the new tenant and their access re-granted.
- App registrations, service principals and GitHub OIDC federated credentials stay behind.
- Some resource types can't be moved at all.

RV Intake runs on managed identity, Key Vault RBAC, Cosmos and Storage data-plane roles, and GitHub OIDC. A directory change at sale time would mean rebuilding all of that during due diligence.

**So the target is: RV Intake gets its own Entra tenant and its own billing account, with nothing shared with any other product.** A separate subscription inside a shared tenant does not achieve this.

The same reasoning applies outside Azure. Auth0, GitHub, SendGrid, Twilio, the domain registrar and Stripe each need to be an account that belongs to RV Intake alone.

---

## 2. Target state

```
Legal owner of RV Intake  (see §4.1 — ADS today, possibly its own LLC)
│
├── Entra tenant: RV Intake only
│   ├── Billing account (MCA, in the legal owner's name and card)
│   │   └── Subscription: today's RVS subscription, moved in
│   │       ├── rg-rvs-staging-westus3
│   │       └── rg-rvs-prod-westus3   (incl. DNS zones for both domains)
│   ├── Admin + break-glass accounts created IN this tenant
│   └── Deployer app registrations + federated credentials
│
├── GitHub organization: RV Intake only  →  repo RVS
├── Auth0 account + tenant: RV Intake only
├── SendGrid account, Twilio account: RV Intake only
├── Registrar account holding rvintake.com + rvserviceflow.com
├── Stripe account (when billing is built, build item 7)
│
└── Every login email is a role alias on rvserviceflow.com
    (azure@, github@, auth0@, …) forwarding to your inbox
```

Tenants and subscriptions cost nothing. The ongoing overhead is one more sign-in.

---

## 3. Where RV Intake stands today

| Area | Today | Problem for a handover | Fix (section) |
|---|---|---|---|
| **Customer contract** | Pilot Agreement names "RV ServiceFlow" as the party. That's a brand, not a legal entity. No assignment clause. | The X-3 data license may not survive a sale, and every contract may need re-signing. | §4.2 — **before Nova signs** |
| **Legal owner** | Arnold Digital Solutions (ADS) signs agreements, files toll-free verification (#680), and appears in the Intake footer. | A buyer can't buy ADS if ADS also owns other products. That forces an asset sale, so every account and contract has to be assigned. | §4.1 — decision |
| **Auth0** | One tenant shared with Benefetch, ManagersFriend, RVMTP and others (#610). | The tenant can't be handed over. Moving users after go-live means a password-hash export through Auth0 support, or forcing every user to reset. | §4.6 — **fold into G-5** |
| **Azure tenant** | To confirm (§5). Probably a tenant that also holds RigRoom, or a personal directory. | Directory change at sale time (§1). | §4.5 |
| **Azure billing** | To confirm (§5). | If it's a personal pay-as-you-go account or a Visual Studio credit subscription, it can't stay with the product. | §4.5 |
| **GitHub** | Personal account `markarnoldutah/RVS`. OIDC subjects are `repo:markarnoldutah/RVS:environment:*`. | You can't hand over a personal account. Moving the repo changes the OIDC subject, which breaks deploys. | §4.4 — do with §4.5 |
| **Ops and DMARC email** | `ops@` and `support@arnolddigitalsolutions.com` (`prod.bicepparam`). DMARC authorization records sit in the ADS zone at its registrar. | Alerts and reports keep going to an address the buyer can't reach. Part of the DNS lives outside the product. | §4.3 |
| **Customer contact** | `support@arnolddigitalsolutions.com` in `SiteIdentity.cs`, matching the Twilio toll-free filing. | Same, plus it's tied to the carrier filing. | §4.1 / §4.7 |
| **Domains** | `rvintake.com`, `rvserviceflow.com`; DNS zones are in Azure (good). Registrar account: to confirm. | Registrant and registrar login must belong to the product's owner. | §4.7 |
| **SendGrid, Twilio** | One account each. Owner login and any sharing with other products: to confirm. | Login emails need to be product aliases, and the accounts must not be shared. | §4.7 |
| **Auth0 identity mail** | Uses its own SendGrid key (Auth0 checklist §8). | Resolves itself if the SendGrid account is RV Intake's own. | §4.6 |

Already in good shape: Bicep is the only source of truth for Azure, both DNS zones are in Azure, nothing in the repo references a RigRoom resource, and the Auth0 configuration is scripted (`Docs/ASOT/Infra/Auth0/`).

---

## 4. Action plan, in order

Order matters. The entity decision sets the names on everything else, and the alias mailboxes have to exist before you create any account with them.

### 4.1 Decide the legal owner *(attorney + CPA)*

There are two options:

- **A. RV Intake gets its own LLC.** It can be owned by you directly or held by ADS. All accounts, contracts and filings go in its name, and a sale is a sale of the LLC: nothing in any system changes. This is cleanest at sale time, but it costs formation fees, a separate tax return, and a bank account and card in the LLC's name. It also interacts with the planned Florida move: forming the new LLC in Florida from the start may be cheaper than moving it later.
- **B. ADS keeps owning RV Intake.** A sale is an asset sale: every account is handed over and every contract assigned. The rest of this document is what makes that workable. It costs nothing now and more legal work at sale time.

**Recommendation:** decide before the first customer signs, and before the Twilio toll-free verification is approved if it hasn't been. Both carry the entity's name and are tedious to change afterwards. Whichever you choose, write the entity's name and address on the billing account, registrar and Stripe from this point on.

If you choose A, everything below that says "the legal owner" means the new LLC. You would also update `SiteIdentity.cs` (Spec A-15) and the toll-free filing together, since carriers check they match.

### 4.2 Fix the Pilot Agreement *(before the first signature)*

`Docs/Legal/RVS_Pilot_Agreement.md`. Have the attorney confirm the exact wording.

1. **Name the legal entity as the party:** "[Legal owner], doing business as RV Intake", not "RV ServiceFlow". RV ServiceFlow isn't a legal person, and the brand is now RV Intake.
2. **Add an assignment clause:** we may assign this Agreement without consent to an affiliate or to a successor in a merger, acquisition or sale of all or substantially all of the business or assets.
3. **Make the X-3 data license run to successors:** "…to RV Intake and its successors and assigns, perpetual and irrevocable…". The Overview calls this license *the one thing that must not be dropped*. Without this wording it may not carry over to a buyer.

The same three points apply to the ToS (`/terms`) wherever it grants rights.

### 4.3 Set up role-alias mail on `rvserviceflow.com`

Every account login and operational address becomes a role alias on the product's own domain, forwarding to your inbox. In a sale the domain goes with the product: the buyer changes one forwarding rule and every login email stays the same. Aliases on `arnolddigitalsolutions.com` would stay with you, and each account would have to be re-verified one at a time.

**Use `rvserviceflow.com`, not `rvintake.com`.** It's the corporate domain and nobody types it. The `rvintake.com` apex deliberately accepts no mail (null MX, `#608`).

- **DNS change (Bicep):** replace the `rvserviceflow.com` null MX with your forwarding provider's MX records, through `mxRecords` in `modules/dns.bicep`. Never add records by hand: an undeclared MX in this zone once sat unnoticed until 2026-09-17. Leave SPF `-all` and DMARC `p=reject` as they are. They govern mail *sent as* the domain, and these aliases only receive.
- **Provider:** a forwarder that only needs MX records, since the zone is in Azure DNS and not Cloudflare. Don't host it in your ADS Workspace or M365: that ties the domain to ADS's mail system.
- **Aliases:** `azure@` · `github@` · `auth0@` · `sendgrid@` · `twilio@` · `registrar@` · `stripe@` · `billing@` · `ops@` · `dmarc@` · `support@`. Use real aliases, not `you+azure@`; several services reject plus-addressing.
- **Repo changes once the aliases work:**
  - `prod.bicepparam`: `ops@arnolddigitalsolutions.com` → `ops@rvserviceflow.com`.
  - `dmarcReportingAddress` → `dmarc@rvserviceflow.com`. The RFC 7489 authorization records (`<domain>._report._dmarc.rvserviceflow.com`) then live in an Azure zone, so Bicep can write them, and the manual step at the ADS registrar goes away. Update `main.bicep`'s `dmarcReportAuthorizationAction` output and the Bicep README "Intake apex mail posture" to match.
  - `support@` in `SiteIdentity.cs`: change only together with the toll-free filing (§4.1, §4.7).

### 4.4 Create the GitHub organization and transfer the repo

Create an organization owned by `github@rvserviceflow.com`, with your personal account as an owner, and transfer `RVS` into it. GitHub redirects the old URL, and issues, PRs and Actions history come along. After the transfer, check that the `staging` and `production` environments still have their variables and secrets, including both SWA deployment tokens.

**This breaks deploys until the federated credentials are updated**, because the OIDC subject includes the owner. Do it in the same sitting as §4.5, which recreates those credentials anyway. Then update the repo's references to `markarnoldutah/RVS`:

- `.github/workflows/README.md`
- `Docs/ASOT/Infra/Bicep.IaC/PROD_DEPLOYER_SP_SETUP.md`
- `Docs/ASOT/Infra/Bicep.IaC/deployment-cmds.azcli` (`GITHUB_REPO`)
- `.github/agents/github-issue-manager.agent.md`

The repo can stay public.

### 4.5 Give RV Intake its own Entra tenant and billing account

1. **Create the tenant** while signed in to Azure. Make its first admin an Entra account created *in* that tenant, such as `admin@<tenant>.onmicrosoft.com`, with `azure@rvserviceflow.com` as its contact. Don't use a personal Microsoft account. Add a second break-glass Global Admin, keep its credentials offline, and turn on MFA for both.
2. **Create a billing account** (Microsoft Customer Agreement) in the legal owner's name and card, and **transfer billing ownership** of the RVS subscription to it. Resources keep running.
3. **Move the subscription into the new tenant (directory change).** First:
   - Run the ACS teardown (`deployment-cmds.azcli` §4e) so there are fewer resource types to move.
   - Check every resource type left against Microsoft's current list of what a directory change breaks: "Transfer an Azure subscription to a different Microsoft Entra directory" on Learn.
4. **Redeploy `main.bicep` for staging and prod.** Bicep recreates most of what the move deleted:
   - Role assignments, including Cosmos and Storage data-plane roles, for the new managed-identity principals.
   - The Key Vault tenant, which is `subscription().tenantId`.
   - System-assigned identities.
5. **Recreate by hand what isn't in Bicep:**
   - The two deployer app registrations and their federated credentials, with the new org subjects from §4.4 (`PROD_DEPLOYER_SP_SETUP.md`).
   - `AZURE_TENANT_ID` and `AZURE_CLIENT_ID` in both GitHub environments.
   - The dev Entra group behind `devBlobAccessPrincipalId`.
   - The object IDs in `dnsZoneContributorPrincipalIds`.
   - Your own `az login --tenant <new>`.
6. **Smoke-test both environments:** a staging deploy from `main`, one intake, the packet email, and a Manager sign-in.

**Why move the subscription instead of building a new one:** a fresh subscription would need new names, because Key Vault purge protection holds a deleted vault's name, and storage, Cosmos and app names are globally unique. It would also need new Azure OpenAI quota. Today gpt-5 and Whisper have zero headroom, and new quota isn't guaranteed. On top of that it would need DNS re-delegation at the registrar and re-validated custom domains. The directory change keeps all of that, and with Bicep and no live customers, the cleanup it causes is one redeploy.

**When:** together with G-5. G-5 already redeploys `main.bicep` and recreates everything, so the extra cost of the move is small.

### 4.6 Give RV Intake its own Auth0 tenant *(fold into G-5)*

The shared tenant (#610) is the hardest thing to separate after go-live, because that's where users and their passwords live. Before go-live, nothing needs migrating: G-5 step 8 already deletes every test user, and `/admin` provisioning recreates Nova's users.

1. **Sign up for a new Auth0 account** with `auth0@rvserviceflow.com` and create one tenant for RV Intake. #610 deferred a second tenant because it needed a paid plan *on the existing account*. Check Auth0's current terms: a separate account may get its own free tenant.
2. **Recreate the configuration from the baseline**, so nothing is set up by hand:
   - Create a new `rvs-config-automation` M2M app with the same scopes.
   - Run `auth0-apply.sh` against the new tenant.
   - Keep the API identifier `https://api.rvserviceflow.com` and the claim namespace exactly as they are. They are opaque identifiers, valid in any tenant.
3. **Update the secrets and settings:**
   - The `Auth0--*` secrets in both Key Vaults.
   - The `Auth0Mgmt--*` secrets.
   - The Manager's Auth0 domain and client ID.
   - Auth0's identity-mail SendGrid key, now from RV Intake's own SendGrid account.
   - Universal Login branding, from `RvsBrand.cs`.
4. **Do it before G-5 step 8**, so Nova's users are created in the new tenant. Leave RV Intake's apps in the old tenant until staging works against the new one, then delete them there.

### 4.7 Hand the remaining accounts to RV Intake

For each account, the login email becomes the alias, the legal and billing name is the legal owner, and it shares nothing with another product.

| Account | Action |
|---|---|
| **Registrar** (`rvintake.com`, `rvserviceflow.com`) | Put both in a registrar account whose login is `registrar@`. Registrant is the legal owner. Turn on auto-renew. |
| **SendGrid** | Login `sendgrid@`. If any other product sends through it, give that product its own account. |
| **Twilio** | Login `twilio@`. Toll-free verification is filed per number in a business's name. If §4.1 creates a new LLC, file under it, ideally before approval. Moving a number between Twilio accounts goes through Twilio support. |
| **Stripe** (build item 7) | Open it under the legal owner from day one. Stripe accounts are hard to move between entities. |
| **Anything new** | Same three rules (login alias, legal owner's name, nothing shared) from the day it's created. |

Two notes:

- **Personal access:** you keep admin access to everything through your own user, invited as an admin or member. The alias is the account's identity, not your sign-in, so at sale time you remove yourself and nothing breaks.
- **Paperwork:** keep a private inventory of every account, its login alias, its billing owner and its recovery method. Don't keep it in this public repo. That inventory is the handover document.

---

## 5. Confirm the current state first

Run these and keep the output. It decides which parts of §4.5 apply.

```bash
# Which tenant the RVS subscription trusts, and who you are in it
az account show --query "{sub:name, tenant:tenantId, user:user.name}" -o table
az ad signed-in-user show --query "{upn:userPrincipalName, type:userType}" -o table

# Every subscription you can see, and its tenant. Does RigRoom share RVS's tenant?
az account list --query "[].{name:name, tenant:tenantId}" -o table

# Every tenant you belong to
az rest --method get --url "https://management.azure.com/tenants?api-version=2022-12-01" \
  --query "value[].{id:tenantId, name:displayName, domain:defaultDomain}" -o table

# Offer type: PayAsYouGo vs MSDN or Visual Studio credit
az rest --method get \
  --url "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)?api-version=2022-12-01" \
  --query "subscriptionPolicies.quotaId" -o tsv

# Billing accounts and their agreement types
az billing account list --query "[].{name:displayName, type:agreementType}" -o table

# Resource types to check against the directory-change list
az resource list --query "[].type" -o tsv | sort | uniq -c
```

How to read the results:

- **UPN contains `#EXT#`:** you sign in with a personal Microsoft account. That's fine for you, but it can't be the product's only admin.
- **quotaId starts with `MSDN` or contains `VisualStudio`:** a personal benefit subscription. It's tied to your subscriber license and can't go to a buyer. Move to a pay-as-you-go or MCA subscription under the billing account.
- **The tenant also holds RigRoom subscriptions or other products' identities:** §4.5 is required.
- **The tenant holds only RV Intake already:** skip the directory change. Do step 1's admin accounts and step 2's billing move only.

---

## 6. Rules from here on

1. **Share nothing between products.** No shared Auth0 tenant, Log Analytics workspace, Key Vault, storage account, DNS zone, SendGrid account or ACR. One shared resource ties two products together.
2. **Bicep stays the only source of truth.** If a tenant transfer ever goes badly, a buyer can still redeploy into their own subscription and copy the data: Cosmos container export, `azcopy` for blobs and tables. That fallback is what keeps this from blocking a sale.
3. **No personal identities as sole owners.** Every account has a role-alias login and the legal owner on the bill. Your personal user is a member you can remove.
4. **New accounts follow §4.7 the day they're created.** Adding a service is when a dependency on the legal owner usually slips in.

---

## 7. At sale time (for reference)

If the work above is done, the handover is:

1. **Option A** (own LLC, sold as a unit): nothing changes in any system. Update the forwarding target and add the buyer's people.
2. **Option B** (asset sale):
   - Add the buyer as Global Admin of the Entra tenant and as owner of the billing account.
   - Add the buyer as owner of the GitHub org, as admin of the Auth0, SendGrid and Twilio accounts, and on the registrar account.
   - Point the `rvserviceflow.com` forwarding at the buyer's inbox.
   - Assign the customer contracts under §4.2's clause.
   - Remove yourself everywhere.
3. **Rotate every secret after you leave:** Key Vault values, the SendGrid and Twilio keys, Auth0 M2M secrets, and the SWA deployment tokens.
