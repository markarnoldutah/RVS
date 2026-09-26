using FluentAssertions;
using RVS.Blazor.Intake.Pages.Steps.Shared;

namespace RVS.UI.Shared.Tests.Pages.Steps;

/// <summary>
/// Tests for <see cref="AttachmentSlots"/> — the Step 7 drop zone's "how many more" prompt (issue #758).
/// </summary>
public class AttachmentSlotsTests
{
    [Fact]
    public void Prompt_NothingAttached_ShouldOfferTheFullAllowance()
    {
        AttachmentSlots.Prompt(maxAttachments: 10, attachedCount: 0).Should().Be("Add up to 10 photos or videos");
    }

    [Theory]
    [InlineData(1, "Add up to 9 more photos or videos")]
    [InlineData(8, "Add up to 2 more photos or videos")]
    public void Prompt_SomeAttached_ShouldCountWhatIsLeft(int attached, string expected)
    {
        AttachmentSlots.Prompt(maxAttachments: 10, attachedCount: attached).Should().Be(expected);
    }

    [Fact]
    public void Prompt_OneSlotLeft_ShouldBeSingular()
    {
        AttachmentSlots.Prompt(maxAttachments: 10, attachedCount: 9).Should().Be("Add 1 more photo or video");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    public void Prompt_Full_ShouldSayHowToMakeRoom(int attached)
    {
        AttachmentSlots.Prompt(maxAttachments: 10, attachedCount: attached)
            .Should().Be("Maximum of 10 reached. Remove one to add another.");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(3, 7)]
    [InlineData(10, 0)]
    [InlineData(12, 0)]
    public void Remaining_ShouldNeverGoNegative(int attached, int expected)
    {
        AttachmentSlots.Remaining(maxAttachments: 10, attachedCount: attached).Should().Be(expected);
    }

    [Fact]
    public void ThumbnailDataUrl_ShouldEncodeTheBytesUnderTheirContentType()
    {
        AttachmentSlots.ThumbnailDataUrl("image/jpeg", [1, 2, 3])
            .Should().Be("data:image/jpeg;base64,AQID");
    }

    [Fact]
    public void ThumbnailDataUrl_NullBytes_ShouldThrow()
    {
        var act = () => AttachmentSlots.ThumbnailDataUrl("image/jpeg", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ThumbnailDataUrl_NoContentType_ShouldThrow(string? contentType)
    {
        var act = () => AttachmentSlots.ThumbnailDataUrl(contentType!, [1]);

        act.Should().Throw<ArgumentException>();
    }
}
