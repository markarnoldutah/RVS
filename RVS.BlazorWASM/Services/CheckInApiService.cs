using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Services;

/// <summary>
/// API service for patient check-in operations.
/// </summary>
public interface ICheckInApiService
{
    /// <summary>
    /// Performs the combined check-in operation (patient upsert, coverage upsert, 
    /// encounter creation, and eligibility checks).
    /// </summary>
    Task<PatientCheckInResponseDto> CheckInAsync(
        string practiceId,
        PatientCheckInRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the coverage decision for an encounter.
    /// </summary>
    Task<CoverageDecisionResponseDto> UpdateCoverageDecisionAsync(
        string practiceId,
        string patientId,
        string encounterId,
        CoverageDecisionUpdateRequestDto request,
        CancellationToken cancellationToken = default);
}

public class CheckInApiService : ICheckInApiService
{
    private readonly HttpClient _httpClient;

    public CheckInApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PatientCheckInResponseDto> CheckInAsync(
        string practiceId,
        PatientCheckInRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/practices/{practiceId}/check-in";

        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PatientCheckInResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize check-in response");
    }

    public async Task<CoverageDecisionResponseDto> UpdateCoverageDecisionAsync(
        string practiceId,
        string patientId,
        string encounterId,
        CoverageDecisionUpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/coverage-decision";

        var response = await _httpClient.PutAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CoverageDecisionResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize coverage decision response");
    }
}
