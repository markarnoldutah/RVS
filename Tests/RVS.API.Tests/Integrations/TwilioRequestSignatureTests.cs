using FluentAssertions;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Tests for <see cref="TwilioRequestSignature"/> — Twilio's <c>X-Twilio-Signature</c> scheme:
/// HMAC-SHA1, keyed with the auth token, over the full URL followed by every POST parameter's
/// name and value in name order, base64 encoded.
/// </summary>
public class TwilioRequestSignatureTests
{
    // The worked example from Twilio's webhook security documentation.
    private const string AuthToken = "12345";
    private const string Url = "https://mycompany.com/myapp.php?foo=1&bar=2";
    private const string Expected = "GvWf1cFY/Q7PnoempGyD5oXAezc=";

    private static readonly Dictionary<string, string> Parameters = new()
    {
        ["To"] = "+18005551212",
        ["From"] = "+14158675310",
        ["Digits"] = "1234",
        ["CallSid"] = "CA1234567890ABCDE",
        ["Caller"] = "+14158675310",
    };

    [Fact]
    public void Compute_WithTwiliosDocumentedExample_ShouldMatchItsSignature()
    {
        TwilioRequestSignature.Compute(AuthToken, Url, Parameters).Should().Be(Expected);
    }

    [Fact]
    public void IsValid_WhenTheSignatureMatches_ShouldBeTrue()
    {
        TwilioRequestSignature.IsValid(AuthToken, Url, Parameters, Expected).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenAParameterWasChanged_ShouldBeFalse()
    {
        var tampered = new Dictionary<string, string>(Parameters) { ["Digits"] = "9999" };

        TwilioRequestSignature.IsValid(AuthToken, Url, tampered, Expected).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenTheUrlDiffers_ShouldBeFalse()
    {
        // Why the webhook signs against the configured public URL, not the request's own:
        // behind App Service the app sees http and an internal host.
        TwilioRequestSignature.IsValid(AuthToken, "http://mycompany.com/myapp.php?foo=1&bar=2", Parameters, Expected)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64-and-wrong")]
    public void IsValid_WhenTheSignatureIsMissingOrWrong_ShouldBeFalse(string? signature)
    {
        TwilioRequestSignature.IsValid(AuthToken, Url, Parameters, signature).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenSignedWithADifferentToken_ShouldBeFalse()
    {
        TwilioRequestSignature.IsValid("54321", Url, Parameters, Expected).Should().BeFalse();
    }
}
