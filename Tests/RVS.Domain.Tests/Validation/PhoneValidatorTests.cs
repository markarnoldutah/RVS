using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class PhoneValidatorTests
{
    [Theory]
    [InlineData("8015551234")]
    [InlineData("(801) 555-1234")]
    [InlineData("+1 801 555 1234")]
    [InlineData("801-555-1234 ext 12")]
    [InlineData("+44 20 7946 0958")]
    public void Validate_WhenPhoneHasAtLeastTenDigits_ShouldSucceed(string phone)
    {
        // Deliberately looser than PhoneNumberNormalizer: a number that won't normalise is still
        // a number the shop can call; it is only dropped from SMS.
        var result = PhoneValidator.Validate(phone);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenPhoneIsMissing_ShouldFailAsRequired(string? phone)
    {
        var result = PhoneValidator.Validate(phone);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Phone number is required");
    }

    [Theory]
    [InlineData("555-1234")]
    [InlineData("call me")]
    public void Validate_WhenPhoneHasFewerThanTenDigits_ShouldFail(string phone)
    {
        var result = PhoneValidator.Validate(phone);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Phone number must have at least 10 digits");
    }

    [Fact]
    public void Validate_WhenPhoneIsLongerThanTheMaximum_ShouldFail()
    {
        var result = PhoneValidator.Validate(new string('1', PhoneValidator.MaxLength + 1));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be($"Phone number must not exceed {PhoneValidator.MaxLength} characters");
    }

    [Fact]
    public void Validate_WhenPhoneIsExactlyTheMaximumLength_ShouldSucceed()
    {
        PhoneValidator.Validate(new string('1', PhoneValidator.MaxLength)).IsValid.Should().BeTrue();
    }
}
