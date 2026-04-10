using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Reference data entity representing general RV manufacturer warranty rules.
/// Stored in a dedicated container partitioned by manufacturer.
/// These are baseline patterns — not model-specific guarantees.
/// The Manager app queries this table to supplement per-RV metadata.
/// </summary>
public class RvWarrantyRule : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "rvWarrantyRule";

    /// <summary>
    /// Parent manufacturer or holding company (e.g., "Thor Industries", "Winnebago Industries").
    /// </summary>
    [JsonProperty("manufacturer")]
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>
    /// Specific brand or division (e.g., "Thor Motor Coach", "Grand Design").
    /// </summary>
    [JsonProperty("brandDivision")]
    public string BrandDivision { get; init; } = string.Empty;

    /// <summary>
    /// Typical base (coach systems) warranty duration (e.g., "1 year", "2 years").
    /// </summary>
    [JsonProperty("baseWarranty")]
    public string BaseWarranty { get; init; } = string.Empty;

    /// <summary>
    /// Typical structural (frame, walls, roof structure) warranty duration (e.g., "3 years", "5 years").
    /// </summary>
    [JsonProperty("structuralWarranty")]
    public string StructuralWarranty { get; init; } = string.Empty;

    /// <summary>
    /// Typical roof membrane/skin warranty duration (e.g., "10–12 years", "N/A").
    /// </summary>
    [JsonProperty("roofWarranty")]
    public string RoofWarranty { get; init; } = string.Empty;

    /// <summary>
    /// Additional notes about the warranty (e.g., "Mileage limits often apply").
    /// </summary>
    [JsonProperty("notes")]
    public string? Notes { get; set; }
}
