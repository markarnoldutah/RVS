using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests for <see cref="LookupApiClient"/>'s channel-tagged intake links (<c>Spec A-13</c>),
/// read by the manager app's Send intake link dialog so the advisor can copy the link
/// rather than only send it (issue #756).
/// </summary>
public class LookupApiClientTests
{
    private static LookupApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new LookupApiClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetLocationIntakeLinksAsync_WhenLocationIdIsBlank_ShouldThrowArgumentException(string? locationId)
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.OK, null));

        var act = () => sut.GetLocationIntakeLinksAsync(locationId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetLocationIntakeLinksAsync_ShouldGetTheIntakeLinksRoute_AndDeserializeTheLinks()
    {
        var handler = new StubHandler(HttpStatusCode.OK, new LocationIntakeLinksResponseDto
        {
            LocationId = "loc/1",
            Slug = "acme-ogden",
            PrintUrl = "https://go.rvintake.com/acme-ogden",
            QrUrl = "https://go.rvintake.com/acme-ogden?src=qr",
            TextReplacementUrl = "https://go.rvintake.com/acme-ogden?src=textrepl",
            QuickReplyUrl = "https://go.rvintake.com/acme-ogden?src=quickreply",
            ManagerAppUrl = "https://go.rvintake.com/acme-ogden?src=mgrapp"
        });
        var sut = CreateClient(handler);

        var result = await sut.GetLocationIntakeLinksAsync("loc/1");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/locations/loc%2F1/intake-links");
        result.Slug.Should().Be("acme-ogden");
        result.ManagerAppUrl.Should().Be("https://go.rvintake.com/acme-ogden?src=mgrapp");
    }

    [Fact]
    public async Task GetLocationIntakeLinksAsync_WhenTheCallerCannotReadLocations_ShouldThrow()
    {
        // The dialog treats a failure as "no link to show" — it must not be handed a null DTO
        // and turn a 403 into an empty copy field.
        var sut = CreateClient(new StubHandler(HttpStatusCode.Forbidden, null));

        var act = () => sut.GetLocationIntakeLinksAsync("loc-1");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode statusCode, object? responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            var response = new HttpResponseMessage(_statusCode);
            if (_responseBody is not null)
            {
                response.Content = JsonContent.Create(_responseBody, _responseBody.GetType(), options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            return Task.FromResult(response);
        }
    }
}
