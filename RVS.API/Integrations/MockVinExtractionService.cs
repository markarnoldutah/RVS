using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Development mock that returns a hardcoded valid VIN with high confidence for any image.
/// </summary>
public sealed class MockVinExtractionService : IVinExtractionService
{
    private const string MockVin = "1RGDE4428R1000001";
    private const double MockConfidence = 0.95;

    private readonly ILogger<MockVinExtractionService> _logger;

    public MockVinExtractionService(ILogger<MockVinExtractionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<VinExtractionResult?> ExtractVinFromImageAsync(byte[] imageData, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        _logger.LogDebug("MockVinExtractionService returning hardcoded VIN {Vin} for image ({Bytes} bytes)",
            MockVin, imageData.Length);

        var result = new VinExtractionResult(MockVin, MockConfidence, nameof(MockVinExtractionService));
        return Task.FromResult<VinExtractionResult?>(result);
    }
}
