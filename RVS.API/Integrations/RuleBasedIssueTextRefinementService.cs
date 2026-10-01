using RVS.Domain.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Integrations;

/// <summary>
/// Rule-based implementation that performs basic text cleanup and keyword-based category suggestion.
/// Used as a fallback when Azure OpenAI is not configured.
/// </summary>
public sealed class RuleBasedIssueTextRefinementService : IIssueTextRefinementService
{
    private const double RefinementConfidence = 0.75;
    private const double SuggestionConfidence = 0.70;

    private readonly ILogger<RuleBasedIssueTextRefinementService> _logger;

    // Keys are codes from IssueCategoryVocabulary. Highest keyword-hit count wins; ties keep
    // the earlier entry, so the more specific categories are listed first.
    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Slides"] = ["slide", "slide-out", "slideout", "slide out", "slide room", "extend", "retract", "slide topper"],
        ["Generator"] = ["generator", "genset", "onan", "gen set"],
        ["LPGas"] = ["propane", "lp gas", "lpg", "gas leak", "gas smell", "regulator", "propane detector"],
        ["Awning"] = ["awning", "canopy", "shade"],
        ["Appliances"] = ["refrigerator", "fridge", "microwave", "oven", "stove", "cooktop", "range", "washer", "dryer", "dishwasher", "appliance", "ice maker"],
        ["HVAC"] = ["air conditioning", "ac", "furnace", "thermostat", "hvac", "climate", "cooling", "heating", "vent", "duct", "heat pump"],
        ["Roof"] = ["roof", "sealant", "reseal", "membrane", "delamination", "water intrusion", "soft spot", "ceiling leak", "seam"],
        ["Chassis"] = ["brake", "tire", "wheel", "bearing", "suspension", "axle", "leveling", "stabilizer", "landing gear"],
        ["Exterior"] = ["door", "window", "latch", "compartment", "hitch", "fiberglass", "decal", "paint", "body panel", "slide seal"],
        ["Interior"] = ["cabinet", "drawer", "dinette", "sofa", "furniture", "flooring", "blinds", "countertop", "upholstery"],
        ["Electrical"] = ["battery", "wiring", "fuse", "outlet", "breaker", "inverter", "converter", "electrical", "power", "volt", "circuit", "solar", "shore power"],
        ["Plumbing"] = ["water", "leak", "pipe", "faucet", "toilet", "drain", "pump", "black tank", "grey tank", "gray tank", "fresh tank", "plumbing", "sewer", "water heater"],
    };

    public RuleBasedIssueTextRefinementService(ILogger<RuleBasedIssueTextRefinementService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IssueTextRefinementResult?> RefineTranscriptAsync(string rawTranscript, string? issueCategory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawTranscript);

        _logger.LogDebug("RuleBasedIssueTextRefinementService refining transcript ({Length} chars)", rawTranscript.Length);

        var cleaned = CleanTranscript(rawTranscript);
        var result = new IssueTextRefinementResult(cleaned, RefinementConfidence, nameof(RuleBasedIssueTextRefinementService));
        return Task.FromResult<IssueTextRefinementResult?>(result);
    }

    /// <inheritdoc />
    public Task<IssueCategorySuggestionResult?> SuggestCategoryAsync(string issueDescription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueDescription);

        _logger.LogDebug("RuleBasedIssueTextRefinementService suggesting category for description ({Length} chars)", issueDescription.Length);

        var descriptionLower = issueDescription.ToLowerInvariant();
        string? bestCategory = null;
        var bestScore = 0;

        foreach (var (category, keywords) in CategoryKeywords)
        {
            var score = keywords.Count(keyword => descriptionLower.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (score > bestScore)
            {
                bestScore = score;
                bestCategory = category;
            }
        }

        var confidence = bestCategory is not null ? SuggestionConfidence : 0.0;
        var result = new IssueCategorySuggestionResult(bestCategory, confidence, nameof(RuleBasedIssueTextRefinementService));
        return Task.FromResult<IssueCategorySuggestionResult?>(result);
    }

    /// <inheritdoc />
    public Task<IssueInsightsSuggestionResult?> SuggestInsightsAsync(string issueDescription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueDescription);

        // Keyword-based urgency/usage inference is not reliable enough for a production fallback.
        // Return null so the UI leaves the fields blank for manual selection.
        _logger.LogDebug("RuleBasedIssueTextRefinementService skipping insights suggestion (no AI configured)");
        return Task.FromResult<IssueInsightsSuggestionResult?>(null);
    }

    private static string CleanTranscript(string raw)
    {
        var cleaned = raw.Trim();

        // Remove common filler words at the start
        string[] fillerPrefixes = ["um ", "uh ", "so ", "like ", "well ", "okay ", "ok "];
        foreach (var filler in fillerPrefixes)
        {
            if (cleaned.StartsWith(filler, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[filler.Length..];
            }
        }

        // Capitalize first letter
        if (cleaned.Length > 0 && char.IsLower(cleaned[0]))
        {
            cleaned = char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
        }

        // Ensure ends with period
        if (cleaned.Length > 0 && !cleaned.EndsWith('.') && !cleaned.EndsWith('!') && !cleaned.EndsWith('?'))
        {
            cleaned += ".";
        }

        return cleaned;
    }
}
