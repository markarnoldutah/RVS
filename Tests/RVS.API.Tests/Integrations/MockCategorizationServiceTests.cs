using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Integrations;

public class MockCategorizationServiceTests
{
    private readonly MockCategorizationService _sut = new(Mock.Of<ILogger<MockCategorizationService>>());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CategorizeAsync_WhenDescriptionIsNullOrWhiteSpace_ShouldThrowArgumentException(string? description)
    {
        var act = () => _sut.CategorizeAsync(description!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CategorizeAsync_ShouldReturnFallbackVocabularyCode()
    {
        var result = await _sut.CategorizeAsync("Some issue description");

        result.Should().Be(IssueCategoryVocabulary.FallbackCode);
        IssueCategoryVocabulary.IsValid(result).Should().BeTrue();
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

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_ShouldReturnThreeStockQuestions()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Questions.Should().HaveCount(3);
        result.Questions[0].QuestionText.Should().Contain("describe the issue");
        result.Questions[1].QuestionText.Should().Contain("first notice");
        result.Questions[2].QuestionText.Should().Contain("intermittent");
        result.Provider.Should().Be(nameof(MockCategorizationService));
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_ShouldReturnQuestionsWithOptions()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Questions.Should().AllSatisfy(q =>
        {
            q.AllowFreeText.Should().BeTrue();
        });
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenDescriptionProvided_ShouldReturnSmartSuggestion()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync("Electrical", issueDescription: "Battery won't charge");

        result.SmartSuggestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenNoDescription_ShouldNotReturnSmartSuggestion()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.SmartSuggestion.Should().BeNull();
    }
}
