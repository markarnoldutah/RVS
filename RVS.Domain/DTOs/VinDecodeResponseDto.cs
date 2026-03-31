namespace RVS.Domain.DTOs;

/// <summary>
/// Response DTO for a VIN decode operation.
/// Returned by the intake VIN lookup endpoint.
/// </summary>
public sealed record VinDecodeResponseDto
{
    /// <summary>The original VIN that was decoded.</summary>
    public required string Vin { get; init; }

    /// <summary>Vehicle manufacturer name (e.g. "Grand Design").</summary>
    public required string Manufacturer { get; init; }

    /// <summary>Vehicle model name (e.g. "Momentum 395MS").</summary>
    public required string Model { get; init; }

    /// <summary>Model year.</summary>
    public required int Year { get; init; }
}
