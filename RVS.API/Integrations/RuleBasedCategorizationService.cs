using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Keyword-matching fallback implementation of <see cref="ICategorizationService"/>.
/// Used as the deterministic fallback when Azure OpenAI is unavailable. Every category it
/// returns is a code from <see cref="IssueCategoryVocabulary"/>; an unmatched description
/// resolves to <see cref="IssueCategoryVocabulary.FallbackCode"/>. It also owns the
/// hand-written per-category diagnostic questions (<c>Spec A-4</c>) served whenever the AI
/// question generator fails or is not configured.
/// </summary>
public sealed class RuleBasedCategorizationService : ICategorizationService
{
    private const string ProviderName = nameof(RuleBasedCategorizationService);

    // Ordered most-specific first: the first category with any keyword hit wins.
    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Slides"] = ["slide-out", "slideout", "slide out", "slide room", "slide won't", "slide will not", "retract", "slide topper"],
        ["Generator"] = ["generator", "genset", "onan", "gen set"],
        ["LPGas"] = ["propane", "lp gas", "lpg", "gas leak", "gas smell", "regulator", "propane detector", "lp detector"],
        ["Awning"] = ["awning", "canopy", "patio shade"],
        ["Appliances"] = ["refrigerator", "fridge", "oven", "stove", "cooktop", "range", "microwave", "washer", "dryer", "dishwasher", "ice maker"],
        ["HVAC"] = ["air conditioning", "a/c", "ac unit", "furnace", "thermostat", "hvac", "cooling", "heating", "heat pump"],
        ["Roof"] = ["roof", "sealant", "resealant", "reseal", "membrane", "delamination", "water intrusion", "soft spot", "ceiling leak", "seam"],
        ["Chassis"] = ["brake", "tire", "wheel", "bearing", "suspension", "axle", "leveling", "stabilizer", "landing gear", "lug"],
        ["Exterior"] = ["door", "window", "latch", "compartment", "hitch", "fiberglass", "decal", "paint", "exterior panel", "slide seal"],
        ["Interior"] = ["cabinet", "drawer", "dinette", "sofa", "furniture", "flooring", "blinds", "countertop", "upholstery"],
        ["Electrical"] = ["battery", "wiring", "fuse", "outlet", "breaker", "inverter", "converter", "solar", "12v", "shore power", "won't charge", "will not charge", "no power"],
        ["Plumbing"] = ["water", "leak", "pipe", "faucet", "toilet", "pump", "black tank", "grey tank", "gray tank", "fresh tank", "drain", "sewer", "water heater"],
    };

    // Hand-written per-category fallback questions (Spec A-4), used whenever the AI call
    // fails or no AI endpoint is configured. Keys MUST be codes from
    // IssueCategoryVocabulary.Codes — CategoriesWithDedicatedQuestions asserts full coverage.
    // Readable mirror + review worksheet: Docs/RVS_DiagnosticQuestionBank.md.
    // Draft set pending review with a working RV technician (issue #511, section 2): keep
    // questions answerable by an owner standing next to the rig with no tools, and prefer
    // observations that change what parts or time a tech books.
    private static readonly Dictionary<string, IReadOnlyList<DiagnosticQuestionItem>> DiagnosticQuestions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Slides"] =
        [
            new DiagnosticQuestionItem(
                "Does the slide move at all when you operate the switch?",
                ["No movement and no sound", "Motor hums or clicks but nothing moves", "Moves part way then stops", "Moves, but slow, jerky, or noisy"],
                true,
                "The slide switch is usually inside near the door or on the main control panel."),
            new DiagnosticQuestionItem(
                "Which slide is affected?",
                ["Living room / main slide", "Bedroom slide", "Kitchen / galley slide", "More than one slide"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Where is the slide stuck right now?",
                ["Stuck fully out", "Stuck fully in", "Stuck part way out", "Comes in but won't seal against the wall"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Have you noticed any of these at the slide?",
                ["Grinding or binding noise", "Visible damage to gears, rails, or arms", "Water coming in past the seal", "Nothing obvious"],
                true,
                "A photo of the mechanism from inside a storage bay or underneath helps the tech prepare.")
        ],
        ["Electrical"] =
        [
            new DiagnosticQuestionItem(
                "Is the problem with 12-volt power, 120-volt power, or both?",
                ["12-volt (interior lights, water pump, furnace fan)", "120-volt (wall outlets, microwave, air conditioner)", "Both", "Not sure"],
                true,
                "12-volt runs off the RV batteries; 120-volt needs shore power or the generator."),
            new DiagnosticQuestionItem(
                "When does the problem happen?",
                ["Only on shore power", "Only on battery", "Only on the generator", "All the time"],
                true,
                null),
            new DiagnosticQuestionItem(
                "What are the house batteries doing?",
                ["Won't hold a charge overnight", "Reading low or dead", "Seem fine", "Not sure how to check"],
                true,
                "The panel by the door or a battery monitor usually shows charge level."),
            new DiagnosticQuestionItem(
                "Have any breakers or fuses tripped?",
                ["Yes, and resetting it didn't hold", "Yes, resetting fixed it for now", "No", "Couldn't find the panel"],
                true,
                "There is a 120-volt breaker panel and a 12-volt fuse block, often near the door or under a seat.")
        ],
        ["Plumbing"] =
        [
            new DiagnosticQuestionItem(
                "Where is the water showing up?",
                ["Inside on the floor", "In a storage bay or the underbelly", "Outside under the RV", "At one fixture (sink, toilet, shower)"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Is it fresh, gray, or black water?",
                ["Fresh / clean supply water", "Gray (sink and shower drains)", "Black (toilet)", "Not sure"],
                true,
                "Fresh is clean supply water; gray is from sinks and the shower; black is from the toilet."),
            new DiagnosticQuestionItem(
                "When does the problem happen?",
                ["Only with the water pump running", "Only on city / campground water", "Both", "Even with the water off and hose disconnected"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Is the water heater involved?",
                ["Yes — no hot water", "Yes — leaking at the water heater", "No", "Not sure"],
                true,
                null)
        ],
        ["HVAC"] =
        [
            new DiagnosticQuestionItem(
                "What part is not working?",
                ["Air conditioner not cooling", "Furnace / heat not working", "Thermostat blank or unresponsive", "Runs but short-cycles or freezes up"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Does the unit try to start?",
                ["Fan runs but no cold or hot air", "Nothing happens at all", "Starts, then shuts off after a few minutes", "Trips a breaker when it starts"],
                true,
                null),
            new DiagnosticQuestionItem(
                "How many roof AC or furnace units are on the RV, and how many are affected?",
                ["One unit — it's affected", "Two units — one affected", "Two units — both affected", "Not sure"],
                true,
                null),
            new DiagnosticQuestionItem(
                "When were the filters and return-air vents last cleaned?",
                ["In the last month", "A few months ago", "Over a year ago, or never", "Not sure"],
                true,
                "Dirty filters and blocked vents are the most common cause of weak airflow.")
        ],
        ["Generator"] =
        [
            new DiagnosticQuestionItem(
                "What does the generator do when you start it?",
                ["Cranks but won't fire", "Clicks, or nothing at all", "Starts, then shuts down", "Runs, but no power at the outlets"],
                true,
                "Start it from the generator switch on the RV, not just the auto-start."),
            new DiagnosticQuestionItem(
                "How much fuel is available to it?",
                ["Main tank above a quarter", "Main tank below a quarter", "Runs off its own propane supply", "Not sure"],
                true,
                "Most built-in generators stop drawing fuel once the main tank drops below about a quarter."),
            new DiagnosticQuestionItem(
                "When did it last run normally?",
                ["It has sat unused for months", "Ran fine last trip", "Has never run right since we got it", "Not sure"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Any warning lights, error codes, or smells?",
                ["Error code or blinking light on the generator", "Smell of fuel or exhaust", "Unusual noise or vibration", "None of these"],
                true,
                null)
        ],
        ["LPGas"] =
        [
            new DiagnosticQuestionItem(
                "Do you smell propane right now?",
                ["Yes — strong smell", "Yes — faint or occasional", "Only when an appliance runs", "No"],
                true,
                "If the smell is strong, leave the RV, shut the tank valve, and don't use switches or flames until it's checked."),
            new DiagnosticQuestionItem(
                "Which propane appliances are not working?",
                ["Furnace", "Water heater", "Cooktop / oven", "Refrigerator on propane", "All of them"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Is the propane leak detector alarming?",
                ["Yes — alarming now", "It alarmed earlier, then stopped", "No", "Not sure which alarm that is"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Have the tanks just been filled or swapped?",
                ["Yes — just filled or swapped", "About half full", "Near empty", "Not sure"],
                true,
                "After a fill, turn the tank valve on slowly — opening it fast can lock out the regulator.")
        ],
        ["Appliances"] =
        [
            new DiagnosticQuestionItem(
                "Which appliance is the problem?",
                ["Refrigerator", "Cooktop / oven", "Microwave", "Washer / dryer", "Ice maker", "Other"],
                true,
                null),
            new DiagnosticQuestionItem(
                "If it's the refrigerator, how is it powered when it fails?",
                ["Fails on propane, works on electric", "Fails on electric, works on propane", "Fails on both", "Not a refrigerator issue"],
                true,
                "Most RV refrigerators run on either propane or 120-volt electric."),
            new DiagnosticQuestionItem(
                "Does the appliance show any sign of power?",
                ["Lights or display on, but doesn't run", "Completely dead", "Works on and off", "Not sure"],
                true,
                null),
            new DiagnosticQuestionItem(
                "How cold is the refrigerator or freezer?",
                ["Fridge warm, freezer still cold", "Both warm", "Cool, but not cold enough", "Not a temperature problem"],
                true,
                null)
        ],
        ["Roof"] =
        [
            new DiagnosticQuestionItem(
                "Where does the water come in?",
                ["Around a roof vent or fan", "Around the air conditioner", "Along a slide-out", "Around a window or marker light", "Can't tell"],
                true,
                null),
            new DiagnosticQuestionItem(
                "When do you notice the leak?",
                ["During or after rain", "After running the water system", "Only with the slide in a certain position", "All the time"],
                true,
                "A leak only in rain points to the roof or seals; a leak that tracks water use points to plumbing."),
            new DiagnosticQuestionItem(
                "Is there a soft or spongy spot?",
                ["Yes — on the roof", "Yes — on an inside wall or ceiling", "Yes — on the floor", "No soft spots noticed"],
                true,
                null),
            new DiagnosticQuestionItem(
                "When was the roof sealant last inspected or resealed?",
                ["Within the last year", "One to three years ago", "Longer, or never", "Not sure"],
                true,
                null)
        ],
        ["Awning"] =
        [
            new DiagnosticQuestionItem(
                "What is the awning doing?",
                ["Won't extend", "Won't retract", "Extends or retracts crooked or jerky", "Fabric or an arm is damaged"],
                true,
                "Note whether it's a manual awning or powered from a switch."),
            new DiagnosticQuestionItem(
                "Is it stuck open right now?",
                ["Yes — stuck fully open", "Yes — stuck part way", "No — it's closed", "Closed, but won't lock for travel"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Did anything happen right before it stopped working?",
                ["Wind or a storm", "It was hit, or something fell on it", "Just stopped on its own", "Not sure"],
                true,
                null)
        ],
        ["Chassis"] =
        [
            new DiagnosticQuestionItem(
                "Which part of the running gear is the concern?",
                ["Brakes", "Tires or wheels", "Wheel bearings or hubs", "Suspension or axles", "Leveling or stabilizer jacks"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Is the RV safe to move or tow right now?",
                ["Yes — drives or tows normally", "Drives, but something feels wrong", "No — not safe to move", "Not sure"],
                true,
                "A loud noise, smoke from a wheel, or a flat tire means treat it as not safe to move."),
            new DiagnosticQuestionItem(
                "What do you notice while driving or towing?",
                ["Grinding or squealing noise", "Vibration or shaking", "Pulling to one side", "Smell of something hot", "Nothing while moving"],
                true,
                null),
            new DiagnosticQuestionItem(
                "If it's the leveling or stabilizer jacks, what happens?",
                ["Won't extend", "Won't retract", "One corner won't move", "Error on the leveling panel", "Not a jack issue"],
                true,
                null)
        ],
        ["Exterior"] =
        [
            new DiagnosticQuestionItem(
                "What part of the body or exterior is affected?",
                ["An entry or compartment door", "A window", "A latch, lock, or hinge", "Exterior panel, trim, or fiberglass", "Steps or entry assist"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Does it still work, or is it stuck?",
                ["Opens and closes, but won't seal or latch", "Stuck open", "Stuck closed", "Works, but damaged"],
                true,
                "A compartment door that won't latch matters before travel — say which one."),
            new DiagnosticQuestionItem(
                "How did it start?",
                ["Wind caught it, or it happened in transit", "Impact or something struck it", "Just wore out or stopped working", "Was like this when we got the RV"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Is weather getting in?",
                ["Yes — water or a draft is coming in", "Not yet, but it won't seal", "No", "Not sure"],
                true,
                null)
        ],
        ["Interior"] =
        [
            new DiagnosticQuestionItem(
                "What inside the RV needs attention?",
                ["A cabinet or drawer", "A door or latch", "Furniture (sofa, dinette, bed)", "Flooring", "Blinds or window coverings", "Trim or molding"],
                true,
                null),
            new DiagnosticQuestionItem(
                "What is wrong with it?",
                ["Broken or cracked", "Loose or pulling away from the wall or floor", "Won't open, close, or latch", "Water-stained or swollen"],
                true,
                "Swollen or stained cabinetry can be a sign of a leak — say where."),
            new DiagnosticQuestionItem(
                "Did it happen in transit?",
                ["Yes — after a trip", "Gradually over time", "Was like this at delivery", "Not sure"],
                true,
                null)
        ],
        [IssueCategoryVocabulary.FallbackCode] =
        [
            new DiagnosticQuestionItem(
                "In your own words, what is happening?",
                [],
                true,
                "Include when it started, how often it happens, and anything you've noticed — sounds, smells, leaks, lights."),
            new DiagnosticQuestionItem(
                "When did the problem start?",
                ["Today", "This week", "This month", "More than a month ago"],
                true,
                null),
            new DiagnosticQuestionItem(
                "How would you describe it now?",
                ["Constant", "Comes and goes", "Getting worse", "Only in certain conditions"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Is the RV usable right now?",
                ["Yes — fully usable", "Usable with workarounds", "Not usable", "Not safe to use"],
                true,
                "This helps us judge how quickly you need to be seen.")
        ],
    };

    /// <summary>
    /// Vocabulary codes that have a hand-written fallback question set (Spec A-4). Exposed for
    /// tests to assert full coverage of <see cref="IssueCategoryVocabulary.Codes"/>; a code
    /// absent here would silently fall through to <see cref="DefaultQuestions"/>.
    /// </summary>
    internal static IReadOnlyCollection<string> CategoriesWithDedicatedQuestions => DiagnosticQuestions.Keys;

    // Defensive catch-all for a category string that is not in the vocabulary at all
    // (every real vocabulary code, including "Other", has its own entry above).
    private static readonly IReadOnlyList<DiagnosticQuestionItem> DefaultQuestions =
    [
        new DiagnosticQuestionItem(
            "Can you describe the issue in more detail?",
            [],
            true,
            "Include when the issue started, how often it occurs, and any symptoms."),
        new DiagnosticQuestionItem(
            "When did you first notice the problem?",
            ["Today", "This week", "This month", "Over a month ago"],
            true,
            null),
        new DiagnosticQuestionItem(
            "Is the issue intermittent or constant?",
            ["Intermittent", "Constant", "Getting worse", "Only under certain conditions"],
            true,
            null)
    ];

    /// <inheritdoc />
    public Task<string> CategorizeAsync(string issueDescription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueDescription);

        var lowerDescription = issueDescription.ToLowerInvariant();

        foreach (var (category, keywords) in CategoryKeywords)
        {
            if (keywords.Any(kw => lowerDescription.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(category);
            }
        }

        return Task.FromResult(IssueCategoryVocabulary.FallbackCode);
    }

    /// <inheritdoc />
    public Task<DiagnosticQuestionsResult> SuggestDiagnosticQuestionsAsync(
        string issueCategory,
        string? issueDescription = null,
        string? manufacturer = null,
        string? model = null,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCategory);

        var questions = DiagnosticQuestions.TryGetValue(issueCategory, out var found) ? found : DefaultQuestions;
        var result = new DiagnosticQuestionsResult(questions, SmartSuggestion: null, ProviderName);
        return Task.FromResult(result);
    }
}
