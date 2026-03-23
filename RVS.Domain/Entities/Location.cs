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