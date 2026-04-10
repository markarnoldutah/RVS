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

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// Customer's preferred notification channel: "email" (default) or "sms".
    /// </summary>
    [JsonProperty("notificationPreference")]
    public string NotificationPreference { get; set; } = "email";

    /// <summary>
    /// UTC timestamp when the customer explicitly opted in to SMS notifications.
    /// Null if the customer has never opted in to SMS. Required for TCPA compliance.
    /// </summary>
    [JsonProperty("smsOptInAtUtc")]
    public DateTime? SmsOptInAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the customer opted out of SMS notifications (replied STOP).
    /// Null if the customer has not opted out. When set, no outbound SMS is allowed.
    /// </summary>
    [JsonProperty("smsOptOutAtUtc")]
    public DateTime? SmsOptOutAtUtc { get; set; }

    /// <summary>
    /// FK to the global customer account record.
    /// All profiles for the same email point to the same account.
    /// </summary>
    [JsonProperty("globalCustomerAcctId")]
    public string GlobalCustomerAcctId { get; set; } = string.Empty;

    /// <summary>
    /// Tracks the full lifecycle of each customer ↔ asset relationship.
    /// Handles ownership transfers: when a different customer submits for
    /// an asset, the previous owner's interaction is set to Inactive.
    /// </summary>
    [JsonProperty("assetsOwned")]
    public List<AssetOwnershipEmbedded> AssetsOwned { get; set; } = [];

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
    /// Returns only asset IDs with Active status — used for intake prefill.
    /// </summary>
    [JsonIgnore]
    public List<string> ActiveAssetIds =>
        AssetsOwned
            .Where(a => a.Status == AssetOwnershipStatus.Active)
            .Select(a => a.AssetId)
            .ToList();

    /// <summary>
    /// Returns the active interaction for an asset, or null.
    /// </summary>
    public AssetOwnershipEmbedded? GetActiveInteraction(string assetId) =>
        AssetsOwned.FirstOrDefault(
            a => a.AssetId == assetId && a.Status == AssetOwnershipStatus.Active);

    /// <summary>
    /// Deactivates the active ownership entry for the specified asset.
    /// No-op if the asset is not actively owned by this profile.
    /// </summary>
    /// <param name="assetId">Asset identifier (VIN), e.g. <c>1FTFW1ET5EKE12345</c>.</param>
    public void DeactivateAsset(string assetId)
    {
        var active = GetActiveInteraction(assetId);
        if (active is null) return;

        active.Status = AssetOwnershipStatus.Inactive;
        active.DeactivatedAtUtc = DateTime.UtcNow;
        active.DeactivationReason = "OwnershipTransfer";
    }

    /// <summary>
    /// Activates a new asset ownership or refreshes an existing active entry
    /// by incrementing <see cref="AssetOwnershipEmbedded.RequestCount"/> and updating
    /// <see cref="AssetOwnershipEmbedded.LastSeenAtUtc"/>.
    /// When asset metadata is provided, it is stored on the entry (new or existing).
    /// </summary>
    /// <param name="assetId">Compound asset identifier (e.g. <c>RV:1FTFW1ET5EKE12345</c>).</param>
    /// <param name="manufacturer">Optional manufacturer name.</param>
    /// <param name="model">Optional model name.</param>
    /// <param name="year">Optional model year.</param>
    public void ActivateOrRefreshAsset(string assetId, string? manufacturer = null, string? model = null, int? year = null)
    {
        var existing = GetActiveInteraction(assetId);
        if (existing is not null)
        {
            existing.RequestCount++;
            existing.LastSeenAtUtc = DateTime.UtcNow;
            if (manufacturer is not null) existing.Manufacturer = manufacturer;
            if (model is not null) existing.Model = model;
            if (year is not null) existing.Year = year;
            return;
        }

        var now = DateTime.UtcNow;
        AssetsOwned.Add(new AssetOwnershipEmbedded
        {
            AssetId = assetId,
            Manufacturer = manufacturer,
            Model = model,
            Year = year,
            Status = AssetOwnershipStatus.Active,
            FirstSeenAtUtc = now,
            LastSeenAtUtc = now,
            RequestCount = 1,
        });
    }
}

// ---------------------------------------------------------------------------
// Embedded: AssetOwnershipEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Records a customer's relationship to a specific asset over time.
/// Handles ownership transfers: when a different customer submits for
/// an asset, the previous owner's interaction is set to Inactive.
/// </summary>
public class AssetOwnershipEmbedded
{
    /// <summary>
    /// Asset identifier — the 17-character Vehicle Identification Number (VIN).
    /// </summary>
    [JsonProperty("assetId")]
    public string AssetId { get; set; } = string.Empty;

    [JsonProperty("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Active = customer currently associated with this asset.
    /// Inactive = customer no longer associated (sold, traded, ownership transfer).
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; } = AssetOwnershipStatus.Active;

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
// AssetOwnershipStatus constants
// ---------------------------------------------------------------------------

/// <summary>
/// String constants (not enum) for Cosmos DB serialization simplicity.
/// </summary>
public static class AssetOwnershipStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}
