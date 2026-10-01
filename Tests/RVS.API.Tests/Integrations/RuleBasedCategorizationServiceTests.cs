using FluentAssertions;
using RVS.API.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Integrations;

public class RuleBasedCategorizationServiceTests
{
    private readonly RuleBasedCategorizationService _sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CategorizeAsync_WhenDescriptionIsNullOrWhiteSpace_ShouldThrowArgumentException(string? description)
    {
        var act = () => _sut.CategorizeAsync(description!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("The battery is dead and won't charge", "Electrical")]
    [InlineData("There is a water leak under the sink", "Plumbing")]
    [InlineData("The furnace stopped working and won't heat", "HVAC")]
    [InlineData("The sealant on the roof is peeling and there is a soft spot", "Roof")]
    [InlineData("Slide-out won't retract all the way", "Slides")]
    [InlineData("The onboard generator will not start", "Generator")]
    [InlineData("I can smell propane near the regulator", "LPGas")]
    [InlineData("The refrigerator stopped working", "Appliances")]
    [InlineData("The awning fabric is torn", "Awning")]
    [InlineData("Grinding noise from a wheel bearing and the brakes feel soft", "Chassis")]
    public async Task CategorizeAsync_WhenDescriptionContainsKeyword_ShouldReturnMatchingCategory(string description, string expectedCategory)
    {
        var result = await _sut.CategorizeAsync(description);

        result.Should().Be(expectedCategory);
        IssueCategoryVocabulary.IsValid(result).Should().BeTrue();
    }

    [Fact]
    public async Task CategorizeAsync_WhenNoKeywordsMatch_ShouldReturnFallbackVocabularyCode()
    {
        var result = await _sut.CategorizeAsync("Something is wrong but I'm not sure what");

        result.Should().Be(IssueCategoryVocabulary.FallbackCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SuggestDiagnosticQuestionsAsync_WhenCategoryIsNullOrWhiteSpace_ShouldThrowArgumentException(string? category)
    {
        var act = () => _sut.SuggestDiagnosticQuestionsAsync(category!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    public static IEnumerable<object[]> VocabularyCategories =>
        IssueCategoryVocabulary.Codes.Select(code => new object[] { code });

    [Fact]
    public void CategoriesWithDedicatedQuestions_ShouldCoverEveryVocabularyCode()
    {
        // AC #1 of #453: every category — including "Other" — has a hand-written set,
        // never the generic default fallback.
        RuleBasedCategorizationService.CategoriesWithDedicatedQuestions
            .Should().BeEquivalentTo(IssueCategoryVocabulary.Codes);
    }

    [Theory]
    [MemberData(nameof(VocabularyCategories))]
    public async Task SuggestDiagnosticQuestionsAsync_ForEveryVocabularyCategory_ShouldReturnTwoToFourQuestions(string category)
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync(category);

        result.Questions.Should().HaveCountGreaterThanOrEqualTo(2).And.HaveCountLessThanOrEqualTo(4);
        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
        result.SmartSuggestion.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(VocabularyCategories))]
    public async Task SuggestDiagnosticQuestionsAsync_ForEveryVocabularyCategory_ShouldReturnWellFormedQuestions(string category)
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync(category);

        result.Questions.Should().AllSatisfy(q =>
        {
            q.QuestionText.Should().NotBeNullOrWhiteSpace();
            q.QuestionText.Trim().Should().EndWith("?");
            q.AllowFreeText.Should().BeTrue("free text is always the fallback answer");

            // A question either offers a short pick-list (2–6 mutually exclusive
            // observations) or is pure free text.
            if (q.Options.Count > 0)
            {
                q.Options.Should().HaveCountGreaterThanOrEqualTo(2).And.HaveCountLessThanOrEqualTo(6);
                q.Options.Should().OnlyContain(o => !string.IsNullOrWhiteSpace(o));
                q.Options.Select(o => o.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
            }

            if (q.HelpText is not null)
            {
                q.HelpText.Should().NotBeNullOrWhiteSpace();
            }
        });
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_ForSlides_ShouldAskWhetherTheSlideMovesAtAll()
    {
        // Spec A-4 names this exact litmus: "Does the slide move at all?" beats "Describe the problem."
        var result = await _sut.SuggestDiagnosticQuestionsAsync("Slides");

        result.Questions.Should().Contain(q => q.QuestionText.Contains("move", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenCategoryIsNotInVocabulary_ShouldReturnDefaultQuestions()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync("SomethingWeNeverSeeded");

        result.Questions.Should().HaveCountGreaterThanOrEqualTo(2).And.HaveCountLessThanOrEqualTo(4);
        result.Questions[0].QuestionText.Should().Contain("describe the issue");
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_ShouldAcceptOptionalContextParameters()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync(
            "Electrical",
            issueDescription: "Battery won't charge",
            manufacturer: "Thor",
            model: "Ace",
            year: 2023);

        result.Questions.Should().NotBeEmpty();
        result.SmartSuggestion.Should().BeNull();
    }
}
