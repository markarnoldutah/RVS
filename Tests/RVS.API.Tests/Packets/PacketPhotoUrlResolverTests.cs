using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Packets;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketPhotoUrlResolver"/> — the packet-composition step that turns a
/// <see cref="ServiceRequest"/>'s image attachments into time-limited read SAS URLs for
/// <see cref="RVS.Domain.Packets.PacketCompositionContext.PhotoUrls"/> (<c>Spec B-2</c> item 8,
/// <c>B-3</c>, <c>X-6</c>, issue <c>#433</c>).
///
/// The contract: SAS URLs are minted per call, never persisted, only for image attachments,
/// and with a lifetime long enough that an emailed packet's thumbnails are not broken by the
/// time a service department opens the mail.
/// </summary>
public class PacketPhotoUrlResolverTests
{
    private const string AttachmentsContainer = "rvs-attachments";

    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly PacketPhotoUrlResolver _sut;

    public PacketPhotoUrlResolverTests()
    {
        _blobMock
            .Setup(b => b.GenerateReadSasUrlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string blob, TimeSpan _, CancellationToken _) =>
                $"https://blob.example.com/{blob}?sv=2024&sp=r&sig=deadbeef");

        _sut = new PacketPhotoUrlResolver(_blobMock.Object, Mock.Of<ILogger<PacketPhotoUrlResolver>>());
    }

    private static ServiceRequest RequestWith(params ServiceRequestAttachmentEmbedded[] attachments) => new()
    {
        Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        TenantId = "ten_1",
        LocationId = "loc_1",
        Status = "New",
        Attachments = [.. attachments],
    };

    private static ServiceRequestAttachmentEmbedded Image(string id, string blobUri = "ten_1/sr_1/photo.jpg") => new()
    {
        AttachmentId = id,
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        BlobUri = blobUri,
    };

    // ── Guard clauses ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ResolveAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ShouldReturnAReadSasUrlForEachImageAttachment_KeyedByAttachmentId()
    {
        var request = RequestWith(
            Image("att_1", "ten_1/sr_1/one.jpg"),
            Image("att_2", "ten_1/sr_1/two.jpg"));

        var map = await _sut.ResolveAsync(request);

        map.Should().HaveCount(2);
        map["att_1"].Should().Be("https://blob.example.com/ten_1/sr_1/one.jpg?sv=2024&sp=r&sig=deadbeef");
        map["att_2"].Should().Be("https://blob.example.com/ten_1/sr_1/two.jpg?sv=2024&sp=r&sig=deadbeef");
    }

    [Fact]
    public async Task ResolveAsync_ShouldGenerateSasFromTheAttachmentsContainerAndTheStoredBlobUri()
    {
        var request = RequestWith(Image("att_1", "ten_1/sr_1/rear.jpg"));

        await _sut.ResolveAsync(request);

        _blobMock.Verify(
            b => b.GenerateReadSasUrlAsync(
                AttachmentsContainer, "ten_1/sr_1/rear.jpg", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ShouldRequestALifetimeThatSurvivesEmailDelivery_NotTheOneHourStaffDefault()
    {
        var request = RequestWith(Image("att_1"));

        await _sut.ResolveAsync(request);

        // Spec X-6: time-limited, but a packet emailed to a shop inbox may not be opened for
        // days. The staff-view default (1 hour) would render broken thumbnails; the packet
        // SAS must comfortably outlast a long weekend.
        _blobMock.Verify(
            b => b.GenerateReadSasUrlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<TimeSpan>(t => t >= TimeSpan.FromDays(2) && t <= TimeSpan.FromDays(7)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ShouldForwardTheCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var request = RequestWith(Image("att_1"));

        await _sut.ResolveAsync(request, cts.Token);

        _blobMock.Verify(
            b => b.GenerateReadSasUrlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), cts.Token),
            Times.Once);
    }

    // ── Filtering ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("audio/mp4")]
    [InlineData("application/pdf")]
    [InlineData("")]
    public async Task ResolveAsync_ShouldSkipNonImageNonVideoAttachments(string contentType)
    {
        var request = RequestWith(
            Image("att_img"),
            new ServiceRequestAttachmentEmbedded
            {
                AttachmentId = "att_other",
                FileName = "note",
                ContentType = contentType,
                BlobUri = "ten_1/sr_1/note",
            });

        var map = await _sut.ResolveAsync(request);

        map.Should().ContainKey("att_img");
        map.Should().NotContainKey("att_other");
    }

    [Fact]
    public async Task ResolveAsync_ShouldResolveAReadSasUrlForVideoAttachments()
    {
        // Videos need a resolved SAS URL too so the packet can link to them (issue #583) —
        // only their raster bytes are skipped, not their URL resolution.
        var request = RequestWith(new ServiceRequestAttachmentEmbedded
        {
            AttachmentId = "att_video",
            FileName = "walkaround.mp4",
            ContentType = "video/mp4",
            BlobUri = "ten_1/sr_1/walkaround.mp4",
        });

        var map = await _sut.ResolveAsync(request);

        map.Should().ContainKey("att_video");
        map["att_video"].Should().Be("https://blob.example.com/ten_1/sr_1/walkaround.mp4?sv=2024&sp=r&sig=deadbeef");
    }

    [Fact]
    public async Task ResolveAsync_ShouldSkipImageAttachmentsWithABlankBlobUri()
    {
        var request = RequestWith(Image("att_1", blobUri: "   "));

        var map = await _sut.ResolveAsync(request);

        map.Should().BeEmpty();
        _blobMock.Verify(
            b => b.GenerateReadSasUrlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_WhenThereAreNoImageAttachments_ShouldReturnAnEmptyMap()
    {
        var request = RequestWith(new ServiceRequestAttachmentEmbedded
        {
            AttachmentId = "att_voice",
            FileName = "voice.m4a",
            ContentType = "audio/mp4",
            BlobUri = "ten_1/sr_1/voice.m4a",
        });

        var map = await _sut.ResolveAsync(request);

        map.Should().BeEmpty();
    }

    // ── Never persisted, minted per call ──────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ShouldNotWriteTheSasUrlBackOntoTheAttachment()
    {
        var attachment = Image("att_1", "ten_1/sr_1/one.jpg");
        var request = RequestWith(attachment);

        await _sut.ResolveAsync(request);

        // The stored blob path is untouched — the SAS lives only in the returned map.
        attachment.BlobUri.Should().Be("ten_1/sr_1/one.jpg");
    }

    [Fact]
    public async Task ResolveAsync_CalledTwice_ShouldMintFreshUrlsEachTime()
    {
        var request = RequestWith(Image("att_1"));

        await _sut.ResolveAsync(request);
        await _sut.ResolveAsync(request);

        _blobMock.Verify(
            b => b.GenerateReadSasUrlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
