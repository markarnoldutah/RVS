using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RVS.API.Services;

/// <summary>
/// Service for single-screen patient check-in workflow.
/// Orchestrates patient upsert, coverage enrollment upsert, encounter creation,
/// coverage decision, and eligibility check execution with optimized Cosmos DB operations.
/// 
/// RU Optimization Strategy:
/// 1. Single patient document load (1 RU point read)
/// 2. All in-memory modifications (patient, coverages, encounter, decision)
/// 3. Single document write before eligibility checks (1 RU)
/// 4. Eligibility checks with pre-loaded patient (0 additional reads)
/// 5. Final document write after eligibility (1 RU)
/// 
/// Total: ~3-4 RU vs. ~15-20 RU for separate API calls
/// 
/// Eligibility Check Behavior:
/// - Eligibility checks are ONLY run when a CoverageDecision is provided in the request.
/// - If CoverageDecision is null, check-in completes without running eligibility checks.
/// - When CoverageDecision is provided, ONLY the PRIMARY coverage is checked.
/// - Secondary coverage is persisted in the decision but NOT checked during check-in.
/// - To check secondary coverage, use the eligibility-checks/run endpoint separately.
/// </summary>
public sealed class PatientCheckInService : IPatientCheckInService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IEligibilityCheckService _eligibilityCheckService;
    private readonly IUserContextAccessor _userContext;

    public PatientCheckInService(
        IPatientRepository patientRepository,
        IEligibilityCheckService eligibilityCheckService,
        IUserContextAccessor userContext)
    {
        _patientRepository = patientRepository;
        _eligibilityCheckService = eligibilityCheckService;
        _userContext = userContext;
    }

    public async Task<PatientCheckInResponseDto> CheckInAsync(
        string tenantId,
        string practiceId,
        PatientCheckInRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
        ArgumentNullException.ThrowIfNull(request);

        var userId = _userContext.UserId;
        var warnings = new List<string>();
        var coverageResults = new List<CoverageEnrollmentCheckInResultDto>();
        var eligibilityResults = new List<EligibilityCheckResultDto>();

        Patient patient;
        bool patientWasCreated = false;
        bool encounterWasCreated = false;

        // =====================================================
        // PHASE 1: Load or Create Patient (1 RU read or 1 RU write)
        // =====================================================
        if (!string.IsNullOrWhiteSpace(request.PatientId))
        {
            // Load existing patient
            patient = await _patientRepository.GetByIdAsync(tenantId, practiceId, request.PatientId)
                ?? throw new KeyNotFoundException($"Patient {request.PatientId} not found.");

            // Apply demographic updates if provided
            if (request.Patient is not null)
            {
                ApplyPatientDemographics(patient, request.Patient, userId);
            }
        }
        else
        {
            // Create new patient
            if (request.Patient is null)
                throw new ArgumentException("Patient demographics are required when creating a new patient.", nameof(request));

            patient = CreateNewPatient(tenantId, practiceId, request.Patient, userId);
            patientWasCreated = true;
        }

        // =====================================================
        // PHASE 2: Upsert Coverage Enrollments (in-memory)
        // =====================================================
        if (request.CoverageEnrollments is not null)
        {
            foreach (var coverageDto in request.CoverageEnrollments)
            {
                bool wasCreated;
                CoverageEnrollmentEmbedded coverage;

                // Check if coverage already exists on the patient (by ID)
                var existingCoverage = !string.IsNullOrWhiteSpace(coverageDto.CoverageEnrollmentId)
                    ? patient.CoverageEnrollments?.FirstOrDefault(c => c.CoverageEnrollmentId == coverageDto.CoverageEnrollmentId)
                    : null;

                if (existingCoverage is not null)
                {
                    // Update existing coverage
                    ApplyCoverageUpdate(existingCoverage, coverageDto, userId);
                    coverage = existingCoverage;
                    wasCreated = false;
                }
                else
                {
                    // Create new coverage (uses client-provided ID or generates one)
                    coverage = CreateNewCoverageEnrollment(coverageDto, userId);
                    patient.CoverageEnrollments ??= [];
                    patient.CoverageEnrollments.Add(coverage);
                    wasCreated = true;
                }

                coverageResults.Add(new CoverageEnrollmentCheckInResultDto
                {
                    CoverageEnrollmentId = coverage.CoverageEnrollmentId,
                    PayerId = coverage.PayerId,
                    PlanType = coverage.PlanType,
                    MemberId = coverage.MemberId,
                    GroupNumber = coverage.GroupNumber,
                    CobPriorityHint = coverage.CobPriorityHint,
                    WasCreated = wasCreated
                });
            }
        }

        // =====================================================
        // PHASE 3: Create or Load Encounter (in-memory)
        // =====================================================
        EncounterEmbedded encounter;

        if (!string.IsNullOrWhiteSpace(request.EncounterId))
        {
            // Load existing encounter
            encounter = patient.Encounters?
                .FirstOrDefault(e => e.Id == request.EncounterId)
                ?? throw new KeyNotFoundException($"Encounter {request.EncounterId} not found.");

            // Apply updates if provided
            if (request.Encounter is not null)
            {
                ApplyEncounterUpdate(encounter, request.Encounter, userId);
            }
        }
        else
        {
            // Create new encounter
            if (request.Encounter is null)
                throw new ArgumentException("Encounter details are required when creating a new encounter.", nameof(request));

            encounter = CreateNewEncounter(request.Encounter, userId);
            patient.Encounters ??= [];
            patient.Encounters.Add(encounter);
            encounterWasCreated = true;
        }

        // =====================================================
        // PHASE 4: Set Coverage Decision (in-memory)
        // =====================================================
        CoverageDecisionResponseDto? coverageDecisionResult = null;

        if (request.CoverageDecision is not null)
        {
            // Validate referenced coverage enrollments exist
            ValidateCoverageEnrollmentExists(patient, request.CoverageDecision.PrimaryCoverageEnrollmentId, "Primary");

            if (!string.IsNullOrWhiteSpace(request.CoverageDecision.SecondaryCoverageEnrollmentId))
            {
                ValidateCoverageEnrollmentExists(patient, request.CoverageDecision.SecondaryCoverageEnrollmentId, "Secondary");
            }

            encounter.CoverageDecision = new CoverageDecisionEmbedded
            {
                PrimaryCoverageEnrollmentId = request.CoverageDecision.PrimaryCoverageEnrollmentId,
                SecondaryCoverageEnrollmentId = request.CoverageDecision.SecondaryCoverageEnrollmentId,
                CobReason = request.CoverageDecision.CobReason,
                CobDeterminationSource = request.CoverageDecision.OverriddenByUser ? "USER" : "AUTO",
                OverriddenByUser = request.CoverageDecision.OverriddenByUser,
                OverrideNote = request.CoverageDecision.OverrideNote,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            coverageDecisionResult = encounter.CoverageDecision.ToDto();
        }

        // =====================================================
        // PHASE 5: Persist Patient Document (1 RU write)
        // =====================================================
        if (patientWasCreated)
        {
            await _patientRepository.CreateAsync(patient);
        }
        else
        {
            patient.MarkAsUpdated(userId);
            await _patientRepository.UpdateAsync(patient);
        }

        // =====================================================
        // PHASE 6: Run Eligibility Checks (Only if CoverageDecision provided)
        // =====================================================
        // Eligibility checks ONLY run when a CoverageDecision is provided.
        // This gives the client explicit control over when checks are executed.
        // Without a CoverageDecision, check-in completes without eligibility verification.
        // 
        // NOTE: Only the PRIMARY coverage is checked during check-in.
        // Secondary coverage checks, if needed, should be initiated separately
        // via the eligibility-checks/run endpoint.
        
        if (request.CoverageDecision is not null)
        {
            // Check primary coverage only
            var primaryCoverage = patient.CoverageEnrollments?
                .FirstOrDefault(c => c.CoverageEnrollmentId == request.CoverageDecision.PrimaryCoverageEnrollmentId);
            
            if (primaryCoverage is not null && 
                !string.IsNullOrWhiteSpace(primaryCoverage.PayerId) && 
                !string.IsNullOrWhiteSpace(primaryCoverage.MemberId))
            {
                await RunEligibilityCheckAsync(
                    patient, encounter, primaryCoverage, 
                    eligibilityResults, warnings, cancellationToken);
            }
            
            // NOTE: Secondary coverage is NOT checked during check-in.
            // The SecondaryCoverageEnrollmentId is persisted in the CoverageDecision (PHASE 4),
            // but eligibility verification for secondary coverage must be initiated separately
            // via POST /api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks/run
        }
        // NOTE: If no CoverageDecision is provided, no eligibility checks are run.
        // This is intentional - the client must explicitly request eligibility verification.

        // =====================================================
        // PHASE 7: Build Response
        // =====================================================
        return new PatientCheckInResponseDto
        {
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            Patient = new PatientCheckInSummaryDto
            {
                PatientId = patient.Id,
                FirstName = patient.FirstName,
                LastName = patient.LastName,
                DateOfBirth = patient.DateOfBirth,
                Email = patient.Email,
                Phone = patient.Phone,
                WasCreated = patientWasCreated
            },
            Encounter = new EncounterCheckInSummaryDto
            {
                EncounterId = encounter.Id,
                LocationId = encounter.LocationId,
                VisitDate = encounter.VisitDate,
                VisitType = encounter.VisitType,
                Status = encounter.Status,
                ExternalRef = encounter.ExternalRef,
                WasCreated = encounterWasCreated
            },
            CoverageEnrollments = coverageResults,
            CoverageDecision = coverageDecisionResult,
            EligibilityChecks = eligibilityResults,
            AllEligibilityChecksSucceeded = eligibilityResults.All(r => r.Succeeded),
            Warnings = warnings
        };
    }

    // =====================================================
    // Helper Methods
    // =====================================================

    private static Patient CreateNewPatient(
        string tenantId,
        string practiceId,
        PatientCheckInDemographicsDto dto,
        string? userId)
    {
        return new Patient
        {
            TenantId = tenantId,
            PracticeId = practiceId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            DateOfBirth = dto.DateOfBirth,
            Email = dto.Email,
            Phone = dto.Phone,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            CoverageEnrollments = [],
            Encounters = []
        };
    }

    private static void ApplyPatientDemographics(
        Patient patient,
        PatientCheckInDemographicsDto dto,
        string? userId)
    {
        if (!string.IsNullOrWhiteSpace(dto.FirstName))
            patient.FirstName = dto.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.LastName))
            patient.LastName = dto.LastName.Trim();
        if (dto.DateOfBirth.HasValue)
            patient.DateOfBirth = dto.DateOfBirth;
        if (dto.Email is not null)
            patient.Email = dto.Email;
        if (dto.Phone is not null)
            patient.Phone = dto.Phone;

        patient.MarkAsUpdated(userId);
    }

    private static CoverageEnrollmentEmbedded CreateNewCoverageEnrollment(
        CoverageEnrollmentUpsertDto dto,
        string? userId)
    {
        return new CoverageEnrollmentEmbedded
        {
            // Use client-provided ID if available, otherwise let the entity generate one
            CoverageEnrollmentId = !string.IsNullOrWhiteSpace(dto.CoverageEnrollmentId) 
                ? dto.CoverageEnrollmentId 
                : Guid.NewGuid().ToString(),
            PayerId = dto.PayerId,
            PlanType = dto.PlanType,
            MemberId = dto.MemberId,
            GroupNumber = dto.GroupNumber,
            RelationshipToSubscriber = dto.RelationshipToSubscriber,
            SubscriberFirstName = dto.SubscriberFirstName,
            SubscriberLastName = dto.SubscriberLastName,
            SubscriberDob = dto.SubscriberDob,
            IsEmployerPlan = dto.IsEmployerPlan,
            EffectiveDate = dto.EffectiveDate,
            TerminationDate = dto.TerminationDate,
            CobPriorityHint = dto.CobPriorityHint,
            CobNotes = dto.CobNotes,
            IsEnabled = true,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        };
    }

    private static void ApplyCoverageUpdate(
        CoverageEnrollmentEmbedded coverage,
        CoverageEnrollmentUpsertDto dto,
        string? userId)
    {
        coverage.PayerId = dto.PayerId;
        coverage.PlanType = dto.PlanType;
        coverage.MemberId = dto.MemberId;
        coverage.GroupNumber = dto.GroupNumber;
        coverage.RelationshipToSubscriber = dto.RelationshipToSubscriber;
        coverage.SubscriberFirstName = dto.SubscriberFirstName;
        coverage.SubscriberLastName = dto.SubscriberLastName;
        coverage.SubscriberDob = dto.SubscriberDob;
        coverage.IsEmployerPlan = dto.IsEmployerPlan;
        coverage.EffectiveDate = dto.EffectiveDate;
        coverage.TerminationDate = dto.TerminationDate;
        coverage.CobPriorityHint = dto.CobPriorityHint;
        coverage.CobNotes = dto.CobNotes;
        coverage.UpdatedAtUtc = DateTime.UtcNow;
        coverage.UpdatedByUserId = userId;
    }

    private static EncounterEmbedded CreateNewEncounter(
        EncounterCheckInDto dto,
        string? userId)
    {
        return new EncounterEmbedded
        {
            LocationId = dto.LocationId,
            VisitDate = dto.VisitDate ?? DateTime.UtcNow,
            VisitType = dto.VisitType,
            Status = "scheduled",
            ExternalRef = dto.ExternalRef,
            CreatedByUserId = userId,
            EligibilityChecks = [],
            CoverageDecision = null
        };
    }

    private static void ApplyEncounterUpdate(
        EncounterEmbedded encounter,
        EncounterCheckInDto dto,
        string? userId)
    {
        if (!string.IsNullOrWhiteSpace(dto.LocationId))
            encounter.LocationId = dto.LocationId;
        if (dto.VisitDate.HasValue)
            encounter.VisitDate = dto.VisitDate.Value;
        if (!string.IsNullOrWhiteSpace(dto.VisitType))
            encounter.VisitType = dto.VisitType;
        if (dto.ExternalRef is not null)
            encounter.ExternalRef = dto.ExternalRef;

        encounter.UpdatedAtUtc = DateTime.UtcNow;
        encounter.UpdatedByUserId = userId;
    }

    private static void ValidateCoverageEnrollmentExists(
        Patient patient,
        string coverageEnrollmentId,
        string coverageType)
    {
        var exists = patient.CoverageEnrollments?
            .Any(c => c.CoverageEnrollmentId == coverageEnrollmentId) ?? false;

        if (!exists)
            throw new KeyNotFoundException($"{coverageType} coverage enrollment {coverageEnrollmentId} not found.");
    }

    private static EligibilityCheckResultDto MapToEligibilityCheckResult(EligibilityCheckEmbedded check)
    {
        return new EligibilityCheckResultDto
        {
            EligibilityCheckId = check.EligibilityCheckId,
            CoverageEnrollmentId = check.CoverageEnrollmentId,
            PayerId = check.PayerId,
            DateOfService = check.DateOfService,
            RequestedAtUtc = check.RequestedAtUtc,
            CompletedAtUtc = check.CompletedAtUtc,
            Status = check.Status,
            RawStatusCode = check.RawStatusCode,
            RawStatusDescription = check.RawStatusDescription,
            PlanName = check.PlanNameSnapshot,
            EffectiveDate = check.EffectiveDateSnapshot,
            TerminationDate = check.TerminationDateSnapshot,
            ErrorMessage = check.ErrorMessage,
            ValidationMessages = check.ValidationMessages,
            CoverageLines = check.CoverageLines?.Select(cl => cl.ToCoverageLineDto()).ToList() ?? []
        };
    }

    /// <summary>
    /// Runs an eligibility check for a single coverage and adds the result to the results list.
    /// Exceptions are caught and added to the warnings list.
    /// </summary>
    private async Task RunEligibilityCheckAsync(
        Patient patient,
        EncounterEmbedded encounter,
        CoverageEnrollmentEmbedded coverage,
        List<EligibilityCheckResultDto> eligibilityResults,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var checkRequest = new EligibilityCheckRequestDto
            {
                CoverageEnrollmentId = coverage.CoverageEnrollmentId,
                ServiceTypeCodes = GetServiceTypeCodesForVisit(encounter.VisitType, coverage.PlanType)
            };

            // Use optimized method with pre-loaded patient (saves 1 RU per check)
            var result = await _eligibilityCheckService.RunWithPatientAsync(
                patient,
                encounter.Id,
                checkRequest,
                cancellationToken,
                timeout: null);

            eligibilityResults.Add(MapToEligibilityCheckResult(result));
        }
        catch (Exception ex)
        {
            warnings.Add($"Eligibility check for {coverage.CoverageEnrollmentId} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the service type codes based on the visit type and coverage plan type.
    /// This ensures we only request relevant benefit categories from Availity.
    /// 
    /// Service type codes follow the X12 EDI 270/271 standard for healthcare eligibility.
    /// </summary>
    private static List<string>? GetServiceTypeCodesForVisit(string? visitType, string? planType)
    {
        // Normalize inputs
        var normalizedVisitType = visitType?.ToUpperInvariant().Replace(" ", "");
        var normalizedPlanType = planType?.ToUpperInvariant();

        // Determine visit category
        var isRoutineVision = IsRoutineVisionVisit(normalizedVisitType);
        var isMedicalEye = IsMedicalEyeVisit(normalizedVisitType);
        var isContactLens = IsContactLensVisit(normalizedVisitType);
        var isGlassesOnly = IsGlassesOnlyVisit(normalizedVisitType);

        // Select appropriate codes based on visit type and plan type combination
        return (isRoutineVision, isMedicalEye, isContactLens, isGlassesOnly, normalizedPlanType) switch
        {
            // Routine Vision Visits
            (true, _, _, _, "VISION") => ["AL", "AN", "AM", "AO", "CP"], // Full vision benefits
            (true, _, _, _, "MEDICAL") => ["BR", "98"],                   // Medical plans for routine vision (limited)
            (true, _, _, _, "MEDICARE") => ["BR", "98"],                  // Medicare for routine vision (limited)
            
            // Medical Eye Visits
            (_, true, _, _, "VISION") => ["AL", "BR", "30"],             // Vision plan covering medical eye
            (_, true, _, _, "MEDICAL") => ["30", "98", "BR"],            // Medical plan for medical eye (primary use case)
            (_, true, _, _, "MEDICARE") => ["30", "98", "BR"],           // Medicare for medical eye
            
            // Contact Lens Visits
            (_, _, true, _, "VISION") => ["AL", "AO"],                   // Contact lens benefits (vision plan)
            (_, _, true, _, "MEDICAL") => ["BR", "98"],                  // Medical plan for contact lens fitting
            
            // Glasses/Eyewear Only Visits
            (_, _, _, true, "VISION") => ["AM", "AO", "CP"],             // Frames, lenses, eyewear
            (_, _, _, true, _) => null,                                   // Non-vision plans rarely cover eyewear
            
            // Dental (if supported)
            (_, _, _, _, "DENTAL") => ["23", "35", "41"],                // Diagnostic, general, preventive only
            
            // Default fallback based on plan type only
            (false, false, false, false, "VISION") => ["AL", "AN"],      // Basic vision exam
            (false, false, false, false, "MEDICAL") => ["30", "98"],     // General medical
            (false, false, false, false, "MEDICARE") => ["30", "98"],    // General Medicare
            
            // Ultimate fallback - let Availity use default
            _ => null
        };
    }

    /// <summary>
    /// Determines if the visit type represents a routine vision exam.
    /// </summary>
    private static bool IsRoutineVisionVisit(string? normalizedVisitType)
    {
        if (string.IsNullOrWhiteSpace(normalizedVisitType)) return false;
        
        return normalizedVisitType.Contains("ROUTINE") ||
               normalizedVisitType.Contains("ANNUAL") ||
               normalizedVisitType.Contains("COMPREHENSIVE") ||
               normalizedVisitType == "ROUTINEVISION";
    }

    /// <summary>
    /// Determines if the visit type represents a medical eye condition.
    /// </summary>
    private static bool IsMedicalEyeVisit(string? normalizedVisitType)
    {
        if (string.IsNullOrWhiteSpace(normalizedVisitType)) return false;
        
        return normalizedVisitType.Contains("MEDICAL") ||
               normalizedVisitType.Contains("EMERGENCY") ||
               normalizedVisitType.Contains("URGENT") ||
               normalizedVisitType.Contains("REDEYE") ||
               normalizedVisitType.Contains("INFECTION") ||
               normalizedVisitType.Contains("INJURY") ||
               normalizedVisitType.Contains("GLAUCOMA") ||
               normalizedVisitType.Contains("DIABETIC") ||
               normalizedVisitType.Contains("RETINAL");
    }

    /// <summary>
    /// Determines if the visit type is for contact lens fitting or evaluation.
    /// </summary>
    private static bool IsContactLensVisit(string? normalizedVisitType)
    {
        if (string.IsNullOrWhiteSpace(normalizedVisitType)) return false;
        
        return normalizedVisitType.Contains("CONTACT") ||
               normalizedVisitType.Contains("CL") ||
               normalizedVisitType == "CONTACTLENSFITTING";
    }

    /// <summary>
    /// Determines if the visit is for glasses dispensing or eyewear only.
    /// </summary>
    private static bool IsGlassesOnlyVisit(string? normalizedVisitType)
    {
        if (string.IsNullOrWhiteSpace(normalizedVisitType)) return false;
        
        return normalizedVisitType.Contains("GLASSES") ||
               normalizedVisitType.Contains("DISPENSE") ||
               normalizedVisitType.Contains("EYEWEAR") ||
               normalizedVisitType.Contains("FRAME") ||
               normalizedVisitType == "GLASSESDISPENSING";
    }


}
