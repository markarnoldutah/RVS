using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

public class ServiceRequestApiClientTests
{
    private static ServiceRequestApiClient CreateClient(HttpClient httpClient) => new(httpClient);

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new ServiceRequestApiClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── SetStatusNoteAsync (Spec C-9) ────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetStatusNoteAsync_WhenDealershipIdIsBlank_ShouldThrowArgumentException(string? dealershipId)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.SetStatusNoteAsync(dealershipId!, "sr-1", "note");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetStatusNoteAsync_WhenServiceRequestIdIsBlank_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.SetStatusNoteAsync("dlr-1", srId!, "note");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetStatusNoteAsync_ShouldPutToStatusNoteRouteWithNoteBody_AndDeserializeResponse()
    {
        var expected = new ServiceRequestDetailResponseDto
        {
            Id = "sr-1",
            TenantId = "ten-1",
            Status = "WaitingOnParts",
            LocationId = "loc-1",
            CustomerStatusNote = new CustomerStatusNoteDto
            {
                Text = "Slide motor on back order, ETA Friday.",
                UpdatedAtUtc = DateTime.UtcNow
            }
        };
        var handler = new CapturingHandler(HttpStatusCode.OK, expected);
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var result = await sut.SetStatusNoteAsync("dlr 1", "sr/1", "Slide motor on back order, ETA Friday.");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be("/api/dealerships/dlr%201/service-requests/sr%2F1/status-note");
        handler.LastRequestBody.Should().Contain("Slide motor on back order");
        result.CustomerStatusNote.Should().NotBeNull();
        result.CustomerStatusNote!.Text.Should().Be("Slide motor on back order, ETA Friday.");
    }

    [Fact]
    public async Task SetStatusNoteAsync_WithNullNote_ShouldSendNullNoteBody()
    {
        var expected = new ServiceRequestDetailResponseDto
        {
            Id = "sr-1", TenantId = "ten-1", Status = "New", LocationId = "loc-1"
        };
        var handler = new CapturingHandler(HttpStatusCode.OK, expected);
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var result = await sut.SetStatusNoteAsync("dlr-1", "sr-1", null);

        handler.LastRequestBody.Should().Contain("\"note\":null");
        result.CustomerStatusNote.Should().BeNull();
    }

    [Fact]
    public async Task SetStatusNoteAsync_WhenApiReturns422_ShouldThrow()
    {
        var handler = new CapturingHandler(HttpStatusCode.UnprocessableEntity, new { message = "invalid" });
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.SetStatusNoteAsync("dlr-1", "sr-1", new string('a', 400));

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── SetDispositionAsync (Spec C-4) ───────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetDispositionAsync_WhenAnyArgumentIsBlank_ShouldThrowArgumentException(string? blank)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        await sut.Invoking(c => c.SetDispositionAsync(blank!, "sr-1", "Spam")).Should().ThrowAsync<ArgumentException>();
        await sut.Invoking(c => c.SetDispositionAsync("dlr-1", blank!, "Spam")).Should().ThrowAsync<ArgumentException>();
        await sut.Invoking(c => c.SetDispositionAsync("dlr-1", "sr-1", blank!)).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetDispositionAsync_ShouldPutToDispositionRouteWithReasonBody_AndDeserializeResponse()
    {
        var expected = new ServiceRequestDetailResponseDto
        {
            Id = "sr-1",
            TenantId = "ten-1",
            Status = "Cancelled",
            LocationId = "loc-1",
            Disposition = new ServiceRequestDispositionDto
            {
                ReasonCode = "Duplicate",
                ReasonLabel = "Duplicate",
                DisposedAtUtc = DateTime.UtcNow
            }
        };
        var handler = new CapturingHandler(HttpStatusCode.OK, expected);
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var result = await sut.SetDispositionAsync("dlr 1", "sr/1", "Duplicate");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be("/api/dealerships/dlr%201/service-requests/sr%2F1/disposition");
        handler.LastRequestBody.Should().Contain("\"reasonCode\":\"Duplicate\"");
        result.Status.Should().Be("Cancelled");
        result.Disposition!.ReasonCode.Should().Be("Duplicate");
    }

    [Fact]
    public async Task SetDispositionAsync_WhenApiReturns422_ShouldThrow()
    {
        var handler = new CapturingHandler(HttpStatusCode.UnprocessableEntity, new { message = "invalid" });
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.SetDispositionAsync("dlr-1", "sr-1", "Other");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── RegeneratePacketAsync (Spec B-1 / C-2, issue #443) ──────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegeneratePacketAsync_WhenAnyArgumentIsBlank_ShouldThrowArgumentException(string? blank)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        await sut.Invoking(c => c.RegeneratePacketAsync(blank!, "sr-1")).Should().ThrowAsync<ArgumentException>();
        await sut.Invoking(c => c.RegeneratePacketAsync("dlr-1", blank!)).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RegeneratePacketAsync_ShouldPostToRegenerateRoute()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted, new { });
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        await sut.RegeneratePacketAsync("dlr 1", "sr/1");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be("/api/dealerships/dlr%201/service-requests/sr%2F1/packet/regenerate");
    }

    [Fact]
    public async Task RegeneratePacketAsync_WhenApiReturns404_ShouldThrow()
    {
        var handler = new CapturingHandler(HttpStatusCode.NotFound, new { message = "not found" });
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.RegeneratePacketAsync("dlr-1", "sr-1");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── GetPacketPdfLinkAsync (Spec C-2, issue #443) ─────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPacketPdfLinkAsync_WhenAnyArgumentIsBlank_ShouldThrowArgumentException(string? blank)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        await sut.Invoking(c => c.GetPacketPdfLinkAsync(blank!, "sr-1")).Should().ThrowAsync<ArgumentException>();
        await sut.Invoking(c => c.GetPacketPdfLinkAsync("dlr-1", blank!)).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPacketPdfLinkAsync_ShouldGetPdfRoute_AndDeserializeLink()
    {
        var expected = new PacketPdfLinkDto
        {
            SasUrl = "https://blob/v2.pdf?sig=abc",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            PacketVersion = 2
        };
        var handler = new CapturingHandler(HttpStatusCode.OK, expected);
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var result = await sut.GetPacketPdfLinkAsync("dlr 1", "sr/1");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be("/api/dealerships/dlr%201/service-requests/sr%2F1/packet/pdf");
        result.SasUrl.Should().Be("https://blob/v2.pdf?sig=abc");
        result.PacketVersion.Should().Be(2);
    }

    [Fact]
    public async Task GetPacketPdfLinkAsync_WhenApiReturns404_ShouldThrow()
    {
        var handler = new CapturingHandler(HttpStatusCode.NotFound, new { message = "not found" });
        var sut = CreateClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetPacketPdfLinkAsync("dlr-1", "sr-1");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public CapturingHandler(HttpStatusCode statusCode, object responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = JsonContent.Create(_responseBody, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
        }
    }
}
