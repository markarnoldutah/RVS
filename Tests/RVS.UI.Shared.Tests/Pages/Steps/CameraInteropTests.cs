using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RVS.Blazor.Intake.Pages.Steps.Shared;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.Pages.Steps;

/// <summary>
/// Tests for <see cref="CameraInterop"/> — the .NET side of the in-page camera in
/// <c>wwwroot/js/interop.js</c> (issue #736).
/// </summary>
public class CameraInteropTests
{
    private readonly ScriptedJSRuntime _js = new();

    [Fact]
    public void Constructor_WithNullJsRuntime_ShouldThrow()
    {
        var act = () => new CameraInterop(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("started", CameraStartResult.Started)]
    [InlineData("denied", CameraStartResult.PermissionDenied)]
    [InlineData("no-camera", CameraStartResult.NoCamera)]
    [InlineData("unsupported", CameraStartResult.Unsupported)]
    [InlineData("failed", CameraStartResult.Failed)]
    [InlineData("something new", CameraStartResult.Failed)]
    public async Task StartAsync_ShouldMapWhatTheBrowserReports(string reported, CameraStartResult expected)
    {
        _js.Handlers[CameraInterop.StartFunction] = _ => reported;

        var result = await new CameraInterop(_js).StartAsync(default, CameraMode.Photo);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(CameraMode.Photo, "photo")]
    [InlineData(CameraMode.Video, "video")]
    public async Task StartAsync_ShouldTellTheBrowserTheMode(CameraMode mode, string expected)
    {
        // Video asks for the microphone too; a photo must not.
        _js.Handlers[CameraInterop.StartFunction] = _ => "started";

        await new CameraInterop(_js).StartAsync(default, mode);

        _js.Calls.Single().Args[1].Should().Be(expected);
    }

    [Fact]
    public async Task StartAsync_WhenTheCallFails_ShouldReportFailed()
    {
        _js.Handlers[CameraInterop.StartFunction] = _ => new JSException("boom");

        var result = await new CameraInterop(_js).StartAsync(default, CameraMode.Photo);

        result.Should().Be(CameraStartResult.Failed);
    }

    [Fact]
    public async Task TakePhotoAsync_ShouldReturnAJpegWithItsThumbnail()
    {
        _js.Handlers[CameraInterop.TakePhotoFunction] = _ => new byte[] { 9, 8, 7 };
        _js.Handlers[CameraInterop.ThumbnailFunction] = _ => "data:image/jpeg;base64,AA==";

        var photo = await new CameraInterop(_js).TakePhotoAsync(default);

        photo.Should().NotBeNull();
        photo!.Bytes.Should().Equal(9, 8, 7);
        photo.ContentType.Should().Be("image/jpeg");
        photo.ThumbnailDataUrl.Should().Be("data:image/jpeg;base64,AA==");
    }

    [Fact]
    public async Task TakePhotoAsync_ShouldAskForAPhotoNoLargerThanTheMaxEdge()
    {
        _js.Handlers[CameraInterop.TakePhotoFunction] = _ => new byte[] { 1 };

        await new CameraInterop(_js).TakePhotoAsync(default);

        _js.Calls.First().Args[1].Should().Be(CameraCapture.PhotoMaxEdgePx);
    }

    [Fact]
    public async Task TakePhotoAsync_WhenNothingCameBack_ShouldReturnNull()
    {
        _js.Handlers[CameraInterop.TakePhotoFunction] = _ => Array.Empty<byte>();

        var photo = await new CameraInterop(_js).TakePhotoAsync(default);

        photo.Should().BeNull();
    }

    [Fact]
    public async Task TakePhotoAsync_WhenTheThumbnailFails_ShouldStillReturnThePhoto()
    {
        _js.Handlers[CameraInterop.TakePhotoFunction] = _ => new byte[] { 1 };
        _js.Handlers[CameraInterop.ThumbnailFunction] = _ => new JSException("canvas tainted");

        var photo = await new CameraInterop(_js).TakePhotoAsync(default);

        photo!.ThumbnailDataUrl.Should().BeNull();
    }

    [Fact]
    public async Task TakePhotoAsync_WhenTheCallFails_ShouldReturnNull()
    {
        _js.Handlers[CameraInterop.TakePhotoFunction] = _ => new JSException("boom");

        var photo = await new CameraInterop(_js).TakePhotoAsync(default);

        photo.Should().BeNull();
    }

    [Fact]
    public async Task StartRecordingAsync_ShouldPassTheRecordingBitrates()
    {
        _js.Handlers[CameraInterop.StartRecordingFunction] = _ => true;

        var started = await new CameraInterop(_js).StartRecordingAsync();

        started.Should().BeTrue();
        _js.Calls.Single().Args.Should().Equal(CameraCapture.VideoBitsPerSecond, CameraCapture.AudioBitsPerSecond);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenTheCallFails_ShouldReturnFalse()
    {
        _js.Handlers[CameraInterop.StartRecordingFunction] = _ => new JSException("no MediaRecorder");

        var started = await new CameraInterop(_js).StartRecordingAsync();

        started.Should().BeFalse();
    }

    [Fact]
    public async Task StopRecordingAsync_ShouldReturnTheClipWithAnUploadableType()
    {
        _js.Handlers[CameraInterop.StopRecordingFunction] = _ => new byte[] { 4, 5 };
        _js.Handlers[CameraInterop.RecordingTypeFunction] = _ => "video/webm;codecs=vp8,opus";

        var clip = await new CameraInterop(_js).StopRecordingAsync();

        clip.Should().NotBeNull();
        clip!.Bytes.Should().Equal(4, 5);
        clip.ContentType.Should().Be("video/webm");
        clip.ThumbnailDataUrl.Should().BeNull();
    }

    [Fact]
    public async Task StopRecordingAsync_WhenNothingWasRecorded_ShouldReturnNull()
    {
        _js.Handlers[CameraInterop.StopRecordingFunction] = _ => null;

        var clip = await new CameraInterop(_js).StopRecordingAsync();

        clip.Should().BeNull();
    }

    [Fact]
    public async Task StopAsync_WhenTheCallFails_ShouldNotThrow()
    {
        // Runs on dispose; a camera that is already gone must not break leaving the step.
        _js.Handlers[CameraInterop.StopFunction] = _ => new JSException("already stopped");

        var act = () => new CameraInterop(_js).StopAsync();

        await act.Should().NotThrowAsync();
    }
}
