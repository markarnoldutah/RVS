using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Typed HTTP client for lookup data operations against the RVS API.
/// Routes map to <c>api/lookups/{lookupSetId}</c>.
/// </summary>
public sealed class LookupApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="LookupApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The configured <see cref="HttpClient"/> injected via DI.</param>
    public LookupApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets a lookup set by its identifier (e.g., "issue-categories", "component-types").
    /// </summary>
    public async Task<LookupSetDto> GetLookupAsync(
        string lookupSetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupSetId);

        return await _httpClient.GetFromJsonAsync<LookupSetDto>(
            $"api/lookups/{Uri.EscapeDataString(lookupSetId)}",
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize lookup response.");
    }

    /// <summary>
    /// Gets all locations for the authenticated user's tenant.
    /// </summary>
    public async Task<List<LocationSummaryResponseDto>> GetLocationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<LocationSummaryResponseDto>>(
            "api/locations",
            cancellationToken) ?? [];
    }

    /// <summary>
    /// Gets a location by its identifier.
    /// </summary>
    public async Task<LocationDetailDto> GetLocationByIdAsync(
        string locationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        return await _httpClient.GetFromJsonAsync<LocationDetailDto>(
            $"api/locations/{Uri.EscapeDataString(locationId)}",
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize location response.");
    }

    /// <summary>
    /// Gets all dealerships for the authenticated user's tenant.
    /// </summary>
    public async Task<List<DealershipSummaryDto>> GetDealershipsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<DealershipSummaryDto>>(
            "api/dealerships",
            cancellationToken) ?? [];
    }

    /// <summary>
    /// Gets a dealership by its identifier.
    /// </summary>
    public async Task<DealershipDetailDto> GetDealershipByIdAsync(
        string dealershipId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);

        return await _httpClient.GetFromJsonAsync<DealershipDetailDto>(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}",
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize dealership response.");
    }

    /// <summary>
    /// Creates a new location.
    /// </summary>
    public async Task<LocationDetailDto> CreateLocationAsync(
        LocationCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PostAsJsonAsync("api/locations", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<LocationDetailDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize location create response.");
    }

    /// <summary>
    /// Updates an existing location.
    /// </summary>
    public async Task<LocationDetailDto> UpdateLocationAsync(
        string locationId,
        LocationCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PutAsJsonAsync(
            $"api/locations/{Uri.EscapeDataString(locationId)}",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<LocationDetailDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize location update response.");
    }

    /// <summary>
    /// Gets the QR code image (PNG) for a location's intake form.
    /// </summary>
    public async Task<byte[]> GetLocationQrCodeAsync(
        string locationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        return await _httpClient.GetByteArrayAsync(
            $"api/locations/{Uri.EscapeDataString(locationId)}/qr-code",
            cancellationToken);
    }

    /// <summary>
    /// Gets the current tenant configuration.
    /// </summary>
    public async Task<TenantConfigResponseDto> GetTenantConfigAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<TenantConfigResponseDto>(
            "api/tenants/config",
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize tenant config response.");
    }

    /// <summary>
    /// Updates the tenant configuration.
    /// </summary>
    public async Task<TenantConfigResponseDto> UpdateTenantConfigAsync(
        TenantConfigUpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PutAsJsonAsync("api/tenants/config", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TenantConfigResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize tenant config update response.");
    }

    /// <summary>
    /// Gets the tenant access gate status.
    /// </summary>
    public async Task<AccessGateStatusDto> GetAccessGateStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<AccessGateStatusDto>(
            "api/tenants/access-gate",
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize access gate response.");
    }
}
