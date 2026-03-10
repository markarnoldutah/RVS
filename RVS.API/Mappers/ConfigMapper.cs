using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between Config entities and DTOs at the API boundary
/// </summary>
public static class ConfigMapper
{
    public static TenantConfig ToEntity(this TenantConfigCreateRequestDto dto, string tenantId, string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var entity = new TenantConfig
        {
            Id = $"{tenantId}_config",
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
            Practice = new PracticeSettings(),
            Encounters = new EncounterSettings(),
            Eligibility = new EligibilitySettings(),
            Cob = new CobSettings(),
            Ui = new UiSettings()
        };

        // Apply the DTO values to the entity
        ApplyCreateOrUpdateFromDto(entity, dto.Practice, dto.Encounters, dto.Eligibility, dto.Cob, dto.Ui);

        return entity;
    }

    public static TenantConfigResponseDto ToDto(this TenantConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new TenantConfigResponseDto
        {
            Id = config.Id,
            TenantId = config.TenantId,

            Practice = new PracticeSettingsDto
            {
                DefaultPracticeId = config.Practice.DefaultPracticeId,
                Locations = config.Practice.Locations
                    .Select(loc => new PracticeLocationConfigDto
                    {
                        PracticeId = loc.PracticeId,
                        DisplayName = loc.DisplayName,
                        City = loc.City,
                        State = loc.State,
                        PhoneNumber = loc.PhoneNumber,
                        BillingNpi = loc.BillingNpi,
                        TaxId = loc.TaxId
                    }).ToList(),

                Providers = config.Practice.Providers
                    .Select(prov => new ProviderConfigDto
                    {
                        ProviderId = prov.ProviderId,
                        FullName = prov.FullName,
                        Npi = prov.Npi,
                        IsActive = prov.IsActive
                    }).ToList()
            },

            Encounters = new EncounterSettingsDto
            {
                DefaultRoutineEncounterTypeCode = config.Encounters.DefaultRoutineEncounterTypeCode,
                DefaultMedicalEncounterTypeCode = config.Encounters.DefaultMedicalEncounterTypeCode,
                EncounterTypes = config.Encounters.EncounterTypes
                    .Select(et => new EncounterTypeConfigDto
                    {
                        Code = et.Code,
                        DisplayName = et.DisplayName,
                        IsRoutineVision = et.IsRoutineVision,
                        IsMedical = et.IsMedical,
                        AllowedCoverageTypes = et.AllowedCoverageTypes.ToList(),
                        DefaultCoverageType = et.DefaultCoverageType
                    }).ToList()
            },

            Eligibility = new EligibilitySettingsDto
            {
                EnableEligibilityChecks = config.Eligibility.EnableEligibilityChecks,
                EnableVisionPayerChecks = config.Eligibility.EnableVisionPayerChecks,
                EnableMedicalPayerChecks = config.Eligibility.EnableMedicalPayerChecks,
                PrimaryClearinghouseCode = config.Eligibility.PrimaryClearinghouseCode,
                RequestTimeoutSeconds = config.Eligibility.RequestTimeoutSeconds,

                PayerBehaviors = config.Eligibility.PayerBehaviors
                    .Select(pb => new PayerEligibilityBehaviorConfigDto
                    {
                        PayerId = pb.PayerId,
                        SupportsRealTimeEligibility = pb.SupportsRealTimeEligibility,
                        SupportsVisionBenefits = pb.SupportsVisionBenefits,
                        SupportsMedicalBenefits = pb.SupportsMedicalBenefits,
                        RequireSubscriberOnEligibility = pb.RequireSubscriberOnEligibility
                    }).ToList()
            },

            Cob = new CobSettingsDto
            {
                RoutineExamPriority = config.Cob.RoutineExamPriority,
                MedicalVisitPriority = config.Cob.MedicalVisitPriority
            },

            Ui = new UiSettingsDto
            {
                ShowCoverageTab = config.Ui.ShowCoverageTab,
                ShowEncountersTab = config.Ui.ShowEncountersTab,
                ShowEligibilityHistoryTab = config.Ui.ShowEligibilityHistoryTab,
                RequireEligibilityBeforeEncounter = config.Ui.RequireEligibilityBeforeEncounter,
                AllowBypassEligibilityWithWarning = config.Ui.AllowBypassEligibilityWithWarning
            },

            AccessGate = new TenantAccessGateDto
            {
                LoginsEnabled = config.AccessGate?.LoginsEnabled ?? true,
                DisabledReason = config.AccessGate?.DisabledReason,
                DisabledMessage = config.AccessGate?.DisabledMessage,
                SupportContactEmail = config.AccessGate?.SupportContactEmail,
                DisabledAtUtc = config.AccessGate?.DisabledAtUtc
            },

            CreatedAtUtc = config.CreatedAtUtc,
            UpdatedAtUtc = config.UpdatedAtUtc
        };
    }

