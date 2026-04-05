namespace RVS.Domain.Integrations;

/// <summary>
/// Extracts a Vehicle Identification Number (VIN) from a photo using AI vision capabilities.
/// </summary>
public interface IVinExtractionService
{
    /// <summary>
    /// Analyzes the provided image and attempts to extract a 17-character VIN.
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
/// <param name="Vin">The extracted 17-character Vehicle Identification Number.</param>
/// <param name="Confidence">Confidence score in the range 0.0–1.0.</param>
/// <param name="Provider">Identifier for the service implementation that fulfilled the request.</param>
public sealed record VinExtractionResult(string Vin, double Confidence, string Provider);
