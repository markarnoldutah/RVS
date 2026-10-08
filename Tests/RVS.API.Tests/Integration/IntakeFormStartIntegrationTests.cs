using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Integration;

/// <summary>
/// End-to-end coverage for <c>POST api/intake/{slug}/starts</c> (<c>Spec A-13</c>, issue #839)
/// through the real <c>Program.cs</c> pipeline. The contract is that it answers 204 whatever it
/// is sent; a controller unit test cannot see model binding turn an empty body into a 400.
/// </summary>
public sealed class IntakeFormStartIntegrationTests : IClassFixture<TenantAccessGateApiFactory>
{
    private const string Slug = "nova-hurricane";
    private const string TenantId = "ten_nova_rv";
    private const string LocationId = "loc_hurricane";
    private const string SessionId = "3f2b8c0e9d4a4f6b8e1c2d3a4b5c6d7e";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeIntakeFormStartRepository _starts = new();

    public IntakeFormStartIntegrationTests(TenantAccessGateApiFactory factory)
    {
        var slugLookups = new FakeSlugLookupRepository();
        slugLookups.Seed(Slug, TenantId, LocationId);

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISlugLookupRepository>();
                services.AddSingleton<ISlugLookupRepository>(slugLookups);

                services.RemoveAll<IIntakeFormStartRepository>();
                services.AddSingleton<IIntakeFormStartRepository>(_starts);
            }));
    }

    [Fact]
    public async Task Start_ForAKnownSlug_ShouldReturn204AndRecordOneRow()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            $"/api/intake/{Slug}/starts", new { sessionId = SessionId, src = "qr" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _starts.Starts.Should().ContainSingle()
            .Which.Should().Match<RVS.Domain.Entities.IntakeFormStart>(s =>
                s.LocationId == LocationId && s.SessionId == SessionId && s.Source == "qr");
    }

    [Fact]
    public async Task Start_ForAnUnknownSlug_ShouldReturn204AndRecordNothing()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/intake/no-such-location/starts", new { sessionId = SessionId, src = "qr" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _starts.Starts.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_WithNoBody_ShouldReturn204AndRecordNothing()
    {
        var response = await _factory.CreateClient().PostAsync(
            $"/api/intake/{Slug}/starts", new StringContent(string.Empty, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _starts.Starts.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_ShouldBeAnonymous()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            $"/api/intake/{Slug}/starts", new { sessionId = SessionId },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }
}
