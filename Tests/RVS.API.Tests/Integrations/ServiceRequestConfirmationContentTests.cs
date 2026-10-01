using FluentAssertions;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class ServiceRequestConfirmationContentTests
{
    private const string DealershipName = "Blue Compass RV";
    private const string StatusUrl = "https://rvintake.com/status/abc123token";
    private const string DealerPhone = "(801) 555-1234";
    private const string FirstName = "Jane";

    /// <summary>
    /// The compliance tail, spelled out rather than read from the production constant: this
    /// wording is submitted verbatim in the toll-free verification application (#659), so the
    /// test is what pins it. Changing it here means changing it on the application too.
    /// </summary>
    private const string Compliance = "Msg & data rates may apply. Reply STOP to opt out, HELP for help.";

    [Fact]
    public void BuildEmailSubject_ShouldContainDealershipName()
    {
        var subject = ServiceRequestConfirmationContent.BuildEmailSubject(DealershipName);

        subject.Should().Contain(DealershipName);
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldGreetTheCustomerByFirstName()
    {
        var body = BuildEmailHtmlBody();

        body.Should().Contain($"<p>Hi {FirstName},</p>");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BuildEmailHtmlBody_WhenFirstNameIsMissing_ShouldGreetWithoutAName(string? firstName)
    {
        var body = BuildEmailHtmlBody(firstName: firstName);

        body.Should().StartWith("<p>Hi,</p>");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldThankCustomerByDealershipName()
    {
        var body = BuildEmailHtmlBody();

        body.Should().Contain(
            $"Thank you for submitting a service request for your RV to <strong>{DealershipName}</strong>. " +
            "Use the link below to check your request status at any time:");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldLinkTheStatusPageWithACallToAction()
    {
        var body = BuildEmailHtmlBody();

        body.Should().Contain($"<a href=\"{StatusUrl}\">Check request status</a>");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldSayItIsSentByRvIntakeOnTheDealersBehalf()
    {
        var body = BuildEmailHtmlBody();

        body.Should().Contain(
            $"You're receiving this email from RV Intake on behalf of {DealershipName} because you submitted a service request.");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldSayWhenTheLinkExpires()
    {
        var body = BuildEmailHtmlBody(expiresInDays: 90);

        body.Should().Contain("This link expires in 90 days.");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldNotClaimTheLinkIsSingleUse()
    {
        // The status link is the customer's standing status page and is reused across requests;
        // unlike the intake invite (#710) it does not stop working once opened.
        var body = BuildEmailHtmlBody();

        body.Should().NotContain("work once");
    }

    [Fact]
    public void BuildEmailHtmlBody_WhenOneDayRemains_ShouldUseTheSingular()
    {
        var body = BuildEmailHtmlBody(expiresInDays: 1);

        body.Should().Contain("This link expires in 1 day.");
    }

    [Fact]
    public void BuildEmailHtmlBody_WhenExpiryIsUnknown_ShouldOmitTheExpirySentence()
    {
        var body = BuildEmailHtmlBody(expiresInDays: null);

        body.Should().NotContain("expires");
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldSendQuestionsToTheDealerPhone()
    {
        var body = BuildEmailHtmlBody();

        body.Should().Contain($"If you have questions, please contact {DealershipName} directly at {DealerPhone}.");
    }

    [Fact]
    public void BuildEmailHtmlBody_WhenDealerPhoneIsMissing_ShouldStillSendQuestionsToTheDealer()
    {
        var body = BuildEmailHtmlBody(dealerPhone: null);

        body.Should().Contain($"If you have questions, please contact {DealershipName} directly.")
            .And.Contain(StatusUrl);
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldHtmlEncodeCustomerAndDealerText()
    {
        var body = ServiceRequestConfirmationContent.BuildEmailHtmlBody(
            "Tom & Jerry's RV", "<b>Jane</b>", StatusUrl, 90, DealerPhone);

        body.Should().Contain("Tom &amp; Jerry&#39;s RV")
            .And.Contain("&lt;b&gt;Jane&lt;/b&gt;")
            .And.NotContain("<b>Jane</b>");
    }

    [Fact]
    public void BuildSmsBody_ShouldContainStatusLink()
    {
        var sms = ServiceRequestConfirmationContent.BuildSmsBody(DealershipName, StatusUrl, DealerPhone);

        sms.Should().Contain(StatusUrl);
    }

    [Fact]
    public void BuildSmsBody_ShouldFitWithinSmsMaxLength()
    {
        var sms = ServiceRequestConfirmationContent.BuildSmsBody(DealershipName, StatusUrl, DealerPhone);

        sms.Length.Should().BeLessThanOrEqualTo(ServiceRequestConfirmationContent.SmsMaxLength);
    }

    [Fact]
    public void BuildSmsBody_WhenDealerPhoneMissing_ShouldStillContainStatusLinkAndFit()
    {
        var sms = ServiceRequestConfirmationContent.BuildSmsBody(DealershipName, StatusUrl, null);

        sms.Should().Contain(StatusUrl);
        sms.Length.Should().BeLessThanOrEqualTo(ServiceRequestConfirmationContent.SmsMaxLength);
    }

    [Fact]
    public void BuildSmsBody_WhenDealershipNameIsVeryLong_ShouldAlwaysKeepStatusLinkIntact()
    {
        var longDealer = new string('A', 300);

        var sms = ServiceRequestConfirmationContent.BuildSmsBody(longDealer, StatusUrl, DealerPhone);

        // Sensible truncation never mangles the critical status link itself.
        sms.Should().Contain(StatusUrl);
    }

    [Fact]
    public void BuildSmsBody_ShouldEndWithTheComplianceTail()
    {
        var sms = ServiceRequestConfirmationContent.BuildSmsBody(DealershipName, StatusUrl, DealerPhone);

        sms.Should().EndWith(Compliance);
    }

    [Fact]
    public void BuildSmsBody_ShouldOfferBothStopAndHelp()
    {
        var sms = ServiceRequestConfirmationContent.BuildSmsBody(DealershipName, StatusUrl, DealerPhone);

        sms.Should().Contain("Reply STOP to opt out").And.Contain("HELP for help");
    }

    [Fact]
    public void BuildSmsBody_ShouldKeepThePhoneAndTheComplianceTailTogether()
    {
        var sms = ServiceRequestConfirmationContent.BuildSmsBody(DealershipName, StatusUrl, DealerPhone);

        // The compliance tail costs a second segment; dropping the phone to save one would
        // defeat the point, since the tail is what the verification application promises.
        sms.Should().Contain(DealerPhone).And.EndWith(Compliance);
    }

    [Fact]
    public void BuildSmsBody_WhenDealerPhoneMissing_ShouldStillEndWithTheComplianceTail()
    {
        var sms = ServiceRequestConfirmationContent.BuildSmsBody(DealershipName, StatusUrl, null);

        sms.Should().Contain(StatusUrl).And.EndWith(Compliance);
        sms.Length.Should().BeLessThanOrEqualTo(ServiceRequestConfirmationContent.SmsMaxLength);
    }

    [Fact]
    public void SmsMaxLength_ShouldAllowTwoConcatenatedSegments()
    {
        // A confirmation is often the customer's first text from us (the web-form path), so it
        // carries the full compliance tail — which no longer fits one 153-char GSM-7 segment.
        ServiceRequestConfirmationContent.SmsMaxLength.Should().Be(306);
    }

    private static string BuildEmailHtmlBody(
        string? firstName = FirstName, int? expiresInDays = 90, string? dealerPhone = DealerPhone) =>
        ServiceRequestConfirmationContent.BuildEmailHtmlBody(DealershipName, firstName, StatusUrl, expiresInDays, dealerPhone);
}
