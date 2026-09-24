using FluentAssertions;
using RVS.UI.Shared.Components;

namespace RVS.UI.Shared.Tests.Components;

/// <summary>
/// The "Service Tag" logo kit ships its lockups with the text converted to outlines, so the app
/// chrome loads the kit's own SVGs rather than inlining a copy of their geometry (Spec THEME-1
/// §4). These tests hold the component to the kit's filenames and prove each one is actually
/// shipped in the RCL's <c>wwwroot/brand/</c>.
/// </summary>
public class BrandMarkAssetsTests
{
    [Theory]
    [InlineData(BrandWordmarkVariant.Horizontal, false, "logo-horizontal.svg")]
    [InlineData(BrandWordmarkVariant.Horizontal, true, "logo-horizontal-reversed.svg")]
    [InlineData(BrandWordmarkVariant.Stacked, false, "logo-stacked.svg")]
    [InlineData(BrandWordmarkVariant.Stacked, true, "logo-stacked-reversed.svg")]
    [InlineData(BrandWordmarkVariant.Glyph, false, "glyph.svg")]
    [InlineData(BrandWordmarkVariant.Glyph, true, "glyph-reversed.svg")]
    [InlineData(BrandWordmarkVariant.Wordmark, false, "wordmark.svg")]
    [InlineData(BrandWordmarkVariant.Wordmark, true, "wordmark-reversed.svg")]
    public void PathFor_ShouldPointAtTheKitsFileForTheVariantAndColourway(
        BrandWordmarkVariant variant, bool reversed, string expectedFile)
    {
        BrandMarkAssets.PathFor(variant, reversed)
            .Should().Be($"_content/RVS.UI.Shared/brand/{expectedFile}");
    }

    [Theory]
    [MemberData(nameof(EveryLockup))]
    public void PathFor_ShouldNameAFileThatShipsInTheRcl(BrandWordmarkVariant variant, bool reversed)
    {
        var fileName = Path.GetFileName(BrandMarkAssets.PathFor(variant, reversed));
        var shipped = Path.Combine(RepoRoot(), "RVS.UI.Shared", "wwwroot", "brand", fileName);

        File.Exists(shipped).Should().BeTrue($"{fileName} is referenced by BrandWordmark");
    }

    [Theory]
    [MemberData(nameof(EveryLockup))]
    public void ShippedLockups_ShouldCarryNoLiveText(BrandWordmarkVariant variant, bool reversed)
    {
        // An SVG loaded as an image cannot reach the self-hosted font, so live <text> would
        // render in a system face. The kit outlines every glyph; keep it that way.
        var fileName = Path.GetFileName(BrandMarkAssets.PathFor(variant, reversed));
        var svg = File.ReadAllText(Path.Combine(RepoRoot(), "RVS.UI.Shared", "wwwroot", "brand", fileName));

        svg.Should().NotContain("<text", fileName);
    }

    public static TheoryData<BrandWordmarkVariant, bool> EveryLockup()
    {
        var data = new TheoryData<BrandWordmarkVariant, bool>();
        foreach (var variant in Enum.GetValues<BrandWordmarkVariant>())
        {
            data.Add(variant, false);
            data.Add(variant, true);
        }

        return data;
    }

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
