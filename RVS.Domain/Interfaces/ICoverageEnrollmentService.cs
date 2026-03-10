using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing patient coverage enrollment operations.
/// Coverage enrollments are embedded within the Patient aggregate.
/// </summary>
public interface ICoverageEnrollmentService
{
    /// <summary>
    /// Adds a new coverage enrollment to a patient.
    /// </summary>
    Task<CoverageEnrollmentEmbedded> AddCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        CoverageEnrollmentCreateRequestDto request);

    /// <summary>
    /// Updates an existing coverage enrollment.
    /// </summary>
    Task<CoverageEnrollmentEmbedded> UpdateCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string coverageEnrollmentId,
        CoverageEnrollmentUpdateRequestDto request);

    /// <summary>
    /// Deletes a coverage enrollment from a patient.
    /// </summary>
    Task DeleteCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string coverageEnrollmentId);
}
