using FluentAssertions;
using RVS.UI.Shared.Components;

namespace RVS.UI.Shared.Tests.Components;

public class StatusBadgeFormattingTests
{
    [Theory]
    [InlineData("New")]
    [InlineData(" new ")]
    public void GetCssClass_New_ReturnsNewClass(string status)
    {
        StatusBadgeFormatting.GetCssClass(status).Should().Be("rvs-status-new");
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("In Progress")]
    [InlineData("in-progress")]
    public void GetCssClass_InProgress_ReturnsInProgressClass(string status)
    {
        StatusBadgeFormatting.GetCssClass(status).Should().Be("rvs-status-in-progress");
    }

    [Theory]
    [InlineData("WaitingOnParts")]
    [InlineData("Waiting on Parts")]
    [InlineData("waiting_on_parts")]
    public void GetCssClass_WaitingOnParts_ReturnsWaitingOnPartsClass(string status)
    {
        StatusBadgeFormatting.GetCssClass(status).Should().Be("rvs-status-waiting-on-parts");
    }

    [Theory]
    [InlineData("WaitingOnCustomer")]
    [InlineData("Waiting on Customer")]
    public void GetCssClass_WaitingOnCustomer_ReturnsWaitingOnCustomerClass(string status)
    {
        StatusBadgeFormatting.GetCssClass(status).Should().Be("rvs-status-waiting-on-customer");
    }

    [Fact]
    public void GetCssClass_Completed_ReturnsCompletedClass()
    {
        StatusBadgeFormatting.GetCssClass("Completed").Should().Be("rvs-status-completed");
    }

    [Fact]
    public void GetCssClass_Cancelled_ReturnsCancelledClass()
    {
        StatusBadgeFormatting.GetCssClass("Cancelled").Should().Be("rvs-status-cancelled");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SomethingElse")]
    [InlineData("Received")]
    [InlineData("Ready")]
    [InlineData("Closed")]
    public void GetCssClass_UnknownOrEmpty_ReturnsDefaultClass(string? status)
    {
        StatusBadgeFormatting.GetCssClass(status).Should().Be("rvs-status-default");
    }
}
