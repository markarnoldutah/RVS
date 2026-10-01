using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests for <see cref="IntakeInviteApiClient"/> — the manager app's Send intake link dialog
/// (<c>Spec A-14</c>, issues #663 and #666).
/// </summary>
public class IntakeInviteApiClientTests
{
    private static IntakeInviteApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new IntakeInviteApiClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── SendAsync ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WhenLocationIdIsBlank_ShouldThrowArgumentException(string? locationId)
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.OK, new { }));

        var act = () => sut.SendAsync(locationId!, new IntakeInviteCreateRequestDto { FirstName = "Jane" });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.OK, new { }));

        var act = () => sut.SendAsync("loc-1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_ShouldPostToTheLocationsInvitesRoute_AndDeserializeTheInvite()
    {
        var handler = new StubHandler(HttpStatusCode.Created, new IntakeInviteDetailResponseDto
        {
            Id = "inv-1",
            LocationId = "loc/1",
            FirstName = "Jane",
            Phone = "+18015551234",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(72),
            DeliveryStatus = "queued"
        });
        var sut = CreateClient(handler);

        var result = await sut.SendAsync(
            "loc/1", new IntakeInviteCreateRequestDto { FirstName = "Jane", Phone = "(801) 555-1234", ConsentCaptured = true });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/locations/loc%2F1/intake-invites");
        handler.LastRequestBody.Should().Contain("\"consentCaptured\":true");
        result.Id.Should().Be("inv-1");
        result.DeliveryStatus.Should().Be("queued");
    }

    [Fact]
    public async Task SendAsync_WhenTextingIsDisabled_ShouldThrowWithTheApiMessage()
    {
        // The API answers 409 with ProblemDetails; the advisor needs to read the reason,
        // not "Response status code does not indicate success".
        var handler = new StubHandler(HttpStatusCode.Conflict, new
        {
            title = "Conflict",
            status = 409,
            detail = "Texting is not enabled yet. Use Fill it in myself to complete the intake during the call."
        });
        var sut = CreateClient(handler);

        var act = () => sut.SendAsync("loc-1", new IntakeInviteCreateRequestDto { FirstName = "Jane", Phone = "8015551234", ConsentCaptured = true });

        var thrown = await act.Should().ThrowAsync<IntakeInviteApiException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
        thrown.Which.Message.Should().Be(
            "Texting is not enabled yet. Use Fill it in myself to complete the intake during the call.");
    }

    [Fact]
    public async Task SendAsync_WhenRateLimited_ShouldThrowWithTheApiMessage()
    {
        var handler = new StubHandler(HttpStatusCode.TooManyRequests, new
        {
            detail = "You've sent the most intake links allowed in an hour. Try again later."
        });
        var sut = CreateClient(handler);

        var act = () => sut.SendAsync("loc-1", new IntakeInviteCreateRequestDto { FirstName = "Jane", Phone = "8015551234", ConsentCaptured = true });

        var thrown = await act.Should().ThrowAsync<IntakeInviteApiException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        thrown.Which.Message.Should().StartWith("You've sent the most intake links");
    }

    [Fact]
    public async Task SendAsync_WhenForbidden_ShouldExplainTheMissingPermission()
    {
        // A 403 from the authorization middleware has no body at all, so the client supplies
        // the explanation. intake-invites:send is on dealer:advisor and dealer:manager.
        var handler = new StubHandler(HttpStatusCode.Forbidden, responseBody: null);
        var sut = CreateClient(handler);

        var act = () => sut.SendAsync("loc-1", new IntakeInviteCreateRequestDto { FirstName = "Jane", Phone = "8015551234", ConsentCaptured = true });

        var thrown = await act.Should().ThrowAsync<IntakeInviteApiException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        thrown.Which.Message.Should().Contain("permission");
    }

    // ── ListRecentAsync ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListRecentAsync_WhenLocationIdIsBlank_ShouldThrowArgumentException(string? locationId)
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.OK, Array.Empty<object>()));

        var act = () => sut.ListRecentAsync(locationId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ListRecentAsync_ShouldGetTheLocationsInvitesRoute()
    {
        var handler = new StubHandler(HttpStatusCode.OK, new[]
        {
            new IntakeInviteSummaryResponseDto
            {
                Id = "inv-1", LocationId = "loc-1", FirstName = "Jane",
                ExpiresAtUtc = DateTime.UtcNow, DeliveryStatus = "delivered"
            }
        });
        var sut = CreateClient(handler);

        var result = await sut.ListRecentAsync("loc-1");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/locations/loc-1/intake-invites");
        result.Should().ContainSingle().Which.DeliveryStatus.Should().Be("delivered");
    }

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ShouldGetOneInviteById()
    {
        var handler = new StubHandler(HttpStatusCode.OK, new IntakeInviteDetailResponseDto
        {
            Id = "inv 1", LocationId = "loc-1", FirstName = "Jane",
            ExpiresAtUtc = DateTime.UtcNow, DeliveryStatus = "delivered"
        });
        var sut = CreateClient(handler);

        var result = await sut.GetAsync("loc-1", "inv 1");

        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/locations/loc-1/intake-invites/inv%201");
        result.DeliveryStatus.Should().Be("delivered");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetAsync_WhenInviteIdIsBlank_ShouldThrowArgumentException(string? id)
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.OK, new { }));

        var act = () => sut.GetAsync("loc-1", id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── GetCapabilityAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetCapabilityAsync_ShouldReportWhetherTextingIsEnabled()
    {
        var handler = new StubHandler(HttpStatusCode.OK, new IntakeInviteCapabilityResponseDto { SmsEnabled = false });
        var sut = CreateClient(handler);

        var result = await sut.GetCapabilityAsync("loc-1");

        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/locations/loc-1/intake-invites/capability");
        result.SmsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetCapabilityAsync_WhenForbidden_ShouldThrowWithTheMissingPermissionMessage()
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.Forbidden, responseBody: null));

        var act = () => sut.GetCapabilityAsync("loc-1");

        var thrown = await act.Should().ThrowAsync<IntakeInviteApiException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public StubHandler(HttpStatusCode statusCode, object? responseBody)
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

            var response = new HttpResponseMessage(_statusCode);
            if (_responseBody is not null)
            {
                response.Content = JsonContent.Create(_responseBody, _responseBody.GetType(), options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            return response;
        }
    }
}
