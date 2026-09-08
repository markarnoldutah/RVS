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

    /// <summary>Azure caps a user delegation key — and therefore any SAS it signs — at 7 days.</summary>
    private static readonly TimeSpan MaxReadSasDuration = TimeSpan.FromDays(7);

    /// <summary>Backdates the SAS/key start to absorb clock skew between this host and Azure.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

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

        var startsOn = DateTimeOffset.UtcNow.Subtract(ClockSkew);
        var expiresOn = DateTimeOffset.UtcNow.Add(UploadSasDuration);

        var userDelegationKey = await GetUserDelegationKeyAsync(startsOn, expiresOn, cancellationToken);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = startsOn,
            ExpiresOn = expiresOn
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
    public Task<string> GenerateReadSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default) =>
        GenerateReadSasUrlAsync(containerName, blobName, ReadSasDuration, cancellationToken);

    /// <inheritdoc />
    public async Task<string> GenerateReadSasUrlAsync(string containerName, string blobName, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lifetime, MaxReadSasDuration);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var startsOn = DateTimeOffset.UtcNow.Subtract(ClockSkew);
        var expiresOn = startsOn.Add(lifetime);

        // Azure requires the SAS interval to sit inside the signing key's interval, and caps
        // a user delegation key at 7 days from its start. The lifetime guard above keeps
        // expiresOn within that ceiling, so the key can cover the SAS exactly.
        var userDelegationKey = await GetUserDelegationKeyAsync(startsOn, expiresOn, cancellationToken);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = startsOn,
            ExpiresOn = expiresOn
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var uriBuilder = new BlobUriBuilder(blobClient.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, _blobServiceClient.AccountName)
        };

        _logger.LogDebug(
            "Generated read SAS URL for blob {BlobName} in container {ContainerName} valid for {Lifetime}",
            blobName, containerName, lifetime);

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
    /// Requests a user delegation key from the storage account, valid for the same window as
    /// the SAS it will sign. The key lets SAS tokens be signed without a storage account key;
    /// Azure requires the SAS start/expiry to fall inside the key's validity.
    /// </summary>
    private async Task<UserDelegationKey> GetUserDelegationKeyAsync(
        DateTimeOffset startsOn, DateTimeOffset expiresOn, CancellationToken ct)
    {
        var response = await _blobServiceClient.GetUserDelegationKeyAsync(startsOn, expiresOn, ct);

        return response.Value;
    }
}
