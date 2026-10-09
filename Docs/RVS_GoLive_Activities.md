# RVS Go-Live Activities

Actions we have agreed should happen immediately before, or during, production go-live. Each one is a deliberate pre-launch setting that has to be undone, or a piece of work parked until there is real traffic to justify it.

This is not a launch-readiness checklist. Gates, pilot sequencing and open questions live in [`RVS_Plan.md`](RVS_Plan.md); cost figures live in [`RVS_Money.md`](RVS_Money.md). An item belongs here only when it is a concrete action tied to the go-live moment.

When an item is done, delete it and note the date in the commit message. Do not tick it off in place.

---

## Infrastructure (prod)

All values are in [`parameters/prod.bicepparam`](ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam). Staging keeps its current settings permanently; none of these apply to it.

| # | Action | Setting | Why it is parked until go-live |
|---|---|---|---|
| G-1 | Upgrade both Static Web Apps to Standard | `swaSkuName = 'Standard'` | Free has no SLA. Nothing else in use depends on Standard: the Manager signs in with Auth0 from WASM, not SWA auth, and Free's two custom domains per app cover the one each app binds. |
| G-2 | Raise or remove the Log Analytics daily cap | `logAnalyticsDailyCapGb`, currently `'0.08'` | At 0.08 GB/day the cap keeps ingestion inside the free grant, but when it is hit, telemetry stops and the packet-pipeline alerts (#494) stop with it until the daily reset. With paying shops that is not acceptable. Measure a few days of real ingestion first (`Usage \| where IsBillable \| summarize sum(Quantity)/1000 by bin(TimeGenerated, 1d)`), then set the cap to several times the observed peak, or `'-1'` for no cap. Keep the daily-cap-reached alert either way. |
| G-3 | Turn the `/health` availability test back on | `deployAvailabilityTest = true` in `prod.bicepparam`; `availabilityTestFrequencySeconds` / `availabilityTestLocations` to size it | Standard availability tests are billed per run. Turning the test on also creates the two alerts that read it (#602): one pages the ops action group when the test fails, and one fires when pings pass but App Insights records nothing, which means the telemetry has gone dark and the #494 alerts are blind. Both alerts are already written, so this is a one-flag change. The committed default is one location every 15 minutes (~2.9K runs/month). Scale up as shops come to depend on it — [Bicep README](ASOT/Infra/Bicep.IaC/README.md#turning-the-availability-test-on). |
| G-6 | Re-verify the three `#494` packet-pipeline alerts fire against live data | No Bicep flag — an operational check, once telemetry is confirmed flowing (G-2, G-3) | The three rules were validated structurally at deploy time (`#494`), never against a real signal — and `#602` showed staging's App Insights can stop ingesting silently for hours while the app keeps serving. Before go-live, trigger or wait for a real occurrence of each EventId (`438001`, `434001`, `521001`; the two bounce rules were removed by `#833`) and confirm the ops action group receives it with `ServiceRequestId` and `TenantId` populated — the verification KQL is in [Bicep README](ASOT/Infra/Bicep.IaC/README.md#monitoring--alerts). See `#732`. |

App Service stays on B1 through go-live; it was considered for F1 and rejected, because F1 cannot hold the `api.*` and `go.*` custom hostnames or their certificates.

---

## Messaging

Unlike the infrastructure items above, these apply to **staging as well as prod**: each environment's outbound SMS stays dark until its own toll-free number is verified. Verification is per number and is filed with Twilio (#659; the ACS numbers were released and their verification abandoned on Oct 2 2026).

| # | Action | Setting | Why it is parked until go-live |
|---|---|---|---|
| G-4 | Turn outbound SMS on for an environment once its toll-free number is verified | `smsEnabled = true` in that environment's `.bicepparam`, alongside its Twilio `smsFromPhoneNumber` and `twilioMessagingServiceSid`, with the `Twilio--*` secrets in Key Vault | Unverified toll-free traffic is blocked, so sending before verification just fails, and the API refuses to start with SMS enabled and no number. While it is off, a `Text` customer's confirmation falls back to email and the A-14 send action refuses with a clear message (`Spec A-2`, `A-14`). Neither environment has its Twilio number yet. Before flipping, confirm the Messaging Service has Advanced Opt-Out on with the HELP text from `InboundSmsReplyContent`, and its webhooks point at that environment's `api.` host. Flip each environment on its own, not both together. |

---

## Data (prod)

| # | Action | Setting | Why it is parked until go-live |
|---|---|---|---|
| G-5 | Wipe all prod data, then provision Nova RV Services fresh through `/admin` | No Bicep flag — a one-time data operation, run as the [G-5 wipe sequence](#g-5-wipe-sequence) below | Everything in prod today is test data: tenants created by hand for hands-on testing, plus whatever demo seed the one-off seeder change wrote (the committed seeder's `--environment Production` writes reference data only — `lookup-sets` and `rv-warranty-rules` — never tenant data). Demo customer accounts carry magic-link tokens hard-coded in the public seed source, and `GET /api/status/{token}` accepts them, so anyone reading the repo can open those status pages on prod. Demo locations carry `.example.com` packet recipients, which hard-bounce. Test intakes have also put names, emails, phone numbers, VINs and photos into Blob storage and the logs. Nova is deleted with everything else and re-created, not carried over, so no test request, attachment or Auth0 password survives into the pilot. At go-live, prod holds only the new Nova plus global reference data, and no token from the repo works against prod. See #609. |

### G-5 wipe sequence

Run it in this order, all of it in one sitting. Resource names are the prod defaults from `main.bicep`; the `az` commands assume `-g rg-rvs-prod-westus3`.

1. **Record the prod tenant IDs before deleting anything.** Run `SELECT c.tenantId, c.name FROM c` against `tenant-configs` in Data Explorer. The Auth0 tenant is shared by dev, staging and prod, and `app_metadata.tenantId` is the only thing that marks a user as prod's, so once Cosmos is empty there is no way to tell which Auth0 users to delete.
2. **Stop the API** (`az webapp stop -n app-rvs-api-prod-wus3`) so nothing writes mid-wipe.
3. **Cosmos (`cosmos-rvs-data-prod-wus3` / `rvs-db`): delete the nine tenant and customer containers** with `az cosmosdb sql container delete`: `service-requests`, `customer-profiles`, `global-customer-accounts`, `asset-ledger`, `dealerships`, `locations`, `slug-lookups`, `tenant-configs`, `intake-invites`. Deleting a container is quicker and more thorough than deleting documents. **Keep `lookup-sets` and `rv-warranty-rules`:** they are global reference data. If either is ever lost, `dotnet run --project RVS.Data.Cosmos.Seed -- --environment Production` refills them, and touches nothing else. Backups are `Continuous7Days`, so the deleted data stays restorable, by someone with control-plane rights, for seven days.
4. **Blob (`strvsprodwus3001`): delete the `rvs-attachments` container**, which step 9 recreates. Delete it through Azure Resource Manager, which needs only Owner or Contributor on the resource group. The data-plane route (`az storage blob delete-batch --auth-mode login`) also needs Storage Blob Data Contributor, which prod deliberately grants to no developer (`devBlobAccessPrincipalId` is unset), and shared-key access is off.

   ```bash
   az storage account blob-service-properties show -n strvsprodwus3001 -g rg-rvs-prod-westus3 \
     --query "{blobSoftDelete: deleteRetentionPolicy.enabled, containerSoftDelete: containerDeleteRetentionPolicy.enabled, versioning: isVersioningEnabled}"
   az storage container-rm list --storage-account strvsprodwus3001 -g rg-rvs-prod-westus3 --query "[].name" -o tsv
   az storage container-rm delete --storage-account strvsprodwus3001 -g rg-rvs-prod-westus3 -n rvs-attachments --yes
   ```

   Bicep turns on none of the three soft-delete or versioning settings. If the first command shows any of them `true`, someone turned it on by hand, and the deleted data is kept until its retention runs out. Turn it off before deleting. Delete any container the list shows that is not in `storage-account.bicep` the same way. Do this early: Azure blocks reuse of a deleted container's name for about 30 seconds.
5. **Table: delete every table in the account** (`intakeRedirectHits` and `intakeFormStarts`), which step 9 recreates. Use Azure Resource Manager here too, for the same reason; `az storage table` has no Resource Manager subcommand, so call the API directly:

   ```bash
   ACCT=$(az storage account show -n strvsprodwus3001 -g rg-rvs-prod-westus3 --query id -o tsv)
   az rest --method get    --url "https://management.azure.com$ACCT/tableServices/default/tables?api-version=2023-05-01" --query "value[].name"
   az rest --method delete --url "https://management.azure.com$ACCT/tableServices/default/tables/intakeRedirectHits?api-version=2023-05-01"
   az rest --method delete --url "https://management.azure.com$ACCT/tableServices/default/tables/intakeFormStarts?api-version=2023-05-01"
   ```

   Do this early: a deleted table's name cannot be reused for a minute or so.
6. **Logs.** App Insights `appi-rvs-api-prod-wus3` and Log Analytics `law-rvs-obs-prod-wus3` keep 30 days. If go-live is more than 30 days after the last test traffic, let them expire. Otherwise delete App Insights first, then the workspace, permanently: `--force true`, or tick "Delete the workspace permanently" in the portal. A plain delete only soft-deletes the workspace for 14 days, and step 9 recreating a workspace with the same name in that window restores the old one, test data included. Step 9 also writes the new App Insights connection string to Key Vault, and the API picks it up when it starts in step 10. Don't use the purge API; it is a slow, filter-based GDPR tool. Also clear `/home/LogFiles` through Kudu, or let its 3-day retention clear it.
7. **SendGrid.** The activity feed cannot be purged and ages out on its own. Check the bounce, block and spam-report suppression lists for any real address used during testing (your own inboxes, Nova's) and remove it, or the first real packet to that address is dropped without an error. From go-live on, the weekly suppression check in the on-call runbook ([Bicep README](ASOT/Infra/Bicep.IaC/README.md), "Packets stopped arriving") is the only bounce detection (#833). Twilio needs nothing until G-4: prod has no number, and opt-outs mirrored into `customer-profiles` went with step 3.
8. **Auth0: delete every user whose `app_metadata.tenantId` is one of step 1's IDs**, Nova's included. Before deleting, check that no staging tenant uses the same ID. Keep your platform-admin user. This matters for Nova in particular: `/admin` provisioning adopts an existing Auth0 user with the same email and `tenantId` instead of creating one (`Auth0ManagementProvisioner.EnsureUserAsync`). The new Nova's ID is derived from its name, so a surviving user would keep their test password and receive no new password email. Leave the API, its audience, the claim namespace and the Post-Login Action alone. Auth0's own logs cannot be purged.
9. **Redeploy `main.bicep` with `prod.bicepparam`** ([Bicep README](ASOT/Infra/Bicep.IaC/README.md#deploy-production)). It recreates the containers, the table and the telemetry resources, and writes the new App Insights connection string to Key Vault and app settings. G-1, G-2 and G-3 can go in the same deploy.
10. **Start the API and provision Nova RV Services in `/admin`**, with its real locations, slugs and packet recipients.
11. **Verify.** The nine containers hold only Nova's documents, and `lookup-sets` still has its data. A seed magic-link token from the repo returns 404 on `/api/status/{token}`. A demo slug on `go.rvintake.com` still redirects (the redirect never fails) but to no location. Blob and Table hold only your smoke test. Telemetry reaches the new App Insights, and `/health` passes. One full intake on Nova delivers its packet email, and each Nova user signs in with a newly set password. Then take the first G-8 reading.

Nothing to wipe in Key Vault (configuration only, behind purge protection), the Static Web Apps, DNS, or Azure OpenAI and Whisper, which keep nothing you control.

---

## Pilot metrics (prod)

The four pilot metrics (#528) are read from the first real packet onward. They come from Cosmos `service-requests` and the `intakeFormStarts` table (Spec A-13, #839), not from App Insights, which keeps 30 days and can drop data. The tracking approach is on #528; the queries are in [`Tools/PilotMetrics.md`](../Tools/PilotMetrics.md).

| # | Action | Setting | Why it is parked until go-live |
|---|---|---|---|
| G-8 | Have the saved metric queries ready, and take the first reading after the G-5 wipe | No Bicep flag: [`Tools/PilotMetrics.md`](../Tools/PilotMetrics.md) — Cosmos SQL (requests and visits per location, capture rate, delivery failures) run in Data Explorer per tenant partition, plus the completion-rate read of `intakeFormStarts` | The queries were drafted on #528 but have never been run. Check them against real prod documents on the first packet, not at the end of the first month. Read nothing before G-5 clears the demo data, or the demo seed's requests count as pilot traffic. |
