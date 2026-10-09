using FluentAssertions;
using MudBlazor;
using RVS.Blazor.Manager.Shared;
using RVS.Domain.Validation;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// The job-type chip (<c>Spec C-11</c>, issue #843). Each value gets its own color, and values
/// whose hues sit close together differ in lightness too (a dark filled chip against a light
/// tinted one), so hue is never the only signal. Every chip also carries its label and an icon.
/// </summary>
public class JobTypeDisplayTests
{
    [Fact]
    public void For_EveryCode_ShouldHaveADistinctColorAndVariant()
    {
        var styles = JobTypes.Codes.Select(JobTypeDisplay.For).ToList();

        styles.Select(s => (s.Color, s.Variant)).Should().OnlyHaveUniqueItems();
        styles.Select(s => s.Color).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void For_EveryCode_ShouldHaveItsLabelAndADistinctIcon()
    {
        var styles = JobTypes.Codes.Select(JobTypeDisplay.For).ToList();

        styles.Select(s => s.Label).Should().Equal(JobTypes.Codes.Select(JobTypes.GetLabel));
        styles.Select(s => s.Icon).Should().OnlyHaveUniqueItems().And.NotContain(string.Empty);
    }

    [Fact]
    public void For_EveryCode_ShouldNotUseTheErrorColor()
    {
        // Error is reserved for error states, which always carry an error icon (THEME-1).
        JobTypes.Codes.Select(JobTypeDisplay.For).Should().NotContain(s => s.Color == Color.Error);
    }

    [Theory]
    [InlineData("OnSite", "Install")]   // Rust and amber
    [InlineData("InShop", "Remote")]    // Ink and blue
    public void For_CloseHues_ShouldDifferInLightness(string a, string b)
    {
        // Filled chips are a dark fill; Text chips are a light tint.
        JobTypeDisplay.For(a).Variant.Should().NotBe(JobTypeDisplay.For(b).Variant);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void For_WhenUnset_ShouldReadNotTriagedInTheNeutralOutline(string? code)
    {
        var style = JobTypeDisplay.For(code);

        style.Label.Should().Be("Not triaged");
        style.Color.Should().Be(Color.Default);
        style.Variant.Should().Be(Variant.Outlined);
    }

    [Fact]
    public void For_WhenUnknown_ShouldShowTheRawValueInTheNeutralOutline()
    {
        var style = JobTypeDisplay.For("Solar");

        style.Label.Should().Be("Solar");
        style.Color.Should().Be(Color.Default);
        style.Variant.Should().Be(Variant.Outlined);
    }

    [Fact]
    public void FilterOptions_ShouldOfferNotTriagedThenEveryCode()
    {
        JobTypeDisplay.FilterOptions.Select(o => o.Value)
            .Should().Equal(new[] { JobTypes.NotTriagedFilter }.Concat(JobTypes.Codes));
        JobTypeDisplay.FilterOptions[0].Label.Should().Be("Not triaged");
    }
}
