using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

public class IntakeFormConfigEmbeddedTests
{
    [Fact]
    public void MaxAttachments_Default_ShouldBeFive()
    {
        new IntakeFormConfigEmbedded().MaxAttachments.Should().Be(5);
    }

    [Fact]
    public void AttachmentCapBounds_ShouldBeOneToFive()
    {
        IntakeFormConfigEmbedded.MinAttachmentCap.Should().Be(1);
        IntakeFormConfigEmbedded.MaxAttachmentCap.Should().Be(5);
    }
}
