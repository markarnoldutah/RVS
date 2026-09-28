using FluentAssertions;
using RVS.Blazor.Intake.Pages.Steps.Shared;

namespace RVS.UI.Shared.Tests.Pages.Steps;

/// <summary>
/// Tests for <see cref="CameraCapture"/> — turning what the in-page camera captured into a Step 7
/// attachment (issue #736).
/// </summary>
public class CameraCaptureTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 27, 10, 15, 30, TimeSpan.Zero);

    [Theory]
    [InlineData("video/webm;codecs=vp8,opus", "video/webm")]
    [InlineData("video/webm", "video/webm")]
    [InlineData("video/mp4;codecs=avc1,mp4a", "video/mp4")]
    [InlineData("VIDEO/MP4", "video/mp4")]
    [InlineData("video/x-matroska;codecs=avc1", "video/webm")]
    [InlineData("image/jpeg", "image/jpeg")]
    public void NormalizeContentType_ShouldDropParametersAndMapToAnUploadableType(string recorded, string expected)
    {
        // The API's allowlist compares bare types; "video/webm;codecs=vp8" would be refused.
        CameraCapture.NormalizeContentType(recorded).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeContentType_WhenNothingWasReported_ShouldAssumeWebm(string? recorded)
    {
        // Chrome's recorder, the one that leaves it blank, records WebM.
        CameraCapture.NormalizeContentType(recorded).Should().Be("video/webm");
    }

    [Theory]
    [InlineData("image/jpeg", "photo-20260927-101530.jpg")]
    [InlineData("video/mp4", "video-20260927-101530.mp4")]
    [InlineData("video/webm", "video-20260927-101530.webm")]
    public void FileName_ShouldNameTheCaptureByKindAndTime(string contentType, string expected)
    {
        CameraCapture.FileName(contentType, At).Should().Be(expected);
    }

    [Fact]
    public void MaxVideoSeconds_ShouldFitTheSizeLimitAtTheRecordingBitrate()
    {
        var seconds = CameraCapture.MaxVideoSeconds(maxFileSizeMb: 25);

        var bytesAtCap = seconds * (CameraCapture.VideoBitsPerSecond + CameraCapture.AudioBitsPerSecond) / 8.0;
        bytesAtCap.Should().BeLessThan(25 * 1024 * 1024);
        seconds.Should().BeGreaterThanOrEqualTo(60);
        (seconds % 5).Should().Be(0);
    }

    [Fact]
    public void MaxVideoSeconds_WithATinyLimit_ShouldStillAllowAShortClip()
    {
        CameraCapture.MaxVideoSeconds(maxFileSizeMb: 1).Should().Be(5);
    }

    [Fact]
    public void ToAttachment_ForAPhoto_ShouldCarryTheBytesThumbnailAndName()
    {
        var media = new CapturedMedia([1, 2, 3], "image/jpeg", "data:image/jpeg;base64,AA==");

        var result = CameraCapture.ToAttachment(media, maxAttachments: 10, attachedCount: 2, maxFileSizeMb: 25, At);

        result.Error.Should().BeNull();
        result.Attachment.Should().NotBeNull();
        result.Attachment!.FileName.Should().Be("photo-20260927-101530.jpg");
        result.Attachment.ContentType.Should().Be("image/jpeg");
        result.Attachment.SizeBytes.Should().Be(3);
        result.Attachment.FileData.Should().Equal(1, 2, 3);
        result.Attachment.ThumbnailDataUrl.Should().Be("data:image/jpeg;base64,AA==");
    }

    [Fact]
    public void ToAttachment_WhenTheListIsFull_ShouldRefuseIt()
    {
        var media = new CapturedMedia([1], "image/jpeg", null);

        var result = CameraCapture.ToAttachment(media, maxAttachments: 3, attachedCount: 3, maxFileSizeMb: 25, At);

        result.Attachment.Should().BeNull();
        result.Error.Should().Be("Maximum 3 files allowed.");
    }

    [Fact]
    public void ToAttachment_WhenTheVideoIsOverTheLimit_ShouldSuggestAShorterClip()
    {
        var media = new CapturedMedia(new byte[1024 * 1024 + 1], "video/mp4", null);

        var result = CameraCapture.ToAttachment(media, maxAttachments: 10, attachedCount: 0, maxFileSizeMb: 1, At);

        result.Attachment.Should().BeNull();
        result.Error.Should().Be("That video is over the 1 MB limit. Please record a shorter clip.");
    }

    [Fact]
    public void ToAttachment_WhenThePhotoIsOverTheLimit_ShouldSaySo()
    {
        var media = new CapturedMedia(new byte[1024 * 1024 + 1], "image/jpeg", null);

        var result = CameraCapture.ToAttachment(media, maxAttachments: 10, attachedCount: 0, maxFileSizeMb: 1, At);

        result.Error.Should().Be("That photo is over the 1 MB limit.");
    }

    [Fact]
    public void ToAttachment_WhenNothingWasCaptured_ShouldAskForAnotherTry()
    {
        var media = new CapturedMedia([], "video/webm", null);

        var result = CameraCapture.ToAttachment(media, maxAttachments: 10, attachedCount: 0, maxFileSizeMb: 25, At);

        result.Attachment.Should().BeNull();
        result.Error.Should().Be("Nothing was captured. Please try again.");
    }

    [Fact]
    public void ToAttachment_WithNullMedia_ShouldThrow()
    {
        var act = () => CameraCapture.ToAttachment(null!, 10, 0, 25, At);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(CameraStartResult.PermissionDenied, "Camera access is blocked")]
    [InlineData(CameraStartResult.NoCamera, "No camera was found")]
    [InlineData(CameraStartResult.Unsupported, "can't use the camera")]
    [InlineData(CameraStartResult.Failed, "couldn't start")]
    public void StartFailureMessage_ShouldExplainEachFailure(CameraStartResult result, string expected)
    {
        CameraCapture.StartFailureMessage(result).Should().Contain(expected);
    }

    [Fact]
    public void StartFailureMessage_WhenStarted_ShouldBeNull()
    {
        CameraCapture.StartFailureMessage(CameraStartResult.Started).Should().BeNull();
    }
}
