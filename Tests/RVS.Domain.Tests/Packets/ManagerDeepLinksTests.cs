using FluentAssertions;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="ManagerDeepLinks"/> — the one place the packet email's link into the
/// authenticated manager app is shaped (<c>Spec C-7</c>, issues #498, #743).
///
/// Contract under test: <c>{base}/sr/{id}</c> opens the request, where every status is one tap
/// away. The link is a plain navigation — it carries no token and writes nothing, so a
/// mail-security scanner that fetches it changes no state.
/// </summary>
public class ManagerDeepLinksTests
{
    private const string BaseUrl = "https://manager.example";

    // ── Build ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WhenBaseUrlIsBlank_ShouldReturnNull(string? baseUrl)
    {
        ManagerDeepLinks.Build(baseUrl, "sr_1").Should().BeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://manager.example")]
    [InlineData("manager.example")]
    public void Build_WhenBaseUrlIsNotHttp_ShouldReturnNull(string baseUrl)
    {
        ManagerDeepLinks.Build(baseUrl, "sr_1").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WhenServiceRequestIdIsBlank_ShouldThrowArgumentException(string? id)
    {
        var act = () => ManagerDeepLinks.Build(BaseUrl, id!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("https://manager.example")]
    [InlineData("https://manager.example/")]
    [InlineData("  https://manager.example/  ")]
    public void Build_ShouldPointTheRequestUrlAtTheSrRoute(string baseUrl)
    {
        var links = ManagerDeepLinks.Build(baseUrl, "sr_1");

        links.Should().NotBeNull();
        links!.RequestUrl.Should().Be("https://manager.example/sr/sr_1");
    }

    [Fact]
    public void Build_ShouldCarryNoStatusQuery()
    {
        var links = ManagerDeepLinks.Build(BaseUrl, "sr_1")!;

        links.RequestUrl.Should().NotContain("?");
    }

    [Fact]
    public void Build_ShouldEscapeTheServiceRequestId()
    {
        var links = ManagerDeepLinks.Build(BaseUrl, "a b/c?d")!;

        links.RequestUrl.Should().Be("https://manager.example/sr/a%20b%2Fc%3Fd");
    }
}
