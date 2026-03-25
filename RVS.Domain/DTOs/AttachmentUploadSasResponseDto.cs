namespace RVS.Domain.DTOs;

/// <summary>
/// Response containing a pre-signed SAS URL for direct client-to-blob upload
/// and the blob name needed for the subsequent confirm step.
/// </summary>
public sealed record AttachmentUploadSasResponseDto
{
    public string SasUrl { get; init; } = default!;
    public string BlobName { get; init; } = default!;
    public DateTime ExpiresAtUtc { get; init; }
}
