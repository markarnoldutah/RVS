using System.Runtime.Serialization;
using Newtonsoft.Json;
using RVS.Domain.Validation;

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
    /// IANA time-zone id for this location (e.g. <c>America/Denver</c>), issue #506. The
    /// service packet's <c>Received</c> line is rendered in this zone so a service advisor
    /// reads the time they actually took the request. <c>null</c> — the default, and what
    /// every location created before #506 carries — leaves that line in UTC. Validated by
    /// <see cref="Validation.TimeZoneValidator"/>.
    /// </summary>
    [JsonProperty("timeZoneId")]
    public string? TimeZoneId { get; set; }

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

    /// <summary>
    /// What this location's customers see of the dealer: the logo on the intake form and the
    /// packet, and the intake form's header colour (<c>Spec A-16</c>, issue #470). Empty — the
    /// default — shows the RV Intake mark on Denim.
    /// </summary>
    [JsonProperty("branding")]
    public LocationBrandingEmbedded Branding { get; set; } = new();

    /// <summary>
    /// Up to two questions the dealer asks on every intake, shown on the diagnostic step after the
    /// AI's and answered in free text (<c>Spec A-18</c>, issue #785). Empty — the default — leaves
    /// the step as the AI builds it.
    /// </summary>
    [JsonProperty("dealerQuestions")]
    public List<string> DealerQuestions { get; set; } = [];

    /// <summary>
    /// Read-time migration (issue #470): the logo lived at <c>packetConfig.logoUrl</c> (#435)
    /// until branding had a home of its own. A stored legacy logo moves into
    /// <see cref="Branding"/> unless branding already has one, and is never written back, so
    /// the next save of the location completes the migration. A legacy value that is not an
    /// absolute https URL is dropped rather than carried — branding rejects it, and a location
    /// that failed validation on a field its settings screen no longer shows could not be saved.
    /// </summary>
    [OnDeserialized]
    internal void MigrateLegacyPacketLogo(StreamingContext context)
    {
        Branding ??= new LocationBrandingEmbedded();
        PacketConfig ??= new PacketConfigEmbedded();

#pragma warning disable CS0618 // the legacy field exists only to be read here
        var legacy = PacketConfig.LegacyLogoUrl?.Trim();
        PacketConfig.LegacyLogoUrl = null;
#pragma warning restore CS0618

        if (!Branding.HasLogo && LocationBrandingValidator.IsHttpsUrl(legacy))
        {
            Branding.LogoUrl = legacy;
        }
    }
}

// ---------------------------------------------------------------------------
// Embedded: LocationBrandingEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// A location's customer-facing branding (<c>Spec A-16</c>, issue #470). Every field is
/// optional; each falls back to the RV Intake default on its own.
/// </summary>
public class LocationBrandingEmbedded
{
    /// <summary>
    /// Absolute https URL of the dealer's logo. Shown top left on the packet, in the intake
    /// form's header bar and on its landing step. <c>null</c> shows the RV Intake mark instead.
    /// Validated by <see cref="LocationBrandingValidator"/>.
    /// </summary>
    [JsonProperty("logoUrl")]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// The intake form's header-bar colour as <c>#RRGGBB</c>. <c>null</c> keeps the brand's
    /// Denim. The foreground on it is chosen for contrast (<see cref="RVS.Domain.Branding.HeaderColor"/>),
    /// and high-contrast mode ignores it.
    /// </summary>
    [JsonProperty("headerColor")]
    public string? HeaderColor { get; set; }

    /// <summary>
    /// The colour that replaces Rust on the intake form — buttons, links, focus rings, checked
    /// controls — as <c>#RRGGBB</c>, stored as the dealer entered it. <c>null</c> keeps Rust. It is
    /// also link text on white, so a colour under 4.5:1 there is shown to customers as the lightest
    /// darker shade that reaches it (<see cref="LocationBrandingValidator.EffectiveAccent"/>).
    /// High contrast ignores it.
    /// </summary>
    [JsonProperty("accentColor")]
    public string? AccentColor { get; set; }

    /// <summary><c>true</c> when a logo URL is set.</summary>
    [JsonIgnore]
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoUrl);

    /// <summary><c>true</c> when a header colour is set.</summary>
    [JsonIgnore]
    public bool HasHeaderColor => !string.IsNullOrWhiteSpace(HeaderColor);

    /// <summary><c>true</c> when an accent colour is set.</summary>
    [JsonIgnore]
    public bool HasAccentColor => !string.IsNullOrWhiteSpace(AccentColor);
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
    public const int DefaultPasteBlockCharacterCap = Packets.PasteBlockGenerator.DefaultCharacterCap;

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

    /// <summary>
    /// Where the location logo was stored before it moved to <see cref="Location.Branding"/>
    /// (issue #470). Read from stored documents only, so <see cref="Location"/> can migrate it,
    /// and never written back.
    /// </summary>
    [JsonProperty("logoUrl")]
    [Obsolete("Moved to Location.Branding.LogoUrl (issue #470). Read only by the Location migration.")]
    public string? LegacyLogoUrl { get; set; }

    /// <summary>Tells Newtonsoft never to write <see cref="LegacyLogoUrl"/>.</summary>
    public bool ShouldSerializeLegacyLogoUrl() => false;
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