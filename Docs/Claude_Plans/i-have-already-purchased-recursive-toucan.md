# Rip out ACS; replace with Twilio (SMS) + Twilio SendGrid (email)

## Context

Microsoft announced in September 2026 that Azure Communication Services is retiring on **September 30, 2028**. ACS Email and ACS SMS are both on the retirement list, and Microsoft advises against onboarding new work in the meantime. RVS sends every packet email (Spec B), customer confirmation (A-2) and advisor invite (A-14) through ACS, and toll-free verification (#659) was being filed against ACS numbers.

Decision (Oct 2 2026): **remove ACS completely, now, before go-live**. There are no live customers (prod holds test tenants only), so there is no parallel run, no fallback chain, no data migration and no cut-over window. Email and SMS can be down between the Bicep deploy and the new provider setup. **The ACS toll-free numbers were released in the portal on Oct 2 2026** (staging `+18662319618`, prod `+18332398230`), so the ACS verification is abandoned. New numbers are bought at Twilio and verified there, reusing the evidence pack in `Docs/ASOT/Infra/TollFreeEvidence/`.

Starting point: the Domain interfaces (`INotificationService`, `ISmsNotificationService`, `PacketEmailMessage`, `INotificationOrchestrator`) use no Azure types. ACS is confined to:
- two implementations
- the Event Grid webhook
- a few names and stored fields
- Bicep/DNS
- docs

The callers (`PacketGenerationService`, `IntakeOrchestrationService`, `IntakeInviteService`, `LocationService`, `InboundSmsEventService`) don't change.

## Decisions

1. **One Twilio subaccount per environment** (staging, prod), each with one toll-free number and one **Messaging Service**. The number goes in the service's sender pool, and the service handles Advanced Opt-Out.
2. **Twilio answers HELP, not our code.** Advanced Opt-Out already replies to STOP/START/HELP on toll-free numbers, so our own reply would text the customer twice. Set the Messaging Service's HELP text to the exact string in `InboundSmsReplyContent` (it's the verification sample). Delete the HELP send path (`SendSystemSmsAsync`, HELP dedupe). The Sep 19 promise stands; only the mechanism changes.
3. **STOP/START/UNSTOP are still mirrored** into `CustomerProfile` across tenants (Sep 18/19 decisions). Prefer Twilio's `OptOutType` webhook parameter, which is what Twilio actually enforced, and fall back to `SmsKeywordVocabulary`.
4. **Provider-neutral names**: `AcsMessageId` → `ProviderMessageId` (JSON `providerMessageId`). No backfill.
5. **Keep the sending subdomains** `mail.rvintake.com` / `mail-staging.rvintake.com` and `DoNotReply@`. SendGrid domain authentication supplies the CNAMEs (return-path `emNNNN.mail…`, `s1/s2._domainkey.mail…`). `_dmarc.mail…` stays; main.bicep L1050–1055 explains why.
6. **Turn SendGrid click and open tracking off** for each message and at account level. Click tracking would rewrite packet SAS links and the status link.
7. **Registration**: real implementation when its secret is present, otherwise NoOp (the existing secondary-fallback style). No ACS branch.
8. **Out of scope**: the SendGrid Event Webhook for #439 bounce handling. ACS email has no delivery events today either. It's the natural follow-up, because `LocationService.DisableRecipientForBounceAsync` is waiting for a signal.

## Phase 0: Accounts and toll-free verification (Mark). Start now; this is the long pole.

- [ ] **Twilio**: create the account and the subaccounts `rvs-staging` and `rvs-prod`. In each:
  - buy a toll-free number
  - create a Messaging Service, put the number in its pool, and enable Advanced Opt-Out with the HELP text from `RVS.API/Integrations/InboundSmsReplyContent.cs`
  - create an API key (SID + secret)
  - note the subaccount Auth Token (webhook signatures need it)
- [ ] **Trust Hub**: Business Profile for **Arnold Digital Solutions**. Twilio's verification asks for a Business Registration Number (EIN); confirm the current field list in the console.
- [ ] **Before submitting**: ship the Privacy page change (Phase 3) so the reviewer sees Twilio named as the text and email provider.
- [ ] **Rewrite `Docs/ASOT/Infra/TollFreeEvidence/README.md` for Twilio:**
  - the console path (Phone Numbers → Regulatory Compliance → Toll-Free Verifications)
  - a field map from the existing sections to Twilio's form: use-case category, summary, samples, opt-in type Verbal + Web Form, opt-in image URLs (a list: `optin-evidence.png` plus each E-shot), volume bucket, additional info
  - the new numbers
  - "separate ACS resource" → "separate Twilio subaccount"
  - local re-run commands with the new config keys
- [ ] **Evidence carry-forward**:
  - E1–E3 don't name the provider and carry over unchanged.
  - Recapture E5 after Phase 1 if its Cosmos document shows `acsMessageId`.
  - The samples and the consent script stay word-for-word (pinned by `IntakeInviteContentTests` and `InboundSmsReplyContentTests`).
- [ ] Submit one verification per number. Retitle #659 for Twilio and note the submission dates there.
- [ ] **SendGrid**:
  - Create the account (Essentials; there is no free tier).
  - Authenticate `mail-staging.rvintake.com` and `mail.rvintake.com` with automated security on, and copy the CNAMEs into the bicepparams (Phase 2).
  - Turn off click and open tracking.
  - Create one restricted **Mail Send** API key per environment.

## Phase 1: Code (single change; TDD order per CLAUDE.md)

**Rename (Domain/data)**
- `IntakeInvite.AcsMessageId` → `ProviderMessageId` (`providerMessageId`).
- `IIntakeInviteRepository.GetByAcsMessageIdAcrossTenantsAsync` → `GetByProviderMessageIdAcrossTenantsAsync`, plus `CosmosIntakeInviteRepository` (L131–158).
- The index path in `modules/cosmos-db.bicep:458` and `RVS.Data.Cosmos.Seed/Program.cs:502`.
- `IInboundSmsEventService`: rename the parameter and make the wording neutral.
- `PacketEmailSizeFitter.AcsMaxRequestBytes` → `TransportMaxRequestBytes`. Keep the **9.5 MB budget**: SendGrid allows 30 MB, but recipient mailboxes (Outlook ≈20 MB after base64) are the real limit. Update `PacketEmailBudgetValidator`, the `PacketEmailOptions` docs and `monitor-alerts.bicep:99`.
- Neutral XML docs on both interfaces, the `IntakeInvite*Dto`s and `Locations.razor:577`.

**Email: SendGrid**
- Package `SendGrid`.
- Options:
  - `SendGridOptions` (`SendGrid:ApiKey`).
  - `Email:FromAddress` and `Email:SenderDisplayName` replace `AzureCommunicationServices:Email:*`. The display name is now actually applied.
- `RVS.API/Integrations/SendGridEmailNotificationService.cs : INotificationService` keeps the current three contracts:
  - `SendEmailAsync`: fire-and-forget, log on failure.
  - `SendTransactionalEmailAsync`: returns `X-Message-Id`, or null; never throws.
  - `SendPacketEmailAsync`: **throws on non-2xx**, which `PacketGenerationService:637`'s retry loop depends on. Attachments are base64.
  - Every message: click and open tracking off.
- Tests: `SendGridEmailNotificationServiceTests` mocks `ISendGridClient`; port the cases from `AcsEmailNotificationServiceTests`.

