using FluentAssertions;
using ImageMagick;
using RVS.API.Packets;

namespace RVS.API.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketPhotoCrop"/> — cropping each packet photo to the PDF grid's cell
/// shape, so a portrait phone shot and a wide one fill identical cells.
/// </summary>
public class PacketPhotoCropTests
{
    private const double CellAspect = 1.45;

    private static (uint Width, uint Height, MagickFormat Format) Inspect(byte[] bytes)
    {
        var info = new MagickImageInfo(bytes);
        return (info.Width, info.Height, info.Format);
    }

    [Fact]
    public void ToAspect_ForAPortraitPhoto_ShouldKeepTheFullWidthAndTrimTopAndBottom()
    {
        var cropped = PacketPhotoCrop.ToAspect(SampleImages.Jpeg(300, 400), CellAspect, maxWidthPx: 1000);

        var (width, height, _) = Inspect(cropped!);
        width.Should().Be(300);
        ((double)width / height).Should().BeApproximately(CellAspect, 0.01);
    }

    [Fact]
    public void ToAspect_ForAWidePhoto_ShouldKeepTheFullHeightAndTrimTheSides()
    {
        var cropped = PacketPhotoCrop.ToAspect(SampleImages.Jpeg(800, 100), CellAspect, maxWidthPx: 1000);

        var (width, height, _) = Inspect(cropped!);
        height.Should().Be(100);
        ((double)width / height).Should().BeApproximately(CellAspect, 0.01);
    }

    [Fact]
    public void ToAspect_ForALargePhoto_ShouldShrinkItToTheMaxWidth()
    {
        // The cell is a few inches wide; a 2560px photo would only bloat the PDF.
        var cropped = PacketPhotoCrop.ToAspect(SampleImages.Jpeg(2560, 1920), CellAspect, maxWidthPx: 800);

        var (width, height, _) = Inspect(cropped!);
        width.Should().Be(800);
        ((double)width / height).Should().BeApproximately(CellAspect, 0.01);
    }

    [Fact]
    public void ToAspect_ForAJpeg_ShouldReturnAJpeg()
    {
        var cropped = PacketPhotoCrop.ToAspect(SampleImages.Jpeg(300, 400), CellAspect, maxWidthPx: 1000);

        Inspect(cropped!).Format.Should().Be(MagickFormat.Jpeg);
    }

    [Fact]
    public void ToAspect_ForAPng_ShouldKeepItAPng()
    {
        // A screenshot or diagram keeps its hard edges.
        var cropped = PacketPhotoCrop.ToAspect(SampleImages.Png(300, 400), CellAspect, maxWidthPx: 1000);

        Inspect(cropped!).Format.Should().Be(MagickFormat.Png);
    }

    [Fact]
    public void ToAspect_WhenTheBytesAreNotAnImage_ShouldReturnNull()
    {
        var cropped = PacketPhotoCrop.ToAspect([1, 2, 3, 4, 5, 6, 7, 8], CellAspect, maxWidthPx: 1000);

        cropped.Should().BeNull();
    }

    [Fact]
    public void ToAspect_WithNullBytes_ShouldThrow()
    {
        var act = () => PacketPhotoCrop.ToAspect(null!, CellAspect, maxWidthPx: 1000);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ToAspect_WithANonPositiveAspect_ShouldThrow(double aspect)
    {
        var act = () => PacketPhotoCrop.ToAspect(SampleImages.Jpeg(10, 10), aspect, maxWidthPx: 1000);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
