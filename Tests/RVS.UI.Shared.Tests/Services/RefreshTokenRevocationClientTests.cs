using System.Net;
using FluentAssertions;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests for <see cref="RefreshTokenRevocationClient"/> — revokes the manager app's refresh token
/// at Auth0 on sign-out, so a token copied before sign-out stops working (issue #498 hardening).
///
/// Contract under test: POSTs <c>client_id</c> + <c>token</c> to <c>{authority}/oauth/revoke</c>;
/// never throws on a failed or unreachable revoke, because sign-out must always proceed.
/// </summary>
public class RefreshTokenRevocationClientTests
{
    private const string Authority = "https://tenant.auth0.example/";
    private const string ClientId = "client-123";

    private static (RefreshTokenRevocationClient Sut, RecordingHandler Handler) Create(
        HttpStatusCode status = HttpStatusCode.OK, Exception? throws = null)
    {
        var handler = new RecordingHandler(status, throws);
        return (new RefreshTokenRevocationClient(new HttpClient(handler), Authority, ClientId), handler);
    }

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new RefreshTokenRevocationClient(null!, Authority, ClientId);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenAuthorityIsBlank_ShouldThrowArgumentException(string? authority)
    {
        var act = () => new RefreshTokenRevocationClient(new HttpClient(), authority!, ClientId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenClientIdIsBlank_ShouldThrowArgumentException(string? clientId)
    {
        var act = () => new RefreshTokenRevocationClient(new HttpClient(), Authority, clientId!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeAsync_WhenTokenIsBlank_ShouldReturnFalseWithoutCallingAuth0(string? token)
    {
        var (sut, handler) = Create();

        var result = await sut.RevokeAsync(token);

        result.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAsync_ShouldPostClientIdAndTokenToTheRevokeEndpoint()
    {
        var (sut, handler) = Create();

        var result = await sut.RevokeAsync("rt-abc");

        result.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be("https://tenant.auth0.example/oauth/revoke");
        request.ContentType.Should().Be("application/x-www-form-urlencoded");
        request.Body.Should().Be("client_id=client-123&token=rt-abc");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task RevokeAsync_WhenAuth0RejectsTheRequest_ShouldReturnFalse(HttpStatusCode status)
    {
        var (sut, _) = Create(status);

        var result = await sut.RevokeAsync("rt-abc");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_WhenAuth0IsUnreachable_ShouldReturnFalseInsteadOfThrowing()
    {
        var (sut, _) = Create(throws: new HttpRequestException("offline"));

        var result = await sut.RevokeAsync("rt-abc");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_WhenAuth0NeverAnswers_ShouldGiveUpAfterTheTimeoutAndReturnFalse()
    {
        // Issue #625: a revoke that never returned left the user stuck on "Log Out".
        var sut = new RefreshTokenRevocationClient(
            new HttpClient(new HangingHandler()), Authority, ClientId, TimeSpan.FromMilliseconds(50));

        var result = await sut.RevokeAsync("rt-abc", TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenTimeoutIsNotPositive_ShouldThrowArgumentOutOfRangeException()
    {
        var act = () => new RefreshTokenRevocationClient(new HttpClient(), Authority, ClientId, TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? ContentType, string Body);

    private sealed class RecordingHandler(HttpStatusCode status, Exception? throws) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Content?.Headers.ContentType?.MediaType,
                body));

            if (throws is not null)
            {
                throw throws;
            }

            return new HttpResponseMessage(status);
        }
    }
}
