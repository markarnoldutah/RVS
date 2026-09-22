using FluentAssertions;
using RVS.UI.Shared.Components;

namespace RVS.UI.Shared.Tests.Components;

/// <summary>
/// Tests for <see cref="AttachmentPreview"/> — the manager SR detail's attachment tiles (issue #699).
/// </summary>
public class AttachmentPreviewTests
{
    // ── Classify ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("IMAGE/JPEG")]
    public void Classify_BrowserRenderableImage_ReturnsImage(string contentType)
    {
        AttachmentPreview.Classify(contentType).Should().Be(AttachmentPreviewKind.Image);
    }

    [Theory]
    [InlineData("image/heic")]
    [InlineData("image/heif")]
    [InlineData("image/HEIC")]
    public void Classify_HeicOrHeif_ReturnsFile(string contentType)
    {
        // Most browsers off Apple cannot draw HEIC, so it gets an icon, not a broken <img>.
        AttachmentPreview.Classify(contentType).Should().Be(AttachmentPreviewKind.File);
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/quicktime")]
    [InlineData("Video/MP4")]
    public void Classify_Video_ReturnsVideo(string contentType)
    {
        AttachmentPreview.Classify(contentType).Should().Be(AttachmentPreviewKind.Video);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("APPLICATION/PDF")]
    public void Classify_Pdf_ReturnsPdf(string contentType)
    {
        AttachmentPreview.Classify(contentType).Should().Be(AttachmentPreviewKind.Pdf);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("application/octet-stream")]
    [InlineData("text/plain")]
    public void Classify_AnythingElse_ReturnsFile(string? contentType)
    {
        AttachmentPreview.Classify(contentType).Should().Be(AttachmentPreviewKind.File);
    }

    // ── VideoThumbnailSrc ────────────────────────────────────────────────────

    [Fact]
    public void VideoThumbnailSrc_AppendsMediaFragmentSoTheFirstFrameIsShown()
    {
        var src = AttachmentPreview.VideoThumbnailSrc("https://blob.test/c/v.mp4?sv=1&sig=abc");

        src.Should().Be("https://blob.test/c/v.mp4?sv=1&sig=abc#t=0.1");
    }

    [Fact]
    public void VideoThumbnailSrc_WhenUrlAlreadyHasAFragment_ReplacesIt()
    {
        var src = AttachmentPreview.VideoThumbnailSrc("https://blob.test/c/v.mp4?sig=abc#t=5");

        src.Should().Be("https://blob.test/c/v.mp4?sig=abc#t=0.1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VideoThumbnailSrc_WhenUrlIsBlank_ShouldThrowArgumentException(string? url)
    {
        var act = () => AttachmentPreview.VideoThumbnailSrc(url!);

        act.Should().Throw<ArgumentException>();
    }
}
