using FluentAssertions;
using RVS.Blazor.Intake.Pages;

namespace RVS.UI.Shared.Tests.Pages;

public class CustomerStatusFormattingTests
{
    // ---- DescribeStatus -------------------------------------------------------

    [Theory]
    [InlineData("New")]
    [InlineData(" new ")]
    public void DescribeStatus_New_ReturnsAwaitingReviewCopy(string status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("Your request has been received and is awaiting review by a service advisor.");
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("In Progress")]
    [InlineData("in-progress")]
    public void DescribeStatus_InProgress_ReturnsTechnicianWorkingCopy(string status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("A technician is actively working on your service request.");
    }

    [Theory]
    [InlineData("WaitingOnParts")]
    [InlineData("Waiting on Parts")]
    public void DescribeStatus_WaitingOnParts_ReturnsPartsOrderedCopy(string status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("Your repair requires parts that have been ordered. We'll update you when they arrive.");
    }

    [Theory]
    [InlineData("WaitingOnCustomer")]
    [InlineData("Waiting on Customer")]
    public void DescribeStatus_WaitingOnCustomer_ReturnsWaitingOnYouCopy(string status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("We're waiting on information or approval from you before work can continue. Please contact the dealership.");
    }

    [Fact]
    public void DescribeStatus_Completed_ReturnsCompletedCopy()
    {
        CustomerStatusFormatting.DescribeStatus("Completed")
            .Should().Be("Your service has been completed. Please contact the dealership for pickup details.");
    }

    [Fact]
    public void DescribeStatus_Cancelled_ReturnsCancelledCopy()
    {
        CustomerStatusFormatting.DescribeStatus("Cancelled")
            .Should().Be("This service request has been cancelled.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SomethingElse")]
    public void DescribeStatus_UnknownOrEmpty_ReturnsGenericCopy(string? status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("Your request is being processed.");
    }

    // ---- FormatPhoneForDisplay ---------------------------------------------------

    [Theory]
    [InlineData("8015550100")]
    [InlineData("(801) 555-0100")]
    [InlineData("801-555-0100")]
    [InlineData("  (801) 555-0100  ")]
    public void FormatPhoneForDisplay_TenDigits_ReturnsCanonicalNanpFormat(string phone)
    {
        CustomerStatusFormatting.FormatPhoneForDisplay(phone).Should().Be("(801) 555-0100");
    }

    [Theory]
    [InlineData("18015550100")]
    [InlineData("1-801-555-0100")]
    [InlineData("+1 (801) 555-0100")]
    public void FormatPhoneForDisplay_ElevenDigitsLeadingOne_ReturnsCanonicalWithCountryCode(string phone)
    {
        CustomerStatusFormatting.FormatPhoneForDisplay(phone).Should().Be("+1 (801) 555-0100");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatPhoneForDisplay_NullOrBlank_ReturnsNull(string? phone)
    {
        CustomerStatusFormatting.FormatPhoneForDisplay(phone).Should().BeNull();
    }

    [Theory]
    [InlineData("555-0100", "555-0100")]
    [InlineData("+44 20 7946 0000", "+44 20 7946 0000")]
    [InlineData("call the front desk", "call the front desk")]
    public void FormatPhoneForDisplay_Unrecognized_ReturnsTrimmedOriginal(string phone, string expected)
    {
        CustomerStatusFormatting.FormatPhoneForDisplay(phone).Should().Be(expected);
    }

    // ---- ToTelHref ----------------------------------------------------------------

    [Theory]
    [InlineData("(801) 555-0100")]
    [InlineData("8015550100")]
    [InlineData("1-801-555-0100")]
    [InlineData("+1 (801) 555-0100")]
    public void ToTelHref_NanpNumber_ReturnsE164TelUri(string phone)
    {
        CustomerStatusFormatting.ToTelHref(phone).Should().Be("tel:+18015550100");
    }

    [Fact]
    public void ToTelHref_InternationalWithPlus_PreservesCountryCode()
    {
        CustomerStatusFormatting.ToTelHref("+44 20 7946 0000").Should().Be("tel:+442079460000");
    }

    [Fact]
    public void ToTelHref_ShortLocalNumber_ReturnsDigitsWithoutCountryCode()
    {
        CustomerStatusFormatting.ToTelHref("555-0100").Should().Be("tel:5550100");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no digits here")]
    public void ToTelHref_NoDigits_ReturnsNull(string? phone)
    {
        CustomerStatusFormatting.ToTelHref(phone).Should().BeNull();
    }
}
