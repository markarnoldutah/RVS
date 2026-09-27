using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Packets;

namespace RVS.API.Tests.Packets;

/// <summary>
/// Tests for <see cref="HttpLocationLogoFetcher"/> — fetching a location's dealer logo so the
/// packet PDF can embed it (<c>Spec A-16</c>, issue #470). Every failure is a <c>null</c>, never
/// an exception: a logo that will not load costs the packet its logo, not the packet.
/// </summary>
public class HttpLocationLogoFetcherTests
{
    private const string LogoUrl = "https://cdn.dealer.example/logo.png";

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private readonly Mock<HttpMessageHandler> _handler = new();

    private HttpLocationLogoFetcher CreateSut() =>
        new(new HttpClient(_handler.Object), Mock.Of<ILogger<HttpLocationLogoFetcher>>());

    private void Respond(HttpResponseMessage response) =>
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

    private static HttpResponseMessage Ok(byte[] body, string contentType = "image/png")
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task FetchAsync_WhenUrlIsBlank_ShouldThrowArgumentException(string? url)
    {
        var act = () => CreateSut().FetchAsync(url!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FetchAsync_WhenTheLogoIsAPng_ShouldReturnItsBytes()
    {
        Respond(Ok(Png));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().Equal(Png);
    }

    [Fact]
    public async Task FetchAsync_WhenTheLogoIsAJpeg_ShouldReturnItsBytes()
    {
        Respond(Ok(Jpeg, "image/jpeg"));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().Equal(Jpeg);
    }

    [Fact]
    public async Task FetchAsync_WhenTheServerMislabelsARealPng_ShouldStillReturnIt()
    {
        // The bytes decide, not the header: plenty of hosts serve images as octet-stream.
        Respond(Ok(Png, "application/octet-stream"));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().Equal(Png);
    }

    [Theory]
    [InlineData("http://cdn.dealer.example/logo.png")]
    [InlineData("ftp://cdn.dealer.example/logo.png")]
    [InlineData("not a url")]
    public async Task FetchAsync_WhenTheUrlIsNotHttps_ShouldReturnNullWithoutARequest(string url)
    {
        var bytes = await CreateSut().FetchAsync(url, TestContext.Current.CancellationToken);

        bytes.Should().BeNull();
        _handler.Protected().Verify(
            "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FetchAsync_WhenTheServerFails_ShouldReturnNull(HttpStatusCode status)
    {
        Respond(new HttpResponseMessage(status));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenTheBodyIsNotAnImage_ShouldReturnNull()
    {
        Respond(Ok("<html>Not found</html>"u8.ToArray(), "text/html"));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenTheImageIsAnSvg_ShouldReturnNull()
    {
        // Mail clients do not render SVG, so the packet could not show it in the email body.
        Respond(Ok("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray(), "image/svg+xml"));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenTheBodyIsLargerThanTheCap_ShouldReturnNull()
    {
        var oversized = new byte[HttpLocationLogoFetcher.MaxLogoBytes + 1];
        Png.CopyTo(oversized, 0);
        Respond(Ok(oversized));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenTheRequestThrows_ShouldReturnNull()
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("DNS failure"));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenTheRequestTimesOut_ShouldReturnNull()
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("timed out"));

        var bytes = await CreateSut().FetchAsync(LogoUrl, TestContext.Current.CancellationToken);

        bytes.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenTheCallerCancels_ShouldThrowOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var act = () => CreateSut().FetchAsync(LogoUrl, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
