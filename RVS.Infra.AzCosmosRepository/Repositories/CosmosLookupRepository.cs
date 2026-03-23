using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories
{

    public class CosmosLookupRepository : CosmosRepositoryBase, ILookupRepository
    {
        // TODO - Consider caching these lookups in memory for performance

        private const string GlobalTenantId = "GLOBAL";

        private readonly Container _container;

        public CosmosLookupRepository(CosmosClient client, 
            string databaseId, 
            string containerId) : base(client)
        {
            _container = client.GetContainer(databaseId, containerId);
        }

        public async Task<LookupSet?> GetGlobalAsync(string lookupSetId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lookupSetId);

            try
            {
                var response = await _container.ReadItemAsync<LookupSet>(
                    id: lookupSetId,
                    partitionKey: new PartitionKey(GlobalTenantId),
                    cancellationToken: cancellationToken);

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertGlobalAsync(LookupSet entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

            // Ensure global partition + updated timestamp are consistent at the boundary.
            // Note: entity.TenantId is init-only; lookup sets are stored under the GLOBAL partition key.
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _container.UpsertItemAsync(
                item: entity,
                partitionKey: new PartitionKey(GlobalTenantId),
                cancellationToken: cancellationToken);
        }

        // TODO CreateGlobalAsync()


        // Future: GetByTenantAsync and merge logic when overrides are enabled.
    }
}
