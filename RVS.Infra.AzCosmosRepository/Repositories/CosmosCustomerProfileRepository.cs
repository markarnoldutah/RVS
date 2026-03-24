using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="CustomerProfile"/> entities.
/// Container: <c>customer-profiles</c>. Partition key: <c>/tenantId</c>.
/// </summary>
public sealed class CosmosCustomerProfileRepository : CosmosRepositoryBase, ICustomerProfileRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosCustomerProfileRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosCustomerProfileRepository"/>.
    /// </summary>
    public CosmosCustomerProfileRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosCustomerProfileRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "customer-profiles");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CustomerProfile?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var response = await _container.ReadItemAsync<CustomerProfile>(
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
    public async Task<CustomerProfile?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.normalizedEmail = @normalizedEmail AND c.type = 'customerProfile'")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@normalizedEmail", email.Trim().ToLowerInvariant());

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId), MaxItemCount = 1 };
        var iterator = _container.GetItemQueryIterator<CustomerProfile>(query, requestOptions: options);

        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;

            var item = page.FirstOrDefault();
            if (item is not null)
            {
                _logger.LogDebug("GetByEmailAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", tenantId, totalCharge);
                return item;
            }
        }

        _logger.LogDebug("GetByEmailAsync [tenant={TenantId}] not found — RequestCharge: {Charge} RU", tenantId, totalCharge);
        return null;
    }

    /// <inheritdoc />
    public async Task<CustomerProfile> CreateAsync(CustomerProfile entity, CancellationToken cancellationToken = default)
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
    public async Task<CustomerProfile> UpdateAsync(CustomerProfile entity, CancellationToken cancellationToken = default)
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
    public async Task<CustomerProfile?> GetByActiveAssetIdAsync(string tenantId, string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.type = 'customerProfile' AND EXISTS(SELECT VALUE a FROM a IN c.assetsOwned WHERE a.assetId = @assetId AND a.status = 'Active')")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@assetId", assetId);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId), MaxItemCount = 1 };
        var iterator = _container.GetItemQueryIterator<CustomerProfile>(query, requestOptions: options);

        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;

            var item = page.FirstOrDefault();
            if (item is not null)
            {
                _logger.LogDebug("GetByActiveAssetIdAsync [tenant={TenantId}, asset={AssetId}] — RequestCharge: {Charge} RU", tenantId, assetId, totalCharge);
                return item;
            }
        }

        _logger.LogDebug("GetByActiveAssetIdAsync [tenant={TenantId}, asset={AssetId}] not found — RequestCharge: {Charge} RU", tenantId, assetId, totalCharge);
        return null;
    }
}
