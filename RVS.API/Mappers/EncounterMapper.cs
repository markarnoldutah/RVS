using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between EncounterEmbedded entities and DTOs at the API boundary.
/// Note: Encounters are now embedded within Patient documents.
/// </summary>
public static class EncounterMapper
{
    // =====================================================
    // Entity ? DTO (Response) Mappings
    // =====================================================

    public static EncounterDetailResponseDto ToDetailDto(this EncounterEmbedded encounter, string patientId, string practiceId)
    {
        ArgumentNullException.ThrowIfNull(encounter);

        return new EncounterDetailResponseDto
        {
            EncounterId = encounter.Id,
            PatientId = patientId,
            PracticeId = practiceId,
            LocationId = encounter.LocationId ?? string.Empty,
            VisitDate = encounter.VisitDate,
            VisitType = encounter.VisitType,
            Status = encounter.Status,
            HasEligibilityChecks = encounter.EligibilityChecks?.Count > 0,
            PrimaryPayerName = null, // TODO: This needs to be derived from related data
            CobSummary = null, // TODO: This needs to be derived from CoverageDecision
            CoverageDecision = encounter.CoverageDecision?.ToDto(),
            EligibilityChecks = encounter.EligibilityChecks?.Select(ToDto).ToList() ?? [],
            // Audit properties (inherited from base EncounterSummaryResponseDto)
            CreatedAtUtc = encounter.CreatedAtUtc,
            UpdatedAtUtc = encounter.UpdatedAtUtc,
            CreatedByUserId = encounter.CreatedByUserId,
            UpdatedByUserId = encounter.UpdatedByUserId
        };
    }

    public static EncounterSummaryResponseDto ToSummaryDto(this EncounterEmbedded encounter, string patientId, string practiceId)
    {
        ArgumentNullException.ThrowIfNull(encounter);

        return new EncounterSummaryResponseDto
        {
            EncounterId = encounter.Id,
            PatientId = patientId,
            PracticeId = practiceId,
            LocationId = encounter.LocationId ?? string.Empty,
            VisitDate = encounter.VisitDate,
            VisitType = encounter.VisitType,
            Status = encounter.Status,
            HasEligibilityChecks = encounter.EligibilityChecks?.Count > 0,
            PrimaryPayerName = null, // TODO: This needs to be derived from related data
            CobSummary = null, // TODO: This needs to be derived from CoverageDecision
            // Audit properties
            CreatedAtUtc = encounter.CreatedAtUtc,
            UpdatedAtUtc = encounter.UpdatedAtUtc,
            CreatedByUserId = encounter.CreatedByUserId,
            UpdatedByUserId = encounter.UpdatedByUserId
        };
    }

    public static CoverageDecisionResponseDto ToDto(this CoverageDecisionEmbedded decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new CoverageDecisionResponseDto
        {
            EncounterCoverageDecisionId = decision.EncounterCoverageDecisionId,
            PrimaryCoverageEnrollmentId = decision.PrimaryCoverageEnrollmentId,
            SecondaryCoverageEnrollmentId = decision.SecondaryCoverageEnrollmentId,
            CobReason = decision.CobReason,
            CobDeterminationSource = decision.CobDeterminationSource,
            OverriddenByUser = decision.OverriddenByUser,
            OverrideNote = decision.OverrideNote,
            CreatedAtUtc = decision.CreatedAtUtc,
            CreatedByUserId = decision.CreatedByUserId,
            UpdatedAtUtc = decision.UpdatedAtUtc,
            UpdatedByUserId = decision.UpdatedByUserId
        };
    }

    public static EligibilityCheckResponseDto ToDto(this EligibilityCheckEmbedded check)
    {
        ArgumentNullException.ThrowIfNull(check);

        return new EligibilityCheckResponseDto
        {
            EligibilityCheckId = check.EligibilityCheckId,
            CoverageEnrollmentId = check.CoverageEnrollmentId,
            PayerId = check.PayerId,
            DateOfService = check.DateOfService,
            RequestedAtUtc = check.RequestedAtUtc,
            CompletedAtUtc = check.CompletedAtUtc,
            Status = check.Status,
            NextPollAfterUtc = check.NextPollAfterUtc,
            PollCount = check.PollCount,
            PayerName = null, // TODO: This needs to be derived from related payer data
            RawStatusCode = check.RawStatusCode,
            RawStatusDescription = check.RawStatusDescription,
            MemberIdSnapshot = check.MemberIdSnapshot,
            GroupNumberSnapshot = check.GroupNumberSnapshot,
            PlanNameSnapshot = check.PlanNameSnapshot,
            EffectiveDateSnapshot = check.EffectiveDateSnapshot,
            TerminationDateSnapshot = check.TerminationDateSnapshot,
            ErrorMessage = check.ErrorMessage,
            ValidationMessages = check.ValidationMessages,
            CoverageLines = check.CoverageLines?.Select(ToCoverageLineDto).ToList() ?? [],
            CreatedAtUtc = check.CreatedAtUtc,
            UpdatedAtUtc = check.UpdatedAtUtc,
            CreatedByUserId = check.CreatedByUserId,
            UpdatedByUserId = check.UpdatedByUserId
        };
    }

