using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RVS.API.HealthChecks;

/// <summary>
/// Health check that validates connectivity to Azure Blob Storage.
/// </summary>
public sealed class BlobStorageHealthCheck : IHealthCheck
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageHealthCheck(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _blobServiceClient.GetPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy("Blob Storage reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Blob Storage unreachable.", ex);
        }
    }
}
