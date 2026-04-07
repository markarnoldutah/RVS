using FluentAssertions;
using RVS.API.Integrations;

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
    [InlineData("Crack in the roof near the slide-out", "Structural")]
    [InlineData("The refrigerator stopped working", "Appliance")]
    [InlineData("The awning mechanism is broken", "Exterior")]
    public async Task CategorizeAsync_WhenDescriptionContainsKeyword_ShouldReturnMatchingCategory(string description, string expectedCategory)
    {
        var result = await _sut.CategorizeAsync(description);

        result.Should().Be(expectedCategory);
    }

    [Fact]
    public async Task CategorizeAsync_WhenNoKeywordsMatch_ShouldReturnGeneral()
    {
        var result = await _sut.CategorizeAsync("Something is wrong but I'm not sure what");

        result.Should().Be("General");
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

    [Theory]
    [InlineData("Electrical")]
    [InlineData("Plumbing")]
    [InlineData("HVAC")]
    [InlineData("Structural")]
    [InlineData("Appliance")]
    [InlineData("Exterior")]
    public async Task SuggestDiagnosticQuestionsAsync_WhenKnownCategory_ShouldReturnThreeQuestions(string category)
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync(category);

        result.Questions.Should().HaveCount(3);
        result.Questions.Should().AllSatisfy(q => q.QuestionText.Should().NotBeNullOrWhiteSpace());
        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenUnknownCategory_ShouldReturnDefaultQuestions()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync("Unknown");

        result.Questions.Should().HaveCount(3);
        result.Questions[0].QuestionText.Should().Contain("describe the issue");
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenKnownCategory_ShouldReturnQuestionsWithOptions()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Questions.Should().AllSatisfy(q =>
        {
            q.Options.Should().NotBeEmpty();
            q.AllowFreeText.Should().BeTrue();
        });
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

        result.Questions.Should().HaveCount(3);
        result.SmartSuggestion.Should().BeNull();
    }
}
