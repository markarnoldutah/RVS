namespace RVS.Domain.Validation;

/// <summary>
/// Coarse machine-fetch filter for <c>go.rvintake.com</c> redirect hits (<c>Spec A-13</c>,
/// issue #599).
///
/// iMessage and most messaging clients fetch a URL to build a link preview the moment it is
/// composed — before anybody taps anything, and possibly once per send. Raw redirect hits
/// therefore over-count opens, by a factor that varies with the client. Hits are still stored
/// append-only; this only flags the obvious machines so reporting can leave them out, and
/// **submissions by source** stays the metric anyone is shown.
///
/// Matched by plain substring, and tuned to accept false negatives over false positives: a
/// missed fetcher only pads a raw count nobody reports on, while a wrongly-flagged browser
/// would quietly drop a real customer out of the conversion denominator. The markers are
/// therefore strings no shipping browser puts in its User-Agent, not clever heuristics.
/// </summary>
public static class BotUserAgentFilter
{
    private static readonly string[] BotMarkers =
    [
        // Link-preview fetchers
        "facebookexternalhit",
        "facebot",
        "twitterbot",
        "whatsapp",
        "telegrambot",
        "slackbot",
        "discordbot",
        "skypeuripreview",
        "linkedinbot",
        "pinterest",
        "redditbot",
        "embedly",
        "quora link preview",
        "vkshare",
        "applebot",
        "snapchat",
        "viber",
        "line-podcast",
        "google-read-aloud",
        "preview",

        // Search crawlers and generic automation
        "bot",
        "crawler",
        "spider",
        "curl/",
        "wget",
        "python-requests",
        "httpclient",
        "okhttp",
        "go-http-client",
        "headlesschrome",
        "phantomjs",
        "monitor",
        "uptime",
        "pingdom"
    ];

    /// <summary>
    /// Whether <paramref name="userAgent"/> looks like a machine fetch rather than a person
    /// tapping a link. A missing or blank User-Agent counts as a machine — no mobile browser
    /// omits it.
    /// </summary>
    public static bool IsLikelyBot(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return true;
        }

        var ua = userAgent.ToLowerInvariant();

        return BotMarkers.Any(marker => ua.Contains(marker, StringComparison.Ordinal));
    }
}
