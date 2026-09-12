using FluentAssertions;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class ServiceRequestConfirmationContentTests
{
    private const string DealershipName = "Blue Compass RV";
    private const string StatusUrl = "https://rvintake.com/status/abc123token";
    private const string DealerPhone = "(801) 555-1234";

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
}
