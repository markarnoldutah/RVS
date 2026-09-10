using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class IssueCategoryVocabularyTests
{
    [Fact]
    public void All_ShouldContainBetweenTenAndFourteenCodes()
    {
        // Spec A-5: "roughly 10-14 codes".
        IssueCategoryVocabulary.All.Should().HaveCountGreaterThanOrEqualTo(10);
        IssueCategoryVocabulary.All.Should().HaveCountLessThanOrEqualTo(14);
    }

    [Fact]
    public void All_ShouldHaveUniqueCodesNamesAndAscendingSortOrder()
    {
        IssueCategoryVocabulary.All.Select(e => e.Code)
            .Should().OnlyHaveUniqueItems();
        IssueCategoryVocabulary.All.Select(e => e.Name)
            .Should().OnlyHaveUniqueItems();
        IssueCategoryVocabulary.All.Select(e => e.SortOrder)
            .Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        IssueCategoryVocabulary.All.Should().AllSatisfy(e =>
        {
            e.Code.Should().NotBeNullOrWhiteSpace();
            e.Name.Should().NotBeNullOrWhiteSpace();
            e.Description.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void Codes_ShouldMirrorAllInOrder()
    {
        IssueCategoryVocabulary.Codes.Should().Equal(IssueCategoryVocabulary.All.Select(e => e.Code));
    }

    [Fact]
    public void All_ShouldContainTheFallbackCode()
    {
        IssueCategoryVocabulary.Codes.Should().Contain(IssueCategoryVocabulary.FallbackCode);
    }

    [Theory]
    [InlineData("Slides")]
    [InlineData("Generator")]
    [InlineData("LPGas")]
    [InlineData("Other")]
    public void IsValid_KnownCode_ReturnsTrue(string code)
    {
        IssueCategoryVocabulary.IsValid(code).Should().BeTrue();
    }

    [Theory]
    [InlineData("slides")]
    [InlineData("  HVAC  ")]
    [InlineData("ELECTRICAL")]
    public void IsValid_IsCaseInsensitiveAndTrims(string code)
    {
        IssueCategoryVocabulary.IsValid(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Structural")]
    [InlineData("General")]
    [InlineData("Slide-Out")]
    [InlineData("Appliance")]
    [InlineData("DriveTrain")]
    public void IsValid_MissingOrRetiredCode_ReturnsFalse(string? code)
    {
        IssueCategoryVocabulary.IsValid(code).Should().BeFalse();
    }

    [Theory]
    [InlineData("slides", "Slides")]
    [InlineData("  hvac ", "HVAC")]
    [InlineData("Generator", "Generator")]
    public void Normalize_KnownCode_ReturnsCanonicalCasing(string input, string expected)
    {
        IssueCategoryVocabulary.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Structural")]
    [InlineData("General")]
    [InlineData("something the AI invented")]
    public void Normalize_MissingOrUnknownCode_ReturnsFallback(string? input)
    {
        IssueCategoryVocabulary.Normalize(input).Should().Be(IssueCategoryVocabulary.FallbackCode);
    }
}
