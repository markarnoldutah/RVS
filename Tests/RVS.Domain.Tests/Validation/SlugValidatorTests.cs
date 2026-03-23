using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class SlugValidatorTests
{
    [Theory]
    [InlineData("camping-world-salt-lake")]
    [InlineData("abc")]
    [InlineData("a-b-c")]
    [InlineData("dealer123")]
    [InlineData("my-rv-dealer")]
    [InlineData("a")]
    public void Validate_ValidSlug_ReturnsSuccess(string slug)
    {
        var result = SlugValidator.Validate(slug);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("Has-Uppercase")]
    [InlineData("ALLCAPS")]
    [InlineData("has spaces")]
    [InlineData("has_underscore")]
    [InlineData("has.period")]
    [InlineData("has@symbol")]
    [InlineData("has/slash")]
    [InlineData("has<angle")]
    [InlineData("café")]
    public void Validate_InvalidCharacters_ReturnsFailure(string slug)
    {
        var result = SlugValidator.Validate(slug);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("lowercase");
    }

    [Fact]
    public void Validate_EmptyString_ReturnsFailure()
    {
        var result = SlugValidator.Validate("");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullInput_ReturnsFailure()
    {
        var result = SlugValidator.Validate(null!);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ExceedsMaxLength_ReturnsFailure()
    {
        var longSlug = new string('a', 65);

        var result = SlugValidator.Validate(longSlug, maxLength: 64);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("64");
    }

    [Fact]
    public void Validate_ExactlyMaxLength_ReturnsSuccess()
    {
        var slug = new string('a', 64);

        var result = SlugValidator.Validate(slug, maxLength: 64);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DefaultMaxLength_Is64()
    {
        var exact = new string('a', 64);
        var over = new string('a', 65);

        SlugValidator.Validate(exact).IsValid.Should().BeTrue();
        SlugValidator.Validate(over).IsValid.Should().BeFalse();
    }
}
