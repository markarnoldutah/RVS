namespace RVS.Domain.DTOs;

/// <summary>
/// Request DTO for the VIN extraction from photo endpoint.
/// Sent by the client as a JSON body to <c>POST api/intake/{locationSlug}/ai/extract-vin</c>.
/// </summary>
public sealed record VinExtractionRequestDto
{
    /// <summary>Base64-encoded image data (without the data URL prefix).</summary>
    public required string ImageBase64 { get; init; }

    /// <summary>MIME content type of the image (e.g. "image/jpeg", "image/png").</summary>
    public required string ContentType { get; init; }
}
