using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="DealerQuestionsValidator"/> — the up-to-two questions a location adds to
/// every intake's diagnostic step (<c>Spec A-18</c>, issue #785).
/// </summary>
public class DealerQuestionsValidatorTests
{
    [Fact]
    public void Validate_WhenQuestionsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => DealerQuestionsValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_WhenNoQuestions_ShouldSucceed()
    {
        DealerQuestionsValidator.Validate([]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTwoQuestions_ShouldSucceed()
    {
        var result = DealerQuestionsValidator.Validate(
            ["Where is the RV stored right now?", "Do you need a loaner tow vehicle?"]);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenMoreThanTwoQuestions_ShouldFail()
    {
        var result = DealerQuestionsValidator.Validate(["One?", "Two?", "Three?"]);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("2");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenAQuestionIsBlank_ShouldFail(string blank)
    {
        DealerQuestionsValidator.Validate(["Where is the RV stored?", blank]).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenAQuestionIsTooLong_ShouldFail()
    {
        var tooLong = new string('a', DealerQuestionsValidator.MaxQuestionLength + 1);

        DealerQuestionsValidator.Validate([tooLong]).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenAQuestionIsExactlyTheMaximumLength_ShouldSucceed()
    {
        var longest = new string('a', DealerQuestionsValidator.MaxQuestionLength);

        DealerQuestionsValidator.Validate([longest]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBothQuestionsAreTheSameIgnoringCase_ShouldFail()
    {
        // Step 6 keys an answer by its question's text, so two identical questions share one answer.
        DealerQuestionsValidator.Validate(["Where is the RV stored?", "where is the RV stored?"])
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Where is the RV stored?")]
    public void ValidateQuestion_WhenBlankOrShortEnough_ShouldSucceed(string? question)
    {
        DealerQuestionsValidator.ValidateQuestion(question).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateQuestion_WhenTooLong_ShouldFail()
    {
        var tooLong = new string('a', DealerQuestionsValidator.MaxQuestionLength + 1);

        var result = DealerQuestionsValidator.ValidateQuestion(tooLong);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(DealerQuestionsValidator.MaxQuestionLength.ToString());
    }

    [Fact]
    public void Normalize_WhenNull_ShouldReturnAnEmptyList()
    {
        DealerQuestionsValidator.Normalize(null).Should().BeEmpty();
    }

    [Fact]
    public void Normalize_ShouldTrimAndDropBlankQuestionsKeepingTheOrder()
    {
        var result = DealerQuestionsValidator.Normalize(["  Where is the RV stored?  ", "", null, " ", "Loaner needed? "]);

        result.Should().Equal("Where is the RV stored?", "Loaner needed?");
    }
}
