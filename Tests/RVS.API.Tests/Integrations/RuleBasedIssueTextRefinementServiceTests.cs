using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class RuleBasedIssueTextRefinementServiceTests
{
    private readonly RuleBasedIssueTextRefinementService _sut = new(Mock.Of<ILogger<RuleBasedIssueTextRefinementService>>());

    // ── RefineTranscriptAsync ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RefineTranscriptAsync_WhenRawTranscriptIsNullOrWhiteSpace_ShouldThrowArgumentException(string? rawTranscript)
    {
        var act = () => _sut.RefineTranscriptAsync(rawTranscript!, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RefineTranscriptAsync_ShouldReturnCleanedDescription()
    {
        var result = await _sut.RefineTranscriptAsync("um my water heater stopped working", null);

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RefineTranscriptAsync_ShouldRemoveFillerWords()
    {
        var result = await _sut.RefineTranscriptAsync("um my water heater stopped working", null);

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().NotStartWith("um ", because: "filler words should be removed");
    }

    [Fact]
    public async Task RefineTranscriptAsync_ShouldCapitalizeFirstLetter()
    {
        var result = await _sut.RefineTranscriptAsync("water heater is broken", null);

        result.Should().NotBeNull();
        result!.CleanedDescription[0].Should().Be('W');
    }

    [Fact]
    public async Task RefineTranscriptAsync_ShouldEndWithPeriod()
    {
        var result = await _sut.RefineTranscriptAsync("water heater is broken", null);

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().EndWith(".");
    }

    [Fact]
    public async Task RefineTranscriptAsync_ShouldReturnProviderName()
    {
        var result = await _sut.RefineTranscriptAsync("some text", null);

        result.Should().NotBeNull();
        result!.Provider.Should().Be("RuleBasedIssueTextRefinementService");
    }

    [Fact]
    public async Task RefineTranscriptAsync_ShouldReturnPositiveConfidence()
    {
        var result = await _sut.RefineTranscriptAsync("some text", null);

        result.Should().NotBeNull();
        result!.Confidence.Should().BeGreaterThan(0.0);
    }

    // ── SuggestCategoryAsync ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SuggestCategoryAsync_WhenDescriptionIsNullOrWhiteSpace_ShouldThrowArgumentException(string? description)
    {
        var act = () => _sut.SuggestCategoryAsync(description!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenDescriptionMentionsElectrical_ShouldSuggestElectrical()
    {
        var result = await _sut.SuggestCategoryAsync("The battery is dead and the lights don't work");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().Be("Electrical");
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenDescriptionMentionsPlumbing_ShouldSuggestPlumbing()
    {
        var result = await _sut.SuggestCategoryAsync("Water leak from the pipe under the sink");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().Be("Plumbing");
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenDescriptionMentionsHvac_ShouldSuggestHvac()
    {
        var result = await _sut.SuggestCategoryAsync("The air conditioning is blowing hot air and the thermostat doesn't respond");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().Be("HVAC");
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenNoKeywordsMatch_ShouldReturnNullCategory()
    {
        var result = await _sut.SuggestCategoryAsync("Something is wrong with my vehicle");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().BeNull();
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task SuggestCategoryAsync_ShouldReturnProviderName()
    {
        var result = await _sut.SuggestCategoryAsync("battery issue");

        result.Should().NotBeNull();
        result!.Provider.Should().Be("RuleBasedIssueTextRefinementService");
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenCategoryFound_ShouldReturnPositiveConfidence()
    {
        var result = await _sut.SuggestCategoryAsync("The battery is dead");

        result.Should().NotBeNull();
        result!.Confidence.Should().BeGreaterThan(0.0);
    }
}
