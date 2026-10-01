using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="DealershipTimeZones"/> — the curated IANA vocabulary that supplies the
/// packet's Received-line abbreviation and the manager app's time-zone picker (issue #506).
/// </summary>
public class DealershipTimeZonesTests
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
    public void All_ShouldCoverTheUsAndCanadaZonesTheProductServes()
    {
        DealershipTimeZones.All.Select(z => z.IanaId).Should().Equal(
            "America/New_York",
            "America/Detroit",
            "America/Indiana/Indianapolis",
            "America/Chicago",
            "America/Denver",
            "America/Boise",
            "America/Phoenix",
            "America/Los_Angeles",
            "America/Anchorage",
            "Pacific/Honolulu",
            "America/Toronto");
    }

    [Fact]
    public void All_ShouldHaveUniqueIanaIds()
    {
        DealershipTimeZones.All.Select(z => z.IanaId)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_ShouldCarryANonBlankDisplayNameAndStandardAbbreviation()
    {
        DealershipTimeZones.All.Should().OnlyContain(
            z => !string.IsNullOrWhiteSpace(z.DisplayName)
                && !string.IsNullOrWhiteSpace(z.StandardAbbreviation));
    }

    [Theory]
    [InlineData("America/Phoenix")]
    [InlineData("Pacific/Honolulu")]
    public void All_ShouldCarryNoDaylightAbbreviation_ForZonesWithoutDst(string ianaId)
    {
        DealershipTimeZones.TryGet(ianaId, out var zone).Should().BeTrue();

        zone.DaylightAbbreviation.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(CuratedIds))]
    public void All_EveryCuratedId_ShouldResolveOnThisHost(string ianaId)
    {
        // Deliberately host-dependent: this turns "the runner has no tzdata" into one named
        // failure rather than a puzzling (UTC+00:00) mismatch across the formatter tests.
        TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out _)
            .Should().BeTrue($"'{ianaId}' must resolve for the Received line to render");
    }

    [Fact]
    public void TryGetAbbreviation_ForDenverInDaylightSaving_ShouldReturnMdt()
    {
        DealershipTimeZones.TryGetAbbreviation("America/Denver", isDaylightSaving: true, out var abbreviation)
            .Should().BeTrue();

        abbreviation.Should().Be("MDT");
    }

    [Fact]
    public void TryGetAbbreviation_ForDenverInStandardTime_ShouldReturnMst()
    {
        DealershipTimeZones.TryGetAbbreviation("America/Denver", isDaylightSaving: false, out var abbreviation)
            .Should().BeTrue();

        abbreviation.Should().Be("MST");
    }

    [Fact]
    public void TryGetAbbreviation_ForPhoenixInSummer_ShouldStillReturnMst()
    {
        // Phoenix does not observe DST; the standard spelling is the only one it ever shows.
        DealershipTimeZones.TryGetAbbreviation("America/Phoenix", isDaylightSaving: true, out var abbreviation)
            .Should().BeTrue();

        abbreviation.Should().Be("MST");
    }

    [Theory]
    [InlineData("america/denver")]
    [InlineData("AMERICA/DENVER")]
    [InlineData("America/Denver")]
    public void IsCurated_ShouldMatchCaseInsensitively(string ianaId)
    {
        DealershipTimeZones.IsCurated(ianaId).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Europe/Berlin")]
    [InlineData("Mars/Olympus_Mons")]
    public void IsCurated_WhenNotInTheTable_ShouldReturnFalse(string? ianaId)
    {
        DealershipTimeZones.IsCurated(ianaId).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Europe/Berlin")]
    public void TryGet_WhenNotInTheTable_ShouldReturnFalse(string? ianaId)
    {
        DealershipTimeZones.TryGet(ianaId, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Europe/Berlin")]
    public void TryGetAbbreviation_WhenNotInTheTable_ShouldReturnFalse(string? ianaId)
    {
        DealershipTimeZones.TryGetAbbreviation(ianaId, isDaylightSaving: false, out _)
            .Should().BeFalse();
    }
}
