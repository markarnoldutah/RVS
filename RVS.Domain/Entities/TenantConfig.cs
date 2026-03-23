using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Tenant-level configuration. Includes access gate settings.
///
/// Cosmos DB partition key: /tenantId
/// </summary>
public class TenantConfig : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "tenantConfig";

    /// <summary>
    /// Tenant access gate — controls whether logins are enabled for this tenant.
    /// </summary>
    [JsonProperty("accessGate")]
    public TenantAccessGateEmbedded AccessGate { get; set; } = new();
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
