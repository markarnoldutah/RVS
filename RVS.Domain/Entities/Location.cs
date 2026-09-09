using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// A service location within a dealership.
///
/// Cosmos DB partition key: /tenantId
/// Unique key policy: [/tenantId, /slug]
/// </summary>
public class Location : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "location";

    /// <summary>
    /// Mirror of <see cref="EntityBase.Id"/> for convenience.
    /// </summary>
    [JsonProperty("locationId")]
    public string LocationId => Id;

    /// <summary>
    /// URL-safe slug for this location (e.g., "salt-lake-service-center").
    /// </summary>
    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Physical address of the location.
    /// </summary>
    [JsonProperty("address")]
    public AddressEmbedded Address { get; set; } = new();

    /// <summary>
    /// Contact phone number for this location.
    /// </summary>
    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// Intake form configuration specific to this location.
    /// </summary>
    [JsonProperty("intakeConfig")]
    public IntakeFormConfigEmbedded IntakeConfig { get; set; } = new();

    /// <summary>
    /// Capability codes (from <see cref="TenantCapabilityEmbedded.Code"/>) that this
    /// location actively supports. Empty list means no capabilities have been configured.
    /// </summary>
    [JsonProperty("enabledCapabilities")]
    public List<string> EnabledCapabilities { get; set; } = [];

    /// <summary>
    /// Per-location configuration for the service packet and its email delivery
    /// (<c>Spec B-6</c> / <c>C-6</c>).
    /// </summary>
    [JsonProperty("packetConfig")]
    public PacketConfigEmbedded PacketConfig { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Embedded: PacketConfigEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Per-location configuration for the one-page service packet and the email that
/// carries it to the service department (<c>Spec B-6</c> / <c>C-6</c>).
///
/// Defaults are chosen so a location is ready to use as soon as its recipient
/// address is set — nothing else has to change.
/// </summary>
public class PacketConfigEmbedded
{
    /// <summary>Maximum number of packet-email recipients allowed per location (<c>Spec B-4</c>).</summary>
    public const int MaxRecipients = 10;

    /// <summary>Default paste-block character cap (<c>Spec B-5</c>).</summary>
    public const int DefaultPasteBlockCharacterCap = 1000;

    /// <summary>Smallest allowed paste-block character cap.</summary>
    public const int MinPasteBlockCharacterCap = 100;

    /// <summary>Largest allowed paste-block character cap.</summary>
    public const int MaxPasteBlockCharacterCap = 5000;

    /// <summary>Default status-link time-to-live, in days (<c>Spec X-5</c>, ≤ 30).</summary>
    public const int DefaultStatusLinkTtlDays = 30;

    /// <summary>Largest allowed status-link time-to-live, in days (<c>Spec X-5</c>).</summary>
    public const int MaxStatusLinkTtlDays = 30;

    /// <summary>
    /// Whether the packet email is delivered for this location. When <c>false</c>, packets are
    /// still generated but nothing is emailed.
    /// </summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Email addresses that receive the packet (<c>Spec B-4</c>). 0–<see cref="MaxRecipients"/>
    /// entries; the location is not operational for email until at least one is set.
    /// </summary>
    [JsonProperty("recipients")]
    public List<string> Recipients { get; set; } = [];

    /// <summary>
    /// Recipient addresses that have been parked after a hard bounce (<c>Spec B-4</c>, issue #439).
    /// A hard bounce disables <b>that one address</b> — it moves here from <see cref="Recipients"/>
    /// so packets keep flowing to the rest — and stays visible so a manager can restore it from
    /// location settings once the address is fixed. Never populated for a brand-new location.
    /// </summary>
    [JsonProperty("disabledRecipients")]
    public List<DisabledRecipientEmbedded> DisabledRecipients { get; set; } = [];

    /// <summary>
    /// Parks <paramref name="email"/> after a hard bounce: removes it from the active
    /// <see cref="Recipients"/> list and records it on <see cref="DisabledRecipients"/> with the
    /// bounce <paramref name="reason"/> and <paramref name="disabledAtUtc"/>. Address matching is
    /// case-insensitive and whitespace-insensitive. Idempotent — returns <c>false</c> without
    /// changing anything when <paramref name="email"/> is not currently an active recipient
    /// (already disabled, or never configured).
    /// </summary>
    /// <returns><c>true</c> when an active recipient was parked; otherwise <c>false</c>.</returns>
    public bool DisableRecipient(string email, string? reason, DateTime disabledAtUtc)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var target = email.Trim();
        var match = Recipients.FirstOrDefault(r => Matches(r, target));
        if (match is null)
        {
            return false;
        }

        Recipients.RemoveAll(r => Matches(r, target));

        if (!DisabledRecipients.Any(d => Matches(d.Email, target)))
        {
            DisabledRecipients.Add(new DisabledRecipientEmbedded
            {
                Email = match.Trim(),
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                DisabledAtUtc = disabledAtUtc,
            });
        }

        return true;
    }

    /// <summary>
    /// Restores <paramref name="email"/> from <see cref="DisabledRecipients"/> back to the active
    /// <see cref="Recipients"/> list. Case- and whitespace-insensitive. Idempotent — returns
    /// <c>false</c> without changing anything when <paramref name="email"/> is not currently
    /// disabled.
    /// </summary>
    /// <returns><c>true</c> when a disabled recipient was restored; otherwise <c>false</c>.</returns>
    public bool ReEnableRecipient(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var target = email.Trim();
        var match = DisabledRecipients.FirstOrDefault(d => Matches(d.Email, target));
        if (match is null)
        {
            return false;
        }

        DisabledRecipients.RemoveAll(d => Matches(d.Email, target));

        if (!Recipients.Any(r => Matches(r, target)))
        {
            Recipients.Add(match.Email.Trim());
        }

        return true;
    }

    private static bool Matches(string? a, string b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Attach the rendered PDF to the packet email (<c>Spec B-4</c>).</summary>
    [JsonProperty("attachPdf")]
    public bool AttachPdf { get; set; } = true;

    /// <summary>Attach the original photos to the packet email (<c>Spec B-4</c>).</summary>
    [JsonProperty("includePhotos")]
    public bool IncludePhotos { get; set; } = true;

    /// <summary>Character cap for the plain-text paste block (<c>Spec B-5</c>).</summary>
    [JsonProperty("pasteBlockCharacterCap")]
    public int PasteBlockCharacterCap { get; set; } = DefaultPasteBlockCharacterCap;

    /// <summary>
    /// Time-to-live, in days, for the customer status link minted into the packet (<c>Spec X-5</c>).
    /// </summary>
    [JsonProperty("statusLinkTtlDays")]
    public int StatusLinkTtlDays { get; set; } = DefaultStatusLinkTtlDays;

    /// <summary>Optional absolute URL to a location-specific logo rendered on the packet.</summary>
    [JsonProperty("logoUrl")]
    public string? LogoUrl { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: DisabledRecipientEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// A packet-email recipient address that has been disabled after a hard bounce
/// (<c>Spec B-4</c>, issue #439), embedded on <see cref="PacketConfigEmbedded"/>.
///
/// It is kept rather than deleted so the disabled address stays visible in location settings
/// and can be re-enabled once it is fixed. <see cref="Reason"/> never carries customer data
/// (<c>Spec X-7</c>) — it is the bounce classification from the mail transport.
/// </summary>
public class DisabledRecipientEmbedded
{
    /// <summary>The email address that hard-bounced and was removed from active delivery.</summary>
    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Short, non-PII reason for the bounce (e.g. the transport's bounce classification). Optional.</summary>
    [JsonProperty("reason")]
    public string? Reason { get; set; }

    /// <summary>UTC time the address was disabled.</summary>
    [JsonProperty("disabledAtUtc")]
    public DateTime DisabledAtUtc { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: AddressEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Physical address embedded within a Location or other entity.
/// </summary>
public class AddressEmbedded
{
    [JsonProperty("address1")]
    public string? Address1 { get; set; }

    [JsonProperty("address2")]
    public string? Address2 { get; set; }

    [JsonProperty("city")]
    public string? City { get; set; }

    [JsonProperty("state")]
    public string? State { get; set; }

    [JsonProperty("postalCode")]
    public string? PostalCode { get; set; }
}