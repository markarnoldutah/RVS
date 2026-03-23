namespace RVS.Domain.Integrations;

/// <summary>
/// Provides SAS token generation and blob upload operations for Azure Blob Storage.
/// Used for service request attachments (photos, documents).
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Generates a time-limited SAS URL that grants upload access to a specific blob.
    /// </summary>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="blobName">Target blob name (typically includes a unique prefix).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pre-signed SAS URL for direct upload.</returns>
    Task<string> GenerateSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a blob from a stream and returns the resulting blob URI.
    /// </summary>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="blobName">Target blob name.</param>
    /// <param name="content">Stream containing the blob content.</param>
    /// <param name="contentType">MIME type of the content (e.g. "image/jpeg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URI of the uploaded blob.</returns>
    Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);
}
