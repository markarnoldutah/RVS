using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// An advisor-initiated intake invite (<c>Spec A-14</c>, issue #663): the single-use, prefilled
/// intake link an advisor texts or emails (issue #693) to a caller who agreed to receive it, or
/// opens for themselves with <i>Fill it in myself</i>.
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
    /// The caller's email address, trimmed and lower-cased like <c>CustomerProfile.Email</c>.
    /// Always present for an emailed invite; optional otherwise. Prefills the intake form.
    /// </summary>
    [JsonProperty("email")]
    public string? Email { get; init; }

    /// <summary>
    /// How the link went to the caller: one of the <see cref="IntakeInviteChannel"/> values
    /// (issue #693). Invites written before email existed have no such field and read as
    /// <see cref="IntakeInviteChannel.Sms"/>, which is what they were. A self-entry invite is
    /// never sent, so its channel means nothing.
    /// </summary>
    [JsonProperty("channel")]
    public string Channel { get; init; } = IntakeInviteChannel.Sms;

    /// <summary>
    /// <c>true</c> for a <i>Fill it in myself</i> invite: minted for the advisor to open, never sent.
    /// </summary>
    [JsonProperty("isSelfEntry")]
    public bool IsSelfEntry { get; init; }

    /// <summary>
    /// When the advisor confirmed the caller's verbal consent to receive the text or email. Recorded
    /// separately from <see cref="SentAtUtc"/> and from delivery, and never cleared. <c>null</c>
    /// only for a self-entry invite, which sends nothing.
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
    /// The ACS message id of the texted link, or the ACS operation id of the emailed one. SMS
    /// delivery reports are matched back to the invite by it; nothing reports email delivery yet.
    /// </summary>
    [JsonProperty("acsMessageId")]
    public string? AcsMessageId { get; set; }

    /// <summary>One of the <see cref="IntakeInviteDeliveryStatus"/> values.</summary>
    [JsonProperty("deliveryStatus")]
    public string DeliveryStatus { get; set; } = IntakeInviteDeliveryStatus.Pending;

    /// <summary>
    /// Whether the token still works at <paramref name="nowUtc"/>: not yet redeemed and not yet
    /// expired (issue #664). Gates both the prefill on open and the attribution on submission.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    public bool IsRedeemableAt(DateTime nowUtc) => RedeemedAtUtc is null && ExpiresAtUtc > nowUtc;

    /// <summary>
    /// Spends the token on the intake submission it produced (issue #664). Called on submission,
    /// never on open: messaging clients fetch the link to build a preview, and that fetch must
    /// not burn the invite before the customer taps it.
    /// </summary>
    /// <param name="serviceRequestId">The service request the submission created.</param>
    /// <param name="nowUtc">The redemption time.</param>
    /// <param name="userId">Audit identity for the update.</param>
    /// <exception cref="InvalidOperationException">The invite was already redeemed.</exception>
    public void MarkRedeemed(string serviceRequestId, DateTime nowUtc, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        if (RedeemedAtUtc is not null)
        {
            throw new InvalidOperationException($"Intake invite '{Id}' was already redeemed.");
        }

        RedeemedAtUtc = nowUtc;
        ServiceRequestId = serviceRequestId;
        MarkAsUpdated(userId);
    }
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

/// <summary>
/// How an <see cref="IntakeInvite"/> reaches the caller (<c>Spec A-14</c>, issue #693).
/// </summary>
public static class IntakeInviteChannel
{
    /// <summary>A text from the shared toll-free number.</summary>
    public const string Sms = "sms";

    /// <summary>An email from the environment's ACS sending domain.</summary>
    public const string Email = "email";

    /// <summary>Whether <paramref name="channel"/> is one of the known values, compared exactly.</summary>
    /// <param name="channel">The value to check.</param>
    public static bool IsKnown(string? channel) => channel is Sms or Email;
}
