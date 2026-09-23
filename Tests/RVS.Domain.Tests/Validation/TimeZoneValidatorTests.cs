using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="TimeZoneValidator"/> — the guard on <c>Location.TimeZoneId</c>
/// (issue #506).
/// </summary>
public class TimeZoneValidatorTests
{
    public static TheoryData<string> CuratedIds()
    {
        var data = new TheoryData<string>();
        foreach (var zone in DealershipTimeZones.All)
        {
            data.Add(zone.IanaId);
        }

        return data;
    }

    [Fact]
    public void Validate_WhenNull_ShouldSucceed()
    {
        // An unset zone is legal: the packet falls back to the UTC Received line.
        var result = TimeZoneValidator.Validate(null);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenBlank_ShouldSucceed(string ianaId)
    {
        TimeZoneValidator.Validate(ianaId).IsValid.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(CuratedIds))]
    public void Validate_ForEveryCuratedZone_ShouldSucceed(string ianaId)
    {
        TimeZoneValidator.Validate(ianaId).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Europe/Berlin")]
    [InlineData("Asia/Kolkata")]
    [InlineData("Australia/Sydney")]
    public void Validate_ForAnUncuratedButResolvableId_ShouldSucceed(string ianaId)
    {
        // The curated table drives the abbreviation and the picker, not what is storable.
        TimeZoneValidator.Validate(ianaId).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("NotAZone")]
    public void Validate_ForAnUnknownId_ShouldFailWithAMessageNamingTheId(string ianaId)
    {
        var result = TimeZoneValidator.Validate(ianaId);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(ianaId);
    }

    [Fact]
    public void Validate_ForAWindowsStyleZoneName_ShouldFail()
    {
        // "Mountain Standard Time" is the Windows registry id, not an IANA one. It carries
        // spaces, so the alphabet rule rejects it before the resolution check is reached.
        TimeZoneValidator.Validate("Mountain Standard Time").IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("America/Denver\"; drop")]
    [InlineData("America/Denver<script>")]
    [InlineData("America/Denver';")]
    [InlineData("America\\Denver")]
    [InlineData("America/Denver\0")]
    public void Validate_ForAnIdCarryingDangerousCharacters_ShouldFail(string ianaId)
    {
        TimeZoneValidator.Validate(ianaId).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ForAnOverlongId_ShouldFail()
    {
        var tooLong = new string('a', TimeZoneValidator.MaxLength + 1);

        var result = TimeZoneValidator.Validate(tooLong);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(TimeZoneValidator.MaxLength.ToString());
    }

    [Fact]
    public void Validate_ShouldAcceptACuratedZoneEvenWhenTheHostCannotResolveIt()
    {
        // The curated short-circuit is what keeps the eleven picker zones saveable on a host
        // with no tzdata; without it a missing ICU would 400 every save.
        DealershipTimeZones.IsCurated("America/Denver").Should().BeTrue();
        TimeZoneValidator.Validate("America/Denver").IsValid.Should().BeTrue();
    }
}
