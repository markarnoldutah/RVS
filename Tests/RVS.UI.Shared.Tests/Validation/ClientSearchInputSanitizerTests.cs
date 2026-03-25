using FluentAssertions;
using RVS.UI.Shared.Validation;

namespace RVS.UI.Shared.Tests.Validation;

public class ClientSearchInputSanitizerTests
{
    [Theory]
    [InlineData("normal search term")]
    [InlineData("RV repair")]
    [InlineData("customer-name")]
    [InlineData("12345")]
    public void Validate_CleanInput_ReturnsSuccess(string input)
    {
        var result = ClientSearchInputSanitizer.Validate(input);

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
    public void Validate_DangerousCharacter_ReturnsFailure(string input)
    {
        var result = ClientSearchInputSanitizer.Validate(input);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("blocked character");
    }

    [Fact]
    public void Validate_ExceedsMaxLength_ReturnsFailure()
    {
        var longInput = new string('a', 501);
        var result = ClientSearchInputSanitizer.Validate(longInput, maxLength: 500);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("500");
    }

    [Fact]
    public void Validate_ExactlyMaxLength_ReturnsSuccess()
    {
        var input = new string('a', 500);
        var result = ClientSearchInputSanitizer.Validate(input, maxLength: 500);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullInput_ReturnsFailure()
    {
        var result = ClientSearchInputSanitizer.Validate(null!);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyInput_ReturnsSuccess()
    {
        var result = ClientSearchInputSanitizer.Validate("");

        result.IsValid.Should().BeTrue();
    }
}
