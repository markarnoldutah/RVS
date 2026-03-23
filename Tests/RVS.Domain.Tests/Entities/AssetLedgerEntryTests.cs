using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="AssetLedgerEntry"/> entity with Section10AEmbedded.
/// </summary>
public class AssetLedgerEntryTests
{
    [Fact]
    public void NewAssetLedgerEntry_Section10AShouldBeNull()
    {
        var entry = new AssetLedgerEntry();

        entry.Section10A.Should().BeNull();
    }

    [Fact]
    public void NewAssetLedgerEntry_ShouldHaveNonEmptyId()
    {
        var entry = new AssetLedgerEntry();

        entry.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Section10AEmbedded_ShouldHaveExpectedDefaults()
    {
        var section = new Section10AEmbedded();

        section.ComponentType.Should().BeNull();
        section.FailureMode.Should().BeNull();
        section.RepairAction.Should().BeNull();
        section.PartsUsed.Should().NotBeNull().And.BeEmpty();
        section.LaborHours.Should().BeNull();
        section.ServiceDateUtc.Should().BeNull();
    }
}
