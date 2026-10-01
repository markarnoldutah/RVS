using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.Infra.AzTableRepository;

/// <summary>
/// Azure Table Storage implementation of <see cref="IIntakeRedirectHitRepository"/>
/// (<c>Spec A-13</c>, issue #599).
///
/// Table Storage rather than Cosmos, deliberately. Redirect hits are a high-volume write path
/// read occasionally, and most of them never convert — link-preview fetchers alone can produce
/// several per link composed. Cosmos would charge request units on every one of those writes to
/// serve a query somebody runs once a month. Here the cost is storage.
///
/// Append-only: no update, no delete. Retention is a storage-lifecycle policy, not application
/// code.
/// </summary>
public sealed class AzTableIntakeRedirectHitRepository : IIntakeRedirectHitRepository
{
    /// <summary>Table name. Alphanumeric only — Table Storage forbids hyphens in table names.</summary>
    public const string TableName = "intakeRedirectHits";

    /// <summary>
    /// Cap on rows returned by one <see cref="QueryAsync"/> call. A report reads a window, not a
    /// history; an unbounded read of a busy location's partition is how a reporting endpoint
    /// becomes an outage.
    /// </summary>
    private const int MaxRowsPerQuery = 10_000;

    /// <summary>
    /// Sorts after every character that can appear in the inverted-tick prefix or the guid
    /// suffix, so <c>{key}~</c> bounds every row key sharing that prefix.
    /// </summary>
    private const char RowKeySuffixSentinel = '~';

    private readonly TableClient _table;
    private readonly ILogger<AzTableIntakeRedirectHitRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AzTableIntakeRedirectHitRepository"/>.
    /// </summary>
    public AzTableIntakeRedirectHitRepository(
        TableServiceClient tableServiceClient,
        ILogger<AzTableIntakeRedirectHitRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(tableServiceClient);

        _table = tableServiceClient.GetTableClient(TableName);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AppendAsync(IntakeRedirectHit hit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hit);

        var entity = new IntakeRedirectHitTableEntity
        {
            PartitionKey = hit.LocationId,
            RowKey = BuildRowKey(hit.OccurredAtUtc),
            TenantId = hit.TenantId,
            Slug = hit.Slug,
            Source = hit.Source,
            OccurredAtUtc = hit.OccurredAtUtc,
            IsLikelyBot = hit.IsLikelyBot,
            UserAgent = hit.UserAgent
        };

        try
        {
            await _table.AddEntityAsync(entity, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // The table is declared in Bicep for every deployed environment, so this is the
            // local-emulator and fresh-account case. Create it and retry once rather than
            // paying an existence check on every hit.
            _logger.LogInformation(
                "Intake redirect hits: table {TableName} was absent; creating it and retrying the append.",
                TableName);

            await _table.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await _table.AddEntityAsync(entity, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IntakeRedirectHit>> QueryAsync(
        string locationId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        var filter = BuildFilter(locationId, fromUtc, toUtc);

        var hits = new List<IntakeRedirectHit>();

        try
        {
            await foreach (var entity in _table
                .QueryAsync<IntakeRedirectHitTableEntity>(filter, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                hits.Add(ToDomain(entity));

                if (hits.Count >= MaxRowsPerQuery)
                {
                    _logger.LogWarning(
                        "Intake redirect hits: query for locationId={LocationId} hit the {Cap}-row cap; the report is truncated.",
                        locationId, MaxRowsPerQuery);
                    break;
                }
            }
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // Nothing has been recorded yet, so the table has never been created. An empty
            // window is the honest answer; the report still shows submissions.
            return [];
        }

        return hits;
    }

    /// <summary>
    /// <c>{inverted ticks:D19}-{short guid}</c>. Inverting the tick count makes ascending row-key
    /// order — the only order Table Storage offers — newest-first.
    /// </summary>
    private static string BuildRowKey(DateTimeOffset occurredAt) =>
        $"{InvertedTicks(occurredAt):D19}-{Guid.NewGuid():N}";

    private static long InvertedTicks(DateTimeOffset value) =>
        DateTimeOffset.MaxValue.UtcTicks - value.UtcTicks;

    /// <summary>
    /// Single-partition range query. Because row keys are inverted ticks, a later instant
    /// produces a smaller key: <paramref name="fromUtc"/> therefore bounds the range from above
    /// and <paramref name="toUtc"/> from below.
    /// </summary>
    private static string BuildFilter(string locationId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        var clauses = new List<string>
        {
            TableClient.CreateQueryFilter($"PartitionKey eq {locationId}")
        };

        if (fromUtc.HasValue)
        {
            // Inclusive lower time bound. "{key}~" sorts after every row key with that prefix,
            // so hits recorded at exactly fromUtc are kept.
            var upperKey = $"{InvertedTicks(fromUtc.Value):D19}{RowKeySuffixSentinel}";
            clauses.Add(TableClient.CreateQueryFilter($"RowKey le {upperKey}"));
        }

        if (toUtc.HasValue)
        {
            // Exclusive upper time bound: strictly greater than the sentinel excludes every hit
            // recorded at exactly toUtc.
            var lowerKey = $"{InvertedTicks(toUtc.Value):D19}{RowKeySuffixSentinel}";
            clauses.Add(TableClient.CreateQueryFilter($"RowKey gt {lowerKey}"));
        }

        return string.Join(" and ", clauses);
    }

    private static IntakeRedirectHit ToDomain(IntakeRedirectHitTableEntity entity) => new()
    {
        LocationId = entity.PartitionKey,
        TenantId = entity.TenantId,
        Slug = entity.Slug,
        Source = entity.Source,
        OccurredAtUtc = entity.OccurredAtUtc,
        IsLikelyBot = entity.IsLikelyBot,
        UserAgent = entity.UserAgent
    };
}
