using System;
using System.Collections.Generic;

namespace RVS.Domain.DTOs;

/// <summary>
/// Response DTO for single-screen patient check-in workflow.
/// Contains the complete result of the check-in operation including
/// patient, encounter, coverage decision, and eligibility check results.
/// </summary>
public sealed record PatientCheckInResponseDto
{
    /// <summary>
    /// The patient ID (existing or newly created).
    /// </summary>
    public required string PatientId { get; init; }

    /// <summary>
    /// The encounter ID (existing or newly created).
    /// </summary>
    public required string EncounterId { get; init; }

    /// <summary>
    /// Patient summary after check-in.
    /// </summary>
    public required PatientCheckInSummaryDto Patient { get; init; }

    /// <summary>
    /// Encounter summary after check-in.
    /// </summary>
    public required EncounterCheckInSummaryDto Encounter { get; init; }

    /// <summary>
    /// Coverage enrollments that were created or updated.
    /// </summary>
    public List<CoverageEnrollmentCheckInResultDto> CoverageEnrollments { get; init; } = [];

    /// <summary>
    /// Coverage decision result (if set during check-in).
    /// </summary>
    public CoverageDecisionResponseDto? CoverageDecision { get; init; }

    /// <summary>
    /// Eligibility check results. Eligibility checks are automatically run for all 
    /// coverages with valid PayerId and MemberId during check-in.
    /// </summary>
    public List<EligibilityCheckResultDto> EligibilityChecks { get; init; } = [];

    /// <summary>
    /// Indicates if all eligibility checks succeeded.
    /// </summary>
    public bool AllEligibilityChecksSucceeded { get; init; }

    /// <summary>
    /// Summary of any warnings or issues encountered during check-in.
    /// </summary>
    public List<string> Warnings { get; init; } = [];
}

/// <summary>
/// Patient summary for check-in response.
/// </summary>
public sealed record PatientCheckInSummaryDto
{
    public required string PatientId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>
    /// Indicates if this was a newly created patient.
    /// </summary>
    public bool WasCreated { get; init; }
}

/// <summary>
/// Encounter summary for check-in response.
/// </summary>
public sealed record EncounterCheckInSummaryDto
{
    public required string EncounterId { get; init; }
    public required string LocationId { get; init; }
    public DateTime VisitDate { get; init; }
    public required string VisitType { get; init; }
    public required string Status { get; init; }
    public string? ExternalRef { get; init; }

    /// <summary>
    /// Indicates if this was a newly created encounter.
    /// </summary>
    public bool WasCreated { get; init; }
}

/// <summary>
/// Coverage enrollment result for check-in response.
/// </summary>
public sealed record CoverageEnrollmentCheckInResultDto
{
    public required string CoverageEnrollmentId { get; init; }
    public required string PayerId { get; init; }
    public required string PlanType { get; init; }
    public required string MemberId { get; init; }
    public string? GroupNumber { get; init; }
    public byte? CobPriorityHint { get; init; }

    /// <summary>
    /// Indicates if this was a newly created enrollment.
    /// </summary>
    public bool WasCreated { get; init; }
}

/// <summary>
/// Eligibility check result for check-in response.
/// </summary>
public sealed record EligibilityCheckResultDto
{
    public required string EligibilityCheckId { get; init; }
    public required string CoverageEnrollmentId { get; init; }
    public required string PayerId { get; init; }
    public DateTime DateOfService { get; init; }
    public DateTime RequestedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public required string Status { get; init; }
     
    /// <summary>
    /// Payer-provided status code.
    /// </summary>
    public string? RawStatusCode { get; init; }

    /// <summary>
    /// Payer-provided status description.
    /// </summary>
    public string? RawStatusDescription { get; init; }

    /// <summary>
    /// Plan name from eligibility response.
    /// </summary>
    public string? PlanName { get; init; }

    /// <summary>
    /// Coverage effective date from eligibility response.
    /// </summary>
    public DateTime? EffectiveDate { get; init; }

    /// <summary>
    /// Coverage termination date from eligibility response.
    /// </summary>
    public DateTime? TerminationDate { get; init; }

    /// <summary>
    /// Error message if the check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Validation messages from payer (populated on request errors).
    /// </summary>
    public List<string>? ValidationMessages { get; init; }

    /// <summary>
    /// Coverage line details.
    /// </summary>
    public List<CoverageLineResponseDto> CoverageLines { get; init; } = [];

    /// <summary>
    /// Indicates if this check succeeded (status = "Complete").
    /// </summary>
    public bool Succeeded => Status == "Complete";
}
