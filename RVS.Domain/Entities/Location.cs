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