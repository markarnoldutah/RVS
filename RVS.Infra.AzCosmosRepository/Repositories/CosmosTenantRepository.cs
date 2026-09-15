using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="Tenant"/> entities.
/// Container: <c>dealerships</c> (shared with <see cref="Dealership"/>, discriminated by
/// <c>type = 'tenant'</c>). Partition key: <c>/tenantId</c>.
/// <para>
/// Convention: document <c>id</c> = <c>tenantId</c>, so a tenant is an O(1) point read.
/// </para>
/// </summary>
public sealed class CosmosTenantRepository : CosmosRepositoryBase, ITenantRepository
{
    private const string TenantType = "tenant";

    private readonly Container _container;
    private readonly ILogger<CosmosTenantRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosTenantRepository"/>.
    /// </summary>
    public CosmosTenantRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosTenantRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "dealerships");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Tenant?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        try
        {
            var response = await _container.ReadItemAsync<Tenant>(
                tenantId,
                new PartitionKey(tenantId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", tenantId, response.RequestCharge);

            // Dealership documents share the container; never hand one back as a tenant.
            return string.Equals(response.Resource.Type, TenantType, StringComparison.Ordinal)
                ? response.Resource
                : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Tenant> CreateAsync(Tenant entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

        try
        {
            var response = await _container.CreateItemAsync(
                entity,
                new PartitionKey(entity.TenantId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("CreateAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ConflictException($"Tenant '{entity.Id}' already exists.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Tenant> UpdateAsync(Tenant entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

        var response = await _container.ReplaceItemAsync(
            entity,
            entity.Id,
            new PartitionKey(entity.TenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpdateAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tenant>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        // Deliberately cross-partition (no PartitionKey on the request): this lists every tenant
        // and is reachable only from the platform-admin endpoints (Spec P-7). It is the one
        // documented exception to single-partition access — see RVS_DataModel.md.
        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type ORDER BY c.name ASC")
            .WithParameter("@type", TenantType);

        var iterator = _container.GetItemQueryIterator<Tenant>(query);

        var results = new List<Tenant>();
        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;
            results.AddRange(page);
        }

        _logger.LogDebug("ListAllAsync — {Count} tenants, RequestCharge: {Charge} RU", results.Count, totalCharge);
        return results;
    }
}
