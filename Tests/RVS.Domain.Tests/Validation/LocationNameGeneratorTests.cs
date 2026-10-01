using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class LocationNameGeneratorTests
{
    // ── ForBusiness (issue #623) ─────────────────────────────────────────────

    [Theory]
    [InlineData("Blue Compass", "Tucson", "Blue Compass - Tucson")]
    [InlineData("  Blue Compass ", "  Tucson  ", "Blue Compass - Tucson")]
    [InlineData("Nova RV Services", "St. George", "Nova RV Services - St. George")]
    public void ForBusiness_ShouldPrependBusinessName(string businessName, string locationName, string expected)
    {
        LocationNameGenerator.ForBusiness(businessName, locationName).Should().Be(expected);
    }

    [Theory]
    [InlineData("Blue Compass - Tucson")]
    [InlineData("blue compass - Tucson")]
    [InlineData("Blue Compass Tucson")]
    [InlineData("Blue Compass: Tucson")]
    [InlineData("Blue Compass — Tucson")]
    [InlineData("Blue Compass -Tucson")]
    public void ForBusiness_WhenLocationAlreadyStartsWithBusinessName_ShouldNotDuplicateIt(string locationName)
    {
        LocationNameGenerator.ForBusiness("Blue Compass", locationName).Should().Be("Blue Compass - Tucson");
    }

    [Fact]
    public void ForBusiness_WhenBusinessNameIsOnlyAWordPrefix_ShouldStillPrepend()
    {
        LocationNameGenerator.ForBusiness("Nova", "Novato").Should().Be("Nova - Novato");
    }

    [Theory]
    [InlineData("Blue Compass")]
    [InlineData(" blue compass ")]
    [InlineData("Blue Compass -")]
    public void ForBusiness_WhenLocationIsJustTheBusinessName_ShouldReturnBusinessName(string locationName)
    {
        LocationNameGenerator.ForBusiness("Blue Compass", locationName).Should().Be("Blue Compass");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ForBusiness_WhenBusinessNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? businessName)
    {
        var act = () => LocationNameGenerator.ForBusiness(businessName!, "Tucson");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ForBusiness_WhenLocationNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? locationName)
    {
        var act = () => LocationNameGenerator.ForBusiness("Blue Compass", locationName!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForBusiness_ShouldSlugWithoutRepeatingTheDealershipSlug()
    {
        var name = LocationNameGenerator.ForBusiness("Blue Compass", "Tucson");

        SlugGenerator.ForLocation(SlugGenerator.Slugify("Blue Compass"), name).Should().Be("blue-compass-tucson");
    }
}
