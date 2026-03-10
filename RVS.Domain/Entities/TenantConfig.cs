using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RVS.Domain.Entities
{
    public class TenantConfig : EntityBase
    {
        public override string Type { get; init; } = "tenantConfig";

        // New MVP config blocks
        [JsonProperty("practiceSettings")]
        public PracticeSettings Practice { get; set; } = new();
        public EncounterSettings Encounters { get; set; } = new();
        public EligibilitySettings Eligibility { get; set; } = new();
        public CobSettings Cob { get; set; } = new();
        public UiSettings Ui { get; set; } = new();

        public TenantAccessGate AccessGate { get; set; } = new();
    }

    // ======================
    // Practice / Providers
    // ======================

    public class PracticeSettings
    {
        // Reference to a Practice entity Id in the practices container
        public string? DefaultPracticeId { get; set; }

        // Left in for future use – not required in the current seed
        public List<PracticeLocationConfig> Locations { get; set; } = new();
        public List<ProviderConfig> Providers { get; set; } = new();
    }

    public class PracticeLocationConfig
    {
        [JsonProperty("practiceLocationConfigId")]
        public string PracticeLocationConfigId { get; init; } = Guid.NewGuid().ToString();

        public required string PracticeId { get; init; }
        public required string DisplayName { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PhoneNumber { get; set; }

        public string? BillingNpi { get; set; }
        public string? TaxId { get; set; }
    }

    public class ProviderConfig
    {
        [JsonProperty("providerConfigId")]
        public string ProviderConfigId { get; init; } = Guid.NewGuid().ToString();

        public required string ProviderId { get; init; }
        public required string FullName { get; set; }
        public string? Npi { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ======================
    // Encounters
    // ======================

    public class EncounterSettings
    {
        public List<EncounterTypeConfig> EncounterTypes { get; set; } = new();

        // Codes that match EncounterTypeConfig.Code
        public string? DefaultRoutineEncounterTypeCode { get; set; } // "ROUTINE_EXAM"
        public string? DefaultMedicalEncounterTypeCode { get; set; } // "MEDICAL_EYE"
    }

    public class EncounterTypeConfig
    {
        [JsonProperty("encounterTypeConfigId")]
        public string EncounterTypeConfigId { get; init; } = Guid.NewGuid().ToString();

        public required string Code { get; init; }  // e.g. "ROUTINE_EXAM"
        public required string DisplayName { get; set; }
        public bool IsRoutineVision { get; set; }
        public bool IsMedical { get; set; }

        // Coverage types the front desk can choose for this encounter
        public List<string> AllowedCoverageTypes { get; set; } = new(); // ["Vision","Medical"]
        public string? DefaultCoverageType { get; set; }                // "Vision" or "Medical"
    }

    // ======================
    // Eligibility / Payer Behavior
    // ======================

    public class EligibilitySettings
    {
        public bool EnableEligibilityChecks { get; set; } = true;
        public bool EnableVisionPayerChecks { get; set; } = true;
        public bool EnableMedicalPayerChecks { get; set; } = true;

        // e.g. "AVAILITY"
        public string? PrimaryClearinghouseCode { get; set; }

        public List<PayerEligibilityBehaviorConfig> PayerBehaviors { get; set; } = new();

        public int RequestTimeoutSeconds { get; set; } = 30;
    }

    public class PayerEligibilityBehaviorConfig
    {
        [JsonProperty("payerEligibilityBehaviorConfigId")]
        public string PayerEligibilityBehaviorConfigId { get; init; } = Guid.NewGuid().ToString();

        public required string PayerId { get; init; }

        public bool SupportsRealTimeEligibility { get; set; } = true;
        public bool SupportsVisionBenefits { get; set; } = true;
        public bool SupportsMedicalBenefits { get; set; } = true;

        public bool RequireSubscriberOnEligibility { get; set; } = false;
    }

    // ======================
    // COB
    // ======================

    public class CobSettings
    {
        // e.g. "VisionThenMedical", "MedicalThenVision", "MedicalOnly"
        public string RoutineExamPriority { get; set; } = "VisionThenMedical";
        public string MedicalVisitPriority { get; set; } = "MedicalThenVision";
    }

    // ======================
    // UI / Workflow
    // ======================

    public class UiSettings
    {
        public bool ShowCoverageTab { get; set; } = true;
        public bool ShowEncountersTab { get; set; } = true;
        public bool ShowEligibilityHistoryTab { get; set; } = true;

        public bool RequireEligibilityBeforeEncounter { get; set; } = true;
        public bool AllowBypassEligibilityWithWarning { get; set; } = true;
    }

    public class TenantAccessGate
    {
        // Hard switch the UI can enforce at startup
        public bool LoginsEnabled { get; set; } = true;

        // Why disabled (if disabled). Keep as string for flexibility.
        // Examples: "PastDue", "Suspended", "Canceled", "ManualHold", "SecurityLock"
        public string? DisabledReason { get; set; }

        // Optional: user-friendly but non-sensitive message to show on login / splash screen
        public string? DisabledMessage { get; set; }

        // Optional: for UI display (“Pay invoice”, “Contact support”)
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Invalid email address format")]
        public string? SupportContactEmail { get; set; }

        // Optional: useful for audit / debugging
        public DateTimeOffset? DisabledAtUtc { get; set; }
    }

}
