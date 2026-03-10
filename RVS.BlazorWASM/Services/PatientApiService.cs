using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Services;

/// <summary>
/// API service for patient operations.
/// </summary>
public interface IPatientApiService
{
    /// <summary>
    /// Searches for patients matching the given criteria.
    /// </summary>
    Task<PagedResult<PatientSearchResultResponseDto>> SearchPatientsAsync(
        string practiceId,
        PatientSearchRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full details of a patient including coverage enrollments.
    /// </summary>
    Task<PatientDetailResponseDto?> GetPatientAsync(
        string practiceId,
        string patientId,
        CancellationToken cancellationToken = default);
}

public class PatientApiService : IPatientApiService
{
    private readonly HttpClient _httpClient;

    public PatientApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<PatientSearchResultResponseDto>> SearchPatientsAsync(
        string practiceId,
        PatientSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/practices/{practiceId}/patients/search";
        
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<PatientSearchResultResponseDto>>(
            cancellationToken: cancellationToken)
            ?? new PagedResult<PatientSearchResultResponseDto>();
    }

    public async Task<PatientDetailResponseDto?> GetPatientAsync(
        string practiceId,
        string patientId,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/practices/{practiceId}/patients/{patientId}";
        
        return await _httpClient.GetFromJsonAsync<PatientDetailResponseDto>(url, cancellationToken);
    }
}
