using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories
{
    public class CosmosPayerRepository : CosmosRepositoryBase, IPayerRepository
    {
        private readonly Container _container;

        public CosmosPayerRepository(
            CosmosClient client,
            string databaseId,
            string containerId) : base(client)
        {
            _container = GetContainer(databaseId, containerId);
        }

        public async Task<List<Payer>> SearchAsync(string tenantId, string? planType, string? search)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

            // Build query once
            var sql = "SELECT * FROM c WHERE c.type = 'payer'";
            if (!string.IsNullOrWhiteSpace(planType))
                sql += " AND ARRAY_CONTAINS(c.supportedPlanTypes, @planType)";
            if (!string.IsNullOrWhiteSpace(search))
                sql += " AND CONTAINS(LOWER(c.name), @search)";

            // Execute both partition queries IN PARALLEL
            var globalTask = QueryPartitionAsync("GLOBAL").ToListAsync();
            var tenantTask = QueryPartitionAsync(tenantId).ToListAsync();

            await Task.WhenAll(globalTask.AsTask(), tenantTask.AsTask());

            var results = new List<Payer>();
            results.AddRange(await globalTask);
            results.AddRange(await tenantTask);

            return results;


            // Helpers
            async IAsyncEnumerable<Payer> QueryPartitionAsync(string partitionKey)
            {
                var iterator = _container.GetItemQueryIterator<Payer>(
                    BuildQueryDef(),
                    requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) });

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var item in response.Resource)
                        yield return item;
                }
            }

            QueryDefinition BuildQueryDef()
            {
                var queryDef = new QueryDefinition(sql);

                if (!string.IsNullOrWhiteSpace(planType))
                    queryDef = queryDef.WithParameter("@planType", planType);

                if (!string.IsNullOrWhiteSpace(search))
                    queryDef = queryDef.WithParameter("@search", search.ToLowerInvariant());

                return queryDef;
            }
        }

        public async Task<Payer?> GetByIdAsync(string tenantId, string payerId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(payerId);

            // Try tenant-specific payer first
            try
            {
                var resp = await _container.ReadItemAsync<Payer>(
                    id: payerId,
                    partitionKey: new PartitionKey(tenantId));

                return resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Fall back to GLOBAL payer
                try
                {
                    var resp = await _container.ReadItemAsync<Payer>(
                        id: payerId,
                        partitionKey: new PartitionKey("GLOBAL"));

                    return resp.Resource;
                }
                catch (CosmosException globalEx) when (globalEx.StatusCode == HttpStatusCode.NotFound)
                {
                    // Not found in either partition - this is the only case where null is appropriate
                    return null;
                }
                // Let other CosmosException types (throttling, auth, etc.) bubble up
            }
            // Let other exceptions (network, timeout, etc.) bubble up naturally
        }
    }
}
