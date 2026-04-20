using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Typed HTTP client for service request operations against the RVS API.
/// Routes map to <c>api/dealerships/{dealershipId}/service-requests</c>.
/// </summary>
public sealed class ServiceRequestApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ServiceRequestApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The configured <see cref="HttpClient"/> injected via DI.</param>
    public ServiceRequestApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Creates a new service request.
    /// </summary>
    public async Task<ServiceRequestDetailResponseDto> CreateAsync(
        string dealershipId,
        ServiceRequestCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PostAsJsonAsync(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ServiceRequestDetailResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize create response.");
    }

    /// <summary>
    /// Gets a single service request by ID.
    /// </summary>
    public async Task<ServiceRequestDetailResponseDto> GetByIdAsync(
        string dealershipId,
        string serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        return await _httpClient.GetFromJsonAsync<ServiceRequestDetailResponseDto>(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}",
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize service request response.");
    }

    /// <summary>
    /// Searches service requests with filter criteria.
    /// </summary>
    public async Task<ServiceRequestSearchResultResponseDto> SearchAsync(
        string dealershipId,
        ServiceRequestSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PostAsJsonAsync(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests/search",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ServiceRequestSearchResultResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize search response.");
    }

    /// <summary>
    /// Updates an existing service request.
    /// </summary>
    public async Task<ServiceRequestDetailResponseDto> UpdateAsync(
        string dealershipId,
        string serviceRequestId,
        ServiceRequestUpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PutAsJsonAsync(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ServiceRequestDetailResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize update response.");
    }

    /// <summary>
    /// Applies outcome fields to multiple service requests in a single batch.
    /// </summary>
    public async Task<BatchOutcomeResponseDto> BatchOutcomeAsync(
        string dealershipId,
        BatchOutcomeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PatchAsJsonAsync(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests/batch-outcome",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BatchOutcomeResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize batch outcome response.");
    }

    /// <summary>
    /// Deletes a service request.
    /// </summary>
    public async Task DeleteAsync(
        string dealershipId,
        string serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        var response = await _httpClient.DeleteAsync(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
