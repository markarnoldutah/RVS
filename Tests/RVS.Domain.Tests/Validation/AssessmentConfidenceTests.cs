using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="AssessmentConfidence"/> — the controlled confidence vocabulary for the
/// structured preliminary assessment (issue #507).
/// </summary>
public class AssessmentConfidenceTests
{
    [Theory]
    [InlineData("high", "high")]
    [InlineData("HIGH", "high")]
    [InlineData("  Medium ", "medium")]
    [InlineData("low", "low")]
    [InlineData("Abstain", "abstain")]
    public void Normalize_WhenValueIsInTheVocabulary_ShouldReturnTheCanonicalCode(string value, string expected)
    {
        AssessmentConfidence.Normalize(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("certain")]
    [InlineData("0.9")]
    public void Normalize_WhenValueIsBlankOrUnknown_ShouldAbstain(string? value)
    {
        AssessmentConfidence.Normalize(value).Should().Be(AssessmentConfidence.Abstain);
    }

    [Theory]
    [InlineData("high", "High")]
    [InlineData("medium", "Medium")]
    [InlineData("low", "Low")]
    public void DisplayName_ForAnOfferedConfidence_ShouldBeTitleCased(string code, string expected)
    {
        AssessmentConfidence.DisplayName(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("abstain")]
    [InlineData("nonsense")]
    [InlineData(null)]
    public void DisplayName_WhenAbstainingOrUnknown_ShouldBeNull(string? code)
    {
        AssessmentConfidence.DisplayName(code).Should().BeNull();
    }
}
