using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="LookupSet"/> entities.
/// Container: <c>lookup-sets</c>. Partition key: <c>/id</c>.
/// </summary>
public sealed class CosmosLookupRepository : CosmosRepositoryBase, ILookupRepository
{
    // TODO - Consider caching these lookups in memory for performance

    private const string GlobalTenantId = "GLOBAL";

    private readonly Container _container;
    private readonly ILogger<CosmosLookupRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosLookupRepository"/>.
    /// </summary>
    public CosmosLookupRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosLookupRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "lookup-sets");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LookupSet?> GetGlobalAsync(string lookupSetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupSetId);

        try
        {
            var response = await _container.ReadItemAsync<LookupSet>(
                id: lookupSetId,
                partitionKey: new PartitionKey(GlobalTenantId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetGlobalAsync [lookupSetId={LookupSetId}] — RequestCharge: {Charge} RU", lookupSetId, response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task UpsertGlobalAsync(LookupSet entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

        // Ensure global partition + updated timestamp are consistent at the boundary.
        // Note: entity.TenantId is init-only; lookup sets are stored under the GLOBAL partition key.
        entity.UpdatedAtUtc = DateTime.UtcNow;

        var response = await _container.UpsertItemAsync(
            item: entity,
            partitionKey: new PartitionKey(GlobalTenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpsertGlobalAsync [lookupSetId={LookupSetId}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
    }

    // TODO CreateGlobalAsync()

    // Future: GetByTenantAsync and merge logic when overrides are enabled.
}
