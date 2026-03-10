using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between Patient entities and DTOs at the API boundary
/// Patient aggregate now includes embedded encounters.
/// </summary>
public static class PatientMapper
{
    // =====================================================
    // Entity ? DTO (Response) Mappings
    // =====================================================

    public static PatientDetailResponseDto ToDetailDto(this Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        return new PatientDetailResponseDto
        {
            PatientId = patient.Id,
            TenantId = patient.TenantId,
            PracticeId = patient.PracticeId,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Email = patient.Email,
            Phone = patient.Phone,
            CoverageEnrollments = patient.CoverageEnrollments?
                .Select(ToDto)
                .ToList() ?? [] ,
            // Recent encounters - show last 5 sorted by visit date descending
            RecentEncounters = patient.Encounters?
                .OrderByDescending(e => e.VisitDate)
                .Take(5)
                .Select(e => e.ToSummaryDto(patient.Id, patient.PracticeId))
                .ToList() ?? [],
            // Audit properties
            CreatedAtUtc = patient.CreatedAtUtc,
            UpdatedAtUtc = patient.UpdatedAtUtc,
            CreatedByUserId = patient.CreatedByUserId,
            UpdatedByUserId = patient.UpdatedByUserId
        };
    }

    public static PatientSearchResultResponseDto ToSearchResultDto(this Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        return new PatientSearchResultResponseDto
        {
            PatientId = patient.Id,
            PracticeId = patient.PracticeId,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            PrimaryMemberId = patient.CoverageEnrollments?
                .Where(c => c.IsEnabled && c.CobPriorityHint == 1)
                .Select(c => c.MemberId)
                .FirstOrDefault(),
            PrimaryPayerName = null // TODO: This needs to be derived from primary coverage enrollment and payer lookup
        };
    }

    public static CoverageEnrollmentResponseDto ToDto(this CoverageEnrollmentEmbedded coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        return new CoverageEnrollmentResponseDto
        {
            CoverageEnrollmentId = coverage.CoverageEnrollmentId,
            PayerId = coverage.PayerId,
            PlanType = coverage.PlanType,
            MemberId = coverage.MemberId,
            GroupNumber = coverage.GroupNumber,
            RelationshipToSubscriber = coverage.RelationshipToSubscriber,
            SubscriberFirstName = coverage.SubscriberFirstName,
            SubscriberLastName = coverage.SubscriberLastName,
            SubscriberDob = coverage.SubscriberDob,
            IsEmployerPlan = coverage.IsEmployerPlan,
            EffectiveDate = coverage.EffectiveDate,
            TerminationDate = coverage.TerminationDate,
            IsEnabled = coverage.IsEnabled,
            CobPriorityHint = coverage.CobPriorityHint,
            IsCobLocked = coverage.IsCobLocked,
            CobNotes = coverage.CobNotes,
            // Audit properties
            CreatedAtUtc = coverage.CreatedAtUtc,
            UpdatedAtUtc = coverage.UpdatedAtUtc,
            CreatedByUserId = coverage.CreatedByUserId,
            UpdatedByUserId = coverage.UpdatedByUserId
        };
    }

    public static PagedResult<PatientSearchResultResponseDto> ToSearchResultDto(this PagedResult<Patient> pagedPatients)
    {
        return new PagedResult<PatientSearchResultResponseDto>
        {
            Items = pagedPatients.Items.Select(ToSearchResultDto).ToList(),
            Page = pagedPatients.Page,
            PageSize = pagedPatients.PageSize,
            TotalCount = pagedPatients.TotalCount
        };
    }

    // =====================================================
    // DTO ? Entity (Request) Mappings
    // =====================================================

    /// <summary>
    /// Maps PatientCreateRequestDto to Patient entity.
    /// </summary>
    public static Patient ToEntity(
        this PatientCreateRequestDto dto,
        string tenantId,
        string practiceId,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);

        return new Patient
        {
            TenantId = tenantId,
            PracticeId = practiceId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            DateOfBirth = dto.DateOfBirth,
            Email = dto.Email,
            Phone = dto.Phone,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// Applies updates from PatientUpdateRequestDto to existing Patient entity.
    /// </summary>
    public static void ApplyUpdate(
        this Patient patient,
        PatientUpdateRequestDto dto,
        string? updatedByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.FirstName is not null) patient.FirstName = dto.FirstName.Trim();
        if (dto.LastName is not null) patient.LastName = dto.LastName.Trim();
        if (dto.DateOfBirth.HasValue) patient.DateOfBirth = dto.DateOfBirth;
        if (dto.Email is not null) patient.Email = dto.Email;
        if (dto.Phone is not null) patient.Phone = dto.Phone;

        patient.MarkAsUpdated(updatedByUserId);
    }

    /// <summary>
    /// Maps CoverageEnrollmentCreateRequestDto to CoverageEnrollmentEmbedded entity.
    /// </summary>
    public static CoverageEnrollmentEmbedded ToEntity(
        this CoverageEnrollmentCreateRequestDto dto,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new CoverageEnrollmentEmbedded
        {
            PayerId = dto.PayerId,
            MemberId = dto.MemberId,
            GroupNumber = dto.GroupNumber,
            RelationshipToSubscriber = dto.RelationshipToSubscriber,
            SubscriberFirstName = dto.SubscriberFirstName,
            SubscriberLastName = dto.SubscriberLastName,
            SubscriberDob = dto.SubscriberDob,
            EffectiveDate = dto.EffectiveDate,
            TerminationDate = dto.TerminationDate,
            PlanType = dto.PlanType,
            CobPriorityHint = dto.CobPriorityHint,
            CobNotes = dto.CobNotes,
            IsEmployerPlan = dto.IsEmployerPlan,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// Applies updates from CoverageEnrollmentUpdateRequestDto to existing CoverageEnrollmentEmbedded entity.
    /// Used within repository update action delegate.
    /// </summary>
    public static void ApplyUpdate(
        this CoverageEnrollmentEmbedded coverage,
        CoverageEnrollmentUpdateRequestDto dto,
        string? updatedByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.PayerId is not null) coverage.PayerId = dto.PayerId;
        if (dto.MemberId is not null) coverage.MemberId = dto.MemberId;
        if (dto.GroupNumber is not null) coverage.GroupNumber = dto.GroupNumber;
        if (dto.RelationshipToSubscriber is not null) coverage.RelationshipToSubscriber = dto.RelationshipToSubscriber;
        if (dto.SubscriberFirstName is not null) coverage.SubscriberFirstName = dto.SubscriberFirstName;
        if (dto.SubscriberLastName is not null) coverage.SubscriberLastName = dto.SubscriberLastName;
        if (dto.SubscriberDob.HasValue) coverage.SubscriberDob = dto.SubscriberDob;
        if (dto.EffectiveDate.HasValue) coverage.EffectiveDate = dto.EffectiveDate;
        if (dto.TerminationDate.HasValue) coverage.TerminationDate = dto.TerminationDate;
        if (dto.PlanType is not null) coverage.PlanType = dto.PlanType;
        if (dto.CobPriorityHint.HasValue) coverage.CobPriorityHint = dto.CobPriorityHint;
        if (dto.CobNotes is not null) coverage.CobNotes = dto.CobNotes;
        coverage.IsEmployerPlan = dto.IsEmployerPlan;

        // Set audit properties
        coverage.UpdatedAtUtc = DateTime.UtcNow;
        coverage.UpdatedByUserId = updatedByUserId;
    }
}
