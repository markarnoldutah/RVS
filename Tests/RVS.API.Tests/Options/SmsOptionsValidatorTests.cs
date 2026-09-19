using FluentAssertions;
using RVS.API.Options;

namespace RVS.API.Tests.Options;

public class SmsOptionsValidatorTests
{
    private readonly SmsOptionsValidator _sut = new();

    [Fact]
    public void Validate_WhenOptionsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.Validate(null, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_WhenDisabledAndNoFromNumber_ShouldSucceed()
    {
        var result = _sut.Validate(null, new SmsOptions { Enabled = false, FromPhoneNumber = "" });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenEnabledWithE164FromNumber_ShouldSucceed()
    {
        var result = _sut.Validate(null, new SmsOptions { Enabled = true, FromPhoneNumber = "+18662319618" });

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenEnabledWithoutFromNumber_ShouldFail(string? fromNumber)
    {
        var result = _sut.Validate(null, new SmsOptions { Enabled = true, FromPhoneNumber = fromNumber! });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("AzureCommunicationServices:Sms:FromPhoneNumber");
    }

    [Theory]
    [InlineData("866-231-9618")]      // normalisable, but not already E.164
    [InlineData("+448015551234")]
    [InlineData("not-a-number")]
    public void Validate_WhenEnabledWithNonE164FromNumber_ShouldFail(string fromNumber)
    {
        var result = _sut.Validate(null, new SmsOptions { Enabled = true, FromPhoneNumber = fromNumber });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("E.164");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenTenantHourlyLimitIsNotPositive_ShouldFail(int limit)
    {
        var result = _sut.Validate(null, new SmsOptions { MaxMessagesPerTenantPerHour = limit });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxMessagesPerTenantPerHour");
    }
}
