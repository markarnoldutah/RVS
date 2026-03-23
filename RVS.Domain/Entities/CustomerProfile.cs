using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Shadow profile — created automatically on first intake submission at a dealership.
/// One per customer per dealership (tenant-scoped).
/// The customer never sees a "Sign Up" screen.
///
/// Cosmos DB partition key: /tenantId
/// Unique key policy: [/tenantId, /email]
/// </summary>
public class CustomerProfile : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "customerProfile";

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("normalizedEmail")]
    public string NormalizedEmail { get; set; } = string.Empty;

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// FK to the global CustomerIdentity record.
    /// All profiles for the same email point to the same identity.
    /// </summary>
    [JsonProperty("customerIdentityId")]
    public string CustomerIdentityId { get; set; } = string.Empty;

    /// <summary>
    /// Tracks the full lifecycle of each customer ↔ VIN relationship.
    /// Replaces a flat KnownVins list to handle ownership transfers.
    /// </summary>
    [JsonProperty("vehicleInteractions")]
    public List<VehicleInteractionEmbedded> VehicleInteractions { get; set; } = [];

    /// <summary>
    /// IDs of all service requests submitted by this customer at this dealership.
    /// </summary>
    [JsonProperty("serviceRequestIds")]
    public List<string> ServiceRequestIds { get; set; } = [];

    /// <summary>
    /// Total count of service requests submitted by this customer at this dealership.
    /// </summary>
    [JsonProperty("totalRequestCount")]
    public int TotalRequestCount { get; set; }

    // ── Convenience helpers (not persisted) ──

    /// <summary>
    /// Returns only VINs with Active status — used for intake prefill.
    /// </summary>
    [JsonIgnore]
    public List<string> ActiveVins =>
        VehicleInteractions
            .Where(v => v.Status == VehicleInteractionStatus.Active)
            .Select(v => v.Vin)
            .ToList();

    /// <summary>
    /// Returns the active interaction for a VIN, or null.
    /// </summary>
    public VehicleInteractionEmbedded? GetActiveInteraction(string vin) =>
        VehicleInteractions.FirstOrDefault(
            v => v.Vin == vin && v.Status == VehicleInteractionStatus.Active);
}

// ---------------------------------------------------------------------------
// Embedded: VehicleInteractionEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Records a customer's relationship to a specific VIN over time.
/// Handles ownership transfers: when a different customer submits for
/// a VIN, the previous owner's interaction is set to Inactive.
/// </summary>
public class VehicleInteractionEmbedded
{
    [JsonProperty("vin")]
    public string Vin { get; set; } = string.Empty;

    [JsonProperty("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Active = customer currently associated with this VIN.
    /// Inactive = customer no longer associated (sold, traded, ownership transfer).
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; } = VehicleInteractionStatus.Active;

    [JsonProperty("firstSeenAtUtc")]
    public DateTime FirstSeenAtUtc { get; set; }

    [JsonProperty("lastSeenAtUtc")]
    public DateTime LastSeenAtUtc { get; set; }

    [JsonProperty("requestCount")]
    public int RequestCount { get; set; }

    [JsonProperty("deactivatedAtUtc")]
    public DateTime? DeactivatedAtUtc { get; set; }

    [JsonProperty("deactivationReason")]
    public string? DeactivationReason { get; set; }
}

// ---------------------------------------------------------------------------
// VehicleInteractionStatus constants
// ---------------------------------------------------------------------------

/// <summary>
/// String constants (not enum) for Cosmos DB serialization simplicity.
/// </summary>
public static class VehicleInteractionStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}
