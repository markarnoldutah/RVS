using System.Globalization;
using System.Text;

namespace RVS.Domain.Packets;

/// <summary>
/// Builds the DMS paste block (<c>Spec B-5</c>, issue <c>#436</c>) — a delimited, plain-text
/// rendering of a request the service advisor selects whole and pastes into a DMS complaint
/// field. This is the DMS integration: manual, honest about being manual, and it removes the
/// retyping the advisor actually cares about.
///
/// It is a pure transform — strings in, one string out, no I/O — and the packet generation
/// orchestrator hands the result to <see cref="PacketCompositionContext.PasteBlock"/>.
///
/// Guarantees, each covered by <c>PasteBlockGeneratorTests</c>:
/// <list type="bullet">
///   <item><b>ASCII-safe.</b> Smart quotes, em/en dashes, ellipses, exotic spaces, and
///   accented Latin letters are folded to their ASCII equivalents; anything that cannot be
///   folded (emoji, CJK, control characters) is dropped. A DMS text field never sees a code
///   point above <c>0x7E</c>.</item>
///   <item><b>Capped.</b> The block is truncated to <paramref name="characterCap"/>
///   (default <see cref="DefaultCharacterCap"/>) by shortening the customer's description at
///   a word boundary and marking the cut with <c>...</c>. The fences, category, and status
///   line always survive so the block stays selectable and the link stays usable.</item>
///   <item><b>Delimited.</b> The same <see cref="Delimiter"/> fences the block top and
///   bottom so one click-drag selects it cleanly.</item>
/// </list>
///
/// Order is <c>Spec B-5</c>: category, then the customer's verbatim description, then the
/// status link.
/// </summary>
public static class PasteBlockGenerator
{
    /// <summary>Default character cap for the block (<c>Spec B-5</c> / <c>B-6</c>).</summary>
    public const int DefaultCharacterCap = 1000;

    /// <summary>The fence line placed above and below the block for clean selection.</summary>
    public const string Delimiter = "----- RV SERVICE FLOW -----";

    /// <summary>Placeholder used when no category was classified.</summary>
    private const string UncategorizedLabel = "UNCATEGORIZED";

    /// <summary>Marker appended to a description that was cut to fit the cap.</summary>
    private const string TruncationMarker = "...";

    private static readonly char[] WordBreakChars = [' ', '\n', '\t'];

    /// <summary>
    /// Builds the paste block for one request.
    /// </summary>
    /// <param name="issueCategory">Classified category, or <c>null</c>/blank when unclassified.</param>
    /// <param name="issueDescription">The customer's description in their own words.</param>
    /// <param name="statusLinkUrl">Fully-formed customer status URL; the status line is omitted when this is <c>null</c>/blank (it is minted upstream by <c>#427</c>).</param>
    /// <param name="characterCap">Maximum length of the whole block; values &#8804; 0 fall back to <see cref="DefaultCharacterCap"/>.</param>
    public static string Generate(
        string? issueCategory,
        string? issueDescription,
        string? statusLinkUrl = null,
        int characterCap = DefaultCharacterCap)
    {
        var cap = characterCap > 0 ? characterCap : DefaultCharacterCap;

        var category = AsciiFold(issueCategory).Trim();
        category = category.Length == 0 ? UncategorizedLabel : category.ToUpperInvariant();

        var description = AsciiFold(issueDescription).Trim();
        var status = AsciiFold(statusLinkUrl).Trim();

        var head = $"{Delimiter}\nCATEGORY: {category}\nCOMPLAINT:";
        var tail = status.Length == 0 ? $"\n{Delimiter}" : $"\nSTATUS: {status}\n{Delimiter}";

        // Budget for the description, leaving one character for the space after "COMPLAINT:".
        var budget = cap - head.Length - tail.Length - 1;

        string body;
        if (description.Length == 0)
        {
            body = string.Empty;
        }
        else if (budget >= description.Length)
        {
            body = " " + description;
        }
        else
        {
            var trimmed = TruncateAtWordBoundary(description, Math.Max(0, budget));
            body = trimmed.Length == 0 ? string.Empty : " " + trimmed;
        }

        return head + body + tail;
    }

