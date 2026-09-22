using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="AssetLedgerEntry"/> entity.
/// </summary>
public class AssetLedgerEntryTests
{
    [Fact]
    public void NewAssetLedgerEntry_ShouldHaveNonEmptyId()
    {
        var entry = new AssetLedgerEntry();

        entry.Id.Should().NotBeNullOrWhiteSpace();
    }
}
