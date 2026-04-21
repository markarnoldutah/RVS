using System.Text;
using System.Text.RegularExpressions;

namespace RVS.UI.Shared.Validation;

/// <summary>
/// Converts a speech-to-text transcript of a dictated VIN into clean alphanumeric characters.
/// Handles spoken numbers ("four" → "4"), IRSA / NATO phonetic alphabet ("foxtrot" → "F"),
/// common homophones ("for" → "4", "eye" → "I"), and falls back to the first letter for any
/// alphabetic word not found in the IRSA map ("banana" → "B"). Digit-bearing tokens are kept
/// as-is (treated as VIN chunks). The result is uppercased and truncated to 17 characters.
/// </summary>
public static partial class VinTranscriptCleaner
{
    private const int MaxVinLength = 17;

    /// <summary>
    /// Word-to-character mapping for spoken numbers, NATO phonetic alphabet, and common homophones.
    /// Keys are lowercase. Values are the single VIN character each word represents.
    /// </summary>
    private static readonly Dictionary<string, string> WordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Spoken digits
        ["zero"] = "0",
        ["one"] = "1",
        ["two"] = "2",
        ["three"] = "3",
        ["four"] = "4",
        ["five"] = "5",
        ["six"] = "6",
        ["seven"] = "7",
        ["eight"] = "8",
        ["nine"] = "9",

        // Common homophones that speech-to-text may produce
        ["for"] = "4",
        ["to"] = "2",
        ["too"] = "2",
        ["won"] = "1",
        ["ate"] = "8",
        ["eye"] = "I",
        ["oh"] = "0",

        // NATO phonetic alphabet (excluding I, O, Q which are invalid in VINs —
        // but we map them anyway; VIN validation handles rejection separately)
        ["alpha"] = "A",
        ["bravo"] = "B",
        ["charlie"] = "C",
        ["delta"] = "D",
        ["echo"] = "E",
        ["foxtrot"] = "F",
        ["golf"] = "G",
        ["hotel"] = "H",
        ["india"] = "I",
        ["juliet"] = "J",
        ["kilo"] = "K",
        ["lima"] = "L",
        ["mike"] = "M",
        ["november"] = "N",
        ["oscar"] = "O",
        ["papa"] = "P",
        ["quebec"] = "Q",
        ["romeo"] = "R",
        ["sierra"] = "S",
        ["tango"] = "T",
        ["uniform"] = "U",
        ["victor"] = "V",
        ["whiskey"] = "W",
        ["x-ray"] = "X",
        ["xray"] = "X",
        ["yankee"] = "Y",
        ["zulu"] = "Z",

        // "double U" handled as a special two-word sequence (see below)
    };

    /// <summary>
    /// Cleans a speech-to-text transcript into a VIN-compatible alphanumeric string.
    /// </summary>
    /// <param name="transcript">Raw transcript from the speech-to-text engine.</param>
    /// <returns>
    /// Uppercase alphanumeric string of up to 17 characters, or an empty string
    /// if the transcript is null, empty, or contains no recognizable VIN characters.
    /// </returns>
    public static string Clean(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return string.Empty;
        }

        var tokens = TokenizeRegex().Split(transcript.Trim());
        var sb = new StringBuilder(MaxVinLength);

        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            // Handle "double <letter>" → repeat the letter (e.g. "double U" → "W" is not relevant,
            // but "double" followed by a single character means repeat it)
            if (token.Equals("double", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
            {
                var nextToken = tokens[i + 1];

                // "double U" → "W"
                if (nextToken.Equals("u", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append('W');
                    i++;
                    continue;
                }

                // Generic "double <char>" → append the character twice
                var resolved = ResolveToken(nextToken);
                sb.Append(resolved);
                sb.Append(resolved);
                i++;
                continue;
            }

            sb.Append(ResolveToken(token));
        }

        // Strip any remaining non-alphanumeric characters
        var cleaned = NonAlphanumericRegex().Replace(sb.ToString(), "");

        // Uppercase and truncate to VIN length
        var upper = cleaned.ToUpperInvariant();
        return upper.Length > MaxVinLength ? upper[..MaxVinLength] : upper;
    }

    /// <summary>
    /// Resolves a single token to its VIN character(s).
    /// Order: word map (spoken digits, homophones, IRSA) → single alphanumeric → digit-bearing VIN chunk → first-letter fallback.
    /// The first-letter fallback implements the rule: any multi-character word not in the IRSA alphabet
    /// collapses to its first letter (e.g. "banana" → "B").
    /// </summary>
    private static string ResolveToken(string token)
    {
        if (WordMap.TryGetValue(token, out var mapped))
        {
            return mapped;
        }

        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            return token;
        }

        var stripped = NonAlphanumericRegex().Replace(token, "");
        if (stripped.Length == 0)
        {
            return string.Empty;
        }

        // Tokens containing any digit are treated as VIN chunks or numerics (e.g. "1HGBH", "17") — keep all.
        // Pure alphabetic tokens that weren't matched above are unknown IRSA words — collapse to first letter.
        return stripped.Any(char.IsDigit) ? stripped : stripped[..1];
    }

    [GeneratedRegex(@"[\s,;]+")]
    private static partial Regex TokenizeRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumericRegex();
}
