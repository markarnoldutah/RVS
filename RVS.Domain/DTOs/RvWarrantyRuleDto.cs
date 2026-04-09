namespace RVS.Domain.DTOs;

/// <summary>
/// Response DTO for an RV manufacturer warranty rule.
/// </summary>
public sealed record RvWarrantyRuleDto
{
    public string Id { get; init; } = default!;
    public string Manufacturer { get; init; } = default!;
    public string BrandDivision { get; init; } = default!;
    public string BaseWarranty { get; init; } = default!;
    public string StructuralWarranty { get; init; } = default!;
    public string RoofWarranty { get; init; } = default!;
    public string? Notes { get; init; }
}
