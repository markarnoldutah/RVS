using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="GlobalCustomerAcct"/> entities.
/// Container: <c>global-customer-accounts</c>. Partition key: <c>/id</c>.
/// Cross-tenant — not scoped by tenantId. NormalizedEmail is a unique key (not partition key).
/// </summary>
public sealed class CosmosGlobalCustomerAcctRepository : CosmosRepositoryBase, IGlobalCustomerAcctRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosGlobalCustomerAcctRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosGlobalCustomerAcctRepository"/>.
    /// </summary>
    public CosmosGlobalCustomerAcctRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosGlobalCustomerAcctRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "global-customer-accounts");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        // Cross-partition query — partition key is /id, so a full scan is required when querying by email.
        // NormalizedEmail has a unique key policy and an index, keeping this query efficient.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.normalizedEmail = @normalizedEmail AND c.type = 'globalCustomerAcct'")
            .WithParameter("@normalizedEmail", normalizedEmail.Trim().ToLowerInvariant());

        var options = new QueryRequestOptions { MaxItemCount = 1 };
        var iterator = _container.GetItemQueryIterator<GlobalCustomerAcct>(query, requestOptions: options);

        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;

            var item = page.FirstOrDefault();
            if (item is not null)
            {
                _logger.LogDebug("GetByEmailAsync — RequestCharge: {Charge} RU", totalCharge);
                return item;
            }
        }

        _logger.LogDebug("GetByEmailAsync not found — RequestCharge: {Charge} RU", totalCharge);
        return null;
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            // Partition key is /id, so the partition key value equals the document id — O(1) point read.
            var response = await _container.ReadItemAsync<GlobalCustomerAcct>(
                id,
                new PartitionKey(id),
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
    public async Task<GlobalCustomerAcct> CreateAsync(GlobalCustomerAcct entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

        var response = await _container.CreateItemAsync(
            entity,
            new PartitionKey(entity.Id),
            cancellationToken: cancellationToken);

        _logger.LogDebug("CreateAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct> UpdateAsync(GlobalCustomerAcct entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

        var response = await _container.ReplaceItemAsync(
            entity,
            entity.Id,
            new PartitionKey(entity.Id),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpdateAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }
}
