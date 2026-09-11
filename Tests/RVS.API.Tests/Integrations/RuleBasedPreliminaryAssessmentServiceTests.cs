using FluentAssertions;
using RVS.API.Integrations;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Tests for <see cref="RuleBasedPreliminaryAssessmentService"/> — the deterministic,
/// category-keyed fallback for the structured preliminary assessment (issue #507).
/// </summary>
public class RuleBasedPreliminaryAssessmentServiceTests
{
    private readonly RuleBasedPreliminaryAssessmentService _sut = new();

    public static TheoryData<string> CategoriesWithGuidance()
    {
        var data = new TheoryData<string>();
        foreach (var code in IssueCategoryVocabulary.Codes.Where(c => c != IssueCategoryVocabulary.FallbackCode))
        {
            data.Add(code);
        }

        return data;
    }

    private static ServiceRequest Request(string? category) => new()
    {
        Id = "sr_1",
        TenantId = "ten_1",
        IssueCategory = category,
        IssueDescription = "Something is wrong.",
    };

    [Fact]
    public async Task AssessAsync_WhenServiceRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.AssessAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void CategoriesWithGuidance_ShouldCoverEveryVocabularyCodeExceptOther()
    {
        RuleBasedPreliminaryAssessmentService.CategoriesWithGuidance
            .Should().BeEquivalentTo(IssueCategoryVocabulary.Codes.Where(c => c != IssueCategoryVocabulary.FallbackCode));
    }

    [Theory]
    [MemberData(nameof(CategoriesWithGuidance))]
    public async Task AssessAsync_ForEveryCategoryWithGuidance_ShouldOfferALowConfidenceAssessment(string category)
    {
        var result = await _sut.AssessAsync(Request(category));

        result.Confidence.Should().Be(AssessmentConfidence.Low);
        result.ProbableCause.Should().NotBeNullOrWhiteSpace();
        result.PossibleFixes.Should().HaveCountGreaterThanOrEqualTo(1).And.HaveCountLessThanOrEqualTo(3);
        result.LikelyParts.Should().NotBeEmpty();
        result.Provider.Should().Be(nameof(RuleBasedPreliminaryAssessmentService));
    }

    [Theory]
    [InlineData("Other")]
    [InlineData("Transmission")]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task AssessAsync_WhenCategoryHasNoGuidance_ShouldAbstain(string? category)
    {
        var result = await _sut.AssessAsync(Request(category));

        result.Confidence.Should().Be(AssessmentConfidence.Abstain);
        result.ProbableCause.Should().BeNull();
        result.PossibleFixes.Should().BeEmpty();
        result.LikelyParts.Should().BeEmpty();
        result.Provider.Should().Be(nameof(RuleBasedPreliminaryAssessmentService));
    }

    [Fact]
    public async Task AssessAsync_ShouldMatchTheCategoryCaseInsensitively()
    {
        var result = await _sut.AssessAsync(Request("slides"));

        result.Confidence.Should().Be(AssessmentConfidence.Low);
    }

    [Fact]
    public async Task AssessAsync_ShouldStampGeneratedAtUtc()
    {
        var result = await _sut.AssessAsync(Request("Slides"));

        result.GeneratedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AssessAsync_ShouldReturnAFreshCopy_SoCallersCannotMutateTheTable()
    {
        var first = await _sut.AssessAsync(Request("Slides"));
        first.PossibleFixes.Clear();

        var second = await _sut.AssessAsync(Request("Slides"));

        second.PossibleFixes.Should().NotBeEmpty();
    }
}
