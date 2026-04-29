using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class IssueCategoryCapabilityMapTests
{
    [Theory]
    [InlineData("Electrical",  new[] { "electrical" })]
    [InlineData("Plumbing",    new[] { "plumbing" })]
    [InlineData("HVAC",        new[] { "hvac" })]
    [InlineData("Appliance",   new[] { "rv-refrigerator" })]
    [InlineData("Appliances",  new[] { "rv-refrigerator" })]
    [InlineData("Slides",      new[] { "slide-out-repair" })]
    [InlineData("Roof",        new[] { "roof-repair" })]
    [InlineData("DieselMotor", new[] { "diesel-service" })]
    public void GetRequiredCapabilities_KnownCategory_ReturnsExpectedCodes(string category, string[] expected)
    {
        var result = IssueCategoryCapabilityMap.GetRequiredCapabilities(category);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetRequiredCapabilities_StructuralCategory_ReturnsAllStructuralCapabilities()
    {
        var result = IssueCategoryCapabilityMap.GetRequiredCapabilities("Structural");

        result.Should().BeEquivalentTo(["body-repair", "roof-repair", "slide-out-repair"]);
    }

    [Fact]
    public void GetRequiredCapabilities_ExteriorCategory_ReturnsBodyAndTireCapabilities()
    {
        var result = IssueCategoryCapabilityMap.GetRequiredCapabilities("Exterior");

        result.Should().BeEquivalentTo(["body-repair", "tire-service"]);
    }

    [Theory]
    [InlineData("electrical")]   // lowercase
    [InlineData("ELECTRICAL")]   // uppercase
    [InlineData("  Electrical  ")] // padded
    public void GetRequiredCapabilities_IsCaseInsensitiveAndTrimsInput(string category)
    {
        var result = IssueCategoryCapabilityMap.GetRequiredCapabilities(category);

        result.Should().BeEquivalentTo(["electrical"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("General")]
    [InlineData("UnknownCategory")]
    [InlineData("GasMotor")]
    [InlineData("DriveTrain")]
    public void GetRequiredCapabilities_NullEmptyOrUnknownCategory_ReturnsEmpty(string? category)
    {
        var result = IssueCategoryCapabilityMap.GetRequiredCapabilities(category);

        result.Should().BeEmpty();
    }
}
