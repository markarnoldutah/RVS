using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class SlugGeneratorTests
{
    [Fact]
    public void Generate_WithDealershipAndLocationName_ShouldProduceKebabCaseSlug()
    {
        var slug = SlugGenerator.Generate("Camping World", "Salt Lake City");

        slug.Should().Be("camping-world-salt-lake-city");
    }

    [Fact]
    public void Generate_WithSingleSegment_ShouldProduceLowercaseHyphenated()
    {
        var slug = SlugGenerator.Generate("Blue Compass RV");

        slug.Should().Be("blue-compass-rv");
    }

    [Fact]
    public void Generate_ShouldStripSpecialCharacters()
    {
        var slug = SlugGenerator.Generate("O'Brien's", "Müller Location #5!");

        slug.Should().Be("obriens-muller-location-5");
    }

    [Fact]
    public void Generate_ShouldCollapseConsecutiveHyphens()
    {
        var slug = SlugGenerator.Generate("Camping World", "  Salt -- Lake  ");

        slug.Should().Be("camping-world-salt-lake");
    }

    [Fact]
    public void Generate_ShouldTrimLeadingAndTrailingHyphens()
    {
        var slug = SlugGenerator.Generate("-Leading-", "-Trailing-");

        slug.Should().Be("leading-trailing");
    }

    [Fact]
    public void Generate_WhenAllSegmentsAreNullOrEmpty_ShouldReturnEmpty()
    {
        var slug = SlugGenerator.Generate(null, "", "  ");

        slug.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WhenOneSegmentIsNull_ShouldSkipIt()
    {
        var slug = SlugGenerator.Generate(null, "Provo Service");

        slug.Should().Be("provo-service");
    }

    [Fact]
    public void Generate_ShouldHandleDiacritics()
    {
        var slug = SlugGenerator.Generate("Café", "Résumé");

        slug.Should().Be("cafe-resume");
    }

    [Fact]
    public void Generate_ShouldHandleNumericSegments()
    {
        var slug = SlugGenerator.Generate("Store 42", "Location 7");

        slug.Should().Be("store-42-location-7");
    }
}
