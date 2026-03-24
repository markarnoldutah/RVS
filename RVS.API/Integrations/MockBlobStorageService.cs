using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Development mock that returns fake SAS URIs without touching Azure Blob Storage.
/// </summary>
public sealed class MockBlobStorageService : IBlobStorageService
{
    private readonly ILogger<MockBlobStorageService> _logger;

    public MockBlobStorageService(ILogger<MockBlobStorageService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> GenerateSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var fakeSas = $"https://mockblob.blob.core.windows.net/{containerName}/{blobName}?sv=mock&sig=fakesig&se=2099-12-31";
        _logger.LogDebug("MockBlobStorageService returning fake SAS URL for {BlobName}", blobName);

        return Task.FromResult(fakeSas);
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
}
