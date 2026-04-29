namespace RVS.Domain.Validation;

/// <summary>
/// Deterministic mapping from a resolved issue category (produced by the AI categorization
/// service) to the set of service-center capability codes typically required to address
/// the issue. Used by the intake capability-assessment flow to compare against the
/// capabilities enabled on the selected location.
/// </summary>
/// <remarks>
/// Capability codes here MUST match the seed codes defined in
/// <c>ConfigMapper.DefaultCapabilities()</c> (e.g. <c>electrical</c>, <c>plumbing</c>,
/// <c>hvac</c>, <c>body-repair</c>, <c>roof-repair</c>, <c>slide-out-repair</c>,
/// <c>rv-refrigerator</c>, <c>tire-service</c>). Categories that have no specific
/// capability requirement (e.g. "General") return an empty list, in which case the
/// assessment is treated as a match regardless of location capabilities.
/// </remarks>
public static class IssueCategoryCapabilityMap
{
    private static readonly Dictionary<string, IReadOnlyList<string>> CategoryToCapabilities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Electrical"] = ["electrical"],
            ["Plumbing"] = ["plumbing"],
            ["HVAC"] = ["hvac"],
            ["Structural"] = ["body-repair", "roof-repair", "slide-out-repair"],
            ["Appliance"] = ["rv-refrigerator"],
            ["Exterior"] = ["body-repair", "tire-service"],
        };

    /// <summary>
    /// Returns the set of capability codes typically required for the given issue category,
    /// or an empty list when the category is not mapped (e.g. "General", null, or unknown).
    /// </summary>
    /// <param name="issueCategory">Issue category resolved by the AI categorization service.</param>
    public static IReadOnlyList<string> GetRequiredCapabilities(string? issueCategory)
    {
        if (string.IsNullOrWhiteSpace(issueCategory))
        {
            return [];
        }

        return CategoryToCapabilities.TryGetValue(issueCategory.Trim(), out var caps) ? caps : [];
    }
}
