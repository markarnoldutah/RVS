using System.Text.RegularExpressions;
using FluentAssertions;

namespace RVS.UI.Shared.Tests.Theme;

/// <summary>
/// Intake is filled out on a phone, so every control is a 44 × 44 px tap target (WCAG 2.5.5,
/// Apple HIG). MudBlazor's own sizes fall short — a <c>Size.Small</c> icon button is 24 px
/// square — so the app stylesheet raises them. These tests hold those rules in place (issue #728).
/// </summary>
public partial class TapTargetCssTests
{
    [Theory]
    [InlineData(".mud-button-root", "min-height")]
    [InlineData(".mud-input .mud-input-root", "min-height")]
    [InlineData(".mud-icon-button", "min-width")]
    public void IntakeAppCss_ShouldRaiseTapTargetsTo44px(string selector, string property)
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "RVS.Blazor.Intake", "wwwroot", "app.css"));

        RuleBody(css, selector).Should().MatchRegex($@"(^|[;\s]){property}:\s*44px;");
    }

    [Fact]
    public void ManagerAppCss_ShouldRaiseButtonHeightsTo44px()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "RVS.Blazor.Manager", "wwwroot", "css", "app.css"));

        RuleBody(css, ".mud-button, .mud-icon-button").Should().MatchRegex(@"(^|[;\s])min-height:\s*44px;");
    }

    /// <summary>The body of the first rule whose selector list starts with <paramref name="selector"/>.</summary>
    private static string RuleBody(string css, string selector)
    {
        var match = Regex.Match(
            Comment().Replace(css, string.Empty),
            $@"(?m)^\s*{Regex.Escape(selector)}\s*(?:,[^{{]*)?\{{(?<body>[^}}]*)\}}");

        match.Success.Should().BeTrue($"a rule for {selector} should exist");
        return match.Groups["body"].Value;
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RVS.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("RVS.slnx not found above the test output directory.");
    }
}
