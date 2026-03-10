using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories
{
    public class CosmosPracticeRepository : CosmosRepositoryBase, IPracticeRepository
    {
        private readonly Container _container;

        public CosmosPracticeRepository(
            CosmosClient client,
            string databaseId,
            string containerId) : base(client)
        {
            _container = GetContainer(databaseId, containerId);
        }

        public async Task<List<Practice>> GetPracticesForTenantAsync(string tenantId, bool includeLocations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

            var sql = "SELECT * FROM c WHERE c.tenantId = @tenantId";
            var query = new QueryDefinition(sql)
                .WithParameter("@tenantId", tenantId);

            var options = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(tenantId)
            };

            var iterator = _container.GetItemQueryIterator<Practice>(query, requestOptions: options);
            var results = new List<Practice>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            // Invariant: ensure tenant scope is consistent for returned entities.
            results.RemoveAll(p => !string.Equals(p.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));

            // You can strip locations if !includeLocations here
            if (!includeLocations)
            {
                foreach (var p in results)
                {
                    // p.Locations = null; // if you want to trim payload
                }
            }

            return results;
        }

        public async Task<Practice?> GetByIdAsync(string tenantId, string practiceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);

            try
            {
                var resp = await _container.ReadItemAsync<Practice>(
                    id: practiceId,
                    partitionKey: new PartitionKey(tenantId));

                var practice = resp.Resource;

                // PK is tenantId, but keep a defensive invariant check.
                if (!string.Equals(practice.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    return null;

                return practice;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
}
