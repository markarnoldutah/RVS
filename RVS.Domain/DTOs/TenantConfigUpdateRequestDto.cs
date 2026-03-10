using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record TenantConfigUpdateRequestDto
    {
        [Required(ErrorMessage = "Practice settings are required.")]
        public PracticeSettingsUpdateDto Practice { get; init; } = new();

        [Required(ErrorMessage = "Encounter settings are required.")]
        public EncounterSettingsUpdateDto Encounters { get; init; } = new();

        [Required(ErrorMessage = "Eligibility settings are required.")]
        public EligibilitySettingsUpdateDto Eligibility { get; init; } = new();

        [Required(ErrorMessage = "COB settings are required.")]
        public CobSettingsUpdateDto Cob { get; init; } = new();

        [Required(ErrorMessage = "UI settings are required.")]
        public UiSettingsUpdateDto Ui { get; init; } = new();
    }

    // -------------------------
    // Nested update DTOs
    // -------------------------

    public sealed record PracticeSettingsUpdateDto
    {
        [StringLength(100, MinimumLength = 1, ErrorMessage = "DefaultPracticeId must be between 1 and 100 characters.")]
        public string? DefaultPracticeId { get; init; }

        public List<PracticeLocationConfigUpdateDto> Locations { get; init; } = new();
        public List<ProviderConfigUpdateDto> Providers { get; init; } = new();
    }

    public sealed record PracticeLocationConfigUpdateDto
    {
        [Required(ErrorMessage = "PracticeId is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "PracticeId must be between 1 and 100 characters.")]
        public string PracticeId { get; init; } = default!;

        [Required(ErrorMessage = "DisplayName is required.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "DisplayName must be between 1 and 200 characters.")]
        public string DisplayName { get; init; } = default!;

        [StringLength(100, MinimumLength = 1, ErrorMessage = "City must be between 1 and 100 characters.")]
        public string? City { get; init; }

        [StringLength(2, MinimumLength = 2, ErrorMessage = "State must be exactly 2 characters.")]
        public string? State { get; init; }

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(20, ErrorMessage = "PhoneNumber must not exceed 20 characters.")]
        public string? PhoneNumber { get; init; }

        [StringLength(10, MinimumLength = 10, ErrorMessage = "BillingNpi must be exactly 10 characters.")]
        public string? BillingNpi { get; init; }

        [StringLength(20, MinimumLength = 1, ErrorMessage = "TaxId must be between 1 and 20 characters.")]
        public string? TaxId { get; init; }
    }

    public sealed record ProviderConfigUpdateDto
    {
        [Required(ErrorMessage = "ProviderId is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "ProviderId must be between 1 and 100 characters.")]
        public string ProviderId { get; init; } = default!;

        [Required(ErrorMessage = "FullName is required.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "FullName must be between 1 and 200 characters.")]
        public string FullName { get; init; } = default!;

        [StringLength(10, MinimumLength = 10, ErrorMessage = "Npi must be exactly 10 characters.")]
        public string? Npi { get; init; }

        public bool IsActive { get; init; }
    }

    public sealed record EncounterSettingsUpdateDto
    {
        public List<EncounterTypeConfigUpdateDto> EncounterTypes { get; init; } = new();

        [StringLength(50, MinimumLength = 1, ErrorMessage = "DefaultRoutineEncounterTypeCode must be between 1 and 50 characters.")]
        public string? DefaultRoutineEncounterTypeCode { get; init; }

        [StringLength(50, MinimumLength = 1, ErrorMessage = "DefaultMedicalEncounterTypeCode must be between 1 and 50 characters.")]
        public string? DefaultMedicalEncounterTypeCode { get; init; }
    }

    public sealed record EncounterTypeConfigUpdateDto
    {
        [Required(ErrorMessage = "Code is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Code must be between 1 and 50 characters.")]
        public string Code { get; init; } = default!;

        [Required(ErrorMessage = "DisplayName is required.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "DisplayName must be between 1 and 200 characters.")]
        public string DisplayName { get; init; } = default!;

        public bool IsRoutineVision { get; init; }
        public bool IsMedical { get; init; }

        public List<string> AllowedCoverageTypes { get; init; } = new();

        [StringLength(50, MinimumLength = 1, ErrorMessage = "DefaultCoverageType must be between 1 and 50 characters.")]
        public string? DefaultCoverageType { get; init; }
    }

    public sealed record EligibilitySettingsUpdateDto
    {
        public bool EnableEligibilityChecks { get; init; }
        public bool EnableVisionPayerChecks { get; init; }
        public bool EnableMedicalPayerChecks { get; init; }

        [StringLength(50, MinimumLength = 1, ErrorMessage = "PrimaryClearinghouseCode must be between 1 and 50 characters.")]
        public string? PrimaryClearinghouseCode { get; init; }

        [Range(1, 300, ErrorMessage = "RequestTimeoutSeconds must be between 1 and 300.")]
        public int RequestTimeoutSeconds { get; init; }

        public List<PayerEligibilityBehaviorConfigUpdateDto> PayerBehaviors { get; init; } = new();
    }

    public sealed record PayerEligibilityBehaviorConfigUpdateDto
    {
        [Required(ErrorMessage = "PayerId is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "PayerId must be between 1 and 100 characters.")]
        public string PayerId { get; init; } = default!;

        public bool SupportsRealTimeEligibility { get; init; }
        public bool SupportsVisionBenefits { get; init; }
        public bool SupportsMedicalBenefits { get; init; }

        public bool RequireSubscriberOnEligibility { get; init; }
    }

    public sealed record CobSettingsUpdateDto
    {
        [Required(ErrorMessage = "RoutineExamPriority is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "RoutineExamPriority must be between 1 and 50 characters.")]
        public string RoutineExamPriority { get; init; } = default!;

        [Required(ErrorMessage = "MedicalVisitPriority is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "MedicalVisitPriority must be between 1 and 50 characters.")]
        public string MedicalVisitPriority { get; init; } = default!;
    }

    public sealed record UiSettingsUpdateDto
    {
        public bool ShowCoverageTab { get; init; }
        public bool ShowEncountersTab { get; init; }
        public bool ShowEligibilityHistoryTab { get; init; }

        public bool RequireEligibilityBeforeEncounter { get; init; }
        public bool AllowBypassEligibilityWithWarning { get; init; }
    }
}

