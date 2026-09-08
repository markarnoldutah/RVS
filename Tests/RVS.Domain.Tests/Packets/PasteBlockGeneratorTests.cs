using FluentAssertions;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PasteBlockGenerator"/> — the pure transform that builds the
/// delimited, ASCII-safe DMS paste block (<c>Spec B-5</c>, issue <c>#436</c>).
///
/// The block is a manual DMS hand-off: the advisor selects it whole and pastes it into a
/// complaint field, so it must be free of Unicode a DMS text field would mangle, capped at
/// a configurable length with truncation at a word boundary, and fenced top and bottom so a
/// single click-drag selects it cleanly. Order is category, then the customer's verbatim
/// description, then the status link.
/// </summary>
public class PasteBlockGeneratorTests
{
    private const string Description = "Generator quits after ten minutes. Smells hot.";
    private const string StatusUrl = "https://rvintake.com/status/abc123";

    private static bool IsAsciiSafe(string s) =>
        s.All(c => c == '\n' || (c >= 0x20 && c <= 0x7E));

    // ── Structure and order ────────────────────────────────────────────────

    [Fact]
    public void Generate_ShouldOrderCategoryThenDescriptionThenStatus()
    {
        var block = PasteBlockGenerator.Generate("Electrical", Description, StatusUrl);

        var categoryAt = block.IndexOf("Electrical".ToUpperInvariant(), StringComparison.Ordinal);
        var descriptionAt = block.IndexOf(Description, StringComparison.Ordinal);
        var statusAt = block.IndexOf(StatusUrl, StringComparison.Ordinal);

        categoryAt.Should().BeGreaterThan(-1);
        descriptionAt.Should().BeGreaterThan(categoryAt);
        statusAt.Should().BeGreaterThan(descriptionAt);
    }

    [Fact]
    public void Generate_ShouldFenceTheBlockTopAndBottomWithTheSameDelimiter()
    {
        var block = PasteBlockGenerator.Generate("Electrical", Description, StatusUrl);
        var lines = block.Split('\n');

        lines.Should().HaveCountGreaterThan(2);
        lines[0].Should().Be(PasteBlockGenerator.Delimiter);
        lines[^1].Should().Be(PasteBlockGenerator.Delimiter);
        lines.Count(l => l == PasteBlockGenerator.Delimiter).Should().Be(2);
    }

