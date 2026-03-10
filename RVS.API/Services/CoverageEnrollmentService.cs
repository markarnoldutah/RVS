using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing patient coverage enrollment operations.
/// Coverage enrollments are embedded within the Patient aggregate.
/// </summary>
public sealed class CoverageEnrollmentService : ICoverageEnrollmentService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserContextAccessor _userContext;

    public CoverageEnrollmentService(
        IPatientRepository patientRepository,
        IUserContextAccessor userContext)
    {
        _patientRepository = patientRepository;
        _userContext = userContext;
    }

    public async Task<CoverageEnrollmentEmbedded> AddCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        CoverageEnrollmentCreateRequestDto requestDto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentNullException.ThrowIfNull(requestDto);

        // Ensure patient exists
        var patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, patientId);
        if (patient is null)
            throw new KeyNotFoundException("Patient not found.");

        var coverageEnrollment = requestDto.ToEntity(_userContext.UserId);

        return await _patientRepository.AddCoverageEnrollmentAsync(tenantId, practiceId, patientId, coverageEnrollment);
    }

    public async Task<CoverageEnrollmentEmbedded> UpdateCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string coverageEnrollmentId,
        CoverageEnrollmentUpdateRequestDto requestDto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(coverageEnrollmentId);
        ArgumentNullException.ThrowIfNull(requestDto);

        var entity = await _patientRepository.GetCoverageEnrollmentAsync(tenantId, practiceId, patientId, coverageEnrollmentId);
        if (entity is null)
            throw new KeyNotFoundException("Coverage enrollment not found.");

        return await _patientRepository.UpdateCoverageEnrollmentAsync(
            tenantId,
            practiceId,
            patientId,
            coverageEnrollmentId,
            coverage => coverage.ApplyUpdate(requestDto, _userContext.UserId));
    }

    public async Task DeleteCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string coverageEnrollmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(coverageEnrollmentId);

        var entity = await _patientRepository.GetCoverageEnrollmentAsync(tenantId, practiceId, patientId, coverageEnrollmentId);
        if (entity is null)
            throw new KeyNotFoundException("Coverage enrollment not found.");

        await _patientRepository.DeleteCoverageEnrollmentAsync(tenantId, practiceId, patientId, coverageEnrollmentId);
    }
}
