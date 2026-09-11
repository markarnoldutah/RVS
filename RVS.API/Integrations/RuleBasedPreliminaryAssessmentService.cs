using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Deterministic fallback for <see cref="IPreliminaryAssessmentService"/> (issue #507): a
/// category-keyed table of the most common causes, possible fixes and parts. It never reads the
/// description, so every result is <see cref="AssessmentConfidence.Low"/>. <c>Other</c> and
/// unknown categories abstain.
/// </summary>
public sealed class RuleBasedPreliminaryAssessmentService : IPreliminaryAssessmentService
{
    private const string ProviderName = nameof(RuleBasedPreliminaryAssessmentService);

    private sealed record Guidance(string ProbableCause, string[] PossibleFixes, string[] LikelyParts);

    // Keys MUST be codes from IssueCategoryVocabulary.Codes (all but Other) — the tests assert
    // full coverage. Draft wording pending review with a working RV technician, like the
    // fallback question bank (issue #511).
    private static readonly Dictionary<string, Guidance> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Slides"] = new(
            "Slide-out drive fault: the motor, its 12V supply, or the slide controller is the most common point of failure.",
            ["Verify 12V at the slide motor and check the slide fuse or breaker",
             "Inspect the drive mechanism (gears, cables, or rails) for binding or damage",
             "Test the slide motor and controller, and replace whichever has failed"],
            ["Slide-out motor", "Slide controller", "Slide fuse or breaker"]),
        ["Electrical"] = new(
            "12V or 120V supply fault: battery state, the converter or charger, or a tripped breaker or blown fuse.",
            ["Check battery charge, connections, and the battery disconnect",
             "Check breakers, fuses, and GFCI outlets on the affected circuit",
             "Test the converter, charger, or inverter output"],
            ["House battery", "Converter/charger", "Fuses and breakers"]),
        ["Plumbing"] = new(
            "Water system fault: a leaking fitting, the water pump, or a failed valve or fixture.",
            ["Pressure-test the fresh water system to locate any leak",
             "Check the water pump, its fuse, and the inline strainer",
             "Inspect the valves, fittings, and fixtures on the affected line"],
            ["Water pump", "PEX fittings", "Faucet or valve cartridge"]),
        ["HVAC"] = new(
            "Heating or cooling fault: the thermostat or control board, the power supply, or a failed air conditioner or furnace component.",
            ["Confirm power and thermostat settings, and check breakers and fuses",
             "Inspect the A/C capacitor, fan motor, and coils, or the furnace igniter and sail switch",
             "Replace the failed control board or component"],
            ["Thermostat", "A/C start capacitor", "Furnace control board"]),
        ["Generator"] = new(
            "Generator fault: fuel supply, air intake, or an overheat or low-oil shutdown.",
            ["Check the oil level, fuel level, and fuel filter",
             "Inspect the air filter and cooling airflow",
             "Check the start battery and read the generator fault code"],
            ["Fuel filter", "Air filter", "Spark plug"]),
        ["LPGas"] = new(
            "LP supply fault: tank level or valve, the regulator, or a detector or appliance gas valve.",
            ["Leak-test the LP system before any other work",
             "Check tank level, the tank valve, and regulator output pressure",
             "Inspect the LP detector and the appliance gas valves"],
            ["LP regulator", "LP detector", "Pigtail hose"]),
        ["Appliances"] = new(
            "Appliance fault: power or LP supply to the unit, or its control board, igniter, or cooling unit.",
            ["Confirm 12V, 120V, and LP supply to the appliance",
             "Read any fault code and check the control board",
             "Test the igniter, heating element, or cooling unit"],
            ["Control board", "Igniter", "Heating element"]),
        ["Roof"] = new(
            "Roof or seam sealant failure letting water in, possibly with damage to the substrate underneath.",
            ["Inspect roof seams, vents, and fixtures for failed sealant",
             "Moisture-test the surrounding area for water damage",
             "Reseal the seams, or repair the membrane and substrate"],
            ["Self-leveling lap sealant", "Roof membrane patch", "Roof vent"]),
        ["Awning"] = new(
            "Awning fault: the motor or its power supply, the control, or the arm, spring, or fabric hardware.",
            ["Check power to the awning motor and its fuse",
             "Test the awning control and wind sensor",
             "Inspect the arms, springs, and fabric for damage"],
            ["Awning motor", "Awning control module", "Awning fabric"]),
        ["Chassis"] = new(
            "Running-gear wear or fault: brakes, bearings, tires, suspension, or the leveling system.",
            ["Inspect the brakes, wheel bearings, and tires",
             "Check the suspension components and hardware",
             "Test the leveling or stabilizer system"],
            ["Brake assembly", "Wheel bearings", "Suspension bushings"]),
        ["Exterior"] = new(
            "Exterior hardware wear or damage: a door, window, latch, compartment, or body panel.",
            ["Inspect the affected hardware and its mounting points",
             "Adjust or replace the latch, hinge, or seal",
             "Repair or replace the damaged panel or trim"],
            ["Door latch", "Window seal", "Compartment lock"]),
        ["Interior"] = new(
            "Interior fitting wear or damage: cabinetry hardware, a furniture mechanism, or trim.",
            ["Inspect the affected fixture and its fasteners",
             "Adjust or replace hinges, drawer slides, or latches",
             "Repair or replace the damaged panel or furniture mechanism"],
            ["Cabinet hinge", "Drawer slide", "Furniture mechanism"]),
    };

    /// <summary>The categories that have table guidance.</summary>
    internal static IReadOnlyCollection<string> CategoriesWithGuidance => Table.Keys;

    /// <inheritdoc />
    public Task<PreliminaryAssessmentEmbedded> AssessAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceRequest);

        var category = serviceRequest.IssueCategory?.Trim();
        if (string.IsNullOrEmpty(category) || !Table.TryGetValue(category, out var guidance))
        {
            return Task.FromResult(new PreliminaryAssessmentEmbedded
            {
                Confidence = AssessmentConfidence.Abstain,
                Provider = ProviderName,
                GeneratedAtUtc = DateTime.UtcNow,
            });
        }

        return Task.FromResult(new PreliminaryAssessmentEmbedded
        {
            ProbableCause = guidance.ProbableCause,
            PossibleFixes = [.. guidance.PossibleFixes],
            LikelyParts = [.. guidance.LikelyParts],
            Confidence = AssessmentConfidence.Low,
            Provider = ProviderName,
            GeneratedAtUtc = DateTime.UtcNow,
        });
    }
}
