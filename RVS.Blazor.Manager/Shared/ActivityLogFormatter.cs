namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// Shared format for the bylined activity log persisted inside
/// <c>ServiceRequest.TechnicianSummary</c>. Comments and synthetic
/// system events (status / tech changes) are stored as
/// <c>[Author — date]\nBody</c> entries separated by a blank line.
/// </summary>
internal static class ActivityLogFormatter
{
    public const string SystemAuthor = "System";
    public const string BylineDateFormat = "MMM d, yyyy h:mm:ss tt";

    public static string BuildBylineHeader(string author) =>
        $"[{author} — {DateTime.Now.ToString(BylineDateFormat)}]";

    public static string BuildSystemEntry(string description) =>
        $"{BuildBylineHeader(SystemAuthor)}\n{description}";

    public static string PrependEntry(string? existingSummary, string newEntry) =>
        string.IsNullOrWhiteSpace(existingSummary)
            ? newEntry
            : $"{newEntry}\n\n{existingSummary}";

    public static string PrependSystemEntry(string? existingSummary, string description) =>
        PrependEntry(existingSummary, BuildSystemEntry(description));
}
