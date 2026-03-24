using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="TenantConfig"/> entities.
/// Container: <c>tenant-configs</c>. Partition key: <c>/id</c>.
/// <para>
/// Convention: document <c>id</c> = <c>{tenantId}_config</c> (e.g. "ten_bluecompass_config").
/// The partition key equals the document id, enabling O(1) point reads by tenantId.
/// </para>
/// </summary>
public sealed class CosmosTenantConfigRepository : CosmosRepositoryBase, ITenantConfigRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosTenantConfigRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosTenantConfigRepository"/>.
    /// </summary>
    public CosmosTenantConfigRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosTenantConfigRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "tenant-configs");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TenantConfig?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        // O(1) point read — document id and partition key both equal "{tenantId}_config".
        var docId = BuildId(tenantId);

        try
        {
            var response = await _container.ReadItemAsync<TenantConfig>(
                docId,
                new PartitionKey(docId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", tenantId, response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<TenantConfig> CreateAsync(TenantConfig entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TenantId, nameof(entity.TenantId));

        var response = await _container.CreateItemAsync(
            entity,
            new PartitionKey(entity.Id),
            cancellationToken: cancellationToken);

        _logger.LogDebug("CreateAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", entity.TenantId, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task SaveAsync(TenantConfig entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TenantId, nameof(entity.TenantId));

        var response = await _container.UpsertItemAsync(
            entity,
            new PartitionKey(entity.Id),
            cancellationToken: cancellationToken);

        _logger.LogDebug("SaveAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", entity.TenantId, response.RequestCharge);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Derives the Cosmos document id from a tenant id using the <c>{tenantId}_config</c> convention.
    /// </summary>
    private static string BuildId(string tenantId) => $"{tenantId}_config";
}
