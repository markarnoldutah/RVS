using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Tenant-level configuration. Includes access gate, practice settings,
/// encounter settings, eligibility settings, COB settings, and UI settings.
///
/// Cosmos DB partition key: /tenantId
/// </summary>
public class TenantConfig : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "tenantConfig";

    [JsonProperty("practiceSettings")]
    public PracticeSettings Practice { get; set; } = new();

    [JsonProperty("encounterSettings")]
    public EncounterSettings Encounters { get; set; } = new();

    [JsonProperty("eligibilitySettings")]
    public EligibilitySettings Eligibility { get; set; } = new();

    [JsonProperty("cobSettings")]
    public CobSettings Cob { get; set; } = new();

    [JsonProperty("uiSettings")]
    public UiSettings Ui { get; set; } = new();

    /// <summary>
    /// Tenant access gate — controls whether logins are enabled for this tenant.
    /// </summary>
    [JsonProperty("accessGate")]
    public TenantAccessGateEmbedded AccessGate { get; set; } = new();
}

// ======================
// Practice / Providers
// ======================

public class PracticeSettings
{
    [JsonProperty("defaultPracticeId")]
    public string? DefaultPracticeId { get; set; }

    [JsonProperty("locations")]
    public List<PracticeLocationConfig> Locations { get; set; } = new();

    [JsonProperty("providers")]
    public List<ProviderConfig> Providers { get; set; } = new();
}

public class PracticeLocationConfig
{
    [JsonProperty("practiceLocationConfigId")]
    public string PracticeLocationConfigId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("practiceId")]
    public required string PracticeId { get; init; }

    [JsonProperty("displayName")]
    public required string DisplayName { get; set; }

    [JsonProperty("city")]
    public string? City { get; set; }

    [JsonProperty("state")]
    public string? State { get; set; }

    [JsonProperty("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonProperty("billingNpi")]
    public string? BillingNpi { get; set; }

    [JsonProperty("taxId")]
    public string? TaxId { get; set; }
}

public class ProviderConfig
{
    [JsonProperty("providerConfigId")]
    public string ProviderConfigId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("providerId")]
    public required string ProviderId { get; init; }

    [JsonProperty("fullName")]
    public required string FullName { get; set; }

    [JsonProperty("npi")]
    public string? Npi { get; set; }

    [JsonProperty("isActive")]
    public bool IsActive { get; set; } = true;
}

// ======================
// Encounters
// ======================

public class EncounterSettings
{
    [JsonProperty("encounterTypes")]
    public List<EncounterTypeConfig> EncounterTypes { get; set; } = new();

    [JsonProperty("defaultRoutineEncounterTypeCode")]
    public string? DefaultRoutineEncounterTypeCode { get; set; }

    [JsonProperty("defaultMedicalEncounterTypeCode")]
    public string? DefaultMedicalEncounterTypeCode { get; set; }
}

public class EncounterTypeConfig
{
    [JsonProperty("encounterTypeConfigId")]
    public string EncounterTypeConfigId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("code")]
    public required string Code { get; init; }

    [JsonProperty("displayName")]
    public required string DisplayName { get; set; }

    [JsonProperty("isRoutineVision")]
    public bool IsRoutineVision { get; set; }

    [JsonProperty("isMedical")]
    public bool IsMedical { get; set; }

    [JsonProperty("allowedCoverageTypes")]
    public List<string> AllowedCoverageTypes { get; set; } = new();

    [JsonProperty("defaultCoverageType")]
    public string? DefaultCoverageType { get; set; }
}

// ======================
// Eligibility / Payer Behavior
// ======================

public class EligibilitySettings
{
    [JsonProperty("enableEligibilityChecks")]
    public bool EnableEligibilityChecks { get; set; } = true;

    [JsonProperty("enableVisionPayerChecks")]
    public bool EnableVisionPayerChecks { get; set; } = true;

    [JsonProperty("enableMedicalPayerChecks")]
    public bool EnableMedicalPayerChecks { get; set; } = true;

    [JsonProperty("primaryClearinghouseCode")]
    public string? PrimaryClearinghouseCode { get; set; }

    [JsonProperty("payerBehaviors")]
    public List<PayerEligibilityBehaviorConfig> PayerBehaviors { get; set; } = new();

    [JsonProperty("requestTimeoutSeconds")]
    public int RequestTimeoutSeconds { get; set; } = 30;
}

public class PayerEligibilityBehaviorConfig
{
    [JsonProperty("payerEligibilityBehaviorConfigId")]
    public string PayerEligibilityBehaviorConfigId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("payerId")]
    public required string PayerId { get; init; }

    [JsonProperty("supportsRealTimeEligibility")]
    public bool SupportsRealTimeEligibility { get; set; } = true;

    [JsonProperty("supportsVisionBenefits")]
    public bool SupportsVisionBenefits { get; set; } = true;

    [JsonProperty("supportsMedicalBenefits")]
    public bool SupportsMedicalBenefits { get; set; } = true;

    [JsonProperty("requireSubscriberOnEligibility")]
    public bool RequireSubscriberOnEligibility { get; set; } = false;
}

// ======================
// COB
// ======================

public class CobSettings
{
    [JsonProperty("routineExamPriority")]
    public string RoutineExamPriority { get; set; } = "VisionThenMedical";

    [JsonProperty("medicalVisitPriority")]
    public string MedicalVisitPriority { get; set; } = "MedicalThenVision";
}

// ======================
// UI / Workflow
// ======================

public class UiSettings
{
    [JsonProperty("showCoverageTab")]
    public bool ShowCoverageTab { get; set; } = true;

    [JsonProperty("showEncountersTab")]
    public bool ShowEncountersTab { get; set; } = true;

    [JsonProperty("showEligibilityHistoryTab")]
    public bool ShowEligibilityHistoryTab { get; set; } = true;

    [JsonProperty("requireEligibilityBeforeEncounter")]
    public bool RequireEligibilityBeforeEncounter { get; set; } = true;

    [JsonProperty("allowBypassEligibilityWithWarning")]
    public bool AllowBypassEligibilityWithWarning { get; set; } = true;
}

// ---------------------------------------------------------------------------
// Embedded: TenantAccessGateEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Controls tenant-level login access. When disabled, the UI shows a friendly message.
/// </summary>
public class TenantAccessGateEmbedded
{
    /// <summary>
    /// Hard switch the UI can enforce at startup.
    /// </summary>
    [JsonProperty("loginsEnabled")]
    public bool LoginsEnabled { get; set; } = true;

    /// <summary>
    /// Why disabled (if disabled). Keep as string for flexibility.
    /// Examples: "PastDue", "Suspended", "Canceled", "ManualHold", "SecurityLock"
    /// </summary>
    [JsonProperty("disabledReason")]
    public string? DisabledReason { get; set; }

    /// <summary>
    /// Optional user-friendly but non-sensitive message to show on login/splash screen.
    /// </summary>
    [JsonProperty("disabledMessage")]
    public string? DisabledMessage { get; set; }

    /// <summary>
    /// Optional support contact email for display in the UI (e.g., "Pay invoice", "Contact support").
    /// </summary>
    [JsonProperty("supportContactEmail")]
    [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Invalid email address format")]
    public string? SupportContactEmail { get; set; }

    /// <summary>
    /// Optional timestamp when the tenant was disabled. Useful for audit/debugging.
    /// </summary>
    [JsonProperty("disabledAtUtc")]
    public DateTimeOffset? DisabledAtUtc { get; set; }
}
