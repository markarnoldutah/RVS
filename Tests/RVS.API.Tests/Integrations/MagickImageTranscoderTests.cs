using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Exercises the real <see cref="MagickImageTranscoder"/> against a genuine HEIC fixture
/// (issue <c>#508</c>): HEIC in ⇒ decodable JPEG out, dimensions preserved, oversized
/// images downscaled, and every non-HEIC / undecodable / oversized input left for the
/// caller to keep as-is.
/// </summary>
public class MagickImageTranscoderTests
{
    private static MagickImageTranscoder Build(ImageTranscodeOptions? options = null) =>
        new(MsOptions.Create(options ?? new ImageTranscodeOptions()),
            Mock.Of<ILogger<MagickImageTranscoder>>());

    // ── CanTranscode ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("image/heic")]
    [InlineData("image/heif")]
    [InlineData("IMAGE/HEIC")]
    [InlineData(" image/heif ")]
    public void CanTranscode_ForHeicOrHeif_ReturnsTrue(string contentType)
    {
        Build().CanTranscode(contentType).Should().BeTrue();
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("application/pdf")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CanTranscode_ForAnythingElse_ReturnsFalse(string? contentType)
    {
        Build().CanTranscode(contentType).Should().BeFalse();
    }

    // ── TranscodeToJpeg — happy path ────────────────────────────────────────

    [Fact]
    public void TranscodeToJpeg_ForRealHeic_ReturnsJpegWithPreservedDimensions()
    {
        var result = Build().TranscodeToJpeg(SampleImages.Heic96x64());

        result.Should().NotBeNull();
        result!.Width.Should().Be(96);
        result.Height.Should().Be(64);
        result.JpegBytes.Should().StartWith(new byte[] { 0xFF, 0xD8, 0xFF }, "output must be a JPEG the packet renderers can decode");
    }

    [Fact]
    public void TranscodeToJpeg_WhenImageExceedsMaxEdge_DownscalesProportionally()
    {
        var result = Build(new ImageTranscodeOptions { MaxEdgePixels = 32 })
            .TranscodeToJpeg(SampleImages.Heic96x64());

        result.Should().NotBeNull();
        result!.Width.Should().Be(32, "the longest edge is clamped to the cap");
        result.Height.Should().BeInRange(20, 22, "the 3:2 aspect ratio is preserved");
        result.JpegBytes.Should().StartWith(new byte[] { 0xFF, 0xD8, 0xFF });
    }

    [Fact]
    public void TranscodeToJpeg_WhenImageIsWithinMaxEdge_DoesNotUpscale()
    {
        var result = Build(new ImageTranscodeOptions { MaxEdgePixels = 4096 })
            .TranscodeToJpeg(SampleImages.Heic96x64());

        result.Should().NotBeNull();
        result!.Width.Should().Be(96);
        result.Height.Should().Be(64);
    }

    // ── TranscodeToJpeg — inputs the caller must keep as-is (null result) ────

    [Fact]
    public void TranscodeToJpeg_ForEmptyInput_ReturnsNull()
    {
        Build().TranscodeToJpeg([]).Should().BeNull();
    }

    [Fact]
    public void TranscodeToJpeg_ForBytesThatAreNotAnImage_ReturnsNull()
    {
        var junk = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 };

        Build().TranscodeToJpeg(junk).Should().BeNull();
    }

    [Fact]
    public void TranscodeToJpeg_WhenSourceExceedsMaxSourceBytes_ReturnsNullWithoutDecoding()
    {
        var result = Build(new ImageTranscodeOptions { MaxSourceBytes = 16 })
            .TranscodeToJpeg(SampleImages.Heic96x64());

        result.Should().BeNull();
    }

    [Fact]
    public void TranscodeToJpeg_ForNullSource_Throws()
    {
        var act = () => Build().TranscodeToJpeg(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
