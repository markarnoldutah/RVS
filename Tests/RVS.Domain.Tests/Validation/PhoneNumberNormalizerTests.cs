using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class PhoneNumberNormalizerTests
{
    // ── Accepted US/CA shapes ────────────────────────────────────────────

    [Theory]
    [InlineData("8015551234", "+18015551234")]
    [InlineData("18015551234", "+18015551234")]
    [InlineData("+18015551234", "+18015551234")]
    [InlineData("(801) 555-1234", "+18015551234")]
    [InlineData("801-555-1234", "+18015551234")]
    [InlineData("801.555.1234", "+18015551234")]
    [InlineData("1 (801) 555-1234", "+18015551234")]
    [InlineData("+1 801 555 1234", "+18015551234")]
    [InlineData("  801 555 1234  ", "+18015551234")]
    [InlineData("(604) 555-0199", "+16045550199")]
    [InlineData("866-231-9618", "+18662319618")]
    public void TryNormalize_WithUsOrCanadianNumber_ShouldReturnE164(string input, string expected)
    {
        var ok = PhoneNumberNormalizer.TryNormalize(input, out var e164);

        ok.Should().BeTrue();
        e164.Should().Be(expected);
    }

    // ── Rejected shapes ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_WhenNullOrWhiteSpace_ShouldReturnFalse(string? input)
    {
        var ok = PhoneNumberNormalizer.TryNormalize(input, out var e164);

        ok.Should().BeFalse();
        e164.Should().BeNull();
    }

    [Theory]
    [InlineData("555-1234")]              // 7 digits: no area code
    [InlineData("801555123")]             // 9 digits
    [InlineData("28015551234")]           // 11 digits not starting with 1
    [InlineData("180155512345")]          // 12 digits
    [InlineData("+448015551234")]         // non-NANP country code
    [InlineData("+2 801 555 1234")]
    public void TryNormalize_WithWrongDigitCountOrCountry_ShouldReturnFalse(string input)
    {
        PhoneNumberNormalizer.TryNormalize(input, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("0015551234")]            // area code starts with 0
    [InlineData("1015551234")]            // area code starts with 1
    [InlineData("8010551234")]            // exchange starts with 0
    [InlineData("8011551234")]            // exchange starts with 1
    public void TryNormalize_WithInvalidNanpAreaCodeOrExchange_ShouldReturnFalse(string input)
    {
        PhoneNumberNormalizer.TryNormalize(input, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("801-555-1234 x12")]      // extension
    [InlineData("1-800-FLOWERS")]         // vanity letters
    [InlineData("801;555;1234")]          // disallowed separator
    [InlineData("801+555+1234")]          // '+' not at the start
    [InlineData("++18015551234")]
    public void TryNormalize_WithDisallowedCharacters_ShouldReturnFalse(string input)
    {
        PhoneNumberNormalizer.TryNormalize(input, out _).Should().BeFalse();
    }

    [Fact]
    public void TryNormalize_WhenInputExceedsMaxLength_ShouldReturnFalse()
    {
        var input = "801" + new string(' ', PhoneNumberNormalizer.MaxInputLength) + "5551234";

        PhoneNumberNormalizer.TryNormalize(input, out _).Should().BeFalse();
    }

    // ── Normalize (nullable convenience) ─────────────────────────────────

    [Fact]
    public void Normalize_WithValidNumber_ShouldReturnE164()
    {
        PhoneNumberNormalizer.Normalize("(801) 555-1234").Should().Be("+18015551234");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("555-1234")]
    public void Normalize_WithUnusableNumber_ShouldReturnNull(string? input)
    {
        PhoneNumberNormalizer.Normalize(input).Should().BeNull();
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        var once = PhoneNumberNormalizer.Normalize("801.555.1234");

        PhoneNumberNormalizer.Normalize(once).Should().Be(once);
    }
}
