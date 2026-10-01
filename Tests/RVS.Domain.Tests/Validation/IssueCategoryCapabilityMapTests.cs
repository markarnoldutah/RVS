using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class IssueCategoryCapabilityMapTests
{
    [Theory]
    [InlineData("Slides",     new[] { "slide-out-repair" })]
    [InlineData("Electrical", new[] { "electrical" })]
    [InlineData("Plumbing",   new[] { "plumbing" })]
    [InlineData("HVAC",       new[] { "hvac" })]
    [InlineData("Generator",  new[] { "generator" })]
    [InlineData("Appliances", new[] { "rv-refrigerator" })]
    [InlineData("Roof",       new[] { "roof-repair" })]
    [InlineData("Chassis",    new[] { "tire-service" })]
    [InlineData("Exterior",   new[] { "body-repair" })]
    public void GetRequiredCapabilities_KnownCategory_ReturnsExpectedCodes(string category, string[] expected)
    {
        var result = IssueCategoryCapabilityMap.GetRequiredCapabilities(category);

        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("electrical")]     // lowercase
    [InlineData("ELECTRICAL")]     // uppercase
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
    [InlineData("LPGas")]        // in the vocabulary, but no specific capability requirement
    [InlineData("Awning")]
    [InlineData("Interior")]
    [InlineData("Other")]
    [InlineData("Structural")]   // retired alias
    [InlineData("General")]      // retired fallback
    [InlineData("UnknownCategory")]
    public void GetRequiredCapabilities_UnmappedOrUnknownCategory_ReturnsEmpty(string? category)
    {
        var result = IssueCategoryCapabilityMap.GetRequiredCapabilities(category);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetRequiredCapabilities_EveryMappedKey_IsAValidVocabularyCode()
    {
        foreach (var code in IssueCategoryVocabulary.Codes)
        {
            // Should not throw and should return a (possibly empty) list for every real code.
            IssueCategoryCapabilityMap.GetRequiredCapabilities(code).Should().NotBeNull();
        }
    }
}
