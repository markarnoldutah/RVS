using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Development mock that returns Grand Design Momentum data for any VIN.
/// </summary>
public sealed class MockVinDecoderService : IVinDecoderService
{
    private readonly ILogger<MockVinDecoderService> _logger;

    public MockVinDecoderService(ILogger<MockVinDecoderService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<VinDecoderResult?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        _logger.LogDebug("MockVinDecoderService returning Grand Design Momentum for VIN ending {VinSuffix}", vin[^4..]);

        var result = new VinDecoderResult(vin, "Grand Design", "Momentum 395MS", 2024);
        return Task.FromResult<VinDecoderResult?>(result);
    }
}
