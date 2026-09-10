namespace RVS.Domain.Validation;

/// <summary>
/// The single source of truth for the controlled issue-category vocabulary (<c>Spec A-5</c>).
/// </summary>
/// <remarks>
/// The Cosmos seeder builds the <c>IssueCategory</c> lookup-set from <see cref="All"/>; the
/// rule-based categorization / refinement fallbacks and <see cref="IssueCategoryCapabilityMap"/>
/// key off these exact codes. The list is sized to how RV failures present to a service
/// manager (~13 codes) rather than how parts are catalogued. <see cref="FallbackCode"/> is
/// stored whenever a submitted or AI-suggested category is missing or unrecognised, so the
/// packet never carries a phantom category.
/// </remarks>
public static class IssueCategoryVocabulary
{
    /// <summary>Code stored when a category is missing, blank, or not in the vocabulary.</summary>
    public const string FallbackCode = "Other";

    /// <summary>A single entry in the controlled vocabulary.</summary>
    /// <param name="Code">Stable code persisted on the request and the ledger.</param>
    /// <param name="Name">Customer-facing label shown in the intake dropdown.</param>
    /// <param name="Description">Help text listing the systems the category covers.</param>
    /// <param name="SortOrder">Display order in dropdowns.</param>
    public sealed record Entry(string Code, string Name, string Description, int SortOrder);

    /// <summary>The full vocabulary, in display order.</summary>
    public static IReadOnlyList<Entry> All { get; } =
    [
        new("Slides",     "Slides",                    "Slide-out rooms, mechanisms, motors, and seals", 10),
        new("Electrical", "Electrical",                "12V DC and 120V AC systems, batteries, wiring, inverters, converters, solar", 20),
        new("Plumbing",   "Plumbing & Water",          "Fresh, grey, and black water systems, pump, water heater, and fixtures", 30),
        new("HVAC",       "HVAC",                      "Air conditioning, furnace, heat pump, thermostat, and ducting", 40),
        new("Generator",  "Generator",                 "Onboard generator that will not start, runs rough, or produces no output", 50),
        new("LPGas",      "LP / Propane",              "Propane tanks, regulators, lines, leaks, and detectors", 60),
        new("Appliances", "Appliances & Refrigerator", "Refrigerator, cooktop, oven, microwave, washer / dryer, and ice maker", 70),
        new("Roof",       "Roof & Seals",              "Roof membrane, sealant, seams, window and slide seals, and water intrusion or leaks", 80),
        new("Awning",     "Awning",                    "Awning fabric, arms, motors, and sensors", 90),
        new("Chassis",    "Chassis & Running Gear",    "Brakes, tires, wheel bearings, suspension, axles, and leveling or stabilizer jacks", 100),
        new("Exterior",   "Body & Exterior",           "Exterior panels, fiberglass, trim, doors, windows, compartment latches, and hitch", 110),
        new("Interior",   "Interior & Cabinetry",      "Cabinets, furniture, flooring, blinds, and interior trim", 120),
        new(FallbackCode, "Other",                     "Anything not covered by the categories above", 130),
    ];

    private static readonly IReadOnlyDictionary<string, string> CanonicalByCode =
        All.ToDictionary(e => e.Code, e => e.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>All valid category codes, in display order.</summary>
    public static IReadOnlyList<string> Codes { get; } = [.. All.Select(e => e.Code)];

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="code"/> is a recognised category
    /// code, compared case-insensitively after trimming.
    /// </summary>
    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && CanonicalByCode.ContainsKey(code.Trim());

    /// <summary>
    /// Returns the canonically-cased code matching <paramref name="code"/>, or
    /// <see cref="FallbackCode"/> when it is null, blank, or not in the vocabulary. This is
    /// how a customer-submitted or AI-suggested category is coerced before it is persisted.
    /// </summary>
    public static string Normalize(string? code) =>
        !string.IsNullOrWhiteSpace(code) && CanonicalByCode.TryGetValue(code.Trim(), out var canonical)
            ? canonical
            : FallbackCode;
}
