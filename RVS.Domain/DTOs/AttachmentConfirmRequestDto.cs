namespace RVS.Domain.DTOs;

/// <summary>
/// Request to confirm a direct-upload attachment after the client has uploaded the blob via SAS URL.
/// </summary>
public sealed record AttachmentConfirmRequestDto
{
    /// <summary>Blob name returned by the upload-url endpoint.</summary>
    public string BlobName { get; init; } = default!;

    /// <summary>Original file name (e.g. "photo.jpg").</summary>
    public string FileName { get; init; } = default!;

    /// <summary>MIME content type (e.g. "image/jpeg").</summary>
    public string ContentType { get; init; } = default!;

    /// <summary>File size in bytes as reported by the client.</summary>
    public long SizeBytes { get; init; }
}
