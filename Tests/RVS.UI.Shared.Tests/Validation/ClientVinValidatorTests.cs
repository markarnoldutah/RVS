using FluentAssertions;
using RVS.UI.Shared.Validation;

namespace RVS.UI.Shared.Tests.Validation;

public class ClientVinValidatorTests
{
    [Theory]
    [InlineData("11111111111111111")]
    [InlineData("1HGBH41JXMN109186")]
    [InlineData("5YJSA1DG9DFP14705")]
    public void Validate_ValidVin_ReturnsSuccess(string vin)
    {
        var result = ClientVinValidator.Validate(vin);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_NullInput_ReturnsFailure()
    {
        var result = ClientVinValidator.Validate(null!);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_EmptyInput_ReturnsFailure()
    {
        var result = ClientVinValidator.Validate("");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_TooShort_ReturnsFailure()
    {
        var result = ClientVinValidator.Validate("1234567890123456");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("17");
    }

    [Fact]
    public void Validate_InvalidCheckDigit_ReturnsFailure()
    {
        var result = ClientVinValidator.Validate("1HGBH41J0MN109186");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not appear to be valid");
    }

    [Fact]
    public void Validate_LowercaseInput_IsNormalized()
    {
        var result = ClientVinValidator.Validate("1hgbh41jxmn109186");

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("1234567890I234567")]
    [InlineData("1234567890O234567")]
    [InlineData("1234567890Q234567")]
    public void Validate_ContainsIOQ_ReturnsFailure(string vin)
    {
        var result = ClientVinValidator.Validate(vin);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("must not contain");
    }

    [Theory]
    [InlineData(" 1HGBH41JXMN109186")]
    [InlineData("1HGBH41JXMN109186 ")]
    [InlineData("  1HGBH41JXMN109186  ")]
    public void Validate_WhitespaceAroundValidVin_ReturnsSuccess(string vin)
    {
        var result = ClientVinValidator.Validate(vin);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_WhitespaceOnlyInput_ReturnsFailure()
    {
        var result = ClientVinValidator.Validate("   ");

        result.IsValid.Should().BeFalse();
    }

    // ── ValidateFormat tests (format-only, no check digit) ──────────────

    [Theory]
    [InlineData("5SFCU2324GE004561")]
    [InlineData("5XWTF2147HF019873")]
    [InlineData("1HGBH41JXMN109186")]
    public void ValidateFormat_ValidFormatVin_ReturnsSuccess(string vin)
    {
        var result = ClientVinValidator.ValidateFormat(vin);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateFormat_InvalidCheckDigit_StillReturnsSuccess()
    {
        var result = ClientVinValidator.ValidateFormat("1HGBH41J0MN109186");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateFormat_TooShort_ReturnsFailure()
    {
        var result = ClientVinValidator.ValidateFormat("1234567890123456");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("17");
    }

    [Theory]
    [InlineData("1234567890I234567")]
    [InlineData("1234567890O234567")]
    public void ValidateFormat_ContainsIOQ_ReturnsFailure(string vin)
    {
        var result = ClientVinValidator.ValidateFormat(vin);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("must not contain");
    }
}
