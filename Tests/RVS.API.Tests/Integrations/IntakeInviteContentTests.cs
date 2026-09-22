using FluentAssertions;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Tests for <see cref="IntakeInviteContent"/> — the text of an advisor intake invite
/// (<c>Spec A-14</c>, issue #663). Like the confirmation, it sheds detail under length pressure
/// and never shortens the link.
/// </summary>
public class IntakeInviteContentTests
{
    private const string Link = "https://go.rvintake.com/nova-hurricane?src=advisor&inv=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    /// <summary>
    /// The compliance tail, spelled out rather than read from the production constant: this
    /// wording is submitted verbatim in the toll-free verification application (#659), so the
    /// test is what pins it. Changing it here means changing it on the application too.
    /// </summary>
    private const string Compliance = "Msg & data rates may apply. Reply STOP to opt out, HELP for help.";

    [Fact]
    public void BuildSmsBody_ShouldGreetByNameNameTheLocationAndCarryTheLinkAndCompliance()
    {
        var body = IntakeInviteContent.BuildSmsBody("Nova RV", "Jane", Link);

        body.Should().StartWith("Nova RV: Hi Jane,");
        body.Should().Contain(Link);
        body.Should().EndWith(Compliance);
        body.Length.Should().BeLessThanOrEqualTo(IntakeInviteContent.SmsMaxLength);
    }

    [Fact]
    public void BuildSmsBody_ShouldOfferBothStopAndHelp()
    {
        var body = IntakeInviteContent.BuildSmsBody("Nova RV", "Jane", Link);

        body.Should().Contain("Reply STOP to opt out").And.Contain("HELP for help");
    }

    [Fact]
    public void BuildSmsBody_WhenTheGreetingDoesNotFit_ShouldDropTheNameFirst()
    {
        var location = new string('N', 90);

        var body = IntakeInviteContent.BuildSmsBody(location, "Maximiliana Josephine", Link);

        body.Should().NotContain("Maximiliana");
        body.Should().StartWith($"{location}:");
        body.Should().Contain(Link).And.EndWith(Compliance);
        body.Length.Should().BeLessThanOrEqualTo(IntakeInviteContent.SmsMaxLength);
    }

    [Fact]
    public void BuildSmsBody_WhenTheLocationNameDoesNotFit_ShouldKeepTheLinkAndCompliance()
    {
        var body = IntakeInviteContent.BuildSmsBody(new string('N', 150), "Jane", Link);

        body.Should().Be($"{Link} {Compliance}");
    }

    [Fact]
    public void BuildSmsBody_WhenEvenTheStopLineDoesNotFit_ShouldSendTheWholeLinkAlone()
    {
        var longLink = "https://go.rvintake.com/" + new string('a', 290);

        IntakeInviteContent.BuildSmsBody("Nova RV", "Jane", longLink).Should().Be(longLink);
    }

    [Theory]
    [InlineData(null, "Jane", Link)]
    [InlineData("Nova RV", null, Link)]
    [InlineData("Nova RV", "Jane", null)]
    public void BuildSmsBody_WhenAnArgumentIsBlank_ShouldThrowArgumentException(string? location, string? firstName, string? link)
    {
        var act = () => IntakeInviteContent.BuildSmsBody(location!, firstName!, link!);

        act.Should().Throw<ArgumentException>();
    }

    // ── Email (issue #693) ───────────────────────────────────────────────────

    [Fact]
    public void BuildEmailSubject_ShouldNameTheLocation()
    {
        IntakeInviteContent.BuildEmailSubject("Nova RV").Should().Contain("Nova RV");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldGreetByNameNameTheLocationAndLinkTheInvite()
    {
        var html = IntakeInviteContent.BuildEmailHtmlBody("Nova RV", "Jane", Link, expiryHours: 72);

        html.Should().Contain("Hi Jane,");
        html.Should().Contain("Nova RV");
        html.Should().Contain($"href=\"{Link.Replace("&", "&amp;")}\"");
        html.Should().Contain("72 hours");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldEncodeWhatTheAdvisorTyped()
    {
        // The first name and location name are free text; the email is HTML.
        var html = IntakeInviteContent.BuildEmailHtmlBody("Smith & Sons RV", "Jo\"e", Link, expiryHours: 72);

        html.Should().Contain("Smith &amp; Sons RV");
        html.Should().NotContain("Jo\"e");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldCarryNoTextingComplianceTail()
    {
        // STOP/HELP are carrier keywords; they mean nothing in an email.
        IntakeInviteContent.BuildEmailHtmlBody("Nova RV", "Jane", Link, expiryHours: 72).Should().NotContain("STOP");
    }

    [Fact]
    public void BuildEmailPlainTextBody_ShouldCarryTheRawLinkAndTheGreeting()
    {
        var text = IntakeInviteContent.BuildEmailPlainTextBody("Nova RV", "Jane", Link, expiryHours: 72);

        text.Should().Contain("Hi Jane,");
        text.Should().Contain(Link);
        text.Should().NotContain("<");
    }

    [Theory]
    [InlineData(null, "Jane", Link)]
    [InlineData("Nova RV", null, Link)]
    [InlineData("Nova RV", "Jane", null)]
    public void BuildEmailHtmlBody_WhenAnArgumentIsBlank_ShouldThrowArgumentException(string? location, string? firstName, string? link)
    {
        var act = () => IntakeInviteContent.BuildEmailHtmlBody(location!, firstName!, link!, expiryHours: 72);

        act.Should().Throw<ArgumentException>();
    }
}
