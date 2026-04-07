using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Keyword-matching fallback implementation of <see cref="ICategorizationService"/>.
/// Used as the deterministic fallback when Azure OpenAI is unavailable.
/// </summary>
public sealed class RuleBasedCategorizationService : ICategorizationService
{
    private const string ProviderName = nameof(RuleBasedCategorizationService);

    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electrical"] = ["battery", "wiring", "fuse", "outlet", "light", "switch", "inverter", "solar", "generator"],
        ["Plumbing"] = ["water", "leak", "pipe", "faucet", "toilet", "pump", "tank", "drain", "sewer"],
        ["HVAC"] = ["air conditioning", "a/c", "furnace", "thermostat", "hvac", "cooling", "heating"],
        ["Structural"] = ["roof", "wall", "floor", "slide", "seal", "crack", "delamination", "frame"],
        ["Appliance"] = ["refrigerator", "fridge", "oven", "stove", "microwave", "washer", "dryer", "dishwasher"],
        ["Exterior"] = ["awning", "door", "window", "paint", "decal", "hitch", "tire", "wheel", "jack"],
    };

    private static readonly Dictionary<string, IReadOnlyList<DiagnosticQuestionItem>> DiagnosticQuestions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electrical"] =
        [
            new DiagnosticQuestionItem(
                "Is the issue with 12V DC or 120V AC power?",
                ["12V DC (Battery)", "120V AC (Shore Power)", "Both", "Not sure"],
                true,
                "12V DC powers lights and fans; 120V AC powers outlets and large appliances."),
            new DiagnosticQuestionItem(
                "Does the problem occur when connected to shore power, on battery, or both?",
                ["Shore power only", "Battery only", "Both", "Not sure"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Have you checked the breaker panel and fuse box?",
                ["Yes — all OK", "Yes — found tripped breaker/blown fuse", "No", "Not sure where they are"],
                true,
                "The breaker panel is usually near the main entry door.")
        ],
        ["Plumbing"] =
        [
            new DiagnosticQuestionItem(
                "Is the leak coming from fresh water, gray water, or black water systems?",
                ["Fresh water", "Gray water", "Black water", "Not sure"],
                true,
                "Fresh water is clean supply; gray water is from sinks/shower; black water is from the toilet."),
            new DiagnosticQuestionItem(
                "Does the issue occur when the water pump is running or only on city water?",
                ["Water pump only", "City water only", "Both", "Not sure"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Where is the leak located — inside or under the unit?",
                ["Inside the unit", "Under the unit", "Both", "Can't tell"],
                true,
                null)
        ],
        ["HVAC"] =
        [
            new DiagnosticQuestionItem(
                "Is the issue with heating, cooling, or both?",
                ["Heating only", "Cooling only", "Both", "Thermostat/controls"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Does the thermostat display correctly and respond to changes?",
                ["Yes — displays and responds", "Displays but doesn't respond", "Blank/no power", "Not sure"],
                true,
                null),
            new DiagnosticQuestionItem(
                "When was the last time the filters were cleaned or replaced?",
                ["Within 30 days", "1–3 months ago", "Over 3 months ago", "Never/don't know"],
                true,
                "Dirty filters are the most common cause of HVAC issues in RVs.")
        ],
        ["Structural"] =
        [
            new DiagnosticQuestionItem(
                "Is there visible water damage or staining near the affected area?",
                ["Yes", "No", "Not sure"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Does the issue worsen when the slides are extended or retracted?",
                ["Worse when extended", "Worse when retracted", "No change", "No slides on this unit"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Was the unit recently in transit or exposed to severe weather?",
                ["Yes — recent travel", "Yes — severe weather", "Both", "No"],
                true,
                null)
        ],
        ["Appliance"] =
        [
            new DiagnosticQuestionItem(
                "Which appliance is affected?",
                ["Refrigerator", "Oven/Stove", "Microwave", "Washer/Dryer", "Water Heater", "Other"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Does the appliance receive power (indicator lights, sounds)?",
                ["Yes — has power", "No — completely dead", "Intermittent", "Not sure"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Has the appliance worked correctly before, or is this a new installation?",
                ["Worked before — recently stopped", "Never worked correctly", "New installation", "Intermittent issue"],
                true,
                null)
        ],
        ["Exterior"] =
        [
            new DiagnosticQuestionItem(
                "Is the issue cosmetic or does it affect functionality?",
                ["Cosmetic only", "Affects functionality", "Both"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Was the damage caused by weather, road debris, or normal wear?",
                ["Weather damage", "Road debris", "Normal wear", "Accident/impact", "Unknown"],
                true,
                null),
            new DiagnosticQuestionItem(
                "Is the affected component covered under the manufacturer warranty?",
                ["Yes", "No", "Not sure", "Warranty expired"],
                true,
                null)
        ],
    };

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

        return Task.FromResult("General");
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