    public static EligibilityCheckSummaryResponseDto ToSummaryDto(this EligibilityCheckEmbedded check)
    {
        ArgumentNullException.ThrowIfNull(check);

        return new EligibilityCheckSummaryResponseDto
        {
            EligibilityCheckId = check.EligibilityCheckId,
            CoverageEnrollmentId = check.CoverageEnrollmentId,
            PayerId = check.PayerId,
            DateOfService = check.DateOfService,
            RequestedAtUtc = check.RequestedAtUtc,
            CompletedAtUtc = check.CompletedAtUtc,
            Status = check.Status,
            NextPollAfterUtc = check.NextPollAfterUtc,
            PollCount = check.PollCount,
            PayerName = null, // TODO: This needs to be derived from related payer data
            CreatedAtUtc = check.CreatedAtUtc,
            UpdatedAtUtc = check.UpdatedAtUtc,
            CreatedByUserId = check.CreatedByUserId,
            UpdatedByUserId = check.UpdatedByUserId
        };
    }

    public static CoverageLineResponseDto ToCoverageLineDto(this CoverageLineEmbedded line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return new CoverageLineResponseDto
        {
            CoverageLineId = line.CoverageLineId,
            ServiceTypeCode = line.ServiceTypeCode,
            CoverageDescription = line.CoverageDescription,
            CopayAmount = line.CopayAmount,
            CoinsurancePercent = line.CoinsurancePercent,
            DeductibleAmount = line.DeductibleAmount,
            RemainingDeductible = line.RemainingDeductible,
            OutOfPocketMax = line.OutOfPocketMax,
            RemainingOutOfPocket = line.RemainingOutOfPocket,
            AllowanceAmount = line.AllowanceAmount,
            NetworkIndicator = line.NetworkIndicator,
            EffectiveDate = line.EffectiveDate,
            TerminationDate = line.TerminationDate,
            AdditionalInfo = line.AdditionalInfo,
            CreatedAtUtc = line.CreatedAtUtc,
            UpdatedAtUtc = line.UpdatedAtUtc,
            CreatedByUserId = line.CreatedByUserId,
            UpdatedByUserId = line.UpdatedByUserId
        };
    }

    // =====================================================
    // DTO ? Entity (Request) Mappings
    // =====================================================

    /// <summary>
    /// Maps EncounterCreateRequestDto to EncounterEmbedded entity.
    /// </summary>
    public static EncounterEmbedded ToEntity(
        this EncounterCreateRequestDto dto,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new EncounterEmbedded
        {
            LocationId = dto.LocationId,
            VisitDate = dto.VisitDate,
            VisitType = dto.VisitType,
            ExternalRef = dto.ExternalRef,
            Status = "scheduled",
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// Applies updates from EncounterUpdateRequestDto to existing EncounterEmbedded entity.
    /// Used within repository update action delegate.
    /// </summary>
    public static void ApplyUpdate(
        this EncounterEmbedded encounter,
        EncounterUpdateRequestDto dto,
        string? updatedByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.VisitDate.HasValue) encounter.VisitDate = dto.VisitDate.Value;
        if (!string.IsNullOrWhiteSpace(dto.VisitType)) encounter.VisitType = dto.VisitType;
        if (!string.IsNullOrWhiteSpace(dto.LocationId)) encounter.LocationId = dto.LocationId;
        if (!string.IsNullOrWhiteSpace(dto.Status)) encounter.Status = dto.Status;

        // Set audit properties
        encounter.UpdatedAtUtc = DateTime.UtcNow;
        encounter.UpdatedByUserId = updatedByUserId;
    }

    /// <summary>
    /// Maps CoverageDecisionUpdateRequestDto to CoverageDecisionEmbedded entity.
    /// </summary>
    public static CoverageDecisionEmbedded ToEntity(
        this CoverageDecisionUpdateRequestDto dto,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new CoverageDecisionEmbedded
        {
            PrimaryCoverageEnrollmentId = dto.PrimaryCoverageEnrollmentId,
            SecondaryCoverageEnrollmentId = dto.SecondaryCoverageEnrollmentId,
            CobReason = dto.CobReason,
            OverriddenByUser = dto.OverriddenByUser,
            OverrideNote = dto.OverrideNote,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// Maps CoverageLineAddRequestDto to CoverageLineEmbedded entity.
    /// </summary>
    public static CoverageLineEmbedded ToEntity(
        this CoverageLineAddRequestDto dto,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new CoverageLineEmbedded
        {
            ServiceTypeCode = dto.ServiceTypeCode,
            CoverageDescription = dto.CoverageDescription,
            CopayAmount = dto.CopayAmount,
            CoinsurancePercent = dto.CoinsurancePercent,
            DeductibleAmount = dto.DeductibleAmount,
            RemainingDeductible = dto.RemainingDeductible,
            OutOfPocketMax = dto.OutOfPocketMax,
            RemainingOutOfPocket = dto.RemainingOutOfPocket,
            AllowanceAmount = dto.AllowanceAmount,
            NetworkIndicator = dto.NetworkIndicator,
            EffectiveDate = dto.EffectiveDate,
            TerminationDate = dto.TerminationDate,
            AdditionalInfo = dto.AdditionalInfo,
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// Maps EligibilityPayloadAddRequestDto to EligibilityPayloadEmbedded entity.
    /// </summary>
    public static EligibilityPayloadEmbedded ToEntity(
        this EligibilityPayloadAddRequestDto dto,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new EligibilityPayloadEmbedded
        {
            Direction = dto.Direction,
            Format = dto.Format,
            StorageUrl = dto.StorageUrl,
            CreatedByUserId = createdByUserId
        };
    }
}
