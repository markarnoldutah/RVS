using Newtonsoft.Json;

namespace RVS.Domain.Entities;

// ============================================================================
// ⚠️ PHI WARNING
// This entity contains Protected Health Information (PHI).
//
// HIPAA requirements:
// - Must be scoped by TenantId AND PracticeId
// - Must never be queried without practice isolation
// - Must not be logged, cached, or serialized outside approved boundaries
// - All access must be authorized at the practice level
//
// Changes to this entity require HIPAA impact review.
// ============================================================================

/// <summary>
/// Patient aggregate root containing all patient data including demographics,
/// coverage enrollments, and encounters with their eligibility checks.
/// 
/// Cosmos DB partition key: /practiceId (practiceId)
/// Average document size: 124KB (4KB patient + 2KB coverages + 120KB encounters)
/// Max document size: 304KB (well under 2MB limit)
/// </summary>
public class Patient : PracticeScopedEntityBase
{
    public override string Type { get; init; } = "patient";

    [JsonProperty("patientId")]
    public string PatientId => Id;

    [JsonProperty("firstName")]
    public required string FirstName { get; set; }

    [JsonProperty("lastName")]
    public required string LastName { get; set; }

    /// <summary>
    /// Patient's date of birth. Timezone-agnostic calendar date.
    /// Stored as ISO date string (yyyy-MM-dd) in Cosmos DB.
    /// </summary>
    [JsonProperty("dateOfBirth")]
    public DateOnly? DateOfBirth { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// Embedded coverage enrollments (Level 1 embedding).
    /// 90% access correlation with patient.
    /// </summary>
    [JsonProperty("coverageEnrollments")]
    public List<CoverageEnrollmentEmbedded> CoverageEnrollments { get; set; } = new();

    /// <summary>
    /// Embedded encounters (Level 2 embedding).
    /// 80% access correlation with patient.
    /// BOUNDED at avg 8 encounters per patient lifetime (max ~20).
    /// Each encounter contains embedded EligibilityChecks and CoverageDecision.
    /// </summary>
    [JsonProperty("encounters")]
    public List<EncounterEmbedded> Encounters { get; set; } = new();
}


// ---------------------------------------------------------------------------
// PHI (Inherited) - CoverageEnrollmentEmbedded
// ---------------------------------------------------------------------------
/// <summary>
/// Embedded coverage enrollment within a Patient document.
/// </summary>
public class CoverageEnrollmentEmbedded
{
    [JsonProperty("coverageEnrollmentId")]
    public string CoverageEnrollmentId { get; init; } = Guid.NewGuid().ToString(); 

    [JsonProperty("payerId")]
    public required string PayerId { get; set; }

    [JsonProperty("planType")]
    public required string PlanType { get; set; }  // Vision, Medical, etc

    [JsonProperty("memberId")]
    public required string MemberId { get; set; }

    [JsonProperty("groupNumber")]
    public string? GroupNumber { get; set; }

    [JsonProperty("relationshipToSubscriber")]
    public string RelationshipToSubscriber { get; set; } = "Self";  // Self, Spouse, Child

    [JsonProperty("subscriberFirstName")]
    public string? SubscriberFirstName { get; set; }

    [JsonProperty("subscriberLastName")]
    public string? SubscriberLastName { get; set; }

    /// <summary>
    /// Subscriber's date of birth. Timezone-agnostic calendar date.
    /// </summary>
    [JsonProperty("subscriberDob")]
    public DateOnly? SubscriberDob { get; set; }

    [JsonProperty("isEmployerPlan")]
    public bool IsEmployerPlan { get; set; }

    /// <summary>
    /// Coverage effective date. Timezone-agnostic calendar date.
    /// </summary>
    [JsonProperty("effectiveDate")]
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>
    /// Coverage termination date. Timezone-agnostic calendar date.
    /// </summary>
    [JsonProperty("terminationDate")]
    public DateOnly? TerminationDate { get; set; }

