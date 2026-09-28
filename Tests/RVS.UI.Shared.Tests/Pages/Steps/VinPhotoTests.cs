using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using RVS.Blazor.Intake.Pages.Steps.Shared;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.Pages.Steps;

/// <summary>
/// Tests for <see cref="VinPhoto"/> — reading Step 3's VIN photo for extraction (issue #736).
/// </summary>
public class VinPhotoTests
{
    private static byte[] Bytes(int length, byte seed = 1) =>
        Enumerable.Range(0, length).Select(i => (byte)(seed + i)).ToArray();

    private static Func<IBrowserFile, Task<IBrowserFile>> ResizeTo(IBrowserFile resized) => _ => Task.FromResult(resized);

    private static readonly Func<IBrowserFile, Task<IBrowserFile>> ResizeFails =
        _ => throw new InvalidOperationException("The browser could not decode the image.");

    [Fact]
    public async Task ReadAsync_WhenTheBrowserResizes_ShouldSendTheWholeResizedJpeg()
    {
        // The browser stream returns a chunk per read. The previous code read once, so a photo
        // bigger than one chunk went out as its first chunk and a buffer of zeros.
        var original = new FakeBrowserFile("IMG_0001.jpg", "image/jpeg", Bytes(9 * 1024 * 1024));
        var resizedBytes = Bytes(200 * 1024, seed: 7);
        var resized = new FakeBrowserFile("IMG_0001.jpg", "image/jpeg", resizedBytes);

        var photo = await VinPhoto.ReadAsync(original, ResizeTo(resized));

        photo.Should().NotBeNull();
        photo!.ContentType.Should().Be("image/jpeg");
        photo.Bytes.Should().Equal(resizedBytes);
    }

    [Fact]
    public async Task ReadAsync_ShouldAskTheBrowserForAJpegNoLargerThanTheMaxEdge()
    {
        var original = new FakeBrowserFile("IMG_0001.jpg", "image/jpeg", Bytes(1024));
        IBrowserFile? asked = null;

        await VinPhoto.ReadAsync(original, f =>
        {
            asked = f;
            return Task.FromResult<IBrowserFile>(new FakeBrowserFile("x.jpg", "image/jpeg", Bytes(10)));
        });

        asked.Should().BeSameAs(original);
        VinPhoto.MaxEdgePx.Should().BeInRange(1600, 4096);
    }

    [Fact]
    public async Task ReadAsync_WhenResizeFailsAndTheOriginalFits_ShouldSendTheWholeOriginal()
    {
        var originalBytes = Bytes(3 * 1024 * 1024);
        var original = new FakeBrowserFile("IMG_0001.png", "image/png", originalBytes);

        var photo = await VinPhoto.ReadAsync(original, ResizeFails);

        photo.Should().NotBeNull();
        photo!.ContentType.Should().Be("image/png");
        photo.Bytes.Should().Equal(originalBytes);
    }

    [Fact]
    public async Task ReadAsync_WhenResizeFailsAndTheOriginalIsOverTheApiLimit_ShouldReturnNull()
    {
        // The API answers 413 above its limit; better to say so here than send it.
        var original = new FakeBrowserFile("IMG_0001.jpg", "image/jpeg", Bytes((int)VinPhoto.MaxUploadBytes + 1));

        var photo = await VinPhoto.ReadAsync(original, ResizeFails);

        photo.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_WhenTheResizedImageIsEmpty_ShouldFallBackToTheOriginal()
    {
        var originalBytes = Bytes(1024);
        var original = new FakeBrowserFile("IMG_0001.jpg", "image/jpeg", originalBytes);

        var photo = await VinPhoto.ReadAsync(original, ResizeTo(new FakeBrowserFile("x.jpg", "image/jpeg", [])));

        photo!.Bytes.Should().Equal(originalBytes);
    }

    [Fact]
    public async Task ReadAsync_WhenTheOriginalHasNoContentType_ShouldInferItFromTheExtension()
    {
        var original = new FakeBrowserFile("IMG_0001.PNG", "", Bytes(1024));

        var photo = await VinPhoto.ReadAsync(original, ResizeFails);

        photo!.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task ReadAsync_WhenTheOriginalHasNoContentTypeOrKnownExtension_ShouldAssumeJpeg()
    {
        var original = new FakeBrowserFile("capture", "", Bytes(1024));

        var photo = await VinPhoto.ReadAsync(original, ResizeFails);

        photo!.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task ReadAsync_WhenTheResizedImageCannotBeRead_ShouldFallBackToTheOriginal()
    {
        var original = new FakeBrowserFile("IMG_0001.jpg", "image/jpeg", Bytes(1024));

        var photo = await VinPhoto.ReadAsync(original, ResizeTo(new ThrowingBrowserFile()));

        photo!.Bytes.Should().HaveCount(1024);
    }

    [Fact]
    public async Task ReadAsync_WhenNeitherImageCanBeRead_ShouldReturnNull()
    {
        // Previously this escaped the handler, and the customer saw nothing happen.
        var photo = await VinPhoto.ReadAsync(new ThrowingBrowserFile(), ResizeFails);

        photo.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_WithNullFile_ShouldThrow()
    {
        var act = () => VinPhoto.ReadAsync(null!, ResizeFails);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void DataUrl_ShouldEncodeTheBytesWithTheirContentType()
    {
        var photo = new VinPhotoImage([1, 2, 3], "image/jpeg");

        photo.DataUrl.Should().Be("data:image/jpeg;base64,AQID");
        photo.Base64.Should().Be("AQID");
    }

    private sealed class ThrowingBrowserFile : IBrowserFile
    {
        public string Name => "broken.jpg";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 10;
        public string ContentType => "image/jpeg";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            throw new IOException("The file reference is no longer valid.");
    }
}
