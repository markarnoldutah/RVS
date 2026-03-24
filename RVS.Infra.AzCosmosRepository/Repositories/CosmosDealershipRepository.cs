using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="Dealership"/> entities.
/// Container: <c>dealerships</c>. Partition key: <c>/tenantId</c>.
/// </summary>
public sealed class CosmosDealershipRepository : CosmosRepositoryBase, IDealershipRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDealershipRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosDealershipRepository"/>.
    /// </summary>
    public CosmosDealershipRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosDealershipRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "dealerships");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Dealership?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var response = await _container.ReadItemAsync<Dealership>(
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
    public async Task<Dealership?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        // Cross-partition query — slug is indexed, query is scoped by type discriminator.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.slug = @slug AND c.type = 'dealership'")
            .WithParameter("@slug", slug);

        var options = new QueryRequestOptions { MaxItemCount = 1 };
        var iterator = _container.GetItemQueryIterator<Dealership>(query, requestOptions: options);

        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;

            var item = page.FirstOrDefault();
            if (item is not null)
            {
                _logger.LogDebug("GetBySlugAsync [slug={Slug}] — RequestCharge: {Charge} RU", slug, totalCharge);
                return item;
            }
        }

        _logger.LogDebug("GetBySlugAsync [slug={Slug}] not found — RequestCharge: {Charge} RU", slug, totalCharge);
        return null;
    }

    /// <inheritdoc />
    public async Task<Dealership> CreateAsync(Dealership entity, CancellationToken cancellationToken = default)
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
    public async Task<Dealership> UpdateAsync(Dealership entity, CancellationToken cancellationToken = default)
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
}
