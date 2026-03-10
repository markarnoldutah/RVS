using Microsoft.Azure.Cosmos;

namespace RVS.Infra.AzCosmosRepository.Repositories
{
    public abstract class CosmosRepositoryBase
    {
        protected readonly CosmosClient Client;

        protected CosmosRepositoryBase(CosmosClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        protected Container GetContainer(string databaseId, string containerId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databaseId);
            ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

            return Client.GetContainer(databaseId, containerId);
        }
    }
}
