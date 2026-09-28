using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace RVS.Blazor.Intake.Pages.Steps.Shared;

/// <summary>
/// The .NET side of the in-page camera in <c>wwwroot/js/interop.js</c> (issue #736). Every call
/// is best-effort: a failure comes back as a result the overlay can show, never an exception.
/// </summary>
public sealed class CameraInterop
{
    public const string StartFunction = "rvs_cameraStart";
    public const string TakePhotoFunction = "rvs_cameraTakePhoto";
    public const string ThumbnailFunction = "rvs_cameraThumbnail";
    public const string StartRecordingFunction = "rvs_cameraStartRecording";
    public const string StopRecordingFunction = "rvs_cameraStopRecording";
    public const string RecordingTypeFunction = "rvs_cameraRecordingType";
    public const string StopFunction = "rvs_cameraStop";

    /// <summary>JPEG quality of a photo, 0–1.</summary>
    private const double PhotoQuality = 0.9;

    /// <summary>Longest edge of a photo's thumbnail; drawn at 48px, so this covers 3x screens.</summary>
    private const int ThumbnailMaxEdgePx = 144;

    private readonly IJSRuntime _js;

    public CameraInterop(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        _js = js;
    }

    /// <summary>Starts the camera into <paramref name="video"/>; video mode also takes the microphone.</summary>
    public async Task<CameraStartResult> StartAsync(ElementReference video, CameraMode mode)
    {
        try
        {
            var reported = await _js.InvokeAsync<string>(StartFunction, video, mode == CameraMode.Video ? "video" : "photo");
            return reported switch
            {
                "started" => CameraStartResult.Started,
                "denied" => CameraStartResult.PermissionDenied,
                "no-camera" => CameraStartResult.NoCamera,
                "unsupported" => CameraStartResult.Unsupported,
                _ => CameraStartResult.Failed
            };
        }
        catch (JSException)
        {
            return CameraStartResult.Failed;
        }
    }

    /// <summary>Takes a JPEG of what <paramref name="video"/> is showing; <c>null</c> when none could be taken.</summary>
    public async Task<CapturedMedia?> TakePhotoAsync(ElementReference video)
    {
        byte[]? bytes;
        try
        {
            bytes = await _js.InvokeAsync<byte[]?>(TakePhotoFunction, video, CameraCapture.PhotoMaxEdgePx, PhotoQuality);
        }
        catch (JSException)
        {
            return null;
        }

        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        string? thumbnail;
        try
        {
            thumbnail = await _js.InvokeAsync<string?>(ThumbnailFunction, ThumbnailMaxEdgePx);
        }
        catch (JSException)
        {
            thumbnail = null;
        }

        return new CapturedMedia(bytes, "image/jpeg", thumbnail);
    }

    /// <summary>Starts recording what the camera is showing, with sound; <c>false</c> when it could not.</summary>
    public async Task<bool> StartRecordingAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>(StartRecordingFunction, CameraCapture.VideoBitsPerSecond, CameraCapture.AudioBitsPerSecond);
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <summary>Stops recording and returns the clip; <c>null</c> when nothing was recorded.</summary>
    public async Task<CapturedMedia?> StopRecordingAsync()
    {
        try
        {
            var bytes = await _js.InvokeAsync<byte[]?>(StopRecordingFunction);
            if (bytes is not { Length: > 0 })
            {
                return null;
            }

            var recorded = await _js.InvokeAsync<string?>(RecordingTypeFunction);
            return new CapturedMedia(bytes, CameraCapture.NormalizeContentType(recorded), null);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>Stops any recording and releases the camera and microphone.</summary>
    public async Task StopAsync()
    {
        try
        {
            await _js.InvokeVoidAsync(StopFunction);
        }
        catch (JSException)
        {
            // Already stopped, or never started.
        }
    }
}
