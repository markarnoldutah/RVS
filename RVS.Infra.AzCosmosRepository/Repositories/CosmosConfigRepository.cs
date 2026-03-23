using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories
{
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

        // -------------------------
        // Tenant config
        // -------------------------
        public async Task<TenantConfig> CreateTenantConfigAsync(TenantConfig tenantConfigEntity)
        {
            ArgumentNullException.ThrowIfNull(tenantConfigEntity);
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantConfigEntity.TenantId, nameof(tenantConfigEntity.TenantId));
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantConfigEntity.Id, nameof(tenantConfigEntity.Id));

            var response = await _tenantsContainer.CreateItemAsync(
                tenantConfigEntity,
                partitionKey: new PartitionKey(tenantConfigEntity.TenantId));

            return response.Resource;
        }

        public async Task<TenantConfig?> GetTenantConfigAsync(string tenantId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

            try
            {
                var resp = await _tenantsContainer.ReadItemAsync<TenantConfig>(
                    id: $"{tenantId}_config",
                    partitionKey: new PartitionKey(tenantId));

                return resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task SaveTenantConfigAsync(TenantConfig tenant)
        {
            ArgumentNullException.ThrowIfNull(tenant);
            ArgumentException.ThrowIfNullOrWhiteSpace(tenant.TenantId, nameof(tenant.TenantId));

            await _tenantsContainer.UpsertItemAsync(
                tenant,
                partitionKey: new PartitionKey(tenant.TenantId));
        }
    }
}
