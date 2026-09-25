#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────
# Verify the packet-pipeline alert rules against a live signal (#732)
# ──────────────────────────────────────────────────────────────
# Posts one synthetic record per alerted EventId straight to the
# environment's Application Insights ingestion endpoint, shaped the way
# the app's exporter writes them: EventId, EventName and the message-
# template arguments land in customDimensions. 434001 is sent as an
# exception, because the app logs it with one and it lands in the
# `exceptions` table. The rest go to `traces`.
#
# That exercises everything downstream of the app: ingestion, each
# rule's KQL, the dimension split, the action group and the email
# payload. It does not prove the app emits that shape. Running a real
# 438001 does that: README "Verifying the alert rules".
#
# The IDs are obviously fake (loc_alertcheck_*, sr_alertcheck_*,
# ten_alertcheck) so nobody acts on the page. Four Sev 1 rules fire
# within ~5–15 minutes; the 439001 digest rule on its next hourly run.
# All auto-resolve after one clean window.
#
# Usage:  ./verify-alert-rules.sh staging
#         ./verify-alert-rules.sh prod      # pages prod on-call; say so first
# ──────────────────────────────────────────────────────────────
set -euo pipefail

env="${1:-}"
case "$env" in
  staging|prod) ;;
  *) echo "usage: $0 staging|prod" >&2; exit 2 ;;
esac

rg="rg-rvs-${env}-westus3"
appi="appi-rvs-api-${env}-wus3"

conn=$(az monitor app-insights component show -g "$rg" --app "$appi" --query connectionString -o tsv)
ikey=$(sed -E 's/.*InstrumentationKey=([^;]+).*/\1/' <<<"$conn")
ingest=$(sed -E 's/.*IngestionEndpoint=([^;]+).*/\1/' <<<"$conn")
ingest="${ingest%/}"

stamp=$(date -u +%Y%m%dT%H%M%SZ)
now=$(date -u +%Y-%m-%dT%H:%M:%SZ)
tenant="ten_alertcheck"
loc="loc_alertcheck_${stamp}"
sr="sr_alertcheck_${stamp}"
note="SYNTHETIC — alert-rule verification (#732), run ${stamp}. Ignore."

# message <eventId> <eventName> <category> <idColumn> <idValue>
message() {
  cat <<JSON
{"name":"Microsoft.ApplicationInsights.Message","time":"${now}","iKey":"${ikey}",
 "data":{"baseType":"MessageData","baseData":{"ver":2,"severityLevel":"$6",
  "message":"$2: ${note}",
  "properties":{"EventId":"$1","EventName":"$2","CategoryName":"$3","$4":"$5","TenantId":"${tenant}"}}}}
JSON
}

exception() {
  cat <<JSON
{"name":"Microsoft.ApplicationInsights.Exception","time":"${now}","iKey":"${ikey}",
 "data":{"baseType":"ExceptionData","baseData":{"ver":2,"severityLevel":"Critical",
  "exceptions":[{"id":1,"typeName":"System.InvalidOperationException","message":"${note}","hasFullStack":false}],
  "properties":{"EventId":"434001","EventName":"PacketGenerationExhausted","CategoryName":"RVS.API.Services.PacketGenerationService","ServiceRequestId":"${sr}","TenantId":"${tenant}"}}}}
JSON
}

body="[
$(message 439002 AllRecipientsBounced         RVS.API.Services.LocationService         LocationId       "$loc" Critical),
$(message 438001 PacketEmailDeliveryExhausted RVS.API.Services.PacketGenerationService ServiceRequestId "$sr"  Critical),
$(exception),
$(message 521001 PacketEmailOversized         RVS.API.Services.PacketGenerationService ServiceRequestId "$sr"  Critical),
$(message 439001 RecipientHardBounced         RVS.API.Services.LocationService         LocationId       "$loc" Warning)
]"

echo "Posting 5 synthetic records to ${appi} (${ingest}) ..."
resp=$(curl -sS -X POST "${ingest}/v2.1/track" -H 'Content-Type: application/json' --data-binary "$body")
echo "$resp"
grep -q '"itemsAccepted":5' <<<"$resp" || { echo "Ingestion did not accept all 5 records." >&2; exit 1; }

cat <<EOF

Accepted. Run ID: ${stamp}   LocationId: ${loc}   ServiceRequestId: ${sr}

1. Rows landed (App Insights → Logs; allow 2–5 min ingestion lag):

   union traces, exceptions
   | where tostring(customDimensions.TenantId) == "${tenant}"
   | project timestamp, itemType, EventId = tostring(customDimensions.EventId),
             LocationId = tostring(customDimensions.LocationId),
             ServiceRequestId = tostring(customDimensions.ServiceRequestId)

2. Alerts fired (after ~15 min for the Sev 1 rules, ~75 min for 439001):

   az rest --method get --url "https://management.azure.com/subscriptions/\$(az account show --query id -o tsv)/providers/Microsoft.AlertsManagement/alerts?api-version=2019-05-05-preview&targetResourceGroup=${rg}&timeRange=1d" \\
     --query "value[?contains(properties.essentials.alertRule, 'sqr-rvs-packet')].{rule:name, sev:properties.essentials.severity, condition:properties.essentials.monitorCondition, fired:properties.essentials.startDateTime}" -o table

3. The ops mailbox has one email per rule, and each names
   TenantId=${tenant} plus the LocationId / ServiceRequestId above.
EOF
