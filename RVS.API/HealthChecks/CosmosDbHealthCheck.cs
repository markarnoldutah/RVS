using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RVS.API.HealthChecks;

/// <summary>
/// Health check that validates connectivity to Azure Cosmos DB.
/// </summary>
public sealed class CosmosDbHealthCheck : IHealthCheck
{
    private readonly CosmosClient _cosmosClient;

    public CosmosDbHealthCheck(CosmosClient cosmosClient)
    {
        _cosmosClient = cosmosClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _cosmosClient.ReadAccountAsync();
            return HealthCheckResult.Healthy("Cosmos DB reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cosmos DB unreachable.", ex);
        }
    }
}
