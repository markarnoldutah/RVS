using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing patient demographics operations.
/// 
/// The patient document contains embedded data as a single aggregate,
/// but operations are split across focused services:
/// - IPatientService: Patient demographics (this interface)
/// - ICoverageEnrollmentService: Coverage enrollment management
/// - IEncounterService: Encounter and coverage decision management
/// - IEligibilityCheckService: Eligibility check operations
/// </summary>
public interface IPatientService
{
    /// <summary>
    /// Searches patients with optional filtering and pagination.
    /// </summary>
    Task<PagedResult<Patient>> SearchPatientsAsync(
        string tenantId,
        string practiceId,
        PatientSearchRequestDto request);

    /// <summary>
    /// Gets a patient by ID.
    /// </summary>
    Task<Patient> GetPatientAsync(
        string tenantId,
        string practiceId,
        string patientId);

    /// <summary>
    /// Creates a new patient.
    /// </summary>
    Task<Patient> CreatePatientAsync(
        string tenantId,
        string practiceId,
        PatientCreateRequestDto request);

    /// <summary>
    /// Updates an existing patient's demographics.
    /// </summary>
    Task<Patient> UpdatePatientAsync(
        string tenantId,
        string practiceId,
        string patientId,
        PatientUpdateRequestDto request);
}
