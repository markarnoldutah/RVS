using Newtonsoft.Json;

using RVS.Domain.Validation;

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
    /// <see cref="Phone"/> in E.164 (<c>+18015551234</c>), or <c>null</c> when it does not
    /// normalise. <see cref="Phone"/> keeps what the customer typed; this is the form a lookup
    /// can match on, which is what lets an inbound STOP find every dealer's record of a number
    /// (issue #665). Set by <c>PhoneNumberNormalizer</c> wherever the phone is written.
    /// </summary>
    [JsonProperty("phoneE164")]
    public string? PhoneE164 { get; set; }

    /// <summary>
    /// When <c>true</c>, the customer has opted out of SMS notifications.
    /// Default is <c>false</c> (both email and SMS are sent).
    /// </summary>
    [JsonProperty("smsOptOut")]
    public bool SmsOptOut { get; set; }

    /// <summary>
    /// When <c>true</c>, the customer has opted out of email notifications.
    /// Default is <c>false</c> (both email and SMS are sent).
    /// </summary>
    [JsonProperty("emailOptOut")]
    public bool EmailOptOut { get; set; }

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
    /// When the last inbound keyword (<c>STOP</c> / <c>START</c> / <c>UNSTOP</c>) that RVS acted
    /// on was sent. Event Grid delivers at least once and in no fixed order, so an event older
    /// than this is ignored rather than allowed to undo a later one (issue #665).
    /// Null when no keyword has ever arrived for this number.
    /// </summary>
    [JsonProperty("smsKeywordAtUtc")]
    public DateTime? SmsKeywordAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the customer opted out of email notifications.
    /// Null if the customer has not opted out. When set, no outbound email is sent.
    /// </summary>
    [JsonProperty("emailOptOutAtUtc")]
    public DateTime? EmailOptOutAtUtc { get; set; }

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

    /// <summary>
    /// Applies an inbound carrier keyword (<c>Spec A-2</c>'s out-of-scope note, issue #665).
    /// <see cref="SmsKeyword.OptOut"/> sets <see cref="SmsOptOut"/>, <see cref="SmsKeyword.OptIn"/>
    /// clears it — and a keyword is the only thing that clears it, because intake can set an
    /// opt-out but never clears one (issue #673). <see cref="SmsKeyword.Help"/> changes nothing:
    /// it is answered with a fixed reply, and is neither consent nor a revocation.
    ///
    /// An event at or before <see cref="SmsKeywordAtUtc"/> is ignored, which covers both the
    /// duplicate deliveries and the out-of-order pairs Event Grid is allowed to produce.
    /// <see cref="SmsOptOutAtUtc"/> keeps the *first* opt-out's time, since that is the evidence
    /// of when the customer asked; a repeat only advances <see cref="SmsKeywordAtUtc"/>.
    /// </summary>
    /// <param name="keyword">What the inbound text meant.</param>
    /// <param name="eventAtUtc">When the customer sent it, per the ACS event.</param>
    /// <returns><c>true</c> when the record changed and needs persisting.</returns>
    public bool ApplySmsKeyword(SmsKeyword keyword, DateTime eventAtUtc)
    {
        // HELP is answered, not recorded: it is neither consent nor a revocation.
        if (keyword is SmsKeyword.None or SmsKeyword.Help)
        {
            return false;
        }

        if (SmsKeywordAtUtc is { } last && eventAtUtc <= last)
        {
            return false;
        }

        SmsKeywordAtUtc = eventAtUtc;

        if (keyword == SmsKeyword.OptOut)
        {
            SmsOptOutAtUtc ??= eventAtUtc;
            SmsOptOut = true;
            return true;
        }

        SmsOptOut = false;
        SmsOptOutAtUtc = null;
        SmsOptInAtUtc = eventAtUtc;
        return true;
    }

    /// <summary>
    /// Applies the opt-out boxes from an intake submission (Spec A-2, issue #673). A ticked box
    /// sets the opt-out and stamps its time if unset; an unticked box changes nothing. The form
    /// never shows a stored opt-out — A-7 prefill is deferred — so an unticked box is not a
    /// choice to opt back in. Only <see cref="ApplySmsKeyword"/> clears <see cref="SmsOptOut"/>;
    /// nothing clears <see cref="EmailOptOut"/> yet.
    /// </summary>
    /// <param name="smsOptOut">The submission's SMS opt-out box.</param>
    /// <param name="emailOptOut">The submission's email opt-out box.</param>
    /// <param name="atUtc">When the submission was received.</param>
    public void ApplyIntakeOptOuts(bool smsOptOut, bool emailOptOut, DateTime atUtc)
    {
        if (smsOptOut)
        {
            SmsOptOut = true;
            SmsOptOutAtUtc ??= atUtc;
        }

        if (emailOptOut)
        {
            EmailOptOut = true;
            EmailOptOutAtUtc ??= atUtc;
        }
    }

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
    /// <param name="assetId">Asset identifier (e.g. <c>1FTFW1ET5EKE12345</c>).</param>
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
