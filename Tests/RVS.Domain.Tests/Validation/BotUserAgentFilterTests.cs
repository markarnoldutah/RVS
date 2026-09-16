using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="BotUserAgentFilter"/> — the coarse machine-fetch filter applied to
/// <c>go.rvintake.com</c> redirect hits (<c>Spec A-13</c>, issue #599). Messaging clients fetch
/// the link to build a preview the moment it is composed, so raw hits include fetches nobody made.
/// </summary>
public class BotUserAgentFilterTests
{
    [Theory]
    [InlineData("facebookexternalhit/1.1")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Safari/605.1.15 facebookexternalhit/1.1 Facebot Twitterbot/1.0")]
    [InlineData("WhatsApp/2.23.20.0")]
    [InlineData("TelegramBot (like TwitterBot)")]
    [InlineData("Slackbot-LinkExpanding 1.0 (+https://api.slack.com/robots)")]
    [InlineData("Discordbot/2.0")]
    [InlineData("SkypeUriPreview Preview/0.5")]
    [InlineData("Googlebot/2.1 (+http://www.google.com/bot.html)")]
    [InlineData("curl/8.4.0")]
    [InlineData("python-requests/2.31.0")]
    [InlineData("Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)")]
    public void IsLikelyBot_WhenMachineFetcher_ShouldReturnTrue(string userAgent)
    {
        BotUserAgentFilter.IsLikelyBot(userAgent).Should().BeTrue();
    }

    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Mobile Safari/537.36")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")]
    public void IsLikelyBot_WhenRealBrowser_ShouldReturnFalse(string userAgent)
    {
        BotUserAgentFilter.IsLikelyBot(userAgent).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsLikelyBot_WhenAbsent_ShouldReturnTrue(string? userAgent)
    {
        // A request with no User-Agent is never a customer tapping a link in a messaging app.
        BotUserAgentFilter.IsLikelyBot(userAgent).Should().BeTrue();
    }

    [Fact]
    public void IsLikelyBot_ShouldMatchCaseInsensitively()
    {
        BotUserAgentFilter.IsLikelyBot("FACEBOOKEXTERNALHIT/1.1").Should().BeTrue();
    }
}
