using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="AssetLedgerEntry"/> entities.
/// Container: <c>asset-ledger</c>. Partition key: <c>/vin</c>.
/// Append-only — entries are immutable once created.
/// </summary>
public sealed class CosmosAssetLedgerRepository : CosmosRepositoryBase, IAssetLedgerRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosAssetLedgerRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosAssetLedgerRepository"/>.
    /// </summary>
    public CosmosAssetLedgerRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosAssetLedgerRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "asset-ledger");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetLedgerEntry>> GetByVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.vin = @vin ORDER BY c.submittedAtUtc ASC")
            .WithParameter("@vin", vin);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(vin) };
        var iterator = _container.GetItemQueryIterator<AssetLedgerEntry>(query, requestOptions: options);

        var results = new List<AssetLedgerEntry>();
        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;
            results.AddRange(page);
        }

        _logger.LogDebug("GetByVinAsync [vin={Vin}] — {Count} items, RequestCharge: {Charge} RU",
            vin, results.Count, totalCharge);

        return results;
    }

    /// <inheritdoc />
    public async Task<AssetLedgerEntry?> GetByIdAsync(string vin, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var response = await _container.ReadItemAsync<AssetLedgerEntry>(
                id,
                new PartitionKey(vin),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetByIdAsync [vin={Vin}, id={Id}] — RequestCharge: {Charge} RU",
                vin, id, response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AssetLedgerEntry> AppendAsync(AssetLedgerEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Vin, nameof(entity.Vin));

        var response = await _container.CreateItemAsync(
            entity,
            new PartitionKey(entity.Vin),
            cancellationToken: cancellationToken);

        _logger.LogDebug("AppendAsync [vin={Vin}, id={Id}] — RequestCharge: {Charge} RU",
            entity.Vin, entity.Id, response.RequestCharge);
        return response.Resource;
    }
}
