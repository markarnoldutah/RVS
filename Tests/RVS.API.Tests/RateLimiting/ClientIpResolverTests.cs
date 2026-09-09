using FluentAssertions;
using Microsoft.AspNetCore.Http;
using RVS.API.RateLimiting;

namespace RVS.API.Tests.RateLimiting;

public sealed class ClientIpResolverTests
{
    [Fact]
    public void Resolve_WhenContextIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => ClientIpResolver.Resolve(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Resolve_WhenNoForwardedHeader_ShouldUseSocketRemoteIp()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");

        ClientIpResolver.Resolve(context).Should().Be("203.0.113.7");
    }

    [Fact]
    public void Resolve_WhenNoForwardedHeaderAndNoRemoteIp_ShouldReturnUnknown()
    {
        var context = new DefaultHttpContext();

        ClientIpResolver.Resolve(context).Should().Be(ClientIpResolver.UnknownKey);
    }

    [Fact]
    public void Resolve_WhenForwardedHeaderPresent_ShouldUseFirstEntry()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");
        context.Request.Headers[ClientIpResolver.ForwardedForHeader] = "198.51.100.23, 70.41.3.18, 10.0.0.1";

        ClientIpResolver.Resolve(context).Should().Be("198.51.100.23");
    }

    [Fact]
    public void Resolve_WhenForwardedEntryHasPort_ShouldStripPort()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ClientIpResolver.ForwardedForHeader] = "198.51.100.23:49812";

        ClientIpResolver.Resolve(context).Should().Be("198.51.100.23");
    }

    [Fact]
    public void Resolve_WhenForwardedEntryIsIpv6_ShouldKeepIt()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ClientIpResolver.ForwardedForHeader] = "2001:db8::1";

        ClientIpResolver.Resolve(context).Should().Be("2001:db8::1");
    }

    [Fact]
    public void Resolve_WhenForwardedHeaderIsWhitespace_ShouldFallBackToSocketRemoteIp()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
        context.Request.Headers[ClientIpResolver.ForwardedForHeader] = "   ";

        ClientIpResolver.Resolve(context).Should().Be("203.0.113.7");
    }
}
