using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="IntakeInvite"/> entities (<c>Spec A-14</c>, issue #663).
/// Container: <c>intake-invites</c>. Partition key: <c>/tenantId</c>.
/// <para>
/// Convention: document <c>id</c> = <c>InviteToken.Hash(token)</c>, so resolving a token is an
/// O(1) point read once the tenant is known. The container has no TTL: the consent record
/// outlives the invite.
/// </para>
/// </summary>
public sealed class CosmosIntakeInviteRepository : CosmosRepositoryBase, IIntakeInviteRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosIntakeInviteRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosIntakeInviteRepository"/>.
    /// </summary>
    public CosmosIntakeInviteRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosIntakeInviteRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "intake-invites");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IntakeInvite?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var response = await _container.ReadItemAsync<IntakeInvite>(
                id,
                new PartitionKey(tenantId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetByIdAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", tenantId, response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IntakeInvite>> ListRecentByAdvisorAsync(
        string tenantId, string locationId, string advisorUserId, DateTime sinceUtc, int maxItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(advisorUserId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItems);

        var query = new QueryDefinition(
            "SELECT TOP @max * FROM c WHERE c.tenantId = @tenantId AND c.type = 'intakeInvite' " +
            "AND c.locationId = @locationId AND c.advisorUserId = @advisorUserId AND c.createdAtUtc >= @since " +
            "ORDER BY c.createdAtUtc DESC")
            .WithParameter("@max", maxItems)
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@locationId", locationId)
            .WithParameter("@advisorUserId", advisorUserId)
            .WithParameter("@since", sinceUtc);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) };
        var iterator = _container.GetItemQueryIterator<IntakeInvite>(query, requestOptions: options);

        var invites = new List<IntakeInvite>();
        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;
            invites.AddRange(page);
        }

        _logger.LogDebug(
            "ListRecentByAdvisorAsync [tenant={TenantId}, location={LocationId}] count={Count} — RequestCharge: {Charge} RU",
            tenantId, locationId, invites.Count, totalCharge);
        return invites;
    }

    /// <inheritdoc />
    public async Task<IntakeInvite> CreateAsync(IntakeInvite entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TenantId, nameof(entity.TenantId));

        var response = await _container.CreateItemAsync(
            entity,
            new PartitionKey(entity.TenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("CreateAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", entity.TenantId, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<IntakeInvite> UpdateAsync(IntakeInvite entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TenantId, nameof(entity.TenantId));

        var response = await _container.ReplaceItemAsync(
            entity,
            entity.Id,
            new PartitionKey(entity.TenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpdateAsync [tenant={TenantId}] — RequestCharge: {Charge} RU", entity.TenantId, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<IntakeInvite?> GetByAcsMessageIdAcrossTenantsAsync(
        string acsMessageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acsMessageId);

        // Cross-partition on purpose (issue #665): a delivery report carries the ACS message id
        // and no tenant. One message id belongs to one invite, so this reads at most one page.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.acsMessageId = @acsMessageId OFFSET 0 LIMIT 1")
            .WithParameter("@acsMessageId", acsMessageId);

        var iterator = _container.GetItemQueryIterator<IntakeInvite>(query);

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            _logger.LogDebug(
                "GetByAcsMessageIdAcrossTenantsAsync [messageId={MessageId}] — RequestCharge: {Charge} RU",
                acsMessageId, page.RequestCharge);

            var invite = page.FirstOrDefault();
            if (invite is not null)
            {
                return invite;
            }
        }

        return null;
    }
}