    /// <summary>
    /// Shortens <paramref name="text"/> so the result — including a trailing
    /// <see cref="TruncationMarker"/> when a cut is made — is at most <paramref name="max"/>
    /// characters, cutting back to the last word boundary rather than mid-word.
    /// </summary>
    private static string TruncateAtWordBoundary(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var room = max - TruncationMarker.Length;
        if (room <= 0)
        {
            return string.Empty;
        }

        var slice = text[..room];
        var lastBreak = slice.LastIndexOfAny(WordBreakChars);
        if (lastBreak > 0)
        {
            slice = slice[..lastBreak];
        }

        slice = slice.TrimEnd();
        return slice.Length == 0 ? string.Empty : slice + TruncationMarker;
    }

    /// <summary>
    /// Folds a string to ASCII: line endings normalise to <c>\n</c>; common typographic
    /// characters map to their ASCII equivalents; accented Latin letters lose their marks;
    /// everything else outside the printable ASCII range (emoji, CJK, control characters) is
    /// dropped.
    /// </summary>
    private static string AsciiFold(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalizedLineEndings = value.Replace("\r\n", "\n").Replace('\r', '\n');

        var sb = new StringBuilder(normalizedLineEndings.Length);
        foreach (var rune in normalizedLineEndings.EnumerateRunes())
        {
            switch (rune.Value)
            {
                // Single quotes / primes / backtick-accents.
                case 0x2018 or 0x2019 or 0x201A or 0x201B or 0x2032 or 0x0060 or 0x00B4:
                    sb.Append('\'');
                    break;
                // Double quotes / prime / guillemets.
                case 0x201C or 0x201D or 0x201E or 0x201F or 0x2033 or 0x00AB or 0x00BB:
                    sb.Append('"');
                    break;
                // Hyphens / dashes / minus.
                case 0x2010 or 0x2011 or 0x2012 or 0x2013 or 0x2014 or 0x2015 or 0x2212:
                    sb.Append('-');
                    break;
                // Ellipsis.
                case 0x2026:
                    sb.Append("...");
                    break;
                // Bullets / middle dot.
                case 0x2022 or 0x00B7 or 0x2027 or 0x25CF or 0x25E6:
                    sb.Append('-');
                    break;
                // Non-breaking and other Unicode spaces.
                case 0x00A0 or 0x1680 or 0x2000 or 0x2001 or 0x2002 or 0x2003 or 0x2004
                    or 0x2005 or 0x2006 or 0x2007 or 0x2008 or 0x2009 or 0x200A or 0x202F
                    or 0x205F or 0x3000:
                    sb.Append(' ');
                    break;
                // Zero-width and BOM: drop.
                case 0x200B or 0x200C or 0x200D or 0xFEFF:
                    break;
                case '\n':
                    sb.Append('\n');
                    break;
                case '\t':
                    sb.Append('\t');
                    break;
                default:
                    if (rune.IsAscii)
                    {
                        // Printable ASCII passes through; other control characters are dropped.
                        if (rune.Value >= 0x20 && rune.Value <= 0x7E)
                        {
                            sb.Append((char)rune.Value);
                        }
                    }
                    else
                    {
                        AppendFolded(sb, rune);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Decomposes a non-ASCII rune and keeps only the printable ASCII that falls out
    /// (e.g. <c>é</c> → <c>e</c>). A rune that decomposes to nothing usable (emoji, CJK) is
    /// dropped entirely.
    /// </summary>
    private static void AppendFolded(StringBuilder sb, Rune rune)
    {
        var decomposed = rune.ToString().Normalize(NormalizationForm.FormKD);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch >= 0x20 && ch <= 0x7E)
            {
                sb.Append(ch);
            }
        }
    }
}
