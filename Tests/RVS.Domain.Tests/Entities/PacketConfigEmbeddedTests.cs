using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

public class PacketConfigEmbeddedTests
{
    [Fact]
    public void NewInstance_HasDefaultsThatNeedOnlyARecipientToBeUsable()
    {
        var config = new PacketConfigEmbedded();

        config.Enabled.Should().BeTrue();
        config.Recipients.Should().BeEmpty();
        config.AttachPdf.Should().BeTrue();
        config.IncludePhotos.Should().BeTrue();
        config.PasteBlockCharacterCap.Should().Be(1000);
        config.StatusLinkTtlDays.Should().Be(30);
        config.LogoUrl.Should().BeNull();
    }

    [Fact]
    public void MaxRecipients_IsTen()
    {
        PacketConfigEmbedded.MaxRecipients.Should().Be(10);
    }

    [Fact]
    public void NewInstance_HasEmptyDisabledRecipients()
    {
        new PacketConfigEmbedded().DisabledRecipients.Should().BeEmpty();
    }

    // ── DisableRecipient (Spec B-4, issue #439) ──────────────────────────────

    [Fact]
    public void DisableRecipient_WhenAddressIsActive_MovesItToDisabledWithReasonAndTimestamp()
    {
        var when = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
        var config = new PacketConfigEmbedded { Recipients = ["keep@dealer.com", "dead@dealer.com"] };

        var changed = config.DisableRecipient("dead@dealer.com", "SuppressedRecipient", when);

        changed.Should().BeTrue();
        config.Recipients.Should().ContainSingle().Which.Should().Be("keep@dealer.com");
        var disabled = config.DisabledRecipients.Should().ContainSingle().Subject;
        disabled.Email.Should().Be("dead@dealer.com");
        disabled.Reason.Should().Be("SuppressedRecipient");
        disabled.DisabledAtUtc.Should().Be(when);
    }

    [Theory]
    [InlineData("DEAD@Dealer.com")]
    [InlineData("  dead@dealer.com  ")]
    public void DisableRecipient_MatchesAddressCaseAndWhitespaceInsensitively(string bounced)
    {
        var config = new PacketConfigEmbedded { Recipients = ["dead@dealer.com"] };

        config.DisableRecipient(bounced, null, DateTime.UtcNow).Should().BeTrue();

        config.Recipients.Should().BeEmpty();
        config.DisabledRecipients.Should().ContainSingle().Which.Email.Should().Be("dead@dealer.com");
    }

    [Fact]
    public void DisableRecipient_WhenAddressNotAnActiveRecipient_ReturnsFalseAndChangesNothing()
    {
        var config = new PacketConfigEmbedded { Recipients = ["keep@dealer.com"] };

        var changed = config.DisableRecipient("stranger@dealer.com", "Bounced", DateTime.UtcNow);

        changed.Should().BeFalse();
        config.Recipients.Should().ContainSingle().Which.Should().Be("keep@dealer.com");
        config.DisabledRecipients.Should().BeEmpty();
    }

    [Fact]
    public void DisableRecipient_WhenAlreadyDisabled_IsIdempotent()
    {
        var config = new PacketConfigEmbedded { Recipients = ["dead@dealer.com"] };
        config.DisableRecipient("dead@dealer.com", "Bounced", DateTime.UtcNow);

        var secondCall = config.DisableRecipient("dead@dealer.com", "Bounced again", DateTime.UtcNow);

        secondCall.Should().BeFalse();
        config.DisabledRecipients.Should().ContainSingle();
        config.DisabledRecipients[0].Reason.Should().Be("Bounced");
    }

    [Fact]
    public void DisableRecipient_BlankReason_IsStoredAsNull()
    {
        var config = new PacketConfigEmbedded { Recipients = ["dead@dealer.com"] };

        config.DisableRecipient("dead@dealer.com", "   ", DateTime.UtcNow);

        config.DisabledRecipients.Should().ContainSingle().Which.Reason.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DisableRecipient_BlankEmail_ReturnsFalse(string? email)
    {
        var config = new PacketConfigEmbedded { Recipients = ["keep@dealer.com"] };

        config.DisableRecipient(email!, "Bounced", DateTime.UtcNow).Should().BeFalse();
        config.Recipients.Should().ContainSingle();
    }

    // ── ReEnableRecipient (Spec B-4, issue #439) ─────────────────────────────

    [Fact]
    public void ReEnableRecipient_WhenAddressIsDisabled_MovesItBackToActive()
    {
        var config = new PacketConfigEmbedded { Recipients = ["keep@dealer.com"] };
        config.DisableRecipient("keep@dealer.com", "Bounced", DateTime.UtcNow); // now nothing active
        config.Recipients.Add("other@dealer.com");

        var changed = config.ReEnableRecipient("KEEP@dealer.com");

        changed.Should().BeTrue();
        config.DisabledRecipients.Should().BeEmpty();
        config.Recipients.Should().BeEquivalentTo(["other@dealer.com", "keep@dealer.com"]);
    }

    [Fact]
    public void ReEnableRecipient_WhenAddressNotDisabled_ReturnsFalseAndChangesNothing()
    {
        var config = new PacketConfigEmbedded { Recipients = ["keep@dealer.com"] };

        config.ReEnableRecipient("keep@dealer.com").Should().BeFalse();
        config.Recipients.Should().ContainSingle().Which.Should().Be("keep@dealer.com");
        config.DisabledRecipients.Should().BeEmpty();
    }
}
