using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="AssetLedgerEntry"/> entities.
/// Container: <c>asset-ledger</c>. Partition key: <c>/assetId</c>.
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
    public async Task<IReadOnlyList<AssetLedgerEntry>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.assetId = @assetId ORDER BY c.submittedAtUtc ASC")
            .WithParameter("@assetId", assetId);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(assetId) };
        var iterator = _container.GetItemQueryIterator<AssetLedgerEntry>(query, requestOptions: options);

        var results = new List<AssetLedgerEntry>();
        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;
            results.AddRange(page);
        }

        _logger.LogDebug("GetByAssetIdAsync [assetId={AssetId}] — {Count} items, RequestCharge: {Charge} RU",
            assetId, results.Count, totalCharge);

        return results;
    }

    /// <inheritdoc />
    public async Task<AssetLedgerEntry?> GetByIdAsync(string assetId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var response = await _container.ReadItemAsync<AssetLedgerEntry>(
                id,
                new PartitionKey(assetId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetByIdAsync [assetId={AssetId}, id={Id}] — RequestCharge: {Charge} RU",
                assetId, id, response.RequestCharge);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.AssetId, nameof(entity.AssetId));

        var response = await _container.CreateItemAsync(
            entity,
            new PartitionKey(entity.AssetId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("AppendAsync [assetId={AssetId}, id={Id}] — RequestCharge: {Charge} RU",
            entity.AssetId, entity.Id, response.RequestCharge);
        return response.Resource;
    }
}
