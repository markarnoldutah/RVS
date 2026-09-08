namespace RVS.Domain.Integrations;

/// <summary>
/// Provides SAS token generation and blob upload operations for Azure Blob Storage.
/// Used for service request attachments (photos, documents).
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Generates a time-limited SAS URL that grants write/create access to a specific blob (15-minute expiry).
    /// Used for direct client-side uploads during intake.
    /// </summary>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="blobName">Target blob name (typically includes a unique prefix).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pre-signed SAS URL for direct upload.</returns>
    Task<string> GenerateUploadSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a time-limited SAS URL that grants read access to a specific blob (1-hour expiry).
    /// Used for staff viewing of existing attachments.
    /// </summary>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="blobName">Target blob name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pre-signed SAS URL for read access.</returns>
    Task<string> GenerateReadSasUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a read SAS URL with an explicit lifetime. Used by service-packet composition,
    /// where the URL is embedded in an emailed HTML packet and must stay valid long enough for a
    /// service department to open the mail (<c>Spec X-6</c>, issue #433). The lifetime must be
    /// positive and, because it is signed with a user delegation key, cannot exceed 7 days.
    /// </summary>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="blobName">Target blob name.</param>
    /// <param name="lifetime">How long the SAS URL remains valid from now.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pre-signed SAS URL for read access.</returns>
    Task<string> GenerateReadSasUrlAsync(string containerName, string blobName, TimeSpan lifetime, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Deletes a blob from storage.
    /// </summary>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="blobName">Target blob name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a blob exists in storage.
    /// </summary>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="blobName">Target blob name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the blob exists, otherwise false.</returns>
    Task<bool> BlobExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
}
