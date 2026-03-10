using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Services;

/// <summary>
/// API service for eligibility check operations.
/// </summary>
public interface IEligibilityCheckApiService
{
    /// <summary>
    /// Gets all eligibility checks for an encounter (no polling).
    /// RU Cost: ~1 RU
    /// </summary>
    Task<List<EligibilityCheckSummaryResponseDto>> GetEligibilityChecksAsync(
        string practiceId,
        string patientId,
        string encounterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets eligibility checks with selective polling.
    /// RU Cost: ~1 RU + ~1.5 RU per check ID
    /// </summary>
    Task<List<EligibilityCheckSummaryResponseDto>> GetEligibilityChecksWithPollingAsync(
        string practiceId,
        string patientId,
        string encounterId,
        List<string> checkIdsToPoll,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a new eligibility check.
    /// RU Cost: ~2 RU
    /// </summary>
    Task<EligibilityCheckResponseDto> RunEligibilityCheckAsync(
        string practiceId,
        string patientId,
        string encounterId,
        EligibilityCheckRequestDto request,
        CancellationToken cancellationToken = default);
}

public class EligibilityCheckApiService : IEligibilityCheckApiService
{
    private readonly HttpClient _httpClient;

    public EligibilityCheckApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<EligibilityCheckSummaryResponseDto>> GetEligibilityChecksAsync(
        string practiceId,
        string patientId,
        string encounterId,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks";

        var response = await _httpClient.GetFromJsonAsync<List<EligibilityCheckSummaryResponseDto>>(
            url,
            cancellationToken);

        return response ?? [];
    }

    public async Task<List<EligibilityCheckSummaryResponseDto>> GetEligibilityChecksWithPollingAsync(
        string practiceId,
        string patientId,
        string encounterId,
        List<string> checkIdsToPoll,
        CancellationToken cancellationToken = default)
    {
        if (checkIdsToPoll == null || checkIdsToPoll.Count == 0)
        {
            return await GetEligibilityChecksAsync(practiceId, patientId, encounterId, cancellationToken);
        }

        var checkIdsParam = string.Join(",", checkIdsToPoll);
        var url = $"api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks?pollCheckIds={checkIdsParam}";

        var response = await _httpClient.GetFromJsonAsync<List<EligibilityCheckSummaryResponseDto>>(
            url,
            cancellationToken);

        return response ?? [];
    }

    public async Task<EligibilityCheckResponseDto> RunEligibilityCheckAsync(
        string practiceId,
        string patientId,
        string encounterId,
        EligibilityCheckRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks/run";

        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<EligibilityCheckResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }
}
