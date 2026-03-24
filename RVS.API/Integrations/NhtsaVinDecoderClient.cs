using System.Text.Json.Serialization;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Decodes VINs using the NHTSA vPIC public API.
/// Returns <c>null</c> on timeout or HTTP error — never throws.
/// </summary>
public sealed class NhtsaVinDecoderClient : IVinDecoderService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NhtsaVinDecoderClient> _logger;

    public NhtsaVinDecoderClient(HttpClient httpClient, ILogger<NhtsaVinDecoderClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VinDecoderResult?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<NhtsaApiResponse>(
                $"vehicles/DecodeVinValues/{vin}?format=json", cancellationToken);

            var result = response?.Results?.FirstOrDefault();
            if (result is null || string.IsNullOrWhiteSpace(result.Make))
            {
                _logger.LogWarning("NHTSA VIN decode returned no results for VIN ending {VinSuffix}", vin[^4..]);
                return null;
            }

            _ = int.TryParse(result.ModelYear, out var year);

            return new VinDecoderResult(vin, result.Make, result.Model ?? string.Empty, year);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "NHTSA VIN decode failed for VIN ending {VinSuffix}", vin[^4..]);
            return null;
        }
    }

    /// <summary>
    /// Top-level NHTSA vPIC API response.
    /// </summary>
    internal sealed class NhtsaApiResponse
    {
        [JsonPropertyName("Results")]
        public List<NhtsaVehicle>? Results { get; set; }
    }

    /// <summary>
    /// Individual vehicle record from the NHTSA vPIC DecodeVinValues endpoint.
    /// </summary>
    internal sealed class NhtsaVehicle
    {
        [JsonPropertyName("Make")]
        public string? Make { get; set; }

        [JsonPropertyName("Model")]
        public string? Model { get; set; }

        [JsonPropertyName("ModelYear")]
        public string? ModelYear { get; set; }

        [JsonPropertyName("Manufacturer")]
        public string? Manufacturer { get; set; }
    }
}
