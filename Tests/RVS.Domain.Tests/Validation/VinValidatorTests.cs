using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class VinValidatorTests
{
    [Theory]
    [InlineData("11111111111111111")]
    [InlineData("1HGBH41JXMN109186")]
    [InlineData("5YJSA1DG9DFP14705")]
    public void Validate_ValidVin_ReturnsSuccess(string vin)
    {
        var result = VinValidator.Validate(vin);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_TooShort_ReturnsFailure()
    {
        var result = VinValidator.Validate("1234567890123456");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("17");
    }

    [Fact]
    public void Validate_TooLong_ReturnsFailure()
    {
        var result = VinValidator.Validate("123456789012345678");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("17");
    }

    [Theory]
    [InlineData("1234567890I234567")]
    [InlineData("1234567890O234567")]
    [InlineData("1234567890Q234567")]
    public void Validate_ContainsIOQ_ReturnsFailure(string vin)
    {
        var result = VinValidator.Validate(vin);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("must not contain");
    }

    [Theory]
    [InlineData("12345678901234 67")]
    [InlineData("1234567890123-567")]
    [InlineData("1234567890!234567")]
    public void Validate_NonAlphanumeric_ReturnsFailure(string vin)
    {
        var result = VinValidator.Validate(vin);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullInput_ReturnsFailure()
    {
        var result = VinValidator.Validate(null!);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyInput_ReturnsFailure()
    {
        var result = VinValidator.Validate("");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_InvalidCheckDigit_ReturnsFailure()
    {
        // "1HGBH41J0MN109186" has check digit '0' instead of correct 'X'
        var result = VinValidator.Validate("1HGBH41J0MN109186");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("check digit");
    }

    [Fact]
    public void Validate_LowercaseInput_IsNormalized()
    {
        // Lowercase version of valid VIN "1HGBH41JXMN109186"
        var result = VinValidator.Validate("1hgbh41jxmn109186");

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("1HGBH41JXMN109186")]
    [InlineData("5YJSA1DG9DFP14705")]
    public void Validate_CheckDigitCalculation_MatchesExpected(string vin)
    {
        VinValidator.Validate(vin).IsValid.Should().BeTrue();
    }
}
