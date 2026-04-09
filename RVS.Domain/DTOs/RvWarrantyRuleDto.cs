namespace RVS.Domain.DTOs;

/// <summary>
/// Response DTO for an RV manufacturer warranty rule.
/// </summary>
public sealed record RvWarrantyRuleDto
{
    /// <summary>Unique identifier for the warranty rule.</summary>
    public string Id { get; init; } = default!;

    /// <summary>Parent manufacturer or holding company.</summary>
    public string Manufacturer { get; init; } = default!;

    /// <summary>Specific brand or division name.</summary>
    public string BrandDivision { get; init; } = default!;

    /// <summary>Typical base (coach systems) warranty duration.</summary>
    public string BaseWarranty { get; init; } = default!;

    /// <summary>Typical structural warranty duration.</summary>
    public string StructuralWarranty { get; init; } = default!;

    /// <summary>Typical roof membrane/skin warranty duration.</summary>
    public string RoofWarranty { get; init; } = default!;

    /// <summary>Additional notes about the warranty.</summary>
    public string? Notes { get; init; }
}
