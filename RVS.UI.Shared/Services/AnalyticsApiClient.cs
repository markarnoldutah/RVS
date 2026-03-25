using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Typed HTTP client for analytics operations against the RVS API.
/// Routes map to <c>api/dealerships/{dealershipId}/analytics</c>.
/// </summary>
public sealed class AnalyticsApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="AnalyticsApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The configured <see cref="HttpClient"/> injected via DI.</param>
    public AnalyticsApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the service request analytics summary for a dealership within an optional date range
    /// and location filter.
    /// </summary>
    /// <param name="dealershipId">The dealership identifier.</param>
    /// <param name="from">Optional start date for the analytics window.</param>
    /// <param name="to">Optional end date for the analytics window.</param>
    /// <param name="locationId">Optional location filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ServiceRequestAnalyticsResponseDto> GetServiceRequestSummaryAsync(
        string dealershipId,
        DateTime? from = null,
        DateTime? to = null,
        string? locationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);

        var queryParams = new List<string>();
        if (from.HasValue)
        {
            queryParams.Add($"from={from.Value:O}");
        }
        if (to.HasValue)
        {
            queryParams.Add($"to={to.Value:O}");
        }
        if (!string.IsNullOrWhiteSpace(locationId))
        {
            queryParams.Add($"locationId={Uri.EscapeDataString(locationId)}");
        }

        var url = $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/analytics/service-requests/summary";
        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        return await _httpClient.GetFromJsonAsync<ServiceRequestAnalyticsResponseDto>(
            url,
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize analytics response.");
    }
}
