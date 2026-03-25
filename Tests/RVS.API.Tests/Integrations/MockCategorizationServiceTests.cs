using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

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
    public async Task CategorizeAsync_ShouldReturnGeneral()
    {
        var result = await _sut.CategorizeAsync("Some issue description");

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

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_ShouldReturnThreeStockQuestions()
    {
        var result = await _sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Should().HaveCount(3);
        result[0].Should().Contain("describe the issue");
        result[1].Should().Contain("first notice");
        result[2].Should().Contain("intermittent");
    }
}
