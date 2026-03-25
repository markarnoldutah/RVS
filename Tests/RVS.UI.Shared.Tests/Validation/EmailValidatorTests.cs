using FluentAssertions;
using RVS.UI.Shared.Validation;

namespace RVS.UI.Shared.Tests.Validation;

public class EmailValidatorTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last@domain.co.uk")]
    [InlineData("test+tag@mail.org")]
    [InlineData("a@b.c")]
    public void Validate_ValidEmail_ReturnsSuccess(string email)
    {
        var result = EmailValidator.Validate(email);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_NullInput_ReturnsFailure()
    {
        var result = EmailValidator.Validate(null!);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null or empty");
    }

    [Fact]
    public void Validate_EmptyString_ReturnsFailure()
    {
        var result = EmailValidator.Validate("");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null or empty");
    }

    [Fact]
    public void Validate_WhitespaceOnly_ReturnsFailure()
    {
        var result = EmailValidator.Validate("   ");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null or empty");
    }

    [Fact]
    public void Validate_MissingAtSign_ReturnsFailure()
    {
        var result = EmailValidator.Validate("userexample.com");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("'@'");
    }

    [Fact]
    public void Validate_MultipleAtSigns_ReturnsFailure()
    {
        var result = EmailValidator.Validate("user@@example.com");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exactly one");
    }

    [Fact]
    public void Validate_EmptyLocalPart_ReturnsFailure()
    {
        var result = EmailValidator.Validate("@example.com");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("local part");
    }

    [Fact]
    public void Validate_EmptyDomainPart_ReturnsFailure()
    {
        var result = EmailValidator.Validate("user@");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("domain part");
    }

    [Fact]
    public void Validate_DomainWithoutDot_ReturnsFailure()
    {
        var result = EmailValidator.Validate("user@localhost");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("'.'");
    }

    [Fact]
    public void Validate_DomainStartsWithDot_ReturnsFailure()
    {
        var result = EmailValidator.Validate("user@.example.com");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("start or end");
    }

    [Fact]
    public void Validate_DomainEndsWithDot_ReturnsFailure()
    {
        var result = EmailValidator.Validate("user@example.com.");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("start or end");
    }

    [Fact]
    public void Validate_ExceedsMaxLength_ReturnsFailure()
    {
        var email = new string('a', 251) + "@b.c";
        var result = EmailValidator.Validate(email);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("254");
    }

    [Theory]
    [InlineData("user<tag>@example.com")]
    [InlineData("user;drop@example.com")]
    [InlineData("user'quote@example.com")]
    [InlineData("user\"double@example.com")]
    [InlineData("user\\slash@example.com")]
    public void Validate_DangerousCharacters_ReturnsFailure(string email)
    {
        var result = EmailValidator.Validate(email);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("blocked");
    }

    [Fact]
    public void Validate_LeadingTrailingWhitespace_IsTrimmed()
    {
        var result = EmailValidator.Validate("  user@example.com  ");

        result.IsValid.Should().BeTrue();
    }
}
