using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Integration;

/// <summary>
/// End-to-end coverage for the <c>go.rvintake.com</c> redirect (<c>Spec A-13</c>, issue #599).
///
/// Unlike <c>GoControllerTests</c>, which calls the action directly, these drive the real
/// <c>Program.cs</c> pipeline. That is the point: the redirect's route sits at the API root,
/// where the interesting failure modes are routing ones a controller unit test cannot see — a
/// malformed route constraint that only surfaces when <c>MapControllers</c> runs, or a bare
/// <c>/{slug}</c> template quietly swallowing <c>/health</c>.
/// </summary>
public sealed class IntakeRedirectIntegrationTests : IClassFixture<TenantAccessGateApiFactory>
{
    private const string Slug = "nova-hurricane";
    private const string TenantId = "ten_nova_rv";
    private const string LocationId = "loc_hurricane";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeIntakeRedirectHitRepository _hits = new();

    public IntakeRedirectIntegrationTests(TenantAccessGateApiFactory factory)
    {
        var slugLookups = new FakeSlugLookupRepository();
        slugLookups.Seed(Slug, TenantId, LocationId);

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISlugLookupRepository>();
                services.AddSingleton<ISlugLookupRepository>(slugLookups);

                services.RemoveAll<IIntakeRedirectHitRepository>();
                services.AddSingleton<IIntakeRedirectHitRepository>(_hits);
            }));
    }

    /// <summary>
    /// Redirects must be observed, not followed — following one would assert against the Intake
    /// SPA, which is not running here.
    /// </summary>
    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task ShortLinkAtTheRoot_ShouldRedirectToIntakeCarryingTheChannel()
    {
        var response = await CreateClient().GetAsync($"/{Slug}?src=qr", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith($"/{Slug}?src=qr");
    }

    [Fact]
    public async Task ShortLinkUnderGo_ShouldRedirectTheSameWay()
    {
        // The /go prefix is the same endpoint on the API's own hostname, for environments with
        // no redirect host bound yet.
        var response = await CreateClient().GetAsync($"/go/{Slug}?src=qr", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith($"/{Slug}?src=qr");
    }

    [Fact]
    public async Task ShortLinkWithoutASource_ShouldRedirectTaggedAsPrint()
    {
        var response = await CreateClient().GetAsync($"/{Slug}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith($"/{Slug}?src=print");
    }

    [Fact]
    public async Task ShortLinkWithAnUnknownSource_ShouldStillRedirect()
    {
        var response = await CreateClient().GetAsync($"/{Slug}?src=nfc", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith($"/{Slug}?src=nfc");
    }

    [Fact]
    public async Task ShortLinkWithAMalformedSource_ShouldStillRedirect()
    {
        var response = await CreateClient().GetAsync($"/{Slug}?src=%3Cscript%3E", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith($"/{Slug}?src=other");
    }

    [Fact]
    public async Task ShortLinkForAnUnknownSlug_ShouldStillRedirectRatherThanReturn404()
    {
        var response = await CreateClient().GetAsync("/no-such-location?src=qr", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith("/no-such-location?src=qr");
    }

    [Fact]
    public async Task ShortLink_ShouldBeAnonymous()
    {
        // No auth header of any kind: this is the first thing a customer touches.
        var response = await CreateClient().GetAsync($"/{Slug}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShortLink_ShouldNotBeCacheable()
    {
        // A cached redirect is followed without touching the endpoint again, and every later
        // tap would go unlogged.
        var response = await CreateClient().GetAsync($"/{Slug}?src=qr", TestContext.Current.CancellationToken);

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldNotBeSwallowedByTheRootSlugRoute()
    {
        // /health is a literal route and wins over the parameterised one; this pins that down,
        // because the root-level template is otherwise shaped to match it.
        var response = await CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.Found);
    }

    [Theory]
    [InlineData("/has_underscore")]
    [InlineData("/has.dot")]
    [InlineData("/two/segments")]
    public async Task NonSlugShapedPaths_ShouldNotMatchTheRedirect(string path)
    {
        var response = await CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShortLinkInTheWrongCase_ShouldStillRedirectToTheCanonicalSlug()
    {
        // Route constraints are case-insensitive, and that is the behaviour we want: somebody
        // typing a slug off a printed card should not be punished for the shift key.
        var response = await CreateClient().GetAsync($"/{Slug.ToUpperInvariant()}?src=print", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith($"/{Slug}?src=print");
    }

    // ── Hit logging ──────────────────────────────────────────────────────

    [Fact]
    public async Task ShortLink_ShouldRecordAHitPartitionedByLocation()
    {
        await CreateClient().GetAsync($"/{Slug}?src=textrepl", TestContext.Current.CancellationToken);

        _hits.Hits.Should().ContainSingle(h =>
            h.LocationId == LocationId
            && h.TenantId == TenantId
            && h.Slug == Slug
            && h.Source == "textrepl");
    }

    [Fact]
    public async Task ShortLinkFromALinkPreviewFetcher_ShouldBeRecordedButFlagged()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/{Slug}?src=quickreply");
        request.Headers.TryAddWithoutValidation("User-Agent", "facebookexternalhit/1.1");

        await client.SendAsync(request, TestContext.Current.CancellationToken);

        _hits.Hits.Should().ContainSingle(h => h.Source == "quickreply" && h.IsLikelyBot);
    }
}
