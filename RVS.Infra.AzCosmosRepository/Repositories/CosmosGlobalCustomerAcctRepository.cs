using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="GlobalCustomerAcct"/> entities.
/// Container: <c>global-customer-accounts</c>. Partition key: <c>/email</c>.
/// Cross-tenant — not scoped by tenantId. Email is stored in normalized form.
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
    public async Task<GlobalCustomerAcct?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Single-partition query — partition key is /email, so this is an efficient indexed query.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.email = @email AND c.type = 'globalCustomerAcct'")
            .WithParameter("@email", normalizedEmail);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(normalizedEmail), MaxItemCount = 1 };
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

        // Cross-partition query — partition key is /email, so a fan-out is required when querying by id.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.id = @id AND c.type = 'globalCustomerAcct'")
            .WithParameter("@id", id);

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
                _logger.LogDebug("GetByIdAsync [{Id}] — RequestCharge: {Charge} RU", id, totalCharge);
                return item;
            }
        }

        _logger.LogDebug("GetByIdAsync [{Id}] not found — RequestCharge: {Charge} RU", id, totalCharge);
        return null;
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct> CreateAsync(GlobalCustomerAcct entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Email, nameof(entity.Email));

        var response = await _container.CreateItemAsync(
            entity,
            new PartitionKey(entity.Email),
            cancellationToken: cancellationToken);

        _logger.LogDebug("CreateAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct> UpdateAsync(GlobalCustomerAcct entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Email, nameof(entity.Email));

        var response = await _container.ReplaceItemAsync(
            entity,
            entity.Id,
            new PartitionKey(entity.Email),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpdateAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct?> GetByMagicLinkTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        // Cross-partition query — partition key is /email, magicLinkToken is unique.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.magicLinkToken = @token AND c.type = 'globalCustomerAcct'")
            .WithParameter("@token", token);

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
                _logger.LogDebug("GetByMagicLinkTokenAsync — RequestCharge: {Charge} RU", totalCharge);
                return item;
            }
        }

        _logger.LogDebug("GetByMagicLinkTokenAsync not found — RequestCharge: {Charge} RU", totalCharge);
        return null;
    }
}
