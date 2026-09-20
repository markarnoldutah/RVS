using FluentAssertions;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class ServiceRequestConfirmationContentTests
{
    private const string DealershipName = "Blue Compass RV";
    private const string StatusUrl = "https://rvintake.com/status/abc123token";
    private const string DealerPhone = "(801) 555-1234";

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
    public void BuildEmailHtmlBody_ShouldThankCustomerByDealershipName()
    {
        var body = ServiceRequestConfirmationContent.BuildEmailHtmlBody(DealershipName, StatusUrl, DealerPhone);

        body.Should().Contain("Thank you").And.Contain(DealershipName);
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldContainStatusPageLink()
    {
        var body = ServiceRequestConfirmationContent.BuildEmailHtmlBody(DealershipName, StatusUrl, DealerPhone);

        body.Should().Contain($"href=\"{StatusUrl}\"").And.Contain(StatusUrl);
    }

    [Fact]
    public void BuildEmailHtmlBody_ShouldContainDealerPhone()
    {
        var body = ServiceRequestConfirmationContent.BuildEmailHtmlBody(DealershipName, StatusUrl, DealerPhone);

        body.Should().Contain(DealerPhone);
    }

    [Fact]
    public void BuildEmailHtmlBody_WhenDealerPhoneIsMissing_ShouldOmitPhoneButKeepRestOfContent()
    {
        var body = ServiceRequestConfirmationContent.BuildEmailHtmlBody(DealershipName, StatusUrl, null);

        body.Should().Contain(DealershipName).And.Contain(StatusUrl);
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
}
