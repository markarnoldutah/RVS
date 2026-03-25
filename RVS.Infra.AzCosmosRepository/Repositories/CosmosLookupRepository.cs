using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="LookupSet"/> entities.
/// Container: <c>lookup-sets</c>. Partition key: <c>/category</c>.
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
    public async Task<LookupSet?> GetGlobalAsync(string category, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var query = new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.tenantId = @tenantId")
            .WithParameter("@tenantId", GlobalTenantId);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(category),
            MaxItemCount = 1,
        };

        using var iterator = _container.GetItemQueryIterator<LookupSet>(query, requestOptions: options);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            _logger.LogDebug("GetGlobalAsync [category={Category}] — RequestCharge: {Charge} RU", category, response.RequestCharge);
            return response.FirstOrDefault();
        }

        return null;
    }

    /// <inheritdoc />
    public async Task UpsertGlobalAsync(LookupSet entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Category, nameof(entity.Category));

        // Ensure updated timestamp is consistent at the boundary.
        entity.UpdatedAtUtc = DateTime.UtcNow;

        var response = await _container.UpsertItemAsync(
            item: entity,
            partitionKey: new PartitionKey(entity.Category),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpsertGlobalAsync [category={Category}] — RequestCharge: {Charge} RU", entity.Category, response.RequestCharge);
    }

    // TODO CreateGlobalAsync()

    // Future: GetByTenantAsync and merge logic when overrides are enabled.
}