    [JsonProperty("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonProperty("cobPriorityHint")]
    public byte? CobPriorityHint { get; set; }          // 1 = usually primary, 2 = secondary

    [JsonProperty("isCobLocked")]
    public bool IsCobLocked { get; set; }

    [JsonProperty("cobNotes")]
    public string? CobNotes { get; set; }

    /// <summary>
    /// Timestamp when the enrollment was created. Always in UTC.
    /// </summary>
    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the enrollment was last updated. Always in UTC. Null if never updated.
    /// </summary>
    [JsonProperty("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [JsonProperty("createdByUserId")]
    public string? CreatedByUserId { get; init; }

    [JsonProperty("updatedByUserId")]
    public string? UpdatedByUserId { get; set; }
}


// ---------------------------------------------------------------------------
// PHI (Inherited) - EncounterEmbedded
// Embedded within Patient document (Level 2 embedding).
// ---------------------------------------------------------------------------
/// <summary>
/// Embedded encounter within a Patient document.
/// Contains visit details, coverage decision, and eligibility checks.
/// </summary>
public class EncounterEmbedded
{
    [JsonProperty("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("locationId")]
    public required string LocationId { get; set; }

    /// <summary>
    /// Scheduled visit date and time. Stored and returned in UTC.
    /// Clients should convert to local timezone for display.
    /// </summary>
    [JsonProperty("visitDate")]
    public required DateTime VisitDate { get; set; }

    [JsonProperty("visitType")]
    public required string VisitType { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = "scheduled";

    [JsonProperty("externalRef")]
    public string? ExternalRef { get; set; }

    [JsonProperty("coverageDecision")]
    public CoverageDecisionEmbedded? CoverageDecision { get; set; }

    [JsonProperty("eligibilityChecks")]
    public List<EligibilityCheckEmbedded> EligibilityChecks { get; set; } = new();

    /// <summary>
    /// Timestamp when the encounter was created. Always in UTC.
    /// </summary>
    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the encounter was last updated. Always in UTC. Null if never updated.
    /// </summary>
    [JsonProperty("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [JsonProperty("createdByUserId")]
    public string? CreatedByUserId { get; init; }

    [JsonProperty("updatedByUserId")]
    public string? UpdatedByUserId { get; set; }
}



// PHI (Inherited) - CoverageDecisionEmbedded
// ---------------------------------------------------------------------------
public class CoverageDecisionEmbedded
{
    [JsonProperty("encounterCoverageDecisionId")]
    public string EncounterCoverageDecisionId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("primaryCoverageEnrollmentId")]
    public required string PrimaryCoverageEnrollmentId { get; init; }

    [JsonProperty("secondaryCoverageEnrollmentId")]
    public string? SecondaryCoverageEnrollmentId { get; set; }

    [JsonProperty("cobReason")]
    public required string CobReason { get; init; }

    [JsonProperty("cobDeterminationSource")]
    public string? CobDeterminationSource { get; set; }

    [JsonProperty("overriddenByUser")]
    public bool OverriddenByUser { get; set; }

    [JsonProperty("overrideNote")]
    public string? OverrideNote { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    [JsonProperty("createdByUserId")]
    public string? CreatedByUserId { get; init; }

    [JsonProperty("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [JsonProperty("updatedByUserId")]
    public string? UpdatedByUserId { get; set; }
}

// ---------------------------------------------------------------------------
// PHI (Inherited) - EligibilityCheckEmbedded
// ---------------------------------------------------------------------------
public class EligibilityCheckEmbedded
{
    [JsonProperty("eligibilityCheckId")]
    public string EligibilityCheckId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("coverageEnrollmentId")]
    public required string CoverageEnrollmentId { get; init; }

    [JsonProperty("payerId")]
    public required string PayerId { get; init; }

    [JsonProperty("dateOfService")]
    public DateTime DateOfService { get; set; }

    [JsonProperty("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonProperty("completedAtUtc")]
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Status of the eligibility check:
    /// - "Pending" - Just created, not yet sent to Availity
    /// - "InProgress" - Sent to Availity, awaiting response (statusCode "0" or "R1")
    /// - "Complete" - Successfully completed (statusCode "4")
    /// - "Failed" - Error occurred (statusCode "19", "7", etc.)
    /// - "Canceled" - Canceled by user or timeout
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Availity's coverage ID for async polling.
    /// Returned from POST /v1/coverages and used to poll GET /v1/coverages/{id}.
    /// </summary>
    [JsonProperty("availityCoverageId")]
    public string? AvailityCoverageId { get; set; }

    /// <summary>
    /// Availity's raw status code (e.g., "0", "4", "19", "R1").
    /// </summary>
    [JsonProperty("rawStatusCode")]
    public string? RawStatusCode { get; set; }

    /// <summary>
    /// Availity's raw status description (e.g., "In Progress", "Complete").
    /// </summary>
    [JsonProperty("rawStatusDescription")]
    public string? RawStatusDescription { get; set; }

    /// <summary>
    /// Suggested time to wait before polling again. Used by UI for backoff.
    /// </summary>
    [JsonProperty("nextPollAfterUtc")]
    public DateTime? NextPollAfterUtc { get; set; }

    /// <summary>
    /// Number of times this check has been polled from Availity.
    /// Used to enforce maximum poll attempts.
    /// </summary>
    [JsonProperty("pollCount")]
    public int PollCount { get; set; }

    /// <summary>
    /// Last time Availity was polled for this check.
    /// </summary>
    [JsonProperty("lastPolledAtUtc")]
    public DateTime? LastPolledAtUtc { get; set; }

    [JsonProperty("memberIdSnapshot")]
    public required string MemberIdSnapshot { get; init; }

    [JsonProperty("groupNumberSnapshot")]
    public string? GroupNumberSnapshot { get; set; }

    [JsonProperty("planNameSnapshot")]
    public string? PlanNameSnapshot { get; set; }

    [JsonProperty("effectiveDateSnapshot")]
    public DateTime? EffectiveDateSnapshot { get; set; }

    [JsonProperty("terminationDateSnapshot")]
    public DateTime? TerminationDateSnapshot { get; set; }

    [JsonProperty("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Validation messages from Availity/payer (populated on request errors).
    /// </summary>
    [JsonProperty("validationMessages")]
    public List<string>? ValidationMessages { get; set; }

    [JsonProperty("coverageLines")]
    public List<CoverageLineEmbedded> CoverageLines { get; set; } = new();

    [JsonProperty("payloads")]
    public List<EligibilityPayloadEmbedded> Payloads { get; set; } = new();

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    [JsonProperty("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [JsonProperty("createdByUserId")]
    public string? CreatedByUserId { get; init; }

    [JsonProperty("updatedByUserId")]
    public string? UpdatedByUserId { get; set; }

    // =====================================================
    // Computed Properties (not persisted)
    // =====================================================

    /// <summary>
    /// True if the check is still in progress and should be polled.
    /// </summary>
    [JsonIgnore]
    public bool IsPollingRequired => Status is "Pending" or "InProgress";

    /// <summary>
    /// True if the check has reached a terminal state (Complete or Failed).
    /// </summary>
    [JsonIgnore]
    public bool IsTerminal => Status is "Complete" or "Failed" or "Canceled";
}
/// <summary>
/// Coverage line details within an EligibilityCheck, representing specific services
/// and their coverage information.
/// </summary>
// ---------------------------------------------------------------------------
// PHI (Inherited) - CoverageLineEmbedded
// ---------------------------------------------------------------------------
public class CoverageLineEmbedded
{
    [JsonProperty("coverageLineId")]
    public string CoverageLineId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("serviceTypeCode")]
    public required string ServiceTypeCode { get; init; }

    [JsonProperty("coverageDescription")]
    public string? CoverageDescription { get; set; }

    [JsonProperty("copayAmount")]
    public decimal? CopayAmount { get; set; }

    [JsonProperty("coinsurancePercent")]
    public decimal? CoinsurancePercent { get; set; }

    [JsonProperty("deductibleAmount")]
    public decimal? DeductibleAmount { get; set; }

    [JsonProperty("remainingDeductible")]
    public decimal? RemainingDeductible { get; set; }

    [JsonProperty("outOfPocketMax")]
    public decimal? OutOfPocketMax { get; set; }

    [JsonProperty("remainingOutOfPocket")]
    public decimal? RemainingOutOfPocket { get; set; }

    [JsonProperty("allowanceAmount")]
    public decimal? AllowanceAmount { get; set; }

    [JsonProperty("networkIndicator")]
    public string? NetworkIndicator { get; set; }

    [JsonProperty("effectiveDate")]
    public DateTime? EffectiveDate { get; set; }

    [JsonProperty("terminationDate")]
    public DateTime? TerminationDate { get; set; }

    [JsonProperty("additionalInfo")]
    public string? AdditionalInfo { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    [JsonProperty("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [JsonProperty("createdByUserId")]
    public string? CreatedByUserId { get; init; }

    [JsonProperty("updatedByUserId")]
    public string? UpdatedByUserId { get; set; }
}

// ---------------------------------------------------------------------------
// PHI (Inherited) - EligibilityPayloadEmbedded
// ---------------------------------------------------------------------------
public class EligibilityPayloadEmbedded
{
    [JsonProperty("payloadId")]
    public string PayloadId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("direction")]
    public required string Direction { get; init; }

    [JsonProperty("format")]
    public required string Format { get; init; }

    [JsonProperty("storageUrl")]
    public required string StorageUrl { get; init; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    [JsonProperty("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [JsonProperty("createdByUserId")]
    public string? CreatedByUserId { get; init; }

    [JsonProperty("updatedByUserId")]
    public string? UpdatedByUserId { get; set; }
}