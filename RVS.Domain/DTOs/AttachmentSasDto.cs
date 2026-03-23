namespace RVS.Domain.DTOs;

/// <summary>
/// SAS URL for time-limited read access to an attachment blob.
/// </summary>
public sealed record AttachmentSasDto
{
    public string SasUrl { get; init; } = default!;
    public DateTime ExpiresAtUtc { get; init; }
}
