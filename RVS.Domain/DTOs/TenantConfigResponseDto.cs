using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record TenantConfigResponseDto
    {
        public string Id { get; init; } = default!;
        public string TenantId { get; init; } = default!;

        public PracticeSettingsDto Practice { get; init; } = new();
        public EncounterSettingsDto Encounters { get; init; } = new();
        public EligibilitySettingsDto Eligibility { get; init; } = new();
        public CobSettingsDto Cob { get; init; } = new();
        public UiSettingsDto Ui { get; init; } = new();

        public DateTime CreatedAtUtc { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }

        public TenantAccessGateDto AccessGate { get; init; } = new();
    }

    public sealed record TenantAccessGateDto
    {
        public bool LoginsEnabled { get; init; } = true;
        public string? DisabledReason { get; init; }
        public string? DisabledMessage { get; init; }
        
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        public string? SupportContactEmail { get; init; }
        
        public DateTimeOffset? DisabledAtUtc { get; init; }
    }


    // -------------------------
    // Nested DTOs
    // -------------------------

    public sealed record PracticeSettingsDto
    {
        public string? DefaultPracticeId { get; init; }

        public List<PracticeLocationConfigDto> Locations { get; init; } = new();
        public List<ProviderConfigDto> Providers { get; init; } = new();
    }

    public sealed record PracticeLocationConfigDto
    {
        public string PracticeId { get; init; } = default!;
        public string DisplayName { get; init; } = default!;
        public string? City { get; init; }
        public string? State { get; init; }
        public string? PhoneNumber { get; init; }

        public string? BillingNpi { get; init; }
        public string? TaxId { get; init; }
    }

    public sealed record ProviderConfigDto
    {
        public string ProviderId { get; init; } = default!;
        public string FullName { get; init; } = default!;
        public string? Npi { get; init; }
        public bool IsActive { get; init; } = true;
    }

    public sealed record EncounterSettingsDto
    {
        public List<EncounterTypeConfigDto> EncounterTypes { get; init; } = new();

        public string? DefaultRoutineEncounterTypeCode { get; init; }
        public string? DefaultMedicalEncounterTypeCode { get; init; }
    }

    public sealed record EncounterTypeConfigDto
    {
        public string Code { get; init; } = default!;
        public string DisplayName { get; init; } = default!;
        public bool IsRoutineVision { get; init; }
        public bool IsMedical { get; init; }

        public List<string> AllowedCoverageTypes { get; init; } = new();
        public string? DefaultCoverageType { get; init; }
    }

    public sealed record EligibilitySettingsDto
    {
        public bool EnableEligibilityChecks { get; init; }
        public bool EnableVisionPayerChecks { get; init; }
        public bool EnableMedicalPayerChecks { get; init; }

        public string? PrimaryClearinghouseCode { get; init; }

        public int RequestTimeoutSeconds { get; init; }

        public List<PayerEligibilityBehaviorConfigDto> PayerBehaviors { get; init; } = new();
    }

    public sealed record PayerEligibilityBehaviorConfigDto
    {
        public string PayerId { get; init; } = default!;

        public bool SupportsRealTimeEligibility { get; init; }
        public bool SupportsVisionBenefits { get; init; }
        public bool SupportsMedicalBenefits { get; init; }

        public bool RequireSubscriberOnEligibility { get; init; }
    }

    public sealed record CobSettingsDto
    {
        public string RoutineExamPriority { get; init; } = default!;
        public string MedicalVisitPriority { get; init; } = default!;
    }

    public sealed record UiSettingsDto
    {
        public bool ShowCoverageTab { get; init; }
        public bool ShowEncountersTab { get; init; }
        public bool ShowEligibilityHistoryTab { get; init; }

        public bool RequireEligibilityBeforeEncounter { get; init; }
        public bool AllowBypassEligibilityWithWarning { get; init; }
    }
}
