using FluentAssertions;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests for <see cref="TenantAccessState"/> — whether the signed-in user's tenant has been
/// disabled, as learned from the API (issue #625).
/// </summary>
public class TenantAccessStateTests
{
    [Fact]
    public void NewState_ShouldNotBeRestricted()
    {
        var state = new TenantAccessState();

        state.IsRestricted.Should().BeFalse();
        state.DisabledMessage.Should().BeNull();
        state.SupportContactEmail.Should().BeNull();
    }

    [Fact]
    public void MarkRestricted_ShouldRecordDetailsAndRaiseChanged()
    {
        var state = new TenantAccessState();
        var raised = 0;
        state.Changed += () => raised++;

        state.MarkRestricted("On hold.", "help@example.com");

        state.IsRestricted.Should().BeTrue();
        state.DisabledMessage.Should().Be("On hold.");
        state.SupportContactEmail.Should().Be("help@example.com");
        raised.Should().Be(1);
    }

    [Fact]
    public void MarkRestricted_WhenAlreadyRestricted_ShouldNotRaiseChangedAgain()
    {
        // Every page fires several calls; each 403 must not re-render the layout.
        var state = new TenantAccessState();
        state.MarkRestricted("On hold.", null);
        var raised = 0;
        state.Changed += () => raised++;

        state.MarkRestricted("On hold.", null);

        raised.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkRestricted_WhenDetailsAreBlank_ShouldStoreNull(string blank)
    {
        var state = new TenantAccessState();

        state.MarkRestricted(blank, blank);

        state.DisabledMessage.Should().BeNull();
        state.SupportContactEmail.Should().BeNull();
    }

    [Fact]
    public void Reset_ShouldClearRestrictionAndRaiseChanged()
    {
        var state = new TenantAccessState();
        state.MarkRestricted("On hold.", "help@example.com");
        var raised = 0;
        state.Changed += () => raised++;

        state.Reset();

        state.IsRestricted.Should().BeFalse();
        state.DisabledMessage.Should().BeNull();
        state.SupportContactEmail.Should().BeNull();
        raised.Should().Be(1);
    }
}
