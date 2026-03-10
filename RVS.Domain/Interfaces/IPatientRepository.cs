using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing patient entities and their embedded collections.
/// All PHI operations are practice-scoped: every method requires both tenantId and practiceId.
/// 
/// Cosmos DB Design:
/// - Partition Key: /practiceId
/// - Patient document includes embedded: CoverageEnrollments[], Encounters[]
/// - Each Encounter includes embedded: CoverageDecision, EligibilityChecks[]
/// </summary>
public interface IPatientRepository
{
    // =====================================================
    // Patient Operations
    // =====================================================

    /// <summary>
    /// Paged search for patients in a specific practice.
    /// Uses projection queries for list view (lightweight ~0.5KB per patient).
    /// For Cosmos, Page/PageSize + ContinuationToken map to MaxItemCount + continuation.
    /// </summary>
    Task<PagedResult<Patient>> SearchAsync(
        string tenantId,
        string practiceId,
        PatientSearchRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single patient entity with all embedded data or null.
    /// Point read operation (~1 RU for 124KB average document).
    /// </summary>
    Task<Patient?> GetByIdAsync(string tenantId, string practiceId, string patientId);

    /// <summary>
    /// Creates a new patient entity (id should be assigned by caller or here).
    /// </summary>
    Task CreateAsync(Patient entity);

    /// <summary>
    /// Updates an existing patient entity (ETag / concurrency handled inside).
    /// </summary>
    Task UpdateAsync(Patient entity);

    // =====================================================
    // Coverage Enrollment Operations (Embedded Documents)
    // =====================================================

    /// <summary>
    /// Get a specific coverage enrollment for a patient.
    /// </summary>
    Task<CoverageEnrollmentEmbedded?> GetCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string coverageEnrollmentId);

    /// <summary>
    /// Add a new coverage enrollment to a patient's embedded collection.
    /// </summary>
    Task<CoverageEnrollmentEmbedded> AddCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        CoverageEnrollmentEmbedded newCoverage);

    /// <summary>
    /// Update an existing coverage enrollment in a patient's embedded collection.
    /// </summary>
    Task<CoverageEnrollmentEmbedded> UpdateCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string coverageEnrollmentId,
        Action<CoverageEnrollmentEmbedded> updateAction);

    /// <summary>
    /// Delete a coverage enrollment from a patient's embedded collection.
    /// </summary>
    Task DeleteCoverageEnrollmentAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string coverageEnrollmentId);

    // =====================================================
    // Encounter Operations (Embedded Documents)
    // =====================================================

    /// <summary>
    /// Get a specific encounter for a patient.
    /// Point read patient, filter encounters array by encounter.id (client-side).
    /// </summary>
    Task<EncounterEmbedded?> GetEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId);

    /// <summary>
    /// Get all encounters for a patient with optional filtering.
    /// Returns encounters array from patient document, sorted client-side.
    /// </summary>
    Task<List<EncounterEmbedded>> GetEncountersAsync(
        string tenantId,
        string practiceId,
        string patientId,
        PatientEncounterSearchRequestDto? request = null);

    /// <summary>
    /// Add a new encounter to a patient's embedded collection.
    /// </summary>
    Task<EncounterEmbedded> AddEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        EncounterEmbedded newEncounter);

    /// <summary>
    /// Update an existing encounter in a patient's embedded collection.
    /// </summary>
    Task<EncounterEmbedded> UpdateEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        Action<EncounterEmbedded> updateAction);

    // =====================================================
    // Coverage Decision Operations (Embedded in Encounter)
    // =====================================================

    /// <summary>
    /// Set or update the coverage decision for an encounter.
    /// </summary>
    Task<CoverageDecisionEmbedded> SetCoverageDecisionAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        CoverageDecisionEmbedded decision);

    /// <summary>
    /// Get the coverage decision for an encounter.
    /// </summary>
    Task<CoverageDecisionEmbedded?> GetCoverageDecisionAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId);

    // =====================================================
    // Eligibility Check Operations (Embedded in Encounter)
    // =====================================================

    /// <summary>
    /// Add a new eligibility check to an encounter's embedded collection.
    /// </summary>
    Task<EligibilityCheckEmbedded> AddEligibilityCheckAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        EligibilityCheckEmbedded newCheck);

    /// <summary>
    /// Update an existing eligibility check.
    /// </summary>
    Task<EligibilityCheckEmbedded> UpdateEligibilityCheckAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        Action<EligibilityCheckEmbedded> updateAction);

    /// <summary>
    /// Get a specific eligibility check from an encounter.
    /// </summary>
    Task<EligibilityCheckEmbedded?> GetEligibilityCheckAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId);

    /// <summary>
    /// Get all eligibility checks for an encounter.
    /// </summary>
    Task<List<EligibilityCheckEmbedded>> GetEligibilityChecksAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId);

    /// <summary>
    /// Add a coverage line to a specific eligibility check.
    /// </summary>
    Task AddCoverageLineAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        CoverageLineEmbedded coverageLine);

    /// <summary>
    /// Add an eligibility payload (request/response) to a specific eligibility check.
    /// </summary>
    Task AddEligibilityPayloadAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        EligibilityPayloadEmbedded payload);
}