**SMS: Twilio**
- Package `Twilio`.
- `TwilioOptions` (`Twilio:` `AccountSid`, `ApiKeySid`, `ApiKeySecret`, `AuthToken`, `MessagingServiceSid`, `WebhookBaseUrl`).
- `SmsOptions.SectionName` `AzureCommunicationServices:Sms` → **`Sms`**. The validator is unchanged.
- `RVS.API/Integrations/TwilioSmsNotificationService.cs : ISmsNotificationService`:
  - Same gate order as today: Enabled → `PhoneNumberNormalizer` → `ISmsSenderNumberResolver` → `ITenantSmsRateLimiter`.
  - Sends with `MessageResource.CreateAsync` using `MessagingServiceSid` + `From`, with `StatusCallback = {WebhookBaseUrl}/api/events/twilio-sms/status`.
  - Returns the Message SID, or null.
  - Remove `SendSystemSmsAsync` from the interface.
  - Inject `ITwilioRestClient` so tests use a recording HTTP client (the same technique as today's ACS SMS tests).
- `SmsServiceCollectionExtensions`: mocks or `!Enabled` or no Twilio credentials → NoOp; otherwise Twilio.

**Inbound webhook**
- `RVS.API/Controllers/TwilioSmsWebhookController.cs`: `[AllowAnonymous]`, system-level (no tenant, like today's `EventsController`).
  - `POST api/events/twilio-sms/inbound` takes form fields `From`, `Body`, `MessageSid`, `OptOutType` → keyword path. Returns `200 text/xml <Response/>`.
  - `POST api/events/twilio-sms/status` takes `MessageSid`, `MessageStatus`: `delivered` → Delivered; `undelivered`/`failed` → Failed; ignore the rest.
  - Validate `X-Twilio-Signature` with `Twilio.Security.RequestValidator` against **`Twilio:WebhookBaseUrl` + path**, not the request URL: App Service TLS termination changes the scheme and host. Bad signature → 403; `AuthToken` unset → 503.
  - Dedupe on `MessageSid` with the existing `InMemoryInboundSmsDeduplicator`.
- `InboundSmsEventService`: delete the HELP send (L79–83, L134–154). `MapDeliveryStatus` switches to Twilio statuses.

**Delete**
- `AcsEmailNotificationService`, `AcsSmsNotificationService`, `EventsController`, `EventGridInboundOptions`, `CreateAcsCredential` (Program.cs L716–718) and the ACS registration (L720–736), plus their tests.
- Packages: `Azure.Communication.Email`, `Azure.Communication.Sms`, `Azure.Messaging.EventGrid` from `RVS.API.csproj`.
- In `THIRD-PARTY-NOTICES.md`, replace L116–117 with `SendGrid` and `Twilio`.
- Clear `appsettings*.json` of the `AzureCommunicationServices` keys, including the staging ACS endpoint in `appsettings.Development.json`. Add `Email:SenderDisplayName` and `Sms:*` defaults.

**Tests**
- New: `SendGridEmailNotificationServiceTests`, `TwilioSmsNotificationServiceTests`, `TwilioSmsWebhookControllerTests` (signature valid/invalid/unconfigured; STOP/START/HELP with and without `OptOutType`; status mapping; duplicate SID).
- Updated: `SmsServiceCollectionExtensionsTests`, `SmsOptionsValidatorTests`, `InboundSmsEventServiceTests` (HELP is a no-op now), `IntakeInvite*Tests`, `PacketEmail*Tests`.

## Phase 2: Infra (Bicep edited by Claude; Mark runs every az command)

**Delete from the repo**
- `modules/communication-services.bicep`, `modules/eventgrid-acs-sms.bicep`, `modules/acs-keyvault-secrets.bicep`.
- In `main.bicep`:
  - their wiring and outputs (L590–632, L829–858, L1295–1329)
  - the ACS DNS (domain TXT, SPF TXT at `mail`, the `azurecomm` DKIM CNAMEs, L622–698 apart from the `_dmarc` sub record)
  - the DMARC report authorization if it was ACS-only
  - the params `deployAcs`, `acsDataLocation`, `acsCustomEmailDomain`, `acsCustomDomainVerified`, `acsSmsFromPhoneNumber`, `acsSmsEnabled`, `eventGridWebhookKey`

**Bicepparams**
- **Remove the released numbers** `+18662319618` (staging) and `+18332398230` (prod), and every `acs*` / `eventGridWebhookKey` line (`parameters/{staging,prod}.bicepparam` L101–159 / L105–150).

**Add**
- Params: `mailSendingDomain`, `sendGridDnsRecords` (array of `{name, value}` CNAMEs), `smsEnabled = false`, `smsFromPhoneNumber` (the Twilio number), `twilioMessagingServiceSid`.
- `modules/app-service-config.bicep` (app + slot): `Email__FromAddress`, `Email__SenderDisplayName`, `Sms__Enabled`, `Sms__FromPhoneNumber`, `Twilio__MessagingServiceSid`, `Twilio__WebhookBaseUrl`.
- Merge the SendGrid CNAMEs and the `_dmarc.<sub>` record into the Intake zone. The apex keeps `-all` and reject.

**Key Vault secrets (by hand)**
- Add: `SendGrid--ApiKey`, `Twilio--AccountSid`, `Twilio--ApiKeySid`, `Twilio--ApiKeySecret`, `Twilio--AuthToken`.
- Delete: `AzureCommunicationServices--Endpoint`, `AzureCommunicationServices--ConnectionString`, `EventGrid--Inbound--Key`.

**Azure resources (Mark).** Bicep runs in incremental mode, so removing modules **does not delete** resources. Delete them explicitly, in this order, per environment:
1. Event Grid subscription and system topic `evgt-rvs-acs-${env}`
2. the ACS Contributor role assignments
3. the email service's sender username, then its domains, then the email service
4. the ACS resource

I'll write the exact `az` commands into `deployment-cmds.azcli` for you to run. The numbers are already released, so deleting the resource leaves no orphaned billing.

**Repo docs for infra**: strip the ACS sections from `Bicep.IaC/README.md` (L429, L1014 onward), `deployment-cmds.azcli` (L100, L373–530) and `review.json`; add SendGrid/Twilio equivalents.

## Phase 3: Docs and policy

- `RVS.Blazor.Intake/Pages/Privacy.razor:66–67`: name Twilio (including SendGrid) for text and email delivery. Bump `PoliciesLastUpdated` in `RVS.Blazor.Intake/SiteIdentity.cs:17`. **This ships before the Phase 0 submission.**
- `Docs/RVS_Plan.md`: an Oct 2 2026 decision-log entry covering the retirement, ACS removed outright before go-live, the numbers released, Twilio + SendGrid, Twilio answering HELP, and the bounce follow-up. Amend L14.
- `Docs/RVS_Spec.md`: L8, L74, L81, L123, L255.
- `Docs/RVS_Money.md`: the cost changes below.
- `Docs/RVS_GoLive_Activities.md`: G-4 now says `smsEnabled` once the Twilio number is verified; the stale "prod owns no number" text goes.
- ASOT:
  - `RVS_Infrastructure.md`: replace L85–187 with Twilio/SendGrid, including key rotation.
  - `RVS_Architecture.md`: L54, L93–122, L144–168.
  - `RVS_DataModel.md`: `providerMessageId`.
  - `RVS_PacketComposition.md`: #521 size-ceiling wording.
- `Docs/RVS_ManualTestPlan.md`: config keys and SMS tests. Archive `Docs/RVS_800Verification_Email.md`.
- `CLAUDE.md` and `.github/copilot-instructions.md`: the Domains table (`mail.*` = SendGrid), the integrations list, the webhook route.

## Cost impact (for `RVS_Money.md`; list prices as of mid-2026, check before committing)

| Line | ACS today | Twilio / SendGrid |
|---|---|---|
| Email | ~$0.002 per request, variable | **$0 marginal** inside SendGrid Essentials (~$20/mo fixed, 50K emails/mo; one account covers both environments) |
| Toll-free numbers × 2 | $4/mo | ~$4.30/mo ($2.15 each) |
| SMS per segment | ~$0.01 incl. carrier surcharge | ~$0.011 ($0.0083 + toll-free carrier fee); inbound STOP/HELP also billed at ~$0.0083 |
| Toll-free verification, platform fee | $0 | $0 |

- **Per location per month: slightly cheaper.** At 100 requests it falls from **$3.21 to ~$3.01**, because email moves out of variable cost. SMS stays at about +$0.01–0.02 per texted request.
- **Fixed: about +$20/mo**, from the SendGrid plan. Hard costs go from about $367 to about $387, which moves break-even by roughly a third of a location (still about six at $65 blended). The extra $20 is about $0.40 per location at 50 locations.
- **Volume headroom**: about 2 emails per request × 60 requests × 50 locations ≈ 6,000/mo, well under 50K.

## Verification

- **Build and tests**: `dotnet build RVS.slnx --configuration Release`, then the Domain and API suites (`dotnet test … --no-build -- --coverage …`), all green. `grep -rn "Azure.Communication\|EventGrid\|AcsMessageId\|acsSms" --include=*.cs --include=*.bicep* --include=*.json .` returns nothing outside `Docs/ARCHIVE`/`Obsolete`.
- **Bicep**: `az bicep build` on `main.bicep` (Mark: `what-if` per environment). It should show the new app settings and DNS only, and no ACS resources.
- **Email (staging)**: send a packet to Gmail and Outlook.com.
  - "Show original" shows SPF, DKIM and DMARC **pass**, aligned to `mail-staging.rvintake.com`.
  - From reads `RV Intake <DoNotReply@…>`.
  - Links are not rewritten to `sendgrid.net`, and the PDF opens.
  - A ~9 MB packet goes through.
  - The A-2 email confirmation and an emailed A-14 invite arrive (`providerMessageId` set).
- **SMS before verification**: use Twilio test credentials and magic number `+15005550006` locally. `curl` with a wrong `X-Twilio-Signature` returns 403.
- **SMS after verification (each environment)**, with `smsEnabled = true`:
  - An A-14 invite to your own phone reaches `Delivered`.
  - **HELP** gets exactly one reply, with the pinned text.
  - **STOP** sets `SmsOptOut` on every tenant's profile for that number, and the next send gets 409.
  - **START** clears it.
  - The A-2 text confirmation arrives.
- **Billing**: the next Azure invoice has no Communication Services lines.
