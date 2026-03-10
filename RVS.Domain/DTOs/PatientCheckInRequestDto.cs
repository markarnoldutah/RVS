using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs;

/// <summary>
/// Composite DTO for single-screen patient check-in workflow.
/// Supports: patient upsert, coverage enrollment upsert, encounter creation, 
/// coverage decision, and eligibility check execution - all in one request.
/// 
/// This reduces multiple API round-trips to a single call and enables
/// optimized Cosmos DB operations (single document read/write pattern).
/// 
/// Eligibility checks are automatically run for all coverages with valid 
/// PayerId and MemberId during check-in.
/// </summary>
public sealed record PatientCheckInRequestDto
{
    // =====================================================
    // Patient Identification / Upsert
    // =====================================================

    /// <summary>
    /// Existing patient ID if checking in a known patient.
    /// Leave null to create a new patient.
    /// </summary>
    [StringLength(100, MinimumLength = 1)]
    public string? PatientId { get; init; }

    /// <summary>
    /// Patient demographics for create or update.
    /// Required if PatientId is null (new patient).
    /// Optional if PatientId is provided (updates only non-null fields).
    /// </summary>
    public PatientCheckInDemographicsDto? Patient { get; init; }

    // =====================================================
    // Coverage Enrollment Upsert
    // =====================================================

    /// <summary>
    /// Coverage enrollments to add or update.
    /// If CoverageEnrollmentId is provided, updates existing; otherwise creates new.
    /// Eligibility checks are automatically run for all coverages with valid PayerId and MemberId.
    /// </summary>
    public List<CoverageEnrollmentUpsertDto>? CoverageEnrollments { get; init; }

    // =====================================================
    // Encounter Creation
    // =====================================================

    /// <summary>
    /// Existing encounter ID if adding eligibility to an existing encounter.
    /// Leave null to create a new encounter.
    /// </summary>
    [StringLength(100, MinimumLength = 1)]
    public string? EncounterId { get; init; }

    /// <summary>
    /// Encounter details for create or update.
    /// Required if EncounterId is null (new encounter).
    /// </summary>
    public EncounterCheckInDto? Encounter { get; init; }

    // =====================================================
    // Coverage Decision (COB)
    // =====================================================

    /// <summary>
    /// Coverage decision for the encounter.
    /// Optional - can be set later via separate API call.
    /// </summary>
    public CoverageDecisionCheckInDto? CoverageDecision { get; init; }
}

/// <summary>
/// Patient demographics for check-in workflow.
/// </summary>
public sealed record PatientCheckInDemographicsDto
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string FirstName { get; init; } = default!;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string LastName { get; init; } = default!;

    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; init; }

    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; init; }

    [Phone]
    [StringLength(20)]
    public string? Phone { get; init; }
}

/// <summary>
/// Coverage enrollment upsert for check-in workflow.
/// </summary>
public sealed record CoverageEnrollmentUpsertDto
{
    /// <summary>
    /// Coverage enrollment ID. Client-generated GUIDs are used as permanent IDs.
    /// - For new coverages: Client generates a GUID and provides it here.
    /// - For existing coverages: Provide the existing ID to update.
    /// The API determines create vs update by checking if the ID exists on the patient.
    /// </summary>
    [StringLength(100, MinimumLength = 1)]
    public string? CoverageEnrollmentId { get; init; }

    [Required(ErrorMessage = "PayerId is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string PayerId { get; init; } = default!;

    [Required(ErrorMessage = "PlanType is required.")]
    [StringLength(50, MinimumLength = 1)]
    public string PlanType { get; init; } = default!;

    [Required(ErrorMessage = "MemberId is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string MemberId { get; init; } = default!;

    [StringLength(100)]
    public string? GroupNumber { get; init; }

    [StringLength(20)]
    public string RelationshipToSubscriber { get; init; } = "Self";

    [StringLength(100)]
    public string? SubscriberFirstName { get; init; }

    [StringLength(100)]
    public string? SubscriberLastName { get; init; }

    [DataType(DataType.Date)]
    public DateOnly? SubscriberDob { get; init; }

    public bool IsEmployerPlan { get; init; }

    [DataType(DataType.Date)]
    public DateOnly? EffectiveDate { get; init; }

    [DataType(DataType.Date)]
    public DateOnly? TerminationDate { get; init; }

    [Range(1, 9)]
    public byte? CobPriorityHint { get; init; }

    [StringLength(500)]
    public string? CobNotes { get; init; }
}

/// <summary>
/// Encounter details for check-in workflow.
/// </summary>
public sealed record EncounterCheckInDto
{
    [Required(ErrorMessage = "LocationId is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string LocationId { get; init; } = default!;

    /// <summary>
    /// Visit date/time in UTC. Defaults to current time if not specified.
    /// </summary>
    public DateTime? VisitDate { get; init; }

    [Required(ErrorMessage = "VisitType is required.")]
    [StringLength(50, MinimumLength = 1)]
    public string VisitType { get; init; } = default!;

    [StringLength(100)]
    public string? ExternalRef { get; init; }
}

/// <summary>
/// Coverage decision (COB) for check-in workflow.
/// </summary>
public sealed record CoverageDecisionCheckInDto
{
    [Required(ErrorMessage = "PrimaryCoverageEnrollmentId is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string PrimaryCoverageEnrollmentId { get; init; } = default!;

    [StringLength(100)]
    public string? SecondaryCoverageEnrollmentId { get; init; }

    [Required(ErrorMessage = "CobReason is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string CobReason { get; init; } = default!;

    public bool OverriddenByUser { get; init; }

    [StringLength(500)]
    public string? OverrideNote { get; init; }
}
