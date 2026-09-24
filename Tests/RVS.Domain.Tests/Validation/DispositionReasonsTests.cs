using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="DispositionReasons"/> — the fixed reason codes for closing a request
/// without work (<c>Spec C-4</c>, issue #445).
/// </summary>
public class DispositionReasonsTests
{
    [Fact]
    public void All_ShouldBeTheFourC4ReasonsInSpecOrder()
    {
        DispositionReasons.All
            .Should().Equal("Duplicate", "Spam", "WrongLocation", "CustomerWithdrew");
    }

    [Theory]
    [InlineData("Duplicate")]
    [InlineData("Spam")]
    [InlineData("WrongLocation")]
    [InlineData("CustomerWithdrew")]
    public void IsValid_WhenKnownCode_ShouldReturnTrue(string code)
    {
        DispositionReasons.IsValid(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("duplicate")]
    [InlineData(" Spam ")]
    [InlineData("Wrong location")]
    [InlineData("Other")]
    public void IsValid_WhenUnknownOrBlankOrMiscased_ShouldReturnFalse(string? code)
    {
        DispositionReasons.IsValid(code).Should().BeFalse();
    }

    [Theory]
    [InlineData("Duplicate", "Duplicate")]
    [InlineData("Spam", "Spam")]
    [InlineData("WrongLocation", "Wrong location")]
    [InlineData("CustomerWithdrew", "Customer withdrew")]
    public void GetLabel_WhenKnownCode_ShouldReturnHumanLabel(string code, string expected)
    {
        DispositionReasons.GetLabel(code).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SomethingNew")]
    public void GetLabel_WhenUnknownCode_ShouldReturnTheRawValueOrEmpty(string? code)
    {
        DispositionReasons.GetLabel(code).Should().Be(code ?? string.Empty);
    }
}