    public static void ApplyUpdateFromDto(this TenantConfig entity, TenantConfigUpdateRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        // Ensure nested objects exist
        entity.Practice ??= new PracticeSettings();
        entity.Encounters ??= new EncounterSettings();
        entity.Eligibility ??= new EligibilitySettings();
        entity.Cob ??= new CobSettings();
        entity.Ui ??= new UiSettings();

        ApplyCreateOrUpdateFromDto(entity, dto.Practice, dto.Encounters, dto.Eligibility, dto.Cob, dto.Ui);
        
        // Note: UpdatedAtUtc and UpdatedByUserId are set by MarkAsUpdated() in the service layer
    }

    private static void ApplyCreateOrUpdateFromDto(
        TenantConfig entity,
        PracticeSettingsUpdateDto? practice,
        EncounterSettingsUpdateDto? encounters,
        EligibilitySettingsUpdateDto? eligibility,
        CobSettingsUpdateDto? cob,
        UiSettingsUpdateDto? ui)
    {
        // -------------------------
        // Practice
        // -------------------------
        if (practice != null)
        {
            entity.Practice.DefaultPracticeId = practice.DefaultPracticeId;

            entity.Practice.Locations = practice.Locations?
                .Select(l => new PracticeLocationConfig
                {
                    PracticeId = l.PracticeId,
                    DisplayName = l.DisplayName,
                    City = l.City,
                    State = l.State,
                    PhoneNumber = l.PhoneNumber,
                    BillingNpi = l.BillingNpi,
                    TaxId = l.TaxId
                })
                .ToList() ?? [];

            entity.Practice.Providers = practice.Providers?
                .Select(p => new ProviderConfig
                {
                    ProviderId = p.ProviderId,
                    FullName = p.FullName,
                    Npi = p.Npi,
                    IsActive = p.IsActive
                })
                .ToList() ?? [];
        }

        // -------------------------
        // Encounters
        // -------------------------
        if (encounters != null)
        {
            entity.Encounters.DefaultRoutineEncounterTypeCode =
                encounters.DefaultRoutineEncounterTypeCode;

            entity.Encounters.DefaultMedicalEncounterTypeCode =
                encounters.DefaultMedicalEncounterTypeCode;

            entity.Encounters.EncounterTypes = encounters.EncounterTypes?
                .Select(e => new EncounterTypeConfig
                {
                    Code = e.Code,
                    DisplayName = e.DisplayName,
                    IsRoutineVision = e.IsRoutineVision,
                    IsMedical = e.IsMedical,
                    AllowedCoverageTypes = e.AllowedCoverageTypes?.ToList() ?? [],
                    DefaultCoverageType = e.DefaultCoverageType
                })
                .ToList() ?? [];
        }

        // -------------------------
        // Eligibility
        // -------------------------
        if (eligibility != null)
        {
            entity.Eligibility.EnableEligibilityChecks = eligibility.EnableEligibilityChecks;
            entity.Eligibility.EnableVisionPayerChecks = eligibility.EnableVisionPayerChecks;
            entity.Eligibility.EnableMedicalPayerChecks = eligibility.EnableMedicalPayerChecks;

            entity.Eligibility.PrimaryClearinghouseCode = eligibility.PrimaryClearinghouseCode;
            entity.Eligibility.RequestTimeoutSeconds = eligibility.RequestTimeoutSeconds;

            entity.Eligibility.PayerBehaviors = eligibility.PayerBehaviors?
                .Select(p => new PayerEligibilityBehaviorConfig
                {
                    PayerId = p.PayerId,
                    SupportsRealTimeEligibility = p.SupportsRealTimeEligibility,
                    SupportsVisionBenefits = p.SupportsVisionBenefits,
                    SupportsMedicalBenefits = p.SupportsMedicalBenefits,
                    RequireSubscriberOnEligibility = p.RequireSubscriberOnEligibility
                })
                .ToList() ?? [];
        }

        // -------------------------
        // COB
        // -------------------------
        if (cob != null)
        {
            entity.Cob.RoutineExamPriority = cob.RoutineExamPriority;
            entity.Cob.MedicalVisitPriority = cob.MedicalVisitPriority;
        }

        // -------------------------
        // UI / Workflow
        // -------------------------
        if (ui != null)
        {
            entity.Ui.ShowCoverageTab = ui.ShowCoverageTab;
            entity.Ui.ShowEncountersTab = ui.ShowEncountersTab;
            entity.Ui.ShowEligibilityHistoryTab = ui.ShowEligibilityHistoryTab;
            entity.Ui.RequireEligibilityBeforeEncounter = ui.RequireEligibilityBeforeEncounter;
            entity.Ui.AllowBypassEligibilityWithWarning = ui.AllowBypassEligibilityWithWarning;
        }
    }


    public static PayerConfigDto ToDto(this PayerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new PayerConfigDto
        {
            PayerId = config.PayerId,
            DisplayNameOverride = config.DisplayName,
            IsEnabled = config.IsEnabled,
            Notes = null // TODO: Add Notes property to PayerConfig entity if needed
        };
    }

    public static List<PayerConfigDto> ToDto(this List<PayerConfig> configs)
    {
        return configs.Select(ToDto).ToList();
    }
}
