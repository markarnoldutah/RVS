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
}
