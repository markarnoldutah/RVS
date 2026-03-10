using System;
using System.Collections.Generic;

namespace RVS.Domain.Integrations.Availity;

/// <summary>
/// Response from POST /v1/coverages - initiates a coverage check.
/// Availity returns an ID and initial status (typically "In Progress" / statusCode "0").
/// </summary>
public sealed record AvailityInitiateResponse
{
    /// <summary>
    /// Availity's unique coverage ID. Use this for subsequent polling.
    /// </summary>
    public required string CoverageId { get; init; }

    /// <summary>
    /// Availity status code:
    /// - "0" = In Progress
    /// - "R1" = Retrying (health plan didn't respond)
    /// - "4" = Complete
    /// - "3" = Complete (Invalid Response - partial)
    /// - "19" = Request Error (validation failed)
    /// - "7", "13", "14", "15" = Communication Error
    /// </summary>
    public required string StatusCode { get; init; }

    /// <summary>
    /// Human-readable status description (e.g., "In Progress", "Complete", "Request Error").
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Estimated time when Availity expects the refresh to be complete.
    /// Use this to determine next poll timing.
    /// </summary>
    public DateTime? EtaDate { get; init; }

    /// <summary>
    /// Full coverage result. Populated when statusCode = "4" (Complete) or "3" (Partial).
    /// Rare on initiate, but possible for immediate completion.
    /// </summary>
    public AvailityEligibilityResult? Result { get; init; }

    /// <summary>
    /// Validation errors from Availity (populated when statusCode = "19").
    /// </summary>
    public List<AvailityValidationMessage>? ValidationMessages { get; init; }

    /// <summary>
    /// Raw error message for non-validation failures.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Response from GET /v1/coverages/{id} - polls for coverage result.
/// </summary>
public sealed record AvailityPollResponse
{
    /// <summary>
    /// Availity status code (see AvailityInitiateResponse for code meanings).
    /// </summary>
    public required string StatusCode { get; init; }

    /// <summary>
    /// Human-readable status description.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Estimated time when Availity expects the refresh to be complete.
    /// Only relevant when statusCode is "0" or "R1".
    /// </summary>
    public DateTime? EtaDate { get; init; }

    /// <summary>
    /// Full coverage result. Only populated when statusCode = "4" (Complete) or "3" (Partial).
    /// </summary>
    public AvailityEligibilityResult? Result { get; init; }

    /// <summary>
    /// Validation errors from payer (populated when statusCode = "19").
    /// </summary>
    public List<AvailityValidationMessage>? ValidationMessages { get; init; }

    /// <summary>
    /// Raw error message for communication errors.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// True if Availity is still processing (statusCode "0" or "R1").
    /// </summary>
    public bool IsProcessing => StatusCode is "0" or "R1";

    /// <summary>
    /// True if the check completed successfully (statusCode "4" or "3").
    /// </summary>
    public bool IsComplete => StatusCode is "4" or "3";

    /// <summary>
    /// True if the check failed (request error or communication error).
    /// </summary>
    public bool IsFailed => StatusCode is "19" or "7" or "13" or "14" or "15";
}

/// <summary>
/// Validation message from Availity or the payer.
/// </summary>
public sealed record AvailityValidationMessage
{
    public string? Field { get; init; }
    public string? Code { get; init; }
    public string? ErrorMessage { get; init; }
    public int? Index { get; init; }
}

/// <summary>
/// Full eligibility result from Availity (populated on successful completion).
/// </summary>
public sealed record AvailityEligibilityResult
{
    public string? PlanName { get; init; }
    public string? GroupNumber { get; init; }
    public string? GroupName { get; init; }
    public string? InsuranceType { get; init; }
    public DateTime? EligibilityStartDate { get; init; }
    public DateTime? EligibilityEndDate { get; init; }
    public DateTime? CoverageStartDate { get; init; }
    public DateTime? CoverageEndDate { get; init; }

    /// <summary>
    /// Subscriber information from the payer response.
    /// </summary>
    public AvailitySubscriberInfo? Subscriber { get; init; }

    /// <summary>
    /// Patient information from the payer response.
    /// </summary>
    public AvailityPatientInfo? Patient { get; init; }

    /// <summary>
    /// Coverage benefit details (deductible, copay, coinsurance, etc.).
    /// </summary>
    public List<AvailityCoverageLine> CoverageLines { get; init; } = [];

    /// <summary>
    /// Raw request/response payload references (e.g., blob URLs for 270/271 EDI).
    /// </summary>
    public List<AvailityPayloadRef> PayloadRefs { get; init; } = [];

    /// <summary>
    /// Payer notes and disclaimers.
    /// </summary>
    public List<string>? PayerNotes { get; init; }
}

/// <summary>
/// Subscriber info from Availity response.
/// </summary>
public sealed record AvailitySubscriberInfo
{
    public string? MemberId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public DateTime? BirthDate { get; init; }
    public string? Gender { get; init; }
}

/// <summary>
/// Patient info from Availity response (when patient differs from subscriber).
/// </summary>
public sealed record AvailityPatientInfo
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public DateTime? BirthDate { get; init; }
    public string? Gender { get; init; }
    public string? SubscriberRelationship { get; init; }
    public string? SubscriberRelationshipCode { get; init; }
}

/// <summary>
/// Coverage line detail (benefit information).
/// </summary>
public sealed record AvailityCoverageLine
{
    public string ServiceTypeCode { get; init; } = default!;
    public string? ServiceTypeDescription { get; init; }
    public string? CoverageType { get; init; }  // e.g., Copay, Deductible, Coinsurance
    public string? Network { get; init; }       // e.g., InNetwork, OutOfNetwork
    public string? Amount { get; init; }        // String for flexibility (currency/percent)
    public string? TimePeriod { get; init; }    // e.g., "Calendar Year", "Visit"
    public string? Level { get; init; }         // e.g., "Individual", "Family"
    public bool? AuthorizationRequired { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Reference to stored request/response payload.
/// </summary>
public sealed record AvailityPayloadRef
{
    public string Direction { get; init; } = default!;  // "Request" or "Response"
    public string Format { get; init; } = default!;     // "JSON", "X12_270", "X12_271"
    public string StorageUrl { get; init; } = default!; // Blob storage URL
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

// ============================================================================
// Legacy types (kept for backward compatibility during migration)
// ============================================================================

/// <summary>
/// [DEPRECATED] Use AvailityPollResponse with Result property instead.
/// Minimal response model for Availity eligibility - kept for migration.
/// </summary>
public sealed record AvailityEligibilityResponse
{
    public string? RawStatusCode { get; init; }
    public string? RawStatusDescription { get; init; }

    public string? PlanName { get; init; }
    public DateTime? EffectiveDate { get; init; }
    public DateTime? TerminationDate { get; init; }

    public string? ErrorMessage { get; init; }

    public List<AvailityCoverageLine> CoverageLines { get; init; } = [];
    public List<AvailityPayloadRef> PayloadRefs { get; init; } = [];
}
