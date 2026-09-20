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
| G-3 | Turn the `/health` availability test back on **and** alert on it | `deployAvailabilityTest = true`, plus a new alert rule in `monitor-alerts.bicep` | Standard availability tests are billed per run, and today no alert rule reads their results, so a failing test notifies nobody. Re-enabling the test is only worth the cost once a failure pages the ops action group. That alert rule does not exist yet and is Bicep work to do before go-live. Reducing the test to one location every 15 minutes cuts runs by about 9× if cost matters. |

App Service stays on B1 through go-live; it was considered for F1 and rejected, because F1 cannot hold the `api.*` and `go.*` custom hostnames or their certificates.

---

## Messaging

Unlike the infrastructure items above, these apply to **staging as well as prod**: each environment's outbound SMS stays dark until its own toll-free number is verified. Verification is per number, and the official turnaround is 5–8 weeks (#659).

| # | Action | Setting | Why it is parked until go-live |
|---|---|---|---|
| G-4 | Turn outbound SMS on for an environment once its toll-free number is verified | `acsSmsEnabled = true` in that environment's `.bicepparam`, alongside a real `acsSmsFromPhoneNumber` | Carriers block an unverified toll-free number's traffic, so sending before verification just fails, and the API refuses to start with SMS enabled and no number. While it is off, a `Text` customer's confirmation falls back to email and the A-14 send action refuses with a clear message (`Spec A-2`, `A-14`). Staging today has `+18662319618` with its status unrecorded; prod owns no number at all (#659). Flip each environment on its own, not both together. |
