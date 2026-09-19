using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// An advisor-initiated intake invite (<c>Spec A-14</c>, issue #663): the single-use, prefilled
/// intake link an advisor texts to a caller who agreed to receive it, or opens for themselves
/// with <i>Fill it in myself</i>.
///
/// <see cref="EntityBase.Id"/> is <c>InviteToken.Hash(token)</c>. The raw token is never stored,
/// and making its hash the id is what turns redemption into a single-partition point read.
///
/// The document has no TTL and is never deleted. Its consent fields are the evidence of opt-in
/// for toll-free verification and for any complaint, so the record outlives the invite itself;
/// <see cref="ExpiresAtUtc"/> retires the token, not the document.
///
/// Cosmos DB container: <c>intake-invites</c>. Partition key: <c>/tenantId</c>.
/// </summary>
public class IntakeInvite : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "intakeInvite";

    /// <summary>The location whose intake form the invite opens.</summary>
    [JsonProperty("locationId")]
    public string LocationId { get; init; } = string.Empty;

    /// <summary>
    /// The advisor who created the invite and, for a texted invite, captured the caller's consent.
    /// The resulting service request is attributed to this user.
    /// </summary>
    [JsonProperty("advisorUserId")]
    public string AdvisorUserId { get; init; } = string.Empty;

    /// <summary>The caller's first name, as the advisor typed it. Prefills the intake form.</summary>
    [JsonProperty("firstName")]
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// The caller's phone number in E.164. Always present for a texted invite; optional for a
    /// self-entry invite. Prefills the intake form.
    /// </summary>
    [JsonProperty("phone")]
    public string? Phone { get; init; }

    /// <summary>
    /// <c>true</c> for a <i>Fill it in myself</i> invite: minted for the advisor to open, never texted.
    /// </summary>
    [JsonProperty("isSelfEntry")]
    public bool IsSelfEntry { get; init; }

    /// <summary>
    /// When the advisor confirmed the caller's verbal consent to receive the text. Recorded
    /// separately from <see cref="SentAtUtc"/> and from delivery, and never cleared. <c>null</c>
    /// only for a self-entry invite, which texts nobody.
    /// </summary>
    [JsonProperty("consentCapturedAtUtc")]
    public DateTime? ConsentCapturedAtUtc { get; init; }

    /// <summary>
    /// When the invite was handed to ACS. <c>null</c> for a self-entry invite, and for a texted
    /// invite whose send never reached ACS (see <see cref="DeliveryStatus"/>).
    /// </summary>
    [JsonProperty("sentAtUtc")]
    public DateTime? SentAtUtc { get; set; }

    /// <summary>When the token stops working: about 72 hours after the invite is created.</summary>
    [JsonProperty("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; init; }

    /// <summary>When the resulting intake was submitted. Single use: once set, the token is spent.</summary>
    [JsonProperty("redeemedAtUtc")]
    public DateTime? RedeemedAtUtc { get; set; }

    /// <summary>The service request the invite produced, set on redemption.</summary>
    [JsonProperty("serviceRequestId")]
    public string? ServiceRequestId { get; set; }

    /// <summary>
    /// The ACS message id of the texted link. ACS delivery reports are matched back to the invite by it.
    /// </summary>
    [JsonProperty("acsMessageId")]
    public string? AcsMessageId { get; set; }

    /// <summary>One of the <see cref="IntakeInviteDeliveryStatus"/> values.</summary>
    [JsonProperty("deliveryStatus")]
    public string DeliveryStatus { get; set; } = IntakeInviteDeliveryStatus.Pending;
}

/// <summary>
/// Delivery states of an <see cref="IntakeInvite"/> (<c>Spec A-14</c>). ACS delivery reports
/// move a texted invite from <see cref="Queued"/> to <see cref="Delivered"/> or <see cref="Failed"/>.
/// </summary>
public static class IntakeInviteDeliveryStatus
{
    /// <summary>Persisted, not yet handed to ACS.</summary>
    public const string Pending = "pending";

    /// <summary>Accepted by ACS; no delivery report yet.</summary>
    public const string Queued = "queued";

    /// <summary>The carrier reported delivery.</summary>
    public const string Delivered = "delivered";

    /// <summary>ACS rejected the send, or the carrier reported a failure.</summary>
    public const string Failed = "failed";

    /// <summary>A self-entry invite: nothing is ever sent.</summary>
    public const string NotSent = "notSent";
}
