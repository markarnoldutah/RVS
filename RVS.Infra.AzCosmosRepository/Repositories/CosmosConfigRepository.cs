using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories
{
    /// <summary>
    /// Cosmos DB repository for tenant configuration.
    /// </summary>
    public class CosmosConfigRepository : CosmosRepositoryBase, IConfigRepository
    {
        private readonly Container _tenantsContainer;

        public CosmosConfigRepository(
            CosmosClient client,
            string databaseId,
            string tenantsContainerId) : base(client)
        {
            _tenantsContainer = GetContainer(databaseId, tenantsContainerId);
        }

        /// <inheritdoc />
        public async Task<TenantConfig> CreateTenantConfigAsync(TenantConfig tenantConfigEntity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tenantConfigEntity);
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantConfigEntity.TenantId, nameof(tenantConfigEntity.TenantId));
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantConfigEntity.Id, nameof(tenantConfigEntity.Id));

            var response = await _tenantsContainer.CreateItemAsync(
                tenantConfigEntity,
                partitionKey: new PartitionKey(tenantConfigEntity.TenantId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }

        /// <inheritdoc />
        public async Task<TenantConfig?> GetTenantConfigAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

            try
            {
                var resp = await _tenantsContainer.ReadItemAsync<TenantConfig>(
                    id: $"{tenantId}_config",
                    partitionKey: new PartitionKey(tenantId),
                    cancellationToken: cancellationToken);

                return resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public async Task SaveTenantConfigAsync(TenantConfig tenant, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tenant);
            ArgumentException.ThrowIfNullOrWhiteSpace(tenant.TenantId, nameof(tenant.TenantId));

            await _tenantsContainer.UpsertItemAsync(
                tenant,
                partitionKey: new PartitionKey(tenant.TenantId),
                cancellationToken: cancellationToken);
        }
    }
}

