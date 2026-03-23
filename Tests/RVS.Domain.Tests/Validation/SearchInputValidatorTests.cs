using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class SearchInputValidatorTests
{
    [Theory]
    [InlineData("normal search term")]
    [InlineData("RV repair")]
    [InlineData("customer-name")]
    [InlineData("12345")]
    [InlineData("a")]
    public void Validate_CleanInput_ReturnsSuccess(string input)
    {
        var result = SearchInputValidator.Validate(input);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("<script>")]
    [InlineData("test>value")]
    [InlineData("drop;table")]
    [InlineData("it's")]
    [InlineData("say\"hello")]
    [InlineData("path\\file")]
    [InlineData("null\0char")]
    public void Validate_DangerousCharacter_ReturnsFailureWithDescriptiveError(string input)
    {
        var result = SearchInputValidator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Contain("blocked character");
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("'; DROP TABLE users;--")]
    public void Validate_MultipleBlockedChars_ReturnsFailure(string input)
    {
        var result = SearchInputValidator.Validate(input);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ExceedsMaxLength_ReturnsFailure()
    {
        var longInput = new string('a', 501);

        var result = SearchInputValidator.Validate(longInput, maxLength: 500);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("500");
    }

    [Fact]
    public void Validate_ExactlyMaxLength_ReturnsSuccess()
    {
        var input = new string('a', 500);

        var result = SearchInputValidator.Validate(input, maxLength: 500);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullInput_ReturnsFailure()
    {
        var result = SearchInputValidator.Validate(null!);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_EmptyInput_ReturnsSuccess()
    {
        var result = SearchInputValidator.Validate("");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhitespaceOnly_ReturnsSuccess()
    {
        var result = SearchInputValidator.Validate("   ");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DefaultMaxLength_Is500()
    {
        var exactlyDefault = new string('a', 500);
        var overDefault = new string('a', 501);

        SearchInputValidator.Validate(exactlyDefault).IsValid.Should().BeTrue();
        SearchInputValidator.Validate(overDefault).IsValid.Should().BeFalse();
    }
}
