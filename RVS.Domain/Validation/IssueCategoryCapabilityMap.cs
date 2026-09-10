namespace RVS.Domain.Validation;

/// <summary>
/// Deterministic mapping from an issue category (see <see cref="IssueCategoryVocabulary"/>) to
/// the set of service-center capability codes typically required to address the issue. Used by
/// the intake capability pre-check (<c>Spec A-12</c>) to compare against the capabilities
/// enabled on the selected location.
/// </summary>
/// <remarks>
/// Keys MUST be codes from <see cref="IssueCategoryVocabulary.Codes"/>. Capability codes MUST
/// match the seed codes in <c>ConfigMapper.DefaultCapabilities()</c> (<c>electrical</c>,
/// <c>plumbing</c>, <c>hvac</c>, <c>generator</c>, <c>body-repair</c>, <c>roof-repair</c>,
/// <c>slide-out-repair</c>, <c>rv-refrigerator</c>, <c>tire-service</c>, <c>diesel-service</c>).
/// Categories with no specific capability requirement (<c>LPGas</c>, <c>Awning</c>,
/// <c>Interior</c>, <c>Other</c>, or an unknown value) return an empty list, in which case the
/// assessment is treated as a match regardless of location capabilities.
/// </remarks>
public static class IssueCategoryCapabilityMap
{
    private static readonly Dictionary<string, IReadOnlyList<string>> CategoryToCapabilities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Slides"]      = ["slide-out-repair"],
            ["Electrical"]  = ["electrical"],
            ["Plumbing"]    = ["plumbing"],
            ["HVAC"]        = ["hvac"],
            ["Generator"]   = ["generator"],
            ["Appliances"]  = ["rv-refrigerator"],
            ["Roof"]        = ["roof-repair"],
            ["Chassis"]     = ["tire-service"],
            ["Exterior"]    = ["body-repair"],
        };

    /// <summary>
    /// Returns the set of capability codes typically required for the given issue category,
    /// or an empty list when the category is not mapped (<c>LPGas</c>, <c>Awning</c>,
    /// <c>Interior</c>, <c>Other</c>, null, or unknown).
    /// </summary>
    /// <param name="issueCategory">Issue category code from <see cref="IssueCategoryVocabulary"/>.</param>
    public static IReadOnlyList<string> GetRequiredCapabilities(string? issueCategory)
    {
        if (string.IsNullOrWhiteSpace(issueCategory))
        {
            return [];
        }

        return CategoryToCapabilities.TryGetValue(issueCategory.Trim(), out var caps) ? caps : [];
    }
}
