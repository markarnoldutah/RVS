using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing patient demographics operations.
/// 
/// All data is stored in a single Patient document with embedded collections:
/// Patient -> CoverageEnrollments[] + Encounters[] -> EligibilityChecks[]
/// 
/// Operations are split across focused services:
/// - PatientService: Patient demographics (this service)
/// - CoverageEnrollmentService: Coverage enrollment management
/// - EncounterService: Encounter and coverage decision management
/// - EligibilityCheckService: Eligibility check operations
/// </summary>
public sealed class PatientService : IPatientService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserContextAccessor _userContext;

    public PatientService(
        IPatientRepository patientRepository,
        IUserContextAccessor userContext)
    {
        _patientRepository = patientRepository;
        _userContext = userContext;
    }

    public async Task<PagedResult<Patient>> SearchPatientsAsync(
        string tenantId,
        string practiceId,
        PatientSearchRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Page <= 0 || request.PageSize <= 0)
            throw new ArgumentException("Invalid paging parameters.", nameof(request));

        return await _patientRepository.SearchAsync(tenantId, practiceId, request);
    }

    public async Task<Patient> GetPatientAsync(string tenantId, string practiceId, string patientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);

        var patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, patientId);
        if (patient is null)
            throw new KeyNotFoundException("Patient not found.");

        return patient;
    }

    public async Task<Patient> CreatePatientAsync(string tenantId, string practiceId, PatientCreateRequestDto requestDto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentNullException.ThrowIfNull(requestDto);

        if (string.IsNullOrWhiteSpace(requestDto.FirstName) || string.IsNullOrWhiteSpace(requestDto.LastName))
            throw new ArgumentException("First and last name are required.", nameof(requestDto));

        var patient = requestDto.ToEntity(tenantId, practiceId, _userContext.UserId);

        await _patientRepository.CreateAsync(patient);
        return patient;
    }

    public async Task<Patient> UpdatePatientAsync(
        string tenantId,
        string practiceId,
        string patientId,
        PatientUpdateRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentNullException.ThrowIfNull(request);

        var patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, patientId);
        if (patient is null)
            throw new KeyNotFoundException("Patient not found.");

        patient.ApplyUpdate(request, _userContext.UserId);

        await _patientRepository.UpdateAsync(patient);
        return patient;
    }
}
