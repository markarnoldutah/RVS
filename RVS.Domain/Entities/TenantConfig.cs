using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Tenant-level configuration. Includes access gate settings and the master list of
/// service capabilities available across all locations.
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

    /// <summary>
    /// Master list of service capabilities offered by this tenant.
    /// Individual locations choose which of these they support via
    /// <see cref="Location.EnabledCapabilities"/>.
    /// </summary>
    [JsonProperty("availableCapabilities")]
    public List<TenantCapabilityEmbedded> AvailableCapabilities { get; set; } = [];
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

// ---------------------------------------------------------------------------
// Embedded: TenantCapabilityEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// A service capability available at the tenant level (e.g., "Diesel Engine Service").
/// Locations opt in to specific capabilities from this master list.
/// </summary>
public class TenantCapabilityEmbedded
{
    /// <summary>
    /// URL-safe unique code (e.g., "diesel-service").
    /// Used as the reference key in <see cref="Location.EnabledCapabilities"/>.
    /// </summary>
    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name (e.g., "Diesel Engine Service").
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description shown to staff when selecting capabilities for a location.
    /// </summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Controls display order in the UI.
    /// </summary>
    [JsonProperty("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    /// When false the capability is soft-deleted and hidden from location selection.
    /// </summary>
    [JsonProperty("isActive")]
    public bool IsActive { get; set; } = true;
}
