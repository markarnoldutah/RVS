using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing patient encounter operations.
/// Encounters are embedded within the Patient aggregate and include coverage decisions.
/// </summary>
public sealed class EncounterService : IEncounterService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserContextAccessor _userContext;

    public EncounterService(
        IPatientRepository patientRepository,
        IUserContextAccessor userContext)
    {
        _patientRepository = patientRepository;
        _userContext = userContext;
    }

    // =====================================================
    // Encounter Operations
    // =====================================================

    public async Task<List<EncounterEmbedded>> GetPatientEncountersAsync(
        string tenantId,
        string practiceId,
        string patientId,
        PatientEncounterSearchRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Page <= 0 || request.PageSize <= 0)
            throw new ArgumentException("Invalid paging parameters.", nameof(request));

        // Ensure patient exists
        var patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, patientId);
        if (patient is null)
            throw new KeyNotFoundException("Patient not found.");

        return await _patientRepository.GetEncountersAsync(tenantId, practiceId, patientId, request);
    }

    public async Task<EncounterEmbedded> GetEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);

        var encounter = await _patientRepository.GetEncounterAsync(tenantId, practiceId, patientId, encounterId);
        if (encounter is null)
            throw new KeyNotFoundException("Encounter not found.");

        return encounter;
    }

    public async Task<EncounterEmbedded> CreateEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        EncounterCreateRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentNullException.ThrowIfNull(request);

        // Ensure patient exists
        var patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, patientId);
        if (patient is null)
            throw new KeyNotFoundException("Patient not found.");

        var encounter = request.ToEntity(_userContext.UserId);

        return await _patientRepository.AddEncounterAsync(tenantId, practiceId, patientId, encounter);
    }

    public async Task<EncounterEmbedded> UpdateEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        EncounterUpdateRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        ArgumentNullException.ThrowIfNull(request);

        return await _patientRepository.UpdateEncounterAsync(
            tenantId,
            practiceId,
            patientId,
            encounterId,
            encounter => encounter.ApplyUpdate(request, _userContext.UserId));
    }

    // =====================================================
    // Coverage Decision Operations
    // =====================================================

    public Task<CoverageDecisionEmbedded?> GetCoverageDecisionAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId)
        => _patientRepository.GetCoverageDecisionAsync(tenantId, practiceId, patientId, encounterId);

    public async Task<CoverageDecisionEmbedded> SetCoverageDecisionAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        CoverageDecisionUpdateRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        ArgumentNullException.ThrowIfNull(request);

        // Validate referenced coverage enrollments exist
        if (!string.IsNullOrWhiteSpace(request.PrimaryCoverageEnrollmentId))
        {
            var coverage = await _patientRepository.GetCoverageEnrollmentAsync(
                tenantId,
                practiceId,
                patientId,
                request.PrimaryCoverageEnrollmentId);

            if (coverage is null)
                throw new KeyNotFoundException("Primary coverage enrollment not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.SecondaryCoverageEnrollmentId))
        {
            var coverage = await _patientRepository.GetCoverageEnrollmentAsync(
                tenantId,
                practiceId,
                patientId,
                request.SecondaryCoverageEnrollmentId);

            if (coverage is null)
                throw new KeyNotFoundException("Secondary coverage enrollment not found.");
        }

        var decision = request.ToEntity(_userContext.UserId);

        return await _patientRepository.SetCoverageDecisionAsync(tenantId, practiceId, patientId, encounterId, decision);
    }
}
