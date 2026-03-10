using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations.Availity;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing eligibility check operations.
/// Eligibility checks are embedded within Encounters, which are embedded in the Patient aggregate.
/// 
/// Implements async polling pattern for Availity Coverages API:
/// 1. InitiateEligibilityCheckAsync - Starts the check, returns immediately with InProgress status
/// 2. PollEligibilityCheckAsync - Polls Availity if check is InProgress, updates status
/// 
/// RU Optimization Notes:
/// - InitiateEligibilityCheckAsync: ~2 RU (1 read + 1 write)
/// - PollEligibilityCheckAsync: ~2-3 RU (1 read + 1-2 writes)
/// - InitiateWithPatientAsync: ~1-2 RU (0 reads + 1-2 writes)
/// </summary>
public sealed class EligibilityCheckService : IEligibilityCheckService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IAvailityEligibilityClient _availityClient;
    private readonly IUserContextAccessor _userContext;

    /// <summary>
    /// Maximum number of times to poll Availity before giving up.
    /// </summary>
    private const int MaxPollAttempts = 20;

    /// <summary>
    /// Default poll interval hint for UI (milliseconds).
    /// </summary>
    private const int DefaultPollIntervalMs = 1500;

    public EligibilityCheckService(
        IPatientRepository patientRepository,
        IAvailityEligibilityClient availityClient,
        IUserContextAccessor userContext)
    {
        _patientRepository = patientRepository;
        _availityClient = availityClient;
        _userContext = userContext;
    }

    #region Initiate Operations (Async Kickoff)

    /// <summary>
    /// Initiates an eligibility check for a specific encounter and coverage enrollment.
    /// Returns immediately after sending the request to Availity (non-blocking).
    /// Caller should poll using GetEligibilityCheckAsync until terminal state.
    /// </summary>
    public async Task<EligibilityCheckEmbedded> RunEligibilityCheckAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        EligibilityCheckRequestDto request,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        ArgumentNullException.ThrowIfNull(request);

        // Load patient document (1 RU point read)
        var patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, patientId);
        if (patient is null)
            throw new KeyNotFoundException("Patient not found.");

        // Delegate to optimized method that reuses the loaded patient
        return await RunWithPatientAsync(
            patient,
            encounterId,
            request,
            cancellationToken,
            timeout);
    }

    /// <summary>
    /// Initiates an eligibility check using a pre-loaded patient document.
    /// Returns immediately after sending the request to Availity (non-blocking).
    /// </summary>
    public async Task<EligibilityCheckEmbedded> RunWithPatientAsync(
        Patient patient,
        string encounterId,
        EligibilityCheckRequestDto request,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        ArgumentNullException.ThrowIfNull(request);

        // Validate encounter and coverage enrollment exist in the loaded patient
        var encounter = patient.Encounters?.FirstOrDefault(e => e.Id == encounterId);
        if (encounter is null)
            throw new KeyNotFoundException("Encounter not found.");

        var coverage = patient.CoverageEnrollments?.FirstOrDefault(c => c.CoverageEnrollmentId == request.CoverageEnrollmentId);
        if (coverage is null)
            throw new KeyNotFoundException("Coverage enrollment not found.");

        var dos = request.OverrideDateOfService ?? encounter.VisitDate;

        // Create pending check entity
        var pending = new EligibilityCheckEmbedded
        {
            CoverageEnrollmentId = request.CoverageEnrollmentId,
            PayerId = coverage.PayerId,
            DateOfService = dos,
            RequestedAtUtc = DateTime.UtcNow,
            Status = "Pending",
            MemberIdSnapshot = coverage.MemberId,
            GroupNumberSnapshot = coverage.GroupNumber,
            PlanNameSnapshot = null,
            EffectiveDateSnapshot = coverage.EffectiveDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            TerminationDateSnapshot = coverage.TerminationDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            CoverageLines = [],
            Payloads = []
        };

        // Add pending check to encounter's in-memory collection
        encounter.EligibilityChecks ??= [];
        encounter.EligibilityChecks.Add(pending);
        encounter.UpdatedAtUtc = DateTime.UtcNow;

        // Persist pending check (1 RU write) - gives caller an ID even if Availity fails
        await _patientRepository.UpdateAsync(patient);

        // Call Availity to initiate the check (non-blocking)
        try
        {
            var availityRequest = BuildAvailityRequest(coverage, dos, request.ServiceTypeCodes);

            var response = await _availityClient.InitiateCoverageCheckAsync(availityRequest, cancellationToken);

            // Update with Availity response
            pending.AvailityCoverageId = response.CoverageId;
            pending.RawStatusCode = response.StatusCode;
            pending.RawStatusDescription = response.Status;
            pending.UpdatedAtUtc = DateTime.UtcNow;

            if (response.StatusCode is "0" or "R1")
            {
                // In Progress - set up for polling
                pending.Status = "InProgress";
                pending.NextPollAfterUtc = response.EtaDate ?? DateTime.UtcNow.AddMilliseconds(DefaultPollIntervalMs);
            }
            else if (response.StatusCode is "4" or "3")
            {
                // Immediately complete (rare but possible)
                pending.Status = "Complete";
                pending.CompletedAtUtc = DateTime.UtcNow;
                pending.NextPollAfterUtc = null;  // Clear poll time when complete

                // Map coverage details if available in immediate response
                if (response.Result is not null)
                {
                    pending.PlanNameSnapshot = response.Result.PlanName;
                    pending.GroupNumberSnapshot = response.Result.GroupNumber ?? pending.GroupNumberSnapshot;
                    pending.EffectiveDateSnapshot = response.Result.EligibilityStartDate ?? response.Result.CoverageStartDate;
                    pending.TerminationDateSnapshot = response.Result.EligibilityEndDate ?? response.Result.CoverageEndDate;
                    pending.CoverageLines = MapCoverageLines(response.Result.CoverageLines);

                    // Add payload refs if provided
                    if (response.Result.PayloadRefs is { Count: > 0 })
                    {
                        pending.Payloads ??= [];
                        foreach (var p in response.Result.PayloadRefs)
                        {
                            pending.Payloads.Add(new EligibilityPayloadEmbedded
                            {
                                Direction = p.Direction,
                                Format = p.Format,
                                StorageUrl = p.StorageUrl
                            });
                        }
                    }
                }
                else
                {
                    // Result is null even though complete - log warning or handle edge case
                    pending.ErrorMessage = "Check completed but no result data was provided by payer.";
                }
            }
            else
            {
                // Error
                pending.Status = "Failed";
                pending.CompletedAtUtc = DateTime.UtcNow;
                pending.NextPollAfterUtc = null;  // Clear poll time when failed
                pending.ErrorMessage = response.ErrorMessage;
                pending.ValidationMessages = response.ValidationMessages?
                    .Select(m => m.ErrorMessage ?? m.Code ?? "Unknown error")
                    .ToList();
            }

            encounter.UpdatedAtUtc = DateTime.UtcNow;
            await _patientRepository.UpdateAsync(patient);

            return pending;
        }
        catch (Exception ex)
        {
            await UpdateCheckStatusInMemoryAndPersist(
                patient, encounter, pending,
                "Failed", $"Failed to initiate check: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Poll Operations

    /// <summary>
    /// Gets eligibility check, polling Availity if check is still in progress.
    /// This method implements the polling proxy pattern.
    /// </summary>
    public async Task<EligibilityCheckEmbedded> GetEligibilityCheckAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eligibilityCheckId);

        // Load full patient document
        var patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, patientId);
        if (patient is null)
            throw new KeyNotFoundException("Patient not found.");

        var encounter = patient.Encounters?.FirstOrDefault(e => e.Id == encounterId);
        if (encounter is null)
            throw new KeyNotFoundException("Encounter not found.");

        var check = encounter.EligibilityChecks?.FirstOrDefault(c => c.EligibilityCheckId == eligibilityCheckId);
        if (check is null)
            throw new KeyNotFoundException("Eligibility check not found.");

        // If check is in progress and has an Availity coverage ID, poll Availity
        if (check.IsPollingRequired && !string.IsNullOrEmpty(check.AvailityCoverageId))
        {
            await PollAndUpdateAsync(patient, encounter, check, CancellationToken.None);
        }

        return check;
    }

    /// <summary>
    /// Polls Availity for the check status and updates the entity.
    /// </summary>
    private async Task PollAndUpdateAsync(
        Patient patient,
        EncounterEmbedded encounter,
        EligibilityCheckEmbedded check,
        CancellationToken cancellationToken)
    {
        // Check poll limits
        if (check.PollCount >= MaxPollAttempts)
        {
            check.Status = "Failed";
            check.CompletedAtUtc = DateTime.UtcNow;
            check.ErrorMessage = "Maximum poll attempts exceeded";
            check.UpdatedAtUtc = DateTime.UtcNow;
            encounter.UpdatedAtUtc = DateTime.UtcNow;
            await _patientRepository.UpdateAsync(patient);
            return;
        }

        try
        {
            var response = await _availityClient.PollCoverageStatusAsync(check.AvailityCoverageId!, cancellationToken);

            check.PollCount++;
            check.LastPolledAtUtc = DateTime.UtcNow;
            check.RawStatusCode = response.StatusCode;
            check.RawStatusDescription = response.Status;
            check.UpdatedAtUtc = DateTime.UtcNow;

            if (response.IsProcessing)
            {
                // Still in progress
                check.Status = "InProgress";
                check.NextPollAfterUtc = response.EtaDate ?? DateTime.UtcNow.AddMilliseconds(DefaultPollIntervalMs * (1 + check.PollCount / 5));
            }
            else if (response.IsComplete)
            {
                // Complete - map results
                check.Status = "Complete";
                check.CompletedAtUtc = DateTime.UtcNow;
                check.NextPollAfterUtc = null;  // Clear poll time when complete

                if (response.Result is not null)
                {
                    check.PlanNameSnapshot = response.Result.PlanName;
                    check.GroupNumberSnapshot = response.Result.GroupNumber ?? check.GroupNumberSnapshot;
                    check.EffectiveDateSnapshot = response.Result.EligibilityStartDate ?? response.Result.CoverageStartDate;
                    check.TerminationDateSnapshot = response.Result.EligibilityEndDate ?? response.Result.CoverageEndDate;
                    check.CoverageLines = MapCoverageLines(response.Result.CoverageLines);

                    // Add payload refs if provided
                    if (response.Result.PayloadRefs is { Count: > 0 })
                    {
                        check.Payloads ??= [];
                        foreach (var p in response.Result.PayloadRefs)
                        {
                            check.Payloads.Add(new EligibilityPayloadEmbedded
                            {
                                Direction = p.Direction,
                                Format = p.Format,
                                StorageUrl = p.StorageUrl
                            });
                        }
                    }
                }
                else
                {
                    // Result is null even though complete - log warning or handle edge case
                    check.ErrorMessage = "Check completed but no result data was provided by payer.";
                }
            }
            else
            {
                // Failed
                check.Status = "Failed";
                check.CompletedAtUtc = DateTime.UtcNow;
                check.NextPollAfterUtc = null;  // Clear poll time when failed
                check.ErrorMessage = response.ErrorMessage;
                check.ValidationMessages = response.ValidationMessages?
                    .Select(m => m.ErrorMessage ?? m.Code ?? "Unknown error")
                    .ToList();
            }

            encounter.UpdatedAtUtc = DateTime.UtcNow;
            await _patientRepository.UpdateAsync(patient);
        }
        catch (Exception ex)
        {
            // Don't fail the check on poll error - allow retry
            check.LastPolledAtUtc = DateTime.UtcNow;
            check.PollCount++;
            check.NextPollAfterUtc = DateTime.UtcNow.AddSeconds(5); // Retry after delay
            check.UpdatedAtUtc = DateTime.UtcNow;
            encounter.UpdatedAtUtc = DateTime.UtcNow;

            // Only fail if we've exhausted retries
            if (check.PollCount >= MaxPollAttempts)
            {
                check.Status = "Failed";
                check.CompletedAtUtc = DateTime.UtcNow;
                check.ErrorMessage = $"Polling failed after {MaxPollAttempts} attempts: {ex.Message}";
            }

            await _patientRepository.UpdateAsync(patient);
        }
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Gets all eligibility checks for an encounter.
    /// Note: Does NOT poll Availity for InProgress checks. Use GetEligibilityCheckAsync for individual polling.
    /// RU Cost: ~1 RU (1 point read) - reads full patient document but filters to encounter's checks.
    /// 
    /// OPTIMIZATION TIP: Client should inspect check.IsPollingRequired and check.NextPollAfterUtc
    /// to determine which checks need polling, then call GetEligibilityCheckAsync only for those.
    /// </summary>
    public async Task<List<EligibilityCheckEmbedded>> GetEligibilityChecksAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);

        return await _patientRepository.GetEligibilityChecksAsync(tenantId, practiceId, patientId, encounterId);
    }

    /// <summary>
    /// Gets eligibility checks for an encounter and optionally polls specific checks.
    /// This is an optimized method for UI polling scenarios where you want to:
    /// 1. Fetch the current state of all checks (1 RU)
    /// 2. Selectively poll only checks that need polling (0-N additional RUs)
    /// 
    /// RU Cost: ~1 RU (base read) + ~1.5 RU per pollCheckId that needs polling
    /// 
    /// Example: 3 checks, 2 need polling = 1 + (2 × 1.5) = 4 RU (vs 1 + 2×2.5 = 6 RU with individual calls)
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="practiceId">Practice identifier</param>
    /// <param name="patientId">Patient identifier</param>
    /// <param name="encounterId">Encounter identifier</param>
    /// <param name="pollCheckIds">Optional list of specific check IDs to poll. If null/empty, no polling occurs.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all eligibility checks (polled checks will have updated status)</returns>
    public async Task<List<EligibilityCheckEmbedded>> GetEligibilityChecksWithSelectivePollingAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        List<string>? pollCheckIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);

        // Load patient document once (1 RU point read)
        var patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, patientId);
        if (patient is null)
            throw new KeyNotFoundException("Patient not found.");

        var encounter = patient.Encounters?.FirstOrDefault(e => e.Id == encounterId);
        if (encounter is null)
            throw new KeyNotFoundException("Encounter not found.");

        var allChecks = encounter.EligibilityChecks ?? [];

        // If no specific checks to poll, return current state (no additional RUs)
        if (pollCheckIds is null || pollCheckIds.Count == 0)
            return allChecks;

        // Poll only the specified checks
        bool anyUpdates = false;
        foreach (var checkId in pollCheckIds)
        {
            var check = allChecks.FirstOrDefault(c => c.EligibilityCheckId == checkId);
            if (check is null)
                continue; // Skip if check not found

            // Only poll if check requires polling and has Availity coverage ID
            if (check.IsPollingRequired && !string.IsNullOrEmpty(check.AvailityCoverageId))
            {
                await PollAndUpdateAsync(patient, encounter, check, cancellationToken);
                anyUpdates = true;
            }
        }

        // If we polled and updated anything, return the updated list
        // Note: PollAndUpdateAsync already persists changes (~1.5 RU per check polled)
        return allChecks;
    }

    public async Task<CoverageLineEmbedded> AddCoverageLineAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        CoverageLineAddRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eligibilityCheckId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ServiceTypeCode, nameof(request.ServiceTypeCode));

        var coverageLine = request.ToEntity(_userContext.UserId);

        await _patientRepository.AddCoverageLineAsync(tenantId, practiceId, patientId, encounterId, eligibilityCheckId, coverageLine);
        return coverageLine;
    }

    public async Task AddEligibilityPayloadAsync(
        string tenantId,
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        EligibilityPayloadAddRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eligibilityCheckId);
        ArgumentNullException.ThrowIfNull(request);

        var payload = request.ToEntity(_userContext.UserId);

        await _patientRepository.AddEligibilityPayloadAsync(tenantId, practiceId, patientId, encounterId, eligibilityCheckId, payload);
    }

    #endregion

    #region Private Helper Methods

    private static AvailityEligibilityRequest BuildAvailityRequest(
        CoverageEnrollmentEmbedded coverage,
        DateTime dateOfService,
        List<string>? serviceTypeCodes)
    {
        return new AvailityEligibilityRequest
        {
            PayerId = coverage.PayerId,
            MemberId = coverage.MemberId,
            GroupNumber = coverage.GroupNumber,
            DateOfService = dateOfService,
            SubscriberFirstName = coverage.SubscriberFirstName,
            SubscriberLastName = coverage.SubscriberLastName,
            SubscriberDob = coverage.SubscriberDob?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            SubscriberRelationship = MapRelationship(coverage.RelationshipToSubscriber),
            ServiceTypeCodes = serviceTypeCodes
        };
    }

    private static string? MapRelationship(string? relationship)
    {
        return relationship?.ToUpperInvariant() switch
        {
            "SELF" => "18",
            "SPOUSE" => "01",
            "CHILD" => "19",
            _ => "G8" // Other
        };
    }

    private async Task UpdateCheckStatusInMemoryAndPersist(
        Patient patient,
        EncounterEmbedded encounter,
        EligibilityCheckEmbedded check,
        string status,
        string errorMessage)
    {
        check.CompletedAtUtc ??= DateTime.UtcNow;
        check.Status = status;
        check.ErrorMessage = errorMessage;
        check.UpdatedAtUtc = DateTime.UtcNow;
        encounter.UpdatedAtUtc = DateTime.UtcNow;

        await _patientRepository.UpdateAsync(patient);
    }

    private static List<CoverageLineEmbedded> MapCoverageLines(List<AvailityCoverageLine>? lines)
    {
        var result = new List<CoverageLineEmbedded>();
        if (lines is null)
            return result;

        foreach (var line in lines)
        {
            var embedded = new CoverageLineEmbedded
            {
                ServiceTypeCode = line.ServiceTypeCode,
                CoverageDescription = line.ServiceTypeDescription ?? line.CoverageType,
                NetworkIndicator = NormalizeNetwork(line.Network),
                AdditionalInfo = line.Notes
            };

            var amt = line.Amount?.Trim();
            if (!string.IsNullOrWhiteSpace(amt))
            {
                if (string.Equals(line.CoverageType, "Copay", StringComparison.OrdinalIgnoreCase)
                    && TryParseMoney(amt, out var copay))
                {
                    embedded.CopayAmount = copay;
                }
                else if (string.Equals(line.CoverageType, "Deductible", StringComparison.OrdinalIgnoreCase)
                    && TryParseMoney(amt, out var ded))
                {
                    embedded.DeductibleAmount = ded;
                }
                else if (string.Equals(line.CoverageType, "Allowance", StringComparison.OrdinalIgnoreCase)
                    && TryParseMoney(amt, out var allowance))
                {
                    embedded.AllowanceAmount = allowance;
                }
                else if (string.Equals(line.CoverageType, "Coinsurance", StringComparison.OrdinalIgnoreCase)
                    && TryParsePercent(amt, out var pct))
                {
                    embedded.CoinsurancePercent = pct;
                }
                else
                {
                    embedded.AdditionalInfo = string.IsNullOrWhiteSpace(embedded.AdditionalInfo)
                        ? amt
                        : embedded.AdditionalInfo + "; " + amt;
                }
            }

            result.Add(embedded);
        }

        return result;
    }

    private static string? NormalizeNetwork(string? network)
    {
        if (string.IsNullOrWhiteSpace(network))
            return null;

        var n = network.Trim();
        return n.Equals("InNetwork", StringComparison.OrdinalIgnoreCase) ? "IN"
            : n.Equals("OutOfNetwork", StringComparison.OrdinalIgnoreCase) ? "OUT"
            : n;
    }

    private static bool TryParseMoney(string input, out decimal value)
    {
        var cleaned = input.Replace("$", "").Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParsePercent(string input, out decimal value)
    {
        var cleaned = input.Replace("%", "").Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
        {
            if (value > 0 && value <= 1) value *= 100;
            return true;
        }
        return false;
    }

    #endregion
}
