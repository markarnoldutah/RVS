using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// Shared format for the bylined activity log persisted inside
/// <c>ServiceRequest.TechnicianSummary</c>. Comments, synthetic system events
/// (status / tech changes) and customer status note writes (<c>Spec C-9</c>) are
/// stored as <c>[Author — date]\nBody</c> entries separated by a blank line.
/// An entry that is not a plain comment carries a kind after the date:
/// <c>[Author — date — Customer status note]</c>.
/// </summary>
public static class ActivityLogFormatter
{
    public const string SystemAuthor = "System";
    public const string BylineDateFormat = "MMM d, yyyy h:mm:ss tt";

    /// <summary>Byline kind for a customer status note write (<c>Spec C-9</c>, issue #620).</summary>
    public const string StatusNoteKind = "Customer status note";

    /// <summary>Body recorded when the manager clears the note instead of replacing it.</summary>
    public const string StatusNoteClearedBody = "Note cleared";

    private const string Separator = " — ";

    // The date never contains an em dash, so excluding it there is what keeps an
    // optional kind from being swallowed into the timestamp.
    private static readonly Regex EntryPattern = new(
        @"^\[(?<author>.+?)\s+—\s+(?<date>[^—\]]+?)(?:\s+—\s+(?<kind>[^\]]+?))?\]\r?\n(?<body>.*)$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex BlankLines = new(@"(\r?\n[ \t]*){2,}", RegexOptions.CultureInvariant);

    public static string BuildBylineHeader(string author, string? kind = null)
    {
        var byline = $"[{author}{Separator}{DateTime.Now.ToString(BylineDateFormat)}";
        return string.IsNullOrWhiteSpace(kind) ? $"{byline}]" : $"{byline}{Separator}{kind}]";
    }

    public static string BuildSystemEntry(string description) =>
        $"{BuildBylineHeader(SystemAuthor)}\n{description}";

    /// <summary>
    /// Builds the activity entry recording that <paramref name="author"/> set or cleared the
    /// customer status note. The note has no history of its own — it is overwritten in place —
    /// so this entry is the only record that the write happened.
    /// </summary>
    public static string BuildStatusNoteEntry(string author, string? note)
    {
        var body = string.IsNullOrWhiteSpace(note)
            ? StatusNoteClearedBody
            : BlankLines.Replace(note.Trim(), "\n");

        return $"{BuildBylineHeader(author, StatusNoteKind)}\n{body}";
    }

    public static string PrependEntry(string? existingSummary, string newEntry) =>
        string.IsNullOrWhiteSpace(existingSummary)
            ? newEntry
            : $"{newEntry}\n\n{existingSummary}";

    public static string PrependSystemEntry(string? existingSummary, string description) =>
        PrependEntry(existingSummary, BuildSystemEntry(description));

    public static string PrependStatusNoteEntry(string? existingSummary, string author, string? note) =>
        PrependEntry(existingSummary, BuildStatusNoteEntry(author, note));

    /// <summary>
    /// Parses one log block. Returns <c>false</c> for anything without a byline — notably the
    /// legacy intake-generated summary, which is rendered from its own fields instead.
    /// </summary>
    public static bool TryParseEntry(string? block, [NotNullWhen(true)] out ActivityLogEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(block)) return false;

        var match = EntryPattern.Match(block);
        if (!match.Success) return false;

        var kind = match.Groups["kind"];
        entry = new ActivityLogEntry(
            Author: match.Groups["author"].Value.Trim(),
            Timestamp: match.Groups["date"].Value.Trim(),
            Kind: kind.Success ? kind.Value.Trim() : null,
            Body: match.Groups["body"].Value.Trim());

        return true;
    }
}

/// <summary>One parsed activity log block. <paramref name="Timestamp"/> is the raw byline text.</summary>
public sealed record ActivityLogEntry(string Author, string Timestamp, string? Kind, string Body);
