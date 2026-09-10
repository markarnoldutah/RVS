using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Exercises the real <see cref="MagickImageTranscoder"/> against genuine fixtures: HEIC in ⇒
/// decodable JPEG out with dimensions preserved (issue <c>#508</c>); and, since <c>#562</c>, a
/// full-resolution JPEG downscaled and shrunk, a PNG kept as PNG, an already-small image left
/// untouched, and every non-raster / undecodable / oversized input left for the caller to keep
/// as-is.
/// </summary>
public class MagickImageTranscoderTests
{
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    private static MagickImageTranscoder Build(ImageTranscodeOptions? options = null) =>
        new(MsOptions.Create(options ?? new ImageTranscodeOptions()),
            Mock.Of<ILogger<MagickImageTranscoder>>());

    // ── CanNormalize ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("image/heic")]
    [InlineData("image/heif")]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("IMAGE/JPEG")]
    [InlineData(" image/png ")]
    public void CanNormalize_ForAnySupportedRaster_ReturnsTrue(string contentType)
    {
        Build().CanNormalize(contentType).Should().BeTrue();
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("video/mp4")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CanNormalize_ForAnythingElse_ReturnsFalse(string? contentType)
    {
        Build().CanNormalize(contentType).Should().BeFalse();
    }

    // ── Normalize — HEIC path, unchanged from #508 ─────────────────────────────

    [Fact]
    public void Normalize_ForRealHeic_ReturnsJpegWithPreservedDimensions()
    {
        var result = Build().Normalize(SampleImages.Heic96x64(), "image/heic");

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/jpeg");
        result.Width.Should().Be(96);
        result.Height.Should().Be(64);
        result.Bytes.Should().StartWith(JpegMagic, "output must be a JPEG the packet renderers can decode");
    }

    [Fact]
    public void Normalize_WhenHeicJpegIsLargerThanTheHeicOriginal_StillConverts()
    {
        // HEIC is more space-efficient than JPEG, so the 96×64 fixture grows on re-encode.
        // The never-grow guard must not fire here — a kept .heic does not render off Apple
        // devices, which is the whole reason #508 exists.
        var heic = SampleImages.Heic96x64();

        var result = Build().Normalize(heic, "image/heic");

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/jpeg");
        result.Bytes.Length.Should().BeGreaterThan(heic.Length);
    }

    [Fact]
    public void Normalize_WhenHeicExceedsMaxEdge_DownscalesProportionally()
    {
        var result = Build(new ImageTranscodeOptions { MaxEdgePixels = 32 })
            .Normalize(SampleImages.Heic96x64(), "image/heic");

        result.Should().NotBeNull();
        result!.Width.Should().Be(32, "the longest edge is clamped to the cap");
        result.Height.Should().BeInRange(20, 22, "the 3:2 aspect ratio is preserved");
    }

    // ── Normalize — full-resolution JPEG, the #562 common case ─────────────────

    [Fact]
    public void Normalize_ForFullResolutionJpeg_DownscalesToMaxEdgeAndShrinks()
    {
        var source = SampleImages.Jpeg(4000, 3000, quality: 92);

        var result = Build().Normalize(source, "image/jpeg");

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/jpeg");
        result.Width.Should().Be(1600, "the default cap is 1600 on the longest edge");
        result.Height.Should().Be(1200, "the 4:3 aspect ratio is preserved");
        result.Bytes.Length.Should().BeLessThan(source.Length, "a sixfold-smaller pixel count re-encodes materially smaller");
        result.Bytes.Should().StartWith(JpegMagic);
    }

    [Fact]
    public void Normalize_ForJpegWithinMaxEdgeButHighQuality_ReEncodesAtConfiguredQuality()
    {
        // Within the edge cap, so no downscale — the saving comes from dropping quality 95 → 82.
        var source = SampleImages.Jpeg(1200, 900, quality: 95);

        var result = Build().Normalize(source, "image/jpeg");

        result.Should().NotBeNull();
        result!.Width.Should().Be(1200);
        result.Height.Should().Be(900);
        result.Bytes.Length.Should().BeLessThan(source.Length);
    }

    // ── Normalize — PNG stays PNG ─────────────────────────────────────────────

    [Fact]
    public void Normalize_ForPng_KeepsPngFormatAndDownscales()
    {
        var source = SampleImages.Png(3000, 2000);

        var result = Build().Normalize(source, "image/png");

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/png", "a PNG is a screenshot / diagram — JPEG artifacts would show on hard edges");
        result.Width.Should().Be(1600);
        result.Height.Should().BeInRange(1066, 1067);
        result.Bytes.Should().StartWith(PngMagic);
        result.Bytes.Length.Should().BeLessThan(source.Length);
    }

    // ── Normalize — never grow an already-web-safe image ──────────────────────

    [Fact]
    public void Normalize_ForSmallLowQualityJpeg_KeepsOriginalWhenReEncodeWouldGrowIt()
    {
        // Already under the edge cap and encoded far below quality 82, so re-encoding at 82
        // inflates it. The caller must keep the original: return null.
        var source = SampleImages.Jpeg(300, 300, quality: 20);

        var result = Build().Normalize(source, "image/jpeg");

        result.Should().BeNull();
    }

    // ── Normalize — inputs the caller must keep as-is (null result) ───────────

    [Fact]
    public void Normalize_ForEmptyInput_ReturnsNull()
    {
        Build().Normalize([], "image/jpeg").Should().BeNull();
    }

    [Fact]
    public void Normalize_ForBytesThatAreNotAnImage_ReturnsNull()
    {
        var junk = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 };

        Build().Normalize(junk, "image/jpeg").Should().BeNull();
    }

    [Fact]
    public void Normalize_WhenSourceExceedsMaxSourceBytes_ReturnsNullWithoutDecoding()
    {
        var result = Build(new ImageTranscodeOptions { MaxSourceBytes = 16 })
            .Normalize(SampleImages.Heic96x64(), "image/heic");

        result.Should().BeNull();
    }

    [Fact]
    public void Normalize_ForNullSource_Throws()
    {
        var act = () => Build().Normalize(null!, "image/jpeg");

        act.Should().Throw<ArgumentNullException>();
    }
}
