namespace RVS.Domain.DTOs;

/// <summary>
/// Typed result payload for a VIN extraction AI operation.
/// Returned inside <see cref="AiOperationResponseDto{T}"/> from the
/// <c>POST api/intake/{locationSlug}/ai/extract-vin</c> endpoint.
/// </summary>
public sealed record VinExtractionResultDto
{
    /// <summary>
    /// The extracted 17-character Vehicle Identification Number,
    /// or <c>null</c> when no VIN was detected in the image.
    /// </summary>
    public string? Vin { get; init; }
}
