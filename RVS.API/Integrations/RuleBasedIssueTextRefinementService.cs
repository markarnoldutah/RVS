using RVS.Domain.Integrations;

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

    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electrical"] = ["battery", "wiring", "fuse", "outlet", "light", "switch", "inverter", "converter", "generator", "electrical", "power", "volt", "circuit"],
        ["Plumbing"] = ["water", "leak", "pipe", "faucet", "toilet", "drain", "pump", "tank", "plumbing", "sewer", "heater"],
        ["HVAC"] = ["air conditioning", "ac", "heat", "furnace", "thermostat", "hvac", "climate", "cooling", "heating", "vent", "duct"],
        ["Appliance"] = ["refrigerator", "fridge", "microwave", "oven", "stove", "washer", "dryer", "dishwasher", "appliance"],
        ["Structural"] = ["roof", "wall", "floor", "door", "window", "frame", "body", "seal", "crack", "structural", "delamination"],
        ["Slide-Out"] = ["slide", "slide-out", "slideout", "slide out", "extend", "retract"],
        ["Awning"] = ["awning", "canopy", "shade"]
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
