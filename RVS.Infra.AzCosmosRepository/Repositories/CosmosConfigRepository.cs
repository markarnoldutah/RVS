using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RVS.Infra.AzCosmosRepository.Repositories
{
    public class CosmosConfigRepository : CosmosRepositoryBase, IConfigRepository
    {
        private readonly Container _tenantsContainer;
        private readonly Container _payerConfigsContainer;

        public CosmosConfigRepository(
            CosmosClient client,
            string databaseId,
            string tenantsContainerId,
            string payerConfigsContainerId) : base(client)
        {
            _tenantsContainer = GetContainer(databaseId, tenantsContainerId);
            _payerConfigsContainer = GetContainer(databaseId, payerConfigsContainerId);
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

        // -------------------------
        // Practice payer configs (runtime)
        // -------------------------
        public async Task<List<PayerConfig>> GetPayerConfigsAsync(string tenantId, string practiceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);

            var sql = "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.type = 'payerConfig' AND c.practiceId = @practiceId";
            var queryDef = new QueryDefinition(sql)
                .WithParameter("@tenantId", tenantId)
                .WithParameter("@practiceId", practiceId);

            var options = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(tenantId)
            };

            var iterator = _payerConfigsContainer.GetItemQueryIterator<PayerConfig>(queryDef, requestOptions: options);
            var list = new List<PayerConfig>();

            while (iterator.HasMoreResults)
            {
                var resp = await iterator.ReadNextAsync();
                list.AddRange(resp.Resource);
            }

            return list;
        }

        public async Task<PayerConfig?> GetPayerConfigAsync(string tenantId, string practiceId, string payerId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(payerId);

            var sql = "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.type = 'payerConfig' AND c.practiceId = @practiceId AND c.payerId = @payerId";
            var queryDef = new QueryDefinition(sql)
                .WithParameter("@tenantId", tenantId)
                .WithParameter("@practiceId", practiceId)
                .WithParameter("@payerId", payerId);

            var options = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(tenantId),
                MaxItemCount = 1
            };

            var iterator = _payerConfigsContainer.GetItemQueryIterator<PayerConfig>(queryDef, requestOptions: options);
            if (!iterator.HasMoreResults) return null;

            var resp = await iterator.ReadNextAsync();
            return resp.Resource.FirstOrDefault();
        }

        public async Task SavePayerConfigAsync(string tenantId, PayerConfig payerConfig)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentNullException.ThrowIfNull(payerConfig);
            ArgumentException.ThrowIfNullOrWhiteSpace(payerConfig.PayerId, nameof(payerConfig.PayerId));
            ArgumentException.ThrowIfNullOrWhiteSpace(payerConfig.PracticeId, nameof(payerConfig.PracticeId));

            if (!string.IsNullOrWhiteSpace(payerConfig.TenantId) &&
                !string.Equals(payerConfig.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("PayerConfig TenantId does not match provided tenantId.", nameof(payerConfig));

            await _payerConfigsContainer.UpsertItemAsync(
                payerConfig,
                partitionKey: new PartitionKey(tenantId));
        }
    }
}
