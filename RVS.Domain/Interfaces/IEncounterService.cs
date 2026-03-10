using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing patient encounter operations.
/// Encounters are embedded within the Patient aggregate and include coverage decisions.
/// </summary>
public interface IEncounterService
{
    // =====================================================
    // Encounter Operations
    // =====================================================

    /// <summary>
    /// Searches encounters for a patient with optional filtering.
    /// </summary>
    Task<List<EncounterEmbedded>> GetPatientEncountersAsync(
        string tenantId,
        string practiceId,
        string patientId,
        PatientEncounterSearchRequestDto request);

    /// <summary>
    /// Gets a specific encounter for a patient.
    /// </summary>
    Task<EncounterEmbedded> GetEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId);

    /// <summary>
    /// Creates a new encounter for a patient.
    /// </summary>
    Task<EncounterEmbedded> CreateEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        EncounterCreateRequestDto request);

    /// <summary>
    /// Updates an existing encounter.
    /// </summary>
    Task<EncounterEmbedded> UpdateEncounterAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        EncounterUpdateRequestDto request);

    // =====================================================
    // Coverage Decision Operations
    // =====================================================

    /// <summary>
    /// Gets the coverage decision for an encounter.
    /// </summary>
    Task<CoverageDecisionEmbedded?> GetCoverageDecisionAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId);

    /// <summary>
    /// Sets or updates the coverage decision for an encounter.
    /// </summary>
    Task<CoverageDecisionEmbedded> SetCoverageDecisionAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        CoverageDecisionUpdateRequestDto request);
}
