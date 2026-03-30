using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

public class IntakeApiClientTests
{
    private static IntakeApiClient CreateClient(HttpClient httpClient) => new(httpClient);

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new IntakeApiClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── GetUploadSasAsync ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUploadSasAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetUploadSasAsync(slug!, "sr-1", "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUploadSasAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetUploadSasAsync("slug", srId!, "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUploadSasAsync_WhenFileNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? fileName)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetUploadSasAsync("slug", "sr-1", fileName!, "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUploadSasAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetUploadSasAsync("slug", "sr-1", "photo.jpg", contentType!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetUploadSasAsync_WhenApiReturns200_ShouldDeserializeResponse()
    {
        var expected = new AttachmentUploadSasResponseDto
        {
            SasUrl = "https://blob.test/container/blob?sig=abc",
            BlobName = "ten_1/sr_1/guid_photo.jpg",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var result = await sut.GetUploadSasAsync("my-slug", "sr-1", "photo.jpg", "image/jpeg");

        result.SasUrl.Should().Be(expected.SasUrl);
        result.BlobName.Should().Be(expected.BlobName);
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("sr-1");
        handler.LastRequest.RequestUri.ToString().Should().Contain("fileName=photo.jpg");
        handler.LastRequest.RequestUri.ToString().Should().Contain("contentType=image%2Fjpeg");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    // ── ConfirmUploadAsync ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConfirmUploadAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.ConfirmUploadAsync(slug!, "sr-1", new AttachmentConfirmRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConfirmUploadAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.ConfirmUploadAsync("slug", srId!, new AttachmentConfirmRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.ConfirmUploadAsync("slug", "sr-1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenApiReturns201_ShouldDeserializeAttachmentDto()
    {
        var expected = new AttachmentDto
        {
            AttachmentId = "att-1",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 12345,
            BlobUri = "ten_1/sr_1/guid_photo.jpg",
            CreatedAtUtc = DateTime.UtcNow
        };

        var handler = new FakeHttpHandler(HttpStatusCode.Created, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var confirmRequest = new AttachmentConfirmRequestDto
        {
            BlobName = "ten_1/sr_1/guid_photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 12345
        };

        var result = await sut.ConfirmUploadAsync("my-slug", "sr-1", confirmRequest);

        result.AttachmentId.Should().Be(expected.AttachmentId);
        result.FileName.Should().Be(expected.FileName);
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("sr-1");
        handler.LastRequest.RequestUri.ToString().Should().Contain("attachments/confirm");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task GetUploadSasAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, new { message = "Bad request" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var act = () => sut.GetUploadSasAsync("slug", "sr-1", "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound, new { message = "Not found" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var act = () => sut.ConfirmUploadAsync("slug", "sr-1", new AttachmentConfirmRequestDto());

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── DecodeVinAsync ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DecodeVinAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.DecodeVinAsync(slug!, "1RGDE4428R1000001");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DecodeVinAsync_WhenVinIsNullOrWhiteSpace_ShouldThrowArgumentException(string? vin)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.DecodeVinAsync("slug", vin!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturns200_ShouldDeserializeResponse()
    {
        var expected = new VinDecodeResponseDto
        {
            Vin = "1RGDE4428R1000001",
            Manufacturer = "Grand Design",
            Model = "Momentum 395MS",
            Year = 2024
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var result = await sut.DecodeVinAsync("my-slug", "1RGDE4428R1000001");

        result.Should().NotBeNull();
        result!.Vin.Should().Be(expected.Vin);
        result.Manufacturer.Should().Be(expected.Manufacturer);
        result.Model.Should().Be(expected.Model);
        result.Year.Should().Be(expected.Year);
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("vin-decode");
        handler.LastRequest.Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturns404_ShouldReturnNull()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound, new { message = "VIN not found" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var result = await sut.DecodeVinAsync("slug", "1RGDE4428R1000001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, new { message = "Server error" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var act = () => sut.DecodeVinAsync("slug", "1RGDE4428R1000001");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── Test helper ──────────────────────────────────────────────────────

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpHandler(HttpStatusCode statusCode, object responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = JsonContent.Create(_responseBody, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
            return Task.FromResult(response);
        }
    }
}
