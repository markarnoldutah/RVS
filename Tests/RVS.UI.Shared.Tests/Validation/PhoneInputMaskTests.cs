using FluentAssertions;
using RVS.Domain.Validation;
using RVS.UI.Shared.Validation;

namespace RVS.UI.Shared.Tests.Validation;

/// <summary>
/// Tests for <see cref="PhoneInputMask"/> — what the intake phone field lets a customer type (issue #758).
/// </summary>
public class PhoneInputMaskTests
{
    [Theory]
    [InlineData("")]
    [InlineData("8")]
    [InlineData("8015551234")]
    [InlineData("(801) 555-1234")]
    [InlineData("801.555.1234")]
    [InlineData("+1 801 555 1234")]
    [InlineData("+44 20 7946 0958")]
    public void IsAllowed_PhoneShapedText_ShouldBeTrue(string text)
    {
        PhoneInputMask.IsAllowed(text).Should().BeTrue();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("801555abcd")]
    [InlineData("1-800-FLOWERS")]
    [InlineData("801 555 1234 x12")]
    [InlineData("801;555")]
    [InlineData("<script/>")]
    public void IsAllowed_TextWithLettersOrSymbols_ShouldBeFalse(string text)
    {
        PhoneInputMask.IsAllowed(text).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_Null_ShouldBeTrue()
    {
        // An empty field is the required-field rule's business, not the mask's.
        PhoneInputMask.IsAllowed(null).Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_LongerThanThePhoneRuleAllows_ShouldBeFalse()
    {
        PhoneInputMask.IsAllowed(new string('5', PhoneValidator.MaxLength + 1)).Should().BeFalse();
    }

    [Fact]
    public void Pattern_ShouldBeAnchoredAtBothEnds()
    {
        // MudBlazor's RegexMask requires it, and tests every keystroke against the whole text.
        PhoneInputMask.Pattern.Should().StartWith("^").And.EndWith("$");
    }
}
