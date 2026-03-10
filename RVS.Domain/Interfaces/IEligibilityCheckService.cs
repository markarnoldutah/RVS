using System;
using System.Threading;
using System.Threading.Tasks;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing eligibility check operations.
/// Eligibility checks are embedded within Encounters, which are embedded in the Patient aggregate.
/// 
/// Implements async polling pattern for Availity Coverages API:
/// 1. RunEligibilityCheckAsync - Initiates the check, returns immediately with InProgress status
/// 2. GetEligibilityCheckAsync - Returns current status, polls Availity if still InProgress
/// 
/// Status Flow:
/// Pending ? InProgress ? Complete | Failed | Canceled
/// </summary>
public interface IEligibilityCheckService
{
    // =====================================================
    // Initiate Operations (Async Kickoff)
    // =====================================================

    /// <summary>
    /// Initiates an eligibility check for a specific encounter and coverage enrollment.
    /// Returns immediately after sending the request to Availity (non-blocking).
    /// 
    /// The returned check will have:
    /// - Status = "InProgress" (if Availity accepted the request)
    /// - Status = "Failed" (if request validation failed)
    /// - AvailityCoverageId (for subsequent polling)
    /// - NextPollAfterUtc (suggested time to poll again)
    /// 
    /// Caller should poll using GetEligibilityCheckAsync until check.IsTerminal == true.
    /// RU Cost: ~2 RU (1 point read + 1 write)
    /// </summary>
    Task<EligibilityCheckEmbedded> RunEligibilityCheckAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        EligibilityCheckRequestDto request,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null);

    /// <summary>
    /// Initiates an eligibility check using a pre-loaded patient document.
    /// Use this method in single-screen/check-in workflows where the patient is already loaded.
    /// Returns immediately after sending the request to Availity (non-blocking).
    /// RU Cost: ~1-2 RU (0 reads + 1-2 writes)
    /// </summary>
    /// <param name="patient">Pre-loaded patient document with encounters and coverage enrollments.</param>
    /// <param name="encounterId">The encounter identifier.</param>
    /// <param name="request">The eligibility check request details.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <param name="timeout">Optional timeout for the Availity call.</param>
    /// <returns>The initiated eligibility check (typically with Status = "InProgress").</returns>
    Task<EligibilityCheckEmbedded> RunWithPatientAsync(
        Patient patient,
        string encounterId,
        EligibilityCheckRequestDto request,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null);

    // =====================================================
    // Read Operations (with Polling Proxy)
    // =====================================================

    /// <summary>
    /// Gets all eligibility checks for an encounter.
    /// Note: Does NOT poll Availity for InProgress checks. Use GetEligibilityCheckAsync for individual polling.
    /// RU Cost: ~1 RU (point read)
    /// </summary>
    Task<List<EligibilityCheckEmbedded>> GetEligibilityChecksAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId);

    /// <summary>
    /// Gets eligibility checks for an encounter and optionally polls specific checks.
    /// This is an optimized method for UI polling scenarios.
    /// 
    /// Usage pattern:
    /// 1. Call with pollCheckIds = null to get current state (1 RU)
    /// 2. Client examines response and identifies checks that need polling (based on NextPollAfterUtc)
    /// 3. Call again with pollCheckIds set to only those needing updates
    /// 
    /// RU Cost: ~1 RU (base read) + ~1.5 RU per pollCheckId that actually gets polled
    /// 
    /// Example: If 3 checks exist but only 2 need polling:
    ///   Cost = 1 (read) + 2×1.5 (polls) = 4 RU
    ///   vs individual GET calls: 1 (list) + 2×2.5 (individual) = 6 RU
    ///   Savings: 33%
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="practiceId">Practice identifier</param>
    /// <param name="patientId">Patient identifier</param>
    /// <param name="encounterId">Encounter identifier</param>
    /// <param name="pollCheckIds">Optional list of specific check IDs to poll. If null/empty, no polling occurs.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all eligibility checks (polled checks will have updated status)</returns>
    Task<List<EligibilityCheckEmbedded>> GetEligibilityChecksWithSelectivePollingAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        List<string>? pollCheckIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific eligibility check by ID.
    /// If the check is InProgress, this method polls Availity and updates the status before returning.
    /// This is the recommended method for UI polling loops when polling a single check.
    /// RU Cost: ~2.5 RU (1 read + 1.5 write) if polling occurs, ~1 RU if check is already terminal
    /// </summary>
    Task<EligibilityCheckEmbedded> GetEligibilityCheckAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId);

    // =====================================================
    // Write Operations
    // =====================================================

    /// <summary>
    /// Adds a coverage line to an existing eligibility check.
    /// </summary>
    Task<CoverageLineEmbedded> AddCoverageLineAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        CoverageLineAddRequestDto request);

    /// <summary>
    /// Adds an eligibility payload reference to an existing eligibility check.
    /// </summary>
    Task AddEligibilityPayloadAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        EligibilityPayloadAddRequestDto request);
}
