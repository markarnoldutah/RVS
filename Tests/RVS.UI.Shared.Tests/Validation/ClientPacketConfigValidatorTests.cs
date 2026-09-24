using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Validation;

namespace RVS.UI.Shared.Tests.Validation;

public class ClientPacketConfigValidatorTests
{
    private static List<string> Addresses(int count) =>
        [.. Enumerable.Range(1, count).Select(i => $"advisor{i}@dealer.com")];

    // ── ValidateNewRecipient ─────────────────────────────────────────────────

    [Fact]
    public void ValidateNewRecipient_ValidUniqueAddress_ReturnsSuccess()
    {
        var result = ClientPacketConfigValidator.ValidateNewRecipient("service@dealer.com", ["parts@dealer.com"]);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateNewRecipient_SurroundingWhitespace_IsTrimmedAndAccepted()
    {
        var result = ClientPacketConfigValidator.ValidateNewRecipient("  service@dealer.com  ", []);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void ValidateNewRecipient_InvalidAddress_ReturnsFailure(string? address)
    {
        var result = ClientPacketConfigValidator.ValidateNewRecipient(address, []);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateNewRecipient_DuplicateIgnoringCase_ReturnsFailure()
    {
        var result = ClientPacketConfigValidator.ValidateNewRecipient("Service@Dealer.com", ["service@dealer.com"]);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already");
    }

    [Fact]
    public void ValidateNewRecipient_ListAlreadyAtTen_ReturnsFailure()
    {
        var result = ClientPacketConfigValidator.ValidateNewRecipient("eleven@dealer.com", Addresses(10));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("10");
    }

    [Fact]
    public void ValidateNewRecipient_NullCurrentList_ThrowsArgumentNullException()
    {
        var act = () => ClientPacketConfigValidator.ValidateNewRecipient("a@dealer.com", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── ValidateForSave: recipient bound ─────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void ValidateForSave_EnabledWithOneToTenRecipients_ReturnsSuccess(int count)
    {
        var config = new PacketConfigDto { Enabled = true, Recipients = Addresses(count) };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateForSave_EnabledWithNoRecipients_ReturnsFailure()
    {
        var config = new PacketConfigDto { Enabled = true, Recipients = [] };

        var result = ClientPacketConfigValidator.ValidateForSave(config);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at least one");
    }

    [Fact]
    public void ValidateForSave_DisabledWithNoRecipients_ReturnsSuccess()
    {
        var config = new PacketConfigDto { Enabled = false, Recipients = [] };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateForSave_ElevenRecipients_ReturnsFailure(bool enabled)
    {
        var config = new PacketConfigDto { Enabled = enabled, Recipients = Addresses(11) };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateForSave_DuplicateRecipients_ReturnsFailure()
    {
        var config = new PacketConfigDto { Recipients = ["a@dealer.com", "A@dealer.com"] };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateForSave_InvalidRecipientAddress_ReturnsFailure()
    {
        var config = new PacketConfigDto { Recipients = ["good@dealer.com", "bad"] };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeFalse();
    }

    // ── ValidateForSave: remaining B-6 fields (delegated to the domain rule) ─

    [Theory]
    [InlineData(99)]
    [InlineData(5001)]
    public void ValidateForSave_PasteBlockCapOutOfRange_ReturnsFailure(int cap)
    {
        var config = new PacketConfigDto { Recipients = Addresses(1), PasteBlockCharacterCap = cap };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void ValidateForSave_StatusLinkTtlOutOfRange_ReturnsFailure(int days)
    {
        var config = new PacketConfigDto { Recipients = Addresses(1), StatusLinkTtlDays = days };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateForSave_InvalidLogoUrl_ReturnsFailure()
    {
        var config = new PacketConfigDto { Recipients = Addresses(1), LogoUrl = "not-a-url" };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateForSave_DefaultsPlusOneRecipient_ReturnsSuccess()
    {
        // Spec B-6: a location works with one setting changed — the recipient address.
        var config = new PacketConfigDto { Recipients = ["service@dealer.com"] };

        ClientPacketConfigValidator.ValidateForSave(config).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateForSave_NullConfig_ThrowsArgumentNullException()
    {
        var act = () => ClientPacketConfigValidator.ValidateForSave(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
