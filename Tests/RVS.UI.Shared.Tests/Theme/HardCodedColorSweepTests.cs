using System.Text.RegularExpressions;
using FluentAssertions;

namespace RVS.UI.Shared.Tests.Theme;

/// <summary>
/// The palette has one home, <c>RVS.UI.Shared/Theme/</c> (Spec THEME-1): "never inline ad-hoc
/// colors". A hex literal in a page or component either duplicates a palette value and drifts
/// from it, or bypasses the theme entirely — it ignores dark and high-contrast mode, and on
/// Intake it ignores the dealer's accent (Spec A-16). This test is the sweep from issue #728,
/// kept running so the next one is not a manual job.
/// </summary>
public partial class HardCodedColorSweepTests
{
    private static readonly string[] SweptProjects = ["RVS.Blazor.Intake", "RVS.Blazor.Manager", "RVS.UI.Shared"];

    private static readonly string[] SweptExtensions = [".razor", ".cs", ".css"];

    /// <summary>Whole files that are allowed to declare colour.</summary>
    private static readonly string[] ExemptFiles =
    [
        // The palette's home, and the plain-CSS mirror of it.
        "RVS.UI.Shared/Theme/",
        "RVS.UI.Shared/wwwroot/css/design-tokens.css",
    ];

    /// <summary>
    /// Individual literals that are deliberately not theme colours. Each one names the file it
    /// may appear in; the same literal anywhere else still fails.
    /// </summary>
    private static readonly (string File, string Literal, string Why)[] Allowed =
    [
        ("RVS.Blazor.Intake/Layout/DealerLogo.razor.css", "#FFFFFF",
            "the neutral white mount under a dealer's logo — what the logo was drawn on, not a brand colour"),
        ("RVS.Blazor.Manager/Pages/Locations.razor.css", "#FFFFFF",
            "the Intake header preview: the same white logo mount, and Intake's white page ground"),
        ("RVS.Blazor.Manager/Pages/Locations.razor", "#FFFFFF",
            "white text on the previewed dealer accent, as IntakeTheme's PrimaryContrastText renders it"),
        ("RVS.Blazor.Intake/wwwroot/app.css", "#fff",
            "the fallback in var(--mud-palette-background, #fff), for the moment before MudThemeProvider renders"),
        ("RVS.Blazor.Intake/Layout/MainLayout.razor.css", "rgba(0, 0, 0, 0.2)", "a drop shadow"),
        ("RVS.Blazor.Manager/wwwroot/css/app.css", "rgba(0, 0, 0, 0.2)", "a drop shadow"),
        ("RVS.Blazor.Manager/Shared/ServiceRequestDetailDialog.razor.css", "#fff",
            "the play icon on a video thumbnail — sits on the photo, not on the theme"),
        ("RVS.Blazor.Manager/Shared/ServiceRequestDetailDialog.razor.css", "rgba(0, 0, 0, 0.25)",
            "the scrim under that play icon"),
        ("RVS.Blazor.Intake/Pages/Steps/Step3_VinLookupStep.razor", "rgba(255,255,255,0.7)",
            "the scrim over the VIN photo while it is read"),
    ];

    [Fact]
    public void BlazorSources_ShouldNotHardCodeColours()
    {
        var root = RepoRoot();
        var offenders = new List<string>();

        foreach (var file in SweptFiles(root))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (ExemptFiles.Any(relative.StartsWith))
            {
                continue;
            }

            var source = StripComments(File.ReadAllText(file));
            foreach (Match match in ColourLiteral().Matches(source))
            {
                var literal = match.Value;
                if (Allowed.Any(a => a.File == relative && a.Literal == literal))
                {
                    continue;
                }

                offenders.Add($"{relative}: {literal}");
            }
        }

        offenders.Should().BeEmpty(
            "colour belongs in RVS.UI.Shared/Theme — reach it through a --mud-palette-* variable or Color.*");
    }

    [Fact]
    public void AllowList_ShouldNotGoStale()
    {
        // An allowance whose literal has gone is an allowance nobody will notice being reused.
        var root = RepoRoot();

        foreach (var (file, literal, why) in Allowed)
        {
            var source = StripComments(File.ReadAllText(Path.Combine(root, file)));
            source.Should().Contain(literal, $"{file} is allowed {literal} for {why}");
        }
    }

    private static IEnumerable<string> SweptFiles(string root) =>
        SweptProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, project), "*", SearchOption.AllDirectories))
            .Where(path => SweptExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !IsUnder(path, "bin") && !IsUnder(path, "obj") && !IsUnder(path, "lib"));

    private static bool IsUnder(string path, string directory) =>
        path.Replace('\\', '/').Contains($"/{directory}/", StringComparison.Ordinal);

    private static string StripComments(string source) =>
        LineComment().Replace(BlockComment().Replace(source, string.Empty), string.Empty);

    /// <summary>
    /// <c>#RGB</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c> (issue numbers are digits only, so a
    /// three-character hex must contain a letter to count), <c>rgb()</c>/<c>rgba()</c>/<c>hsl()</c>,
    /// and a CSS named colour used as a colour value.
    /// </summary>
    [GeneratedRegex(
        @"#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})\b" +
        @"|#(?=[0-9]*[A-Fa-f])[0-9A-Fa-f]{3}\b" +
        @"|\b(?:rgba?|hsla?)\([^)]*\)" +
        @"|(?<=(?:color|background|background-color|border|border-color|outline)\s*:\s*[^;""]*)(?<!-)\b(?:white|black|lightyellow|red|green|blue|yellow|gray|grey)\b(?!-)")]
    private static partial Regex ColourLiteral();

    /// <summary><c>/* */</c> and Razor <c>@* *@</c> comments.</summary>
    [GeneratedRegex(@"/\*.*?\*/|@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    /// <summary><c>//</c> and <c>///</c> comments, but not the <c>//</c> in a URL scheme.</summary>
    [GeneratedRegex(@"(?<![:""'])//.*$", RegexOptions.Multiline)]
    private static partial Regex LineComment();

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
