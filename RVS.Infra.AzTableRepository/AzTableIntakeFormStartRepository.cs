using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.Infra.AzTableRepository;

/// <summary>
/// Azure Table Storage implementation of <see cref="IIntakeFormStartRepository"/>
/// (<c>Spec A-13</c>, issue #839). Modelled on <see cref="AzTableIntakeRedirectHitRepository"/>,
/// for the same reason: a write on every visit, read by hand once a month, where Cosmos would
/// charge request units for every one of them.
///
/// Append-only: no update, no delete, and no application read path.
/// </summary>
public sealed class AzTableIntakeFormStartRepository : IIntakeFormStartRepository
{
    /// <summary>Table name. Alphanumeric only — Table Storage forbids hyphens in table names.</summary>
    public const string TableName = "intakeFormStarts";

    private readonly TableClient _table;
    private readonly ILogger<AzTableIntakeFormStartRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AzTableIntakeFormStartRepository"/>.
    /// </summary>
    public AzTableIntakeFormStartRepository(
        TableServiceClient tableServiceClient,
        ILogger<AzTableIntakeFormStartRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(tableServiceClient);

        _table = tableServiceClient.GetTableClient(TableName);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AppendAsync(IntakeFormStart start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);

        var entity = new IntakeFormStartTableEntity
        {
            PartitionKey = start.LocationId,
            RowKey = BuildRowKey(start.OccurredAtUtc),
            TenantId = start.TenantId,
            Slug = start.Slug,
            Source = start.Source,
            SessionId = start.SessionId,
            OccurredAtUtc = start.OccurredAtUtc,
            IsLikelyBot = start.IsLikelyBot,
            UserAgent = start.UserAgent
        };

        try
        {
            await _table.AddEntityAsync(entity, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // The table is declared in Bicep for every deployed environment, so this is the
            // local-emulator and fresh-account case. Create it and retry once rather than
            // paying an existence check on every start.
            _logger.LogInformation(
                "Intake form starts: table {TableName} was absent; creating it and retrying the append.",
                TableName);

            await _table.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await _table.AddEntityAsync(entity, cancellationToken);
        }
    }

    /// <summary>
    /// <c>{inverted ticks:D19}-{short guid}</c>, as for redirect hits: ascending row-key order is
    /// newest-first, and a month is a row-key range.
    /// </summary>
    private static string BuildRowKey(DateTimeOffset occurredAt) =>
        $"{DateTimeOffset.MaxValue.UtcTicks - occurredAt.UtcTicks:D19}-{Guid.NewGuid():N}";
}
