using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Keyword-matching fallback implementation of <see cref="ICategorizationService"/>.
/// Used as the deterministic fallback when Azure OpenAI is unavailable.
/// </summary>
public sealed class RuleBasedCategorizationService : ICategorizationService
{
    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electrical"] = ["battery", "wiring", "fuse", "outlet", "light", "switch", "inverter", "solar", "generator"],
        ["Plumbing"] = ["water", "leak", "pipe", "faucet", "toilet", "pump", "tank", "drain", "sewer"],
        ["HVAC"] = ["air conditioning", "a/c", "furnace", "thermostat", "hvac", "cooling", "heating"],
        ["Structural"] = ["roof", "wall", "floor", "slide", "seal", "crack", "delamination", "frame"],
        ["Appliance"] = ["refrigerator", "fridge", "oven", "stove", "microwave", "washer", "dryer", "dishwasher"],
        ["Exterior"] = ["awning", "door", "window", "paint", "decal", "hitch", "tire", "wheel", "jack"],
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> DiagnosticQuestions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electrical"] =
        [
            "Is the issue with 12V DC or 120V AC power?",
            "Does the problem occur when connected to shore power, on battery, or both?",
            "Have you checked the breaker panel and fuse box?"
        ],
        ["Plumbing"] =
        [
            "Is the leak coming from fresh water, gray water, or black water systems?",
            "Does the issue occur when the water pump is running or only on city water?",
            "Where is the leak located — inside or under the unit?"
        ],
        ["HVAC"] =
        [
            "Is the issue with heating, cooling, or both?",
            "Does the thermostat display correctly and respond to changes?",
            "When was the last time the filters were cleaned or replaced?"
        ],
        ["Structural"] =
        [
            "Is there visible water damage or staining near the affected area?",
            "Does the issue worsen when the slides are extended or retracted?",
            "Was the unit recently in transit or exposed to severe weather?"
        ],
        ["Appliance"] =
        [
            "Which appliance is affected?",
            "Does the appliance receive power (indicator lights, sounds)?",
            "Has the appliance worked correctly before, or is this a new installation?"
        ],
        ["Exterior"] =
        [
            "Is the issue cosmetic or does it affect functionality?",
            "Was the damage caused by weather, road debris, or normal wear?",
            "Is the affected component covered under the manufacturer warranty?"
        ],
    };

    private static readonly IReadOnlyList<string> DefaultQuestions =
    [
        "Can you describe the issue in more detail?",
        "When did you first notice the problem?",
        "Is the issue intermittent or constant?"
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
    public Task<IReadOnlyList<string>> SuggestDiagnosticQuestionsAsync(string issueCategory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCategory);

        var questions = DiagnosticQuestions.TryGetValue(issueCategory, out var found) ? found : DefaultQuestions;
        return Task.FromResult(questions);
    }
}
