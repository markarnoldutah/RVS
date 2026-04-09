using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using RVS.Domain.Integrations;

namespace RVS.Infra.AzBlobRepository;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IBlobStorageService"/>.
/// Generates SAS URLs for upload (15 min, Write/Create) and read (1 hr, Read) with tenant-scoped blob paths.
/// Blob path format: {tenantId}/{locationId}/{srId}/{attId}_{filename}
/// Assumes containers already exist — they are provisioned as part of infrastructure deployment.
/// </summary>
public sealed class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;

    private static readonly TimeSpan UploadSasDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReadSasDuration = TimeSpan.FromHours(1);

    public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GenerateUploadSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var userDelegationKey = await GetUserDelegationKeyAsync(cancellationToken);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.Add(UploadSasDuration)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var uriBuilder = new BlobUriBuilder(blobClient.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, _blobServiceClient.AccountName)
        };

        _logger.LogDebug("Generated upload SAS URL for blob {BlobName} in container {ContainerName}", blobName, containerName);

        return uriBuilder.ToUri().ToString();
    }

    /// <inheritdoc />
    public async Task<string> GenerateReadSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var userDelegationKey = await GetUserDelegationKeyAsync(cancellationToken);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.Add(ReadSasDuration)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var uriBuilder = new BlobUriBuilder(blobClient.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, _blobServiceClient.AccountName)
        };

        _logger.LogDebug("Generated read SAS URL for blob {BlobName} in container {ContainerName}", blobName, containerName);

        return uriBuilder.ToUri().ToString();
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

        _logger.LogDebug("Uploaded blob {BlobName} to container {ContainerName}", blobName, containerName);

        return blobClient.Uri.ToString();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        _logger.LogDebug("Deleted blob {BlobName} from container {ContainerName}", blobName, containerName);
    }

    /// <inheritdoc />
    public async Task<bool> BlobExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        var response = await blobClient.ExistsAsync(cancellationToken);

        return response.Value;
    }

    /// <summary>
    /// Requests a short-lived user delegation key from the storage account.
    /// The key is used to sign SAS tokens without requiring a storage account key.
    /// </summary>
    private async Task<UserDelegationKey> GetUserDelegationKeyAsync(CancellationToken ct)
    {
        var expiry = DateTimeOffset.UtcNow.Add(UploadSasDuration);
        var response = await _blobServiceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow, expiry, ct);

        return response.Value;
    }
}
