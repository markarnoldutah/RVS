using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using RVS.Domain.Security;

namespace RVS.Domain.Tests.Security;

/// <summary>
/// Tests for <see cref="InviteToken"/> — the single-use A-14 intake invite token
/// (<c>Spec A-14</c>, <c>Spec X-5</c>, issue #663). The raw token travels in the texted link;
/// only its hash is ever stored.
/// </summary>
public class InviteTokenTests
{
    // ── Generate ─────────────────────────────────────────────────────────

    [Fact]
    public void Generate_ShouldBe32RandomBytesAsUnpaddedBase64Url()
    {
        var token = InviteToken.Generate();

        token.Should().HaveLength(InviteToken.Length);
        token.Should().MatchRegex("^[A-Za-z0-9_-]+$", "base64url is URL-safe and unpadded");
        Base64UrlDecode(token).Should().HaveCount(32);
    }

    [Fact]
    public void Generate_ShouldNotRepeat()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => InviteToken.Generate()).ToList();

        tokens.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_ShouldAlwaysBeWellFormed()
    {
        Enumerable.Range(0, 200)
            .Select(_ => InviteToken.Generate())
            .Should().AllSatisfy(t => InviteToken.IsWellFormed(t).Should().BeTrue());
    }

    // ── Hash ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_ShouldBeTheLowercaseHexSha256OfTheToken()
    {
        var token = InviteToken.Generate();
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

        InviteToken.Hash(token).Should().Be(expected);
    }

    [Fact]
    public void Hash_ShouldBeDeterministic()
    {
        var token = InviteToken.Generate();

        InviteToken.Hash(token).Should().Be(InviteToken.Hash(token));
    }

    [Fact]
    public void Hash_ShouldNotContainTheToken()
    {
        var token = InviteToken.Generate();

        InviteToken.Hash(token).Should().NotContain(token).And.HaveLength(64);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Hash_WhenBlank_ShouldThrowArgumentException(string? token)
    {
        var act = () => InviteToken.Hash(token!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Hash_WhenMalformed_ShouldThrowArgumentException()
    {
        var act = () => InviteToken.Hash("not/a+token");

        act.Should().Throw<ArgumentException>();
    }

    // ── IsWellFormed ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tooshort")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]   // 42 chars
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 44 chars
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]  // padded
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]  // base64, not base64url
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA ")]
    public void IsWellFormed_WhenNotA43CharBase64UrlString_ShouldBeFalse(string? candidate)
    {
        InviteToken.IsWellFormed(candidate).Should().BeFalse();
    }

    [Fact]
    public void IsWellFormed_When43Base64UrlCharacters_ShouldBeTrue()
    {
        InviteToken.IsWellFormed("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA-_").Should().BeTrue();
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }
}
