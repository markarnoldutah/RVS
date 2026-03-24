using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="CustomerProfile"/> entity with AssetOwnershipEmbedded.
/// </summary>
public class CustomerProfileTests
{
    [Fact]
    public void NewCustomerProfile_TypeShouldBeCustomerProfile()
    {
        var profile = new CustomerProfile();

        profile.Type.Should().Be("customerProfile");
    }

    [Fact]
    public void NewCustomerProfile_ShouldHaveEmptyAssetsOwned()
    {
        var profile = new CustomerProfile();

        profile.AssetsOwned.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ActiveAssetIds_ShouldReturnOnlyActiveAssets()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded { AssetId = "RV:VIN-1", Status = AssetOwnershipStatus.Active },
                new AssetOwnershipEmbedded { AssetId = "RV:VIN-2", Status = AssetOwnershipStatus.Inactive },
                new AssetOwnershipEmbedded { AssetId = "RV:VIN-3", Status = AssetOwnershipStatus.Active }
            ]
        };

        profile.ActiveAssetIds.Should().BeEquivalentTo(["RV:VIN-1", "RV:VIN-3"]);
    }

    [Fact]
    public void GetActiveInteraction_ShouldReturnActiveAssetForAssetId()
    {
        var active = new AssetOwnershipEmbedded { AssetId = "RV:VIN-1", Status = AssetOwnershipStatus.Active };
        var profile = new CustomerProfile
        {
            AssetsOwned = [active]
        };

        profile.GetActiveInteraction("RV:VIN-1").Should().BeSameAs(active);
    }

    [Fact]
    public void GetActiveInteraction_ShouldReturnNullForInactiveAssetId()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded { AssetId = "RV:VIN-1", Status = AssetOwnershipStatus.Inactive }
            ]
        };

        profile.GetActiveInteraction("RV:VIN-1").Should().BeNull();
    }

    // ── DeactivateAsset ──────────────────────────────────────────────────────

    [Fact]
    public void DeactivateAsset_WhenActiveAssetExists_ShouldSetInactive()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded { AssetId = "RV:VIN-1", Status = AssetOwnershipStatus.Active, RequestCount = 3 }
            ]
        };

        profile.DeactivateAsset("RV:VIN-1");

        var asset = profile.AssetsOwned.First();
        asset.Status.Should().Be(AssetOwnershipStatus.Inactive);
        asset.DeactivatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        asset.DeactivationReason.Should().Be("OwnershipTransfer");
    }

    [Fact]
    public void DeactivateAsset_WhenAssetNotActive_ShouldBeNoOp()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded { AssetId = "RV:VIN-1", Status = AssetOwnershipStatus.Inactive }
            ]
        };

        profile.DeactivateAsset("RV:VIN-1");

        profile.AssetsOwned.First().Status.Should().Be(AssetOwnershipStatus.Inactive);
    }

    [Fact]
    public void DeactivateAsset_WhenAssetNotOwned_ShouldBeNoOp()
    {
        var profile = new CustomerProfile();

        profile.DeactivateAsset("RV:VIN-UNKNOWN");

        profile.AssetsOwned.Should().BeEmpty();
    }

    // ── ActivateOrRefreshAsset ───────────────────────────────────────────────

    [Fact]
    public void ActivateOrRefreshAsset_WhenNewAsset_ShouldAddActiveEntry()
    {
        var profile = new CustomerProfile();

        profile.ActivateOrRefreshAsset("RV:VIN-1");

        profile.AssetsOwned.Should().ContainSingle();
        var asset = profile.AssetsOwned.First();
        asset.AssetId.Should().Be("RV:VIN-1");
        asset.Status.Should().Be(AssetOwnershipStatus.Active);
        asset.RequestCount.Should().Be(1);
        asset.FirstSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        asset.LastSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ActivateOrRefreshAsset_WhenAlreadyActive_ShouldIncrementRequestCountAndUpdateLastSeen()
    {
        var originalLastSeen = DateTime.UtcNow.AddDays(-5);
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded
                {
                    AssetId = "RV:VIN-1",
                    Status = AssetOwnershipStatus.Active,
                    RequestCount = 2,
                    FirstSeenAtUtc = DateTime.UtcNow.AddDays(-30),
                    LastSeenAtUtc = originalLastSeen,
                }
            ]
        };

        profile.ActivateOrRefreshAsset("RV:VIN-1");

        var asset = profile.AssetsOwned.First();
        asset.RequestCount.Should().Be(3);
        asset.LastSeenAtUtc.Should().BeAfter(originalLastSeen);
        asset.LastSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ActivateOrRefreshAsset_WhenInactiveVersionExists_ShouldAddNewActiveEntry()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded
                {
                    AssetId = "RV:VIN-1",
                    Status = AssetOwnershipStatus.Inactive,
                    RequestCount = 5,
                }
            ]
        };

        profile.ActivateOrRefreshAsset("RV:VIN-1");

        profile.AssetsOwned.Should().HaveCount(2);
        profile.AssetsOwned.Should().ContainSingle(a => a.Status == AssetOwnershipStatus.Active);
        profile.AssetsOwned.First(a => a.Status == AssetOwnershipStatus.Active).RequestCount.Should().Be(1);
    }
}
