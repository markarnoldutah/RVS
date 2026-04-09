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
            // Use a data-plane list call — works with Storage Blob Data Reader (and above)
            // without requiring ARM-level Storage Account Contributor.
            await foreach (var _ in _blobServiceClient
                .GetBlobContainersAsync(cancellationToken: cancellationToken)
                .AsPages(pageSizeHint: 1)
                .WithCancellation(cancellationToken))
            {
                break;
            }

            return HealthCheckResult.Healthy("Blob Storage reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Blob Storage unreachable.", ex);
        }
    }
}
