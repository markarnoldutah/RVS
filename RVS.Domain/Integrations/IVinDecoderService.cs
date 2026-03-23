namespace RVS.Domain.Integrations;

/// <summary>
/// Decodes a Vehicle Identification Number (VIN) using the NHTSA vPIC API to
/// retrieve manufacturer, model, and year information.
/// </summary>
public interface IVinDecoderService
{
    /// <summary>
    /// Decodes a VIN and returns the resolved vehicle details.
    /// </summary>
    /// <param name="vin">17-character Vehicle Identification Number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded vehicle information, or <c>null</c> if the VIN could not be resolved.</returns>
    Task<VinDecoderResult?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a VIN decode operation from the NHTSA vPIC API.
/// </summary>
/// <param name="Vin">The original VIN that was decoded.</param>
/// <param name="Manufacturer">Vehicle manufacturer name.</param>
/// <param name="Model">Vehicle model name.</param>
/// <param name="Year">Model year.</param>
public sealed record VinDecoderResult(string Vin, string Manufacturer, string Model, int Year);
