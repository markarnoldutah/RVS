using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Services;

/// <summary>
/// API service for lookup data (visit types, payers, locations, etc.).
/// Note: This service is used internally by <see cref="LookupCacheService"/>.
/// Prefer injecting <see cref="ILookupCacheService"/> in components to avoid redundant API calls.
/// </summary>
public interface ILookupApiService
{
    Task<List<LookupItemDto>> GetVisitTypesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetPlanTypesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetRelationshipTypesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetCobReasonsAsync(CancellationToken cancellationToken = default);
    Task<List<PayerResponseDto>> GetPayersAsync(CancellationToken cancellationToken = default);
    Task<List<LocationSummaryResponseDto>> GetLocationsAsync(string practiceId, CancellationToken cancellationToken = default);
}

public class LookupApiService : ILookupApiService
{
    private readonly HttpClient _httpClient;

    public LookupApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<LookupItemDto>> GetVisitTypesAsync(CancellationToken cancellationToken = default)
    {
        var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
            "api/lookups/visit-types", cancellationToken);
        return lookupSet?.Items.ToList() ?? [];
    }

    public async Task<List<LookupItemDto>> GetPlanTypesAsync(CancellationToken cancellationToken = default)
    {
        var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
            "api/lookups/plan-types", cancellationToken);
        return lookupSet?.Items.ToList() ?? [];
    }

    public async Task<List<LookupItemDto>> GetRelationshipTypesAsync(CancellationToken cancellationToken = default)
    {
        var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
            "api/lookups/relationship-types", cancellationToken);
        return lookupSet?.Items.ToList() ?? [];
    }

    public async Task<List<LookupItemDto>> GetCobReasonsAsync(CancellationToken cancellationToken = default)
    {
        var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
            "api/lookups/cob-reasons", cancellationToken);
        return lookupSet?.Items.ToList() ?? [];
    }

    public async Task<List<PayerResponseDto>> GetPayersAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<PayerResponseDto>>(
            "api/payers", cancellationToken) ?? [];
    }

    public async Task<List<LocationSummaryResponseDto>> GetLocationsAsync(
        string practiceId,
        CancellationToken cancellationToken = default)
    {
        // Get practice detail which includes locations
        var practice = await _httpClient.GetFromJsonAsync<PracticeDetailResponseDto>(
            $"api/practices/{practiceId}", cancellationToken);
        
        return practice?.Locations ?? [];
    }
}
