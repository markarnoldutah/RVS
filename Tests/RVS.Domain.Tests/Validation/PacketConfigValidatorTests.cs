using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class PacketConfigValidatorTests
{
    // ── Happy paths ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultConfig_ReturnsSuccess()
    {
        var result = PacketConfigValidator.Validate(new PacketConfigEmbedded());

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_SingleRecipientAndDefaults_ReturnsSuccess()
    {
        var config = new PacketConfigEmbedded { Recipients = ["service@dealer.com"] };

        var result = PacketConfigValidator.Validate(config);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TenRecipients_ReturnsSuccess()
    {
        var config = new PacketConfigEmbedded
        {
            Recipients = [.. Enumerable.Range(1, 10).Select(i => $"advisor{i}@dealer.com")],
        };

        var result = PacketConfigValidator.Validate(config);

        result.IsValid.Should().BeTrue();
    }

    // ── Recipient bound (Spec B-4: 1–10) ─────────────────────────────────────

    [Fact]
    public void Validate_ElevenRecipients_ReturnsFailure()
    {
        var config = new PacketConfigEmbedded
        {
            Recipients = [.. Enumerable.Range(1, 11).Select(i => $"advisor{i}@dealer.com")],
        };

        var result = PacketConfigValidator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("10");
    }

    [Fact]
    public void Validate_NoRecipients_ReturnsSuccess()
    {
        // Defaults leave the list empty; the location simply is not operational for email yet.
        var config = new PacketConfigEmbedded { Recipients = [] };

        var result = PacketConfigValidator.Validate(config);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullRecipients_ReturnsFailure()
    {
        var config = new PacketConfigEmbedded { Recipients = null! };

        var result = PacketConfigValidator.Validate(config);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("two addresses@dealer.com")]
    public void Validate_InvalidRecipientAddress_ReturnsFailure(string recipient)
    {
        var config = new PacketConfigEmbedded { Recipients = ["good@dealer.com", recipient] };

        var result = PacketConfigValidator.Validate(config);

        result.IsValid.Should().BeFalse();
    }

    // ── Disabled recipients (Spec B-4, issue #439) ──────────────────────────

    [Fact]
    public void Validate_DisabledRecipientWithValidAddress_ReturnsSuccess()
    {
        var config = new PacketConfigEmbedded
        {
            Recipients = ["live@dealer.com"],
            DisabledRecipients =
            [
                new DisabledRecipientEmbedded { Email = "dead@dealer.com", Reason = "Bounced", DisabledAtUtc = DateTime.UtcNow },
            ],
        };

        PacketConfigValidator.Validate(config).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullDisabledRecipients_ReturnsFailure()
    {
        var config = new PacketConfigEmbedded { DisabledRecipients = null! };

        PacketConfigValidator.Validate(config).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Validate_DisabledRecipientWithBadAddress_ReturnsFailure(string email)
    {
        var config = new PacketConfigEmbedded
        {
            DisabledRecipients = [new DisabledRecipientEmbedded { Email = email }],
        };

        PacketConfigValidator.Validate(config).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AddressInBothActiveAndDisabledLists_ReturnsFailure()
    {
        var config = new PacketConfigEmbedded
        {
            Recipients = ["dup@dealer.com"],
            DisabledRecipients = [new DisabledRecipientEmbedded { Email = "DUP@dealer.com" }],
        };

        var result = PacketConfigValidator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("both");
    }

    // ── Paste-block cap (Spec B-5) ───────────────────────────────────────────

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void Validate_PasteBlockCapWithinRange_ReturnsSuccess(int cap)
    {
        var config = new PacketConfigEmbedded { PasteBlockCharacterCap = cap };

        PacketConfigValidator.Validate(config).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(5001)]
    [InlineData(-1)]
    public void Validate_PasteBlockCapOutOfRange_ReturnsFailure(int cap)
    {
        var config = new PacketConfigEmbedded { PasteBlockCharacterCap = cap };

        PacketConfigValidator.Validate(config).IsValid.Should().BeFalse();
    }

    // ── Status-link TTL (Spec X-5: ≤ 30 days) ────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(30)]
    public void Validate_StatusLinkTtlWithinRange_ReturnsSuccess(int days)
    {
        var config = new PacketConfigEmbedded { StatusLinkTtlDays = days };

        PacketConfigValidator.Validate(config).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(-5)]
    public void Validate_StatusLinkTtlOutOfRange_ReturnsFailure(int days)
    {
        var config = new PacketConfigEmbedded { StatusLinkTtlDays = days };

        PacketConfigValidator.Validate(config).IsValid.Should().BeFalse();
    }

    // ── Optional logo ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://cdn.dealer.com/logo.png")]
    [InlineData("http://dealer.com/assets/logo.svg")]
    public void Validate_ValidLogoUrl_ReturnsSuccess(string url)
    {
        var config = new PacketConfigEmbedded { LogoUrl = url };

        PacketConfigValidator.Validate(config).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://dealer.com/logo.png")]
    [InlineData("/relative/logo.png")]
    [InlineData("javascript:alert(1)")]
    public void Validate_InvalidLogoUrl_ReturnsFailure(string url)
    {
        var config = new PacketConfigEmbedded { LogoUrl = url };

        PacketConfigValidator.Validate(config).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullConfig_ThrowsArgumentNullException()
    {
        var act = () => PacketConfigValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
