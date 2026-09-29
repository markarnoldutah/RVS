using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class VehicleIdentifierValidatorTests
{
    // ── Normalize ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(" 152263 ", "152263")]
    [InlineData("152 263", "152263")]
    [InlineData("1hgbh41jxmn109186", "1HGBH41JXMN109186")]
    [InlineData("ab-1234", "AB-1234")]
    public void Normalize_ShouldTrimUppercaseAndDropWhitespace(string? input, string expected)
    {
        VehicleIdentifierValidator.Normalize(input).Should().Be(expected);
    }

    // ── Validate ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("152263")]          // Lance truck camper serial (issue #807)
    [InlineData("1HGBH41JXMN109186")] // 17-character VIN
    [InlineData("1HGBH41JXMN109187")] // VIN with a bad check digit — decode decides, not the gate
    [InlineData("AB-1234")]
    [InlineData("7")]
    [InlineData(" 152263 ")]
    public void Validate_WhenVinOrSerialNumber_ShouldSucceed(string value)
    {
        var result = VehicleIdentifierValidator.Validate(value);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenBlank_ShouldFail(string? value)
    {
        var result = VehicleIdentifierValidator.Validate(value);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_WhenLongerThanMaxLength_ShouldFail()
    {
        var result = VehicleIdentifierValidator.Validate(new string('1', VehicleIdentifierValidator.MaxLength + 1));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(VehicleIdentifierValidator.MaxLength.ToString());
    }

    [Theory]
    [InlineData("1234<5")]
    [InlineData("12;345")]
    [InlineData("12'345")]
    [InlineData("12/345")]
    [InlineData("12.345")]
    public void Validate_WhenContainsCharactersOtherThanLettersDigitsOrHyphen_ShouldFail(string value)
    {
        var result = VehicleIdentifierValidator.Validate(value);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("N-A")]
    public void Validate_WhenContainsNoDigit_ShouldFail(string value)
    {
        var result = VehicleIdentifierValidator.Validate(value);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("1HGBH41JXMN10918O")] // letter O where a zero belongs
    [InlineData("1HGBH41JXMN1091I6")]
    [InlineData("1HGBH41JXMN10-186")]
    public void Validate_WhenSeventeenCharactersButNotAWellFormedVin_ShouldFail(string value)
    {
        var result = VehicleIdentifierValidator.Validate(value);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("VIN");
    }

    // ── IsVin ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1HGBH41JXMN109186", true)]
    [InlineData("1hgbh41jxmn109186", true)]
    [InlineData("1HGBH41JXMN109187", true)] // well-formed; the check digit is the decoder's concern
    [InlineData("152263", false)]
    [InlineData("1HGBH41JXMN10918O", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsVin_ShouldBeTrueOnlyForAWellFormedSeventeenCharacterVin(string? value, bool expected)
    {
        VehicleIdentifierValidator.IsVin(value).Should().Be(expected);
    }
}