    [Fact]
    public void Generate_ShouldUpperCaseTheCategory()
    {
        var block = PasteBlockGenerator.Generate("Slide System", Description, StatusUrl);

        block.Should().Contain("SLIDE SYSTEM").And.NotContain("Slide System");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WhenCategoryIsMissing_ShouldFallBackToUncategorized(string? category)
    {
        var block = PasteBlockGenerator.Generate(category, Description, StatusUrl);

        block.Should().Contain("UNCATEGORIZED");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WhenStatusLinkIsMissing_ShouldOmitTheStatusLine(string? url)
    {
        var block = PasteBlockGenerator.Generate("Electrical", Description, url);

        block.Should().NotContain("STATUS:");
    }

    [Fact]
    public void Generate_WhenStatusLinkIsPresent_ShouldRenderItOnAStatusLine()
    {
        var block = PasteBlockGenerator.Generate("Electrical", Description, StatusUrl);

        block.Should().Contain($"STATUS: {StatusUrl}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WhenDescriptionIsMissing_ShouldStillEmitAFencedBlock(string? description)
    {
        var block = PasteBlockGenerator.Generate("Electrical", description, StatusUrl);

        var lines = block.Split('\n');
        lines[0].Should().Be(PasteBlockGenerator.Delimiter);
        lines[^1].Should().Be(PasteBlockGenerator.Delimiter);
        block.Should().Contain("ELECTRICAL");
    }

    [Fact]
    public void Generate_WhenDescriptionFitsUnderCap_ShouldCarryItVerbatim()
    {
        var block = PasteBlockGenerator.Generate("Electrical", Description, StatusUrl);

        block.Should().Contain(Description);
    }

    // ── ASCII safety (Unicode substitution) ───────────────────────────────

    [Fact]
    public void Generate_ShouldReplaceSmartQuotesWithStraightQuotes()
    {
        var input = "“the ‘slide’ won’t move”";

        var block = PasteBlockGenerator.Generate("Electrical", input, StatusUrl);

        block.Should().Contain("\"the 'slide' won't move\"");
        IsAsciiSafe(block).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldReplaceEmAndEnDashesWithHyphens()
    {
        var input = "slide out — grinds – then stops";

        var block = PasteBlockGenerator.Generate("Electrical", input, StatusUrl);

        block.Should().Contain("slide out - grinds - then stops");
        IsAsciiSafe(block).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldReplaceNonBreakingAndExoticSpacesWithRegularSpaces()
    {
        var input = "motor hums no movement";

        var block = PasteBlockGenerator.Generate("Electrical", input, StatusUrl);

        block.Should().Contain("motor hums no movement");
        IsAsciiSafe(block).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldExpandTheEllipsisCharacterToThreeDots()
    {
        var block = PasteBlockGenerator.Generate("Electrical", "hums… then quits", StatusUrl);

        block.Should().Contain("hums... then quits");
        IsAsciiSafe(block).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldFoldAccentedLatinLettersToTheirBaseLetter()
    {
        var block = PasteBlockGenerator.Generate("Electrical", "café door résumé", StatusUrl);

        block.Should().Contain("cafe door resume");
        IsAsciiSafe(block).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldDropCharactersItCannotFoldSuchAsEmoji()
    {
        var block = PasteBlockGenerator.Generate("Electrical", "slide broke 😠 badly", StatusUrl);

        block.Should().Contain("slide broke  badly");
        IsAsciiSafe(block).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldProduceOnlyAsciiEvenWhenEveryFieldCarriesUnicode()
    {
        var block = PasteBlockGenerator.Generate(
            "Électrical",
            "“won’t” start — café … 😠",
            "https://rvintake.com/status/é");

        IsAsciiSafe(block).Should().BeTrue();
    }

    // ── Word-boundary truncation to a configurable cap ────────────────────

    [Fact]
    public void Generate_WhenBlockExceedsDefaultCap_ShouldTruncateToOneThousandCharacters()
    {
        var longDescription = string.Join(" ", Enumerable.Repeat("wordword", 400));

        var block = PasteBlockGenerator.Generate("Electrical", longDescription, StatusUrl);

        block.Length.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public void Generate_WhenTruncating_ShouldCutAtAWordBoundaryAndMarkTheCut()
    {
        var longDescription = string.Join(" ", Enumerable.Repeat("wordword", 400));

        var block = PasteBlockGenerator.Generate("Electrical", longDescription, StatusUrl);

        block.Should().Contain("...");
        // Everything before the marker is a clean prefix of the source — no split word.
        var complaint = block.Split('\n').First(l => l.StartsWith("COMPLAINT:", StringComparison.Ordinal));
        var kept = complaint["COMPLAINT:".Length..].Replace("...", string.Empty).Trim();
        longDescription.Should().StartWith(kept);
        kept.Should().EndWith("wordword");
    }

    [Fact]
    public void Generate_ShouldHonorAConfigurableCap()
    {
        var longDescription = string.Join(" ", Enumerable.Repeat("wordword", 400));

        var block = PasteBlockGenerator.Generate("Electrical", longDescription, StatusUrl, characterCap: 250);

        block.Length.Should().BeLessThanOrEqualTo(250);
        block.Should().Contain("...");
    }

    [Fact]
    public void Generate_WhenDescriptionIsShorterThanCap_ShouldNotAppendATruncationMarker()
    {
        var block = PasteBlockGenerator.Generate("Electrical", Description, StatusUrl, characterCap: 1000);

        block.Should().NotContain("...");
    }

    [Fact]
    public void Generate_ShouldKeepTheClosingFenceEvenWhenTheDescriptionIsTruncated()
    {
        var longDescription = string.Join(" ", Enumerable.Repeat("wordword", 400));

        var block = PasteBlockGenerator.Generate("Electrical", longDescription, StatusUrl, characterCap: 300);

        block.Split('\n')[^1].Should().Be(PasteBlockGenerator.Delimiter);
        block.Should().Contain($"STATUS: {StatusUrl}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Generate_WhenCapIsNotPositive_ShouldFallBackToTheDefaultCap(int cap)
    {
        var longDescription = string.Join(" ", Enumerable.Repeat("wordword", 400));

        var block = PasteBlockGenerator.Generate("Electrical", longDescription, StatusUrl, characterCap: cap);

        block.Length.Should().BeLessThanOrEqualTo(1000);
        block.Length.Should().BeGreaterThan(300, "a non-positive cap means 'use the 1,000 default', not 'truncate to nothing'");
    }

    [Fact]
    public void Generate_ShouldNormalizeWindowsLineEndingsInTheDescription()
    {
        var block = PasteBlockGenerator.Generate("Electrical", "line one\r\nline two", StatusUrl);

        block.Should().NotContain("\r");
        block.Should().Contain("line one\nline two");
    }
}
