using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Slug → tenantId + locationId resolution index.
/// Enables O(1) location lookup by friendly URL slug during intake.
///
/// Cosmos DB partition key: /slug
/// </summary>
public class SlugLookup : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "slugLookup";

    /// <summary>
    /// URL-safe slug (e.g., "camping-world-salt-lake").
    /// Lowercase, alphanumeric with hyphens only.
    /// </summary>
    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// The location ID this slug resolves to.
    /// </summary>
    [JsonProperty("locationId")]
    public string LocationId { get; set; } = string.Empty;

    /// <summary>
    /// Dealership name for display purposes (denormalized).
    /// </summary>
    [JsonProperty("dealershipName")]
    public string DealershipName { get; set; } = string.Empty;

    /// <summary>
    /// Location name for display purposes (denormalized).
    /// </summary>
    [JsonProperty("locationName")]
    public string LocationName { get; set; } = string.Empty;
}
