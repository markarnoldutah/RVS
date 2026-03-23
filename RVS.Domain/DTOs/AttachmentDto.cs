namespace RVS.Domain.DTOs;

/// <summary>
/// Attachment metadata for a file associated with a service request.
/// </summary>
public sealed record AttachmentDto
{
    public string AttachmentId { get; init; } = default!;
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }
    public string BlobUri { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
}
