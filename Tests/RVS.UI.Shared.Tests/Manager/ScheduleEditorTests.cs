using FluentAssertions;
using RVS.Blazor.Manager.Shared;
using RVS.Domain.Validation;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// Tests for <see cref="ScheduleEditor"/> — the drawer's schedule controls (<c>Spec C-12</c>,
/// issue #844): which zone the picker opens on, and which zones it offers.
/// </summary>
public class ScheduleEditorTests
{
    [Fact]
    public void DefaultZone_WhenScheduled_ShouldKeepTheStoredZone()
    {
        ScheduleEditor.DefaultZone("America/Phoenix", "America/Denver", "America/Chicago")
            .Should().Be("America/Phoenix");
    }

    [Fact]
    public void DefaultZone_WhenUnscheduled_ShouldUseTheLocationZone()
    {
        ScheduleEditor.DefaultZone(null, "America/Denver", "America/Chicago").Should().Be("America/Denver");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void DefaultZone_WhenLocationHasNoZone_ShouldUseTheBrowserZone(string? locationZone)
    {
        ScheduleEditor.DefaultZone(null, locationZone, "America/Chicago").Should().Be("America/Chicago");
    }

    [Fact]
    public void DefaultZone_WhenNothingKnown_ShouldUseUtc()
    {
        ScheduleEditor.DefaultZone(null, null, null).Should().Be("UTC");
    }

    [Fact]
    public void ZoneOptions_ShouldListTheCuratedZonesInOrder()
    {
        ScheduleEditor.ZoneOptions("America/Denver").Select(o => o.IanaId)
            .Should().Equal(DealershipTimeZones.All.Select(z => z.IanaId));
    }

    [Fact]
    public void ZoneOptions_WhenSelectedZoneNotCurated_ShouldAppendItSoItStaysSelectable()
    {
        var options = ScheduleEditor.ZoneOptions("Europe/London");

        options.Should().HaveCount(DealershipTimeZones.All.Count + 1);
        options[^1].Should().Be(new ScheduleEditor.ZoneOption("Europe/London", "Europe/London"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("america/denver")]
    public void ZoneOptions_WhenSelectedBlankOrCurated_ShouldNotAppend(string? selected)
    {
        ScheduleEditor.ZoneOptions(selected).Should().HaveCount(DealershipTimeZones.All.Count);
    }
}
