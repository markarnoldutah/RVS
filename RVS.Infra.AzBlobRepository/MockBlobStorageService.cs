using Microsoft.Extensions.Logging;
using RVS.Domain.Integrations;

namespace RVS.Infra.AzBlobRepository;

/// <summary>
/// Development mock that returns fake SAS URIs without touching Azure Blob Storage.
/// </summary>
public sealed class MockBlobStorageService : IBlobStorageService
{
    private readonly ILogger<MockBlobStorageService> _logger;

    /// <summary>A 1×1 transparent PNG, returned by <see cref="DownloadAsync"/> so dev packet
    /// renders show a real (tiny) image rather than a broken-photo placeholder.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public MockBlobStorageService(ILogger<MockBlobStorageService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> GenerateUploadSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var fakeSas = $"https://mockblob.blob.core.windows.net/{containerName}/{blobName}?sv=mock&sp=wc&sig=fakesig&se=2099-12-31";
        _logger.LogDebug("MockBlobStorageService returning fake upload SAS URL for {BlobName}", blobName);

        return Task.FromResult(fakeSas);
    }

    /// <inheritdoc />
    public Task<string> GenerateReadSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var fakeSas = $"https://mockblob.blob.core.windows.net/{containerName}/{blobName}?sv=mock&sp=r&sig=fakesig&se=2099-12-31";
        _logger.LogDebug("MockBlobStorageService returning fake read SAS URL for {BlobName}", blobName);

        return Task.FromResult(fakeSas);
    }

    /// <inheritdoc />
    public Task<string> GenerateReadSasUrlAsync(string containerName, string blobName, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return GenerateReadSasUrlAsync(containerName, blobName, cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        _logger.LogDebug("MockBlobStorageService returning fake bytes for {BlobName}", blobName);

        return Task.FromResult(OnePixelPng);
    }

    /// <inheritdoc />
    public Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var fakeUri = $"https://mockblob.blob.core.windows.net/{containerName}/{blobName}";
        _logger.LogDebug("MockBlobStorageService pretending to upload {BlobName} ({ContentType})", blobName, contentType);

        return Task.FromResult(fakeUri);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        _logger.LogDebug("MockBlobStorageService pretending to delete {BlobName}", blobName);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> BlobExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        _logger.LogDebug("MockBlobStorageService pretending blob {BlobName} exists", blobName);

        return Task.FromResult(true);
    }
}
