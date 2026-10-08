# Pilot metric queries

The saved queries for the four pilot metrics (#528), run by hand once a month. This is go-live item **G-8** in [RVS_GoLive_Activities.md](../Docs/RVS_GoLive_Activities.md). There is no dashboard for the pilot.

Three of the metrics come from Cosmos `service-requests`. Completion rate also needs the `intakeFormStarts` table in Table Storage ([Spec A-13](../Docs/RVS_Spec.md), "Completion rate"; #839). Don't use App Insights for any of them: it keeps 30 days and can drop data.

**Read nothing from before the G-5 wipe.** Demo-seed requests would count as pilot traffic.

**None of these has been run against prod documents yet.** Check the field shapes and status serialisation on the first real packet, not at the end of the first month.

## Cosmos: `service-requests`

Run these in the portal's Data Explorer against `rvs-db` / `service-requests`, one tenant partition at a time. Replace `@monthStart` and `@monthEnd` with the month's UTC bounds, as ISO strings such as `'2026-11-01T00:00:00Z'` and `'2026-12-01T00:00:00Z'`.

```sql
-- 1. Requests per location per month
SELECT c.locationId, COUNT(1) AS requests
FROM c
WHERE c.createdAtUtc >= @monthStart AND c.createdAtUtc < @monthEnd
GROUP BY c.locationId

-- 1b. Visits per location per month: one row per submission (Spec A-17, #806).
-- Cosmos has no COUNT(DISTINCT), so count single-issue requests plus the first request of each multi-issue submission.
SELECT c.locationId, COUNT(1) AS visits
FROM c
WHERE c.createdAtUtc >= @monthStart AND c.createdAtUtc < @monthEnd
  AND (NOT IS_DEFINED(c.submissionId) OR IS_NULL(c.submissionId) OR c.submissionPosition = 1)
GROUP BY c.locationId

-- 1c. Visits per location per channel per month: 1b split by src, for completion rate per channel.
-- A request with no intakeSource predates A-13; it is reported as undefined and counted as print.
SELECT c.locationId, c.intakeSource, COUNT(1) AS visits
FROM c
WHERE c.createdAtUtc >= @monthStart AND c.createdAtUtc < @monthEnd
  AND (NOT IS_DEFINED(c.submissionId) OR IS_NULL(c.submissionId) OR c.submissionPosition = 1)
GROUP BY c.locationId, c.intakeSource

-- 3. Capture rate: fully captured count (divide by query 1's total)
SELECT COUNT(1) AS fullyCaptured
FROM c
WHERE c.createdAtUtc >= @monthStart AND c.createdAtUtc < @monthEnd
  AND LENGTH(c.assetInfo.assetId) = 17 AND IS_STRING(c.assetInfo.manufacturer)
  AND ARRAY_LENGTH(c.attachments) > 0
  AND ARRAY_LENGTH(c.diagnosticResponses) > 0

-- 4. Packet delivery failures
SELECT c.id, c.locationId, c.packetGeneration.status AS gen, c.packetEmailDelivery.status AS email
FROM c
WHERE c.createdAtUtc >= @monthStart AND c.createdAtUtc < @monthEnd
  AND (c.packetGeneration.status = 'Failed' OR c.packetEmailDelivery.status = 'Failed')
```

## Table Storage: `intakeFormStarts` (completion rate)

**Completion rate = visits ÷ starts**, per location per month (query 1b over query 2), and per channel with query 1c.

- The numerator is visits (1b), **not** requests (1): a three-issue visit creates three requests but started the form once.
- The denominator is **distinct `SessionId`** over non-bot rows, not a row count. A tab with `sessionStorage` blocked writes a new row with a new id on each refresh, and nothing stops a client posting twice.

Each row is one Intake visit that reached Step 1 with the form showing. `PartitionKey` is the `locationId`. The columns are `TenantId`, `Slug`, `Source` (the normalised `src`), `SessionId`, `OccurredAtUtc`, `IsLikelyBot` and `UserAgent`.

### 2. Starts per location (and channel) per month

**Access.** Shared-key access is off on the storage account, so read the table with your Entra sign-in. You need **Storage Table Data Reader** (or Contributor) on the prod account. Prod grants it to no developer by default (`devBlobAccessPrincipalId` is unset), so assign it to yourself for the reading and remove it afterwards.

**Azure Storage Explorer:**

1. Open `strvsprodwus3001` → Tables → `intakeFormStarts`.
2. Query with this filter, with the month's UTC bounds:

   ```text
   OccurredAtUtc ge datetime'2026-11-01T00:00:00Z' and OccurredAtUtc lt datetime'2026-12-01T00:00:00Z' and IsLikelyBot eq false
   ```

3. Export the result to CSV, then count distinct sessions per location, and per location and channel:

   ```bash
   python3 - starts.csv <<'EOF'
   import csv, sys
   from collections import defaultdict
   by_loc, by_loc_src = defaultdict(set), defaultdict(set)
   with open(sys.argv[1], newline="") as f:
       for row in csv.DictReader(f):
           by_loc[row["PartitionKey"]].add(row["SessionId"])
           by_loc_src[(row["PartitionKey"], row["Source"])].add(row["SessionId"])
   for loc, ids in sorted(by_loc.items()):
       print(f"{loc}\tall\t{len(ids)}")
   for (loc, src), ids in sorted(by_loc_src.items()):
       print(f"{loc}\t{src}\t{len(ids)}")
   EOF
   ```

A visit that starts on the last evening of a month and submits after midnight UTC counts as a start in one month and a visit in the next. At pilot volumes that's noise. It's not a bug to chase.
