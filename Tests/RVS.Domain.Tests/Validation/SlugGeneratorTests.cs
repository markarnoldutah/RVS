using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class SlugGeneratorTests
{
    // ── Slugify ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Salt Lake Service Center", "salt-lake-service-center")]
    [InlineData("  Phoenix Service Center  ", "phoenix-service-center")]
    [InlineData("RV/Truck & Boat", "rv-truck-boat")]
    [InlineData("Café Joe's #1", "cafe-joe-s-1")]
    [InlineData("Multiple   Spaces", "multiple-spaces")]
    [InlineData("---leading---trailing---", "leading-trailing")]
    public void Slugify_ShouldReturnUrlSafeSlug(string input, string expected)
    {
        SlugGenerator.Slugify(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("***")]
    public void Slugify_WhenInputProducesNoAlphanumerics_ShouldReturnEmpty(string? input)
    {
        SlugGenerator.Slugify(input).Should().BeEmpty();
    }

    [Fact]
    public void Slugify_ShouldRespectMaxLength()
    {
        var longName = new string('a', 200);

        var slug = SlugGenerator.Slugify(longName, maxLength: 32);

        slug.Length.Should().Be(32);
        slug.Should().MatchRegex("^[a-z0-9-]+$");
    }

    // ── ForLocation ──────────────────────────────────────────────────────────

    [Fact]
    public void ForLocation_ShouldCombineOrgAndLocation()
    {
        SlugGenerator.ForLocation("camping-world", "Salt Lake Service Center")
            .Should().Be("camping-world-salt-lake-service-center");
    }

    [Fact]
    public void ForLocation_WhenOrgEmpty_ShouldReturnLocationOnly()
    {
        SlugGenerator.ForLocation(null, "Phoenix").Should().Be("phoenix");
        SlugGenerator.ForLocation("", "Phoenix").Should().Be("phoenix");
    }

    [Fact]
    public void ForLocation_WhenLocationEmpty_ShouldReturnOrgOnly()
    {
        SlugGenerator.ForLocation("camping-world", null).Should().Be("camping-world");
    }

    [Fact]
    public void ForLocation_WhenLocationAlreadyStartsWithOrg_ShouldNotDuplicate()
    {
        SlugGenerator.ForLocation("camping-world", "Camping World Salt Lake")
            .Should().Be("camping-world-salt-lake");
    }

    [Fact]
    public void ForLocation_ShouldProduceValidSlug()
    {
        var slug = SlugGenerator.ForLocation("RV / World", "Provo, UT — Service");

        SlugValidator.Validate(slug).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ForLocation_ShouldRespectMaxLength()
    {
        var slug = SlugGenerator.ForLocation(
            new string('a', 80),
            new string('b', 80),
            maxLength: 64);

        slug.Length.Should().BeLessThanOrEqualTo(64);
        slug.Should().MatchRegex("^[a-z0-9-]+$");
    }
}
