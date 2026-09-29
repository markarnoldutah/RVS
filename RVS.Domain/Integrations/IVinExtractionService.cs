namespace RVS.Domain.Integrations;

/// <summary>
/// Extracts a Vehicle Identification Number (VIN) from a photo using AI vision capabilities —
/// or, for a rig with no VIN such as a truck camper, its serial number (issue #807).
/// </summary>
public interface IVinExtractionService
{
    /// <summary>
    /// Analyzes the provided image and attempts to extract a 17-character VIN, or a serial number
    /// when there is no VIN. The result passes <c>VehicleIdentifierValidator</c>.
    /// </summary>
    /// <param name="imageData">Raw image bytes.</param>
    /// <param name="contentType">MIME content type of the image (e.g. "image/jpeg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Extraction result with VIN and confidence score, or <c>null</c> if extraction failed
    /// or no VIN was detected. Never throws — callers should fall back to manual entry on <c>null</c>.
    /// </returns>
    Task<VinExtractionResult?> ExtractVinFromImageAsync(byte[] imageData, string contentType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a VIN extraction operation from an image.
/// </summary>
/// <param name="Vin">The extracted 17-character VIN, or the serial number of a rig that has no VIN.</param>
/// <param name="Confidence">Confidence score in the range 0.0–1.0.</param>
/// <param name="Provider">Identifier for the service implementation that fulfilled the request.</param>
/// <param name="Manufacturer">The manufacturer printed on the plate, if any — a Step 4 prefill the customer can override.</param>
/// <param name="Model">The model printed on the plate, if any — a Step 4 prefill the customer can override.</param>
/// <param name="Year">The model year, or failing that the year of manufacture, if the plate shows one.</param>
public sealed record VinExtractionResult(
    string Vin,
    double Confidence,
    string Provider,
    string? Manufacturer = null,
    string? Model = null,
    int? Year = null);
