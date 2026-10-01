namespace RVS.Domain.Validation;

/// <summary>
/// Controlled confidence vocabulary for the structured preliminary assessment
/// (<c>Spec B-2</c> item 5, issue #507). <see cref="Abstain"/> means the generator declined to
/// offer a cause, fixes, or parts, and the packet renders none.
/// </summary>
public static class AssessmentConfidence
{
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";
    public const string Abstain = "abstain";

    /// <summary>The canonical codes, most confident first.</summary>
    public static readonly IReadOnlyList<string> AllowedValues = [High, Medium, Low, Abstain];

    /// <summary>
    /// Returns the canonical code for <paramref name="value"/> (case-insensitive, whitespace
    /// ignored). Anything blank or unrecognised is <see cref="Abstain"/> — an unparseable
    /// confidence is never promoted into an offered assessment.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Abstain;
        }

        var trimmed = value.Trim();
        return AllowedValues.FirstOrDefault(v => string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase))
            ?? Abstain;
    }

    /// <summary>
    /// The packet label for an offered confidence (<c>High</c> / <c>Medium</c> / <c>Low</c>), or
    /// <c>null</c> when the value abstains or is not in the vocabulary.
    /// </summary>
    public static string? DisplayName(string? value) => Normalize(value) switch
    {
        High => "High",
        Medium => "Medium",
        Low => "Low",
        _ => null,
    };
}
