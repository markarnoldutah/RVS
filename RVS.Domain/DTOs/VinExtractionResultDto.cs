namespace RVS.Domain.DTOs;

/// <summary>
/// Typed result payload for a VIN extraction AI operation.
/// Returned inside <see cref="AiOperationResponseDto{T}"/> from the
/// <c>POST api/intake/{locationSlug}/ai/extract-vin</c> endpoint.
/// </summary>
public sealed record VinExtractionResultDto
{
    /// <summary>
    /// The extracted 17-character Vehicle Identification Number, the serial number of a rig
    /// that has no VIN (issue #807), or <c>null</c> when neither was detected in the image.
    /// </summary>
    public string? Vin { get; init; }

    /// <summary>The manufacturer printed on the plate, if any. Pre-fills Step 4; the customer can override it.</summary>
    public string? Manufacturer { get; init; }

    /// <summary>The model printed on the plate, if any. Pre-fills Step 4; the customer can override it.</summary>
    public string? Model { get; init; }

    /// <summary>The model year, or failing that the year of manufacture, if the plate shows one.</summary>
    public int? Year { get; init; }
}
