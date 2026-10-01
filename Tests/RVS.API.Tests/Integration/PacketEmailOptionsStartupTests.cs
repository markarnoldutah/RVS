using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Packets;

namespace RVS.API.Tests.Integration;

/// <summary>
/// Proves the <c>PacketEmail:MaxRequestBytes</c> budget is validated when the real
/// <c>Program.cs</c> host starts (issue #521), not per packet at send time.
///
/// The out-of-range budget is applied through <c>ConfigureTestServices</c> rather than an
/// environment variable: environment variables are process-wide and would leak into the other
/// integration tests running in parallel.
/// </summary>
public sealed class PacketEmailOptionsStartupTests
{
    [Fact]
    public async Task Startup_WithTheDefaultBudget_BootsAndBindsIt()
    {
        await using var factory = new TenantAccessGateApiFactory();

        var options = factory.Services.GetRequiredService<IOptions<PacketEmailOptions>>().Value;

        options.MaxRequestBytes.Should().Be(PacketEmailSizeFitter.DefaultMaxRequestBytes);
    }

    [Theory]
    [InlineData(500L)]
    [InlineData(25_000_000L)]
    public async Task Startup_WithAnOutOfRangeBudget_RefusesToStart(long maxRequestBytes)
    {
        await using var factory = new TenantAccessGateApiFactory();
        using var misconfigured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<PacketEmailOptions>(o => o.MaxRequestBytes = maxRequestBytes)));

        var act = () => misconfigured.CreateClient();

        // The host may wrap the validation failure; ToString() includes inner exceptions.
        act.Should().Throw<Exception>()
            .Which.ToString().Should().Contain(nameof(OptionsValidationException))
            .And.Contain("PacketEmail:MaxRequestBytes");
    }
}
