namespace RVS.Domain.Validation;

/// <summary>What an inbound text means to RVS. Anything that is not an exact keyword is <see cref="None"/>.</summary>
public enum SmsKeyword
{
    /// <summary>Not a keyword. Ignored: not stored, not routed, not answered.</summary>
    None = 0,

    /// <summary>The customer asked to stop receiving texts.</summary>
    OptOut = 1,

    /// <summary>The customer asked to receive texts again.</summary>
    OptIn = 2,

    /// <summary>The customer asked for help. Gets one fixed reply and changes nothing.</summary>
    Help = 3,
}

/// <summary>
/// The carrier keywords the inbound handler acts on (issue #665, <c>Spec A-2</c>'s out-of-scope
/// note). On a toll-free number the carrier already enforces these and sends its own reply; RVS
/// mirrors the customer's latest keyword so its own records match what the carrier enforces, and
/// so a send is refused up front rather than discovered through a failed delivery report.
///
/// Only an exact keyword counts, which is what the carrier itself acts on. A sentence that merely
/// contains "stop" is a conversation, and RVS does not have those: outbound texting is one-way
/// (<c>Spec</c>, "Explicitly out of scope"). <c>HELP</c> is the one keyword RVS answers itself,
/// with a single fixed reply — carriers enforce the opt-out keywords on a toll-free number but
/// not this one, and every message RVS sends promises it (<c>RVS_Plan.md</c>, Sep 19 2026).
/// </summary>
public static class SmsKeywordVocabulary
{
    /// <summary>
    /// Longest input worth classifying. A keyword is one short word, so anything longer is prose;
    /// this keeps a multi-segment text from being scanned character by character.
    /// </summary>
    private const int MaxLength = 32;

    private static readonly HashSet<string> OptOutKeywords = new(StringComparer.Ordinal)
    {
        "STOP", "STOPALL", "UNSUBSCRIBE", "CANCEL", "END", "QUIT",
    };

    private static readonly HashSet<string> OptInKeywords = new(StringComparer.Ordinal)
    {
        "START", "UNSTOP",
    };

    private static readonly HashSet<string> HelpKeywords = new(StringComparer.Ordinal)
    {
        "HELP", "INFO",
    };

    /// <summary>
    /// Classifies an inbound message body.
    /// </summary>
    /// <param name="message">The text exactly as the customer sent it.</param>
    /// <returns>
    /// <see cref="SmsKeyword.OptOut"/> for STOP and its standard synonyms,
    /// <see cref="SmsKeyword.OptIn"/> for START and UNSTOP, <see cref="SmsKeyword.Help"/> for
    /// HELP and INFO, and <see cref="SmsKeyword.None"/> for everything else.
    /// </returns>
    /// <example>
    /// <code>
    /// SmsKeywordVocabulary.Classify("  stop. ");  // SmsKeyword.OptOut
    /// SmsKeywordVocabulary.Classify("thanks!");   // SmsKeyword.None
    /// </code>
    /// </example>
    public static SmsKeyword Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaxLength)
        {
            return SmsKeyword.None;
        }

        var candidate = Canonicalize(message);
        if (candidate.Length == 0)
        {
            return SmsKeyword.None;
        }

        if (OptOutKeywords.Contains(candidate))
        {
            return SmsKeyword.OptOut;
        }

        if (OptInKeywords.Contains(candidate))
        {
            return SmsKeyword.OptIn;
        }

        return HelpKeywords.Contains(candidate) ? SmsKeyword.Help : SmsKeyword.None;
    }

    /// <summary>
    /// Upper-cases the message and drops whitespace and punctuation, so that <c>"stop."</c>,
    /// <c>"STOP!"</c> and <c>"stop all"</c> all reach the keyword they plainly mean. A message
    /// carrying any other character is left with it, so prose still fails the match.
    /// </summary>
    private static string Canonicalize(string message)
    {
        Span<char> buffer = stackalloc char[MaxLength];
        var length = 0;

        foreach (var c in message)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
            {
                continue;
            }

            buffer[length++] = char.ToUpperInvariant(c);
        }

        return new string(buffer[..length]);
    }
}
