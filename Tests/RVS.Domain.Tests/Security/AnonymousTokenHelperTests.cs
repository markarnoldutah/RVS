using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using RVS.Domain.Security;

namespace RVS.Domain.Tests.Security;

/// <summary>
/// Tests for the shared anonymous-token helper (Spec X-5, issue #427/#440).
/// The helper mints ≥128-bit tokens, derives the email→partition prefix used on the
/// per-customer status token, and computes the SHA-256 hash that is the only value ever persisted.
/// </summary>
public class AnonymousTokenHelperTests
{
    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
        return Convert.FromBase64String(padded);
    }

    private static bool IsBase64Url(string value) =>
        value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    // ── GenerateRawToken ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateRawToken_ShouldCarryAtLeast128BitsOfEntropy()
    {
        var token = AnonymousTokenHelper.GenerateRawToken();

        DecodeBase64Url(token).Length.Should().BeGreaterThanOrEqualTo(16);
        AnonymousTokenHelper.TokenEntropyBytes.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public void GenerateRawToken_ShouldBeUrlSafeWithNoPadding()
    {
        var token = AnonymousTokenHelper.GenerateRawToken();

        IsBase64Url(token).Should().BeTrue();
        token.Should().NotContain("=").And.NotContain("+").And.NotContain("/");
    }

    [Fact]
    public void GenerateRawToken_ShouldBeUniquePerCall()
    {
        var tokens = Enumerable.Range(0, 50)
            .Select(_ => AnonymousTokenHelper.GenerateRawToken())
            .ToList();

        tokens.Distinct().Should().HaveCount(50);
    }

    // ── EmailPrefix ──────────────────────────────────────────────────────────

    [Fact]
    public void EmailPrefix_ShouldBeDeterministicForSameEmailRegardlessOfCasingOrWhitespace()
    {
        var a = AnonymousTokenHelper.EmailPrefix("Mike@Test.com");
        var b = AnonymousTokenHelper.EmailPrefix("  mike@test.com ");

        a.Should().Be(b);
    }

    [Fact]
    public void EmailPrefix_ShouldDifferForDifferentEmails()
    {
        AnonymousTokenHelper.EmailPrefix("mike@test.com")
            .Should().NotBe(AnonymousTokenHelper.EmailPrefix("jane@test.com"));
    }

    [Fact]
    public void EmailPrefix_ShouldBeUrlSafeEightBytePrefix()
    {
        var prefix = AnonymousTokenHelper.EmailPrefix("mike@test.com");

        IsBase64Url(prefix).Should().BeTrue();
        DecodeBase64Url(prefix).Length.Should().Be(8);
    }

    // ── GenerateStatusToken ──────────────────────────────────────────────────

    [Fact]
    public void GenerateStatusToken_ShouldBePrefixColonRandomSuffix()
    {
        var token = AnonymousTokenHelper.GenerateStatusToken("mike@test.com");

        var parts = token.Split(':');
        parts.Should().HaveCount(2);
        parts[0].Should().Be(AnonymousTokenHelper.EmailPrefix("mike@test.com"));
        IsBase64Url(parts[1]).Should().BeTrue();
        DecodeBase64Url(parts[1]).Length.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public void GenerateStatusToken_ShouldRotateTheRandomSuffixButKeepThePrefix()
    {
        var first = AnonymousTokenHelper.GenerateStatusToken("mike@test.com");
        var second = AnonymousTokenHelper.GenerateStatusToken("mike@test.com");

        first.Split(':')[0].Should().Be(second.Split(':')[0]);
        first.Split(':')[1].Should().NotBe(second.Split(':')[1]);
    }

    // ── ComputeHash ──────────────────────────────────────────────────────────

    [Fact]
    public void ComputeHash_ShouldBeDeterministic()
    {
        AnonymousTokenHelper.ComputeHash("prefix:suffix")
            .Should().Be(AnonymousTokenHelper.ComputeHash("prefix:suffix"));
    }

    [Fact]
    public void ComputeHash_ShouldMatchSha256OfUtf8BytesEncodedBase64Url()
    {
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("prefix:suffix")))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        AnonymousTokenHelper.ComputeHash("prefix:suffix").Should().Be(expected);
    }

    [Fact]
    public void ComputeHash_ShouldNotEqualTheRawInput()
    {
        var raw = AnonymousTokenHelper.GenerateRawToken();

        AnonymousTokenHelper.ComputeHash(raw).Should().NotBe(raw);
    }

    [Fact]
    public void ComputeHash_ShouldAvalancheOnTinyInputChange()
    {
        AnonymousTokenHelper.ComputeHash("prefix:suffix")
            .Should().NotBe(AnonymousTokenHelper.ComputeHash("prefix:suffiy"));
    }

    [Fact]
    public void ComputeHash_ShouldBeUrlSafe()
    {
        IsBase64Url(AnonymousTokenHelper.ComputeHash("prefix:suffix")).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ComputeHash_ShouldRejectNullOrWhitespace(string? value)
    {
        var act = () => AnonymousTokenHelper.ComputeHash(value!);

        act.Should().Throw<ArgumentException>();
    }
}
