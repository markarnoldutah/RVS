using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="Location"/> entities.
/// Container: <c>locations</c>. Partition key: <c>/tenantId</c>.
/// Unique key: <c>/tenantId + /slug</c>.
/// </summary>
public sealed class CosmosLocationRepository : CosmosRepositoryBase, ILocationRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosLocationRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosLocationRepository"/>.
    /// </summary>
    public CosmosLocationRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosLocationRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "locations");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Location?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var response = await _container.ReadItemAsync<Location>(
                id,
                new PartitionKey(tenantId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetByIdAsync [{Id}] — RequestCharge: {Charge} RU", id, response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Location?> GetBySlugAsync(string tenantId, string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.slug = @slug AND c.type = 'location'")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@slug", slug);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId), MaxItemCount = 1 };
        var iterator = _container.GetItemQueryIterator<Location>(query, requestOptions: options);

        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;

            var item = page.FirstOrDefault();
            if (item is not null)
            {
                _logger.LogDebug("GetBySlugAsync [tenant={TenantId}, slug={Slug}] — RequestCharge: {Charge} RU",
                    tenantId, slug, totalCharge);
                return item;
            }
        }

        _logger.LogDebug("GetBySlugAsync [tenant={TenantId}, slug={Slug}] not found — RequestCharge: {Charge} RU",
            tenantId, slug, totalCharge);
        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Location>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.type = 'location' ORDER BY c.name ASC")
            .WithParameter("@tenantId", tenantId);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) };
        var iterator = _container.GetItemQueryIterator<Location>(query, requestOptions: options);

        var results = new List<Location>();
        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;
            results.AddRange(page);
        }

        _logger.LogDebug("ListByTenantAsync [tenant={TenantId}] — {Count} items, RequestCharge: {Charge} RU",
            tenantId, results.Count, totalCharge);

        return results;
    }

    /// <inheritdoc />
    public async Task<Location> CreateAsync(Location entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TenantId, nameof(entity.TenantId));

        var response = await _container.CreateItemAsync(
            entity,
            new PartitionKey(entity.TenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("CreateAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<Location> UpdateAsync(Location entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TenantId, nameof(entity.TenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

        var response = await _container.ReplaceItemAsync(
            entity,
            entity.Id,
            new PartitionKey(entity.TenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpdateAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var response = await _container.DeleteItemAsync<Location>(
            id,
            new PartitionKey(tenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("DeleteAsync [{Id}] — RequestCharge: {Charge} RU", id, response.RequestCharge);
    }
}
