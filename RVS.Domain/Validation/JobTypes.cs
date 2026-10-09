namespace RVS.Domain.Validation;

/// <summary>
/// How the operator will do the work on a service request (<c>Spec C-11</c>, issue #843). This is
/// not the issue category (<c>Spec A-5</c>): the category is what is wrong, the job type is how it
/// gets handled. The operator sets it at triage; the customer never does, so it is unset until then.
/// </summary>
/// <remarks>
/// One fixed set for every location, for the same reasons as the status vocabulary (<c>Spec C-8</c>).
/// The Cosmos seeder builds the <c>JobType</c> lookup-set from <see cref="All"/>. Codes are stored
/// verbatim and matched ordinally, like <see cref="DispositionReasons"/>, because the only writer
/// is the manager app's picker.
/// </remarks>
public static class JobTypes
{
    /// <summary>Remote support: a phone or video call.</summary>
    public const string Remote = "Remote";

    /// <summary>On-site repair: the operator goes to the RV.</summary>
    public const string OnSite = "OnSite";

    /// <summary>In-shop repair: the RV comes to the operator.</summary>
    public const string InShop = "InShop";

    /// <summary>An inspection.</summary>
    public const string Inspection = "Inspection";

    /// <summary>An install or upgrade.</summary>
    public const string Install = "Install";

    /// <summary>Anything else.</summary>
    public const string Other = "Other";

    /// <summary>
    /// Search-filter value for requests with no job type yet. Never stored on a request.
    /// </summary>
    public const string NotTriagedFilter = "NotTriaged";

    /// <summary>What an unset job type reads as.</summary>
    public const string NotTriagedLabel = "Not triaged";

    /// <summary>A single job type.</summary>
    /// <param name="Code">Stable code persisted on the request.</param>
    /// <param name="Name">Label shown in the manager app.</param>
    /// <param name="SortOrder">Display order in pickers and filters.</param>
    public sealed record Entry(string Code, string Name, int SortOrder);

    /// <summary>Every job type, in <c>Spec C-11</c> order.</summary>
    public static IReadOnlyList<Entry> All { get; } =
    [
        new(Remote,     "Remote support",    10),
        new(OnSite,     "On-site repair",    20),
        new(InShop,     "In-shop repair",    30),
        new(Inspection, "Inspection",        40),
        new(Install,    "Install / upgrade", 50),
        new(Other,      "Other",             60),
    ];

    /// <summary>Every job-type code, in <c>Spec C-11</c> order.</summary>
    public static IReadOnlyList<string> Codes { get; } = [.. All.Select(e => e.Code)];

    private static readonly IReadOnlyDictionary<string, string> NameByCode =
        All.ToDictionary(e => e.Code, e => e.Name, StringComparer.Ordinal);

    /// <summary>Whether <paramref name="code"/> is exactly one of <see cref="Codes"/>.</summary>
    public static bool IsValid(string? code) => code is not null && NameByCode.ContainsKey(code);

    /// <summary>
    /// Whether <paramref name="filter"/> is a usable job-type filter: a code, or
    /// <see cref="NotTriagedFilter"/>.
    /// </summary>
    public static bool IsValidFilter(string? filter) =>
        IsValid(filter) || string.Equals(filter, NotTriagedFilter, StringComparison.Ordinal);

    /// <summary>
    /// Whether a request with <paramref name="jobType"/> passes <paramref name="filter"/>. A blank
    /// filter passes everything; <see cref="NotTriagedFilter"/> passes only unset job types.
    /// </summary>
    public static bool MatchesFilter(string? jobType, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return string.Equals(filter, NotTriagedFilter, StringComparison.Ordinal)
            ? string.IsNullOrWhiteSpace(jobType)
            : string.Equals(jobType, filter, StringComparison.Ordinal);
    }

    /// <summary>
    /// The label for a job type. Unset reads <see cref="NotTriagedLabel"/>; an unknown code comes
    /// back as-is, so a value written by a later version still displays something.
    /// </summary>
    public static string GetLabel(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return NotTriagedLabel;
        }

        return NameByCode.TryGetValue(code, out var name) ? name : code;
    }
}
