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
                new AssetOwnershipEmbedded { AssetId = "VIN-1", Status = AssetOwnershipStatus.Active },
                new AssetOwnershipEmbedded { AssetId = "VIN-2", Status = AssetOwnershipStatus.Inactive },
                new AssetOwnershipEmbedded { AssetId = "VIN-3", Status = AssetOwnershipStatus.Active }
            ]
        };

        profile.ActiveAssetIds.Should().BeEquivalentTo(["VIN-1", "VIN-3"]);
    }

    [Fact]
    public void GetActiveInteraction_ShouldReturnActiveAssetForAssetId()
    {
        var active = new AssetOwnershipEmbedded { AssetId = "VIN-1", Status = AssetOwnershipStatus.Active };
        var profile = new CustomerProfile
        {
            AssetsOwned = [active]
        };

        profile.GetActiveInteraction("VIN-1").Should().BeSameAs(active);
    }

    [Fact]
    public void GetActiveInteraction_ShouldReturnNullForInactiveAssetId()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded { AssetId = "VIN-1", Status = AssetOwnershipStatus.Inactive }
            ]
        };

        profile.GetActiveInteraction("VIN-1").Should().BeNull();
    }

    // ── DeactivateAsset ──────────────────────────────────────────────────────

    [Fact]
    public void DeactivateAsset_WhenActiveAssetExists_ShouldSetInactive()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded { AssetId = "VIN-1", Status = AssetOwnershipStatus.Active, RequestCount = 3 }
            ]
        };

        profile.DeactivateAsset("VIN-1");

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
                new AssetOwnershipEmbedded { AssetId = "VIN-1", Status = AssetOwnershipStatus.Inactive }
            ]
        };

        profile.DeactivateAsset("VIN-1");

        profile.AssetsOwned.First().Status.Should().Be(AssetOwnershipStatus.Inactive);
    }

    [Fact]
    public void DeactivateAsset_WhenAssetNotOwned_ShouldBeNoOp()
    {
        var profile = new CustomerProfile();

        profile.DeactivateAsset("VIN-UNKNOWN");

        profile.AssetsOwned.Should().BeEmpty();
    }

    // ── ActivateOrRefreshAsset ───────────────────────────────────────────────

    [Fact]
    public void ActivateOrRefreshAsset_WhenNewAsset_ShouldAddActiveEntry()
    {
        var profile = new CustomerProfile();

        profile.ActivateOrRefreshAsset("VIN-1");

        profile.AssetsOwned.Should().ContainSingle();
        var asset = profile.AssetsOwned.First();
        asset.AssetId.Should().Be("VIN-1");
        asset.Status.Should().Be(AssetOwnershipStatus.Active);
        asset.RequestCount.Should().Be(1);
        asset.FirstSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        asset.LastSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ActivateOrRefreshAsset_WhenNewAssetWithMetadata_ShouldPopulateManufacturerModelYear()
    {
        var profile = new CustomerProfile();

        profile.ActivateOrRefreshAsset("VIN-1", "Grand Design", "Momentum 395G", 2023);

        profile.AssetsOwned.Should().ContainSingle();
        var asset = profile.AssetsOwned.First();
        asset.AssetId.Should().Be("VIN-1");
        asset.Manufacturer.Should().Be("Grand Design");
        asset.Model.Should().Be("Momentum 395G");
        asset.Year.Should().Be(2023);
        asset.Status.Should().Be(AssetOwnershipStatus.Active);
        asset.RequestCount.Should().Be(1);
    }

    [Fact]
    public void ActivateOrRefreshAsset_WhenNewAssetWithoutMetadata_ShouldLeaveFieldsNull()
    {
        var profile = new CustomerProfile();

        profile.ActivateOrRefreshAsset("VIN-1");

        var asset = profile.AssetsOwned.First();
        asset.Manufacturer.Should().BeNull();
        asset.Model.Should().BeNull();
        asset.Year.Should().BeNull();
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
                    AssetId = "VIN-1",
                    Status = AssetOwnershipStatus.Active,
                    RequestCount = 2,
                    FirstSeenAtUtc = DateTime.UtcNow.AddDays(-30),
                    LastSeenAtUtc = originalLastSeen,
                }
            ]
        };

        profile.ActivateOrRefreshAsset("VIN-1");

        var asset = profile.AssetsOwned.First();
        asset.RequestCount.Should().Be(3);
        asset.LastSeenAtUtc.Should().BeAfter(originalLastSeen);
        asset.LastSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ActivateOrRefreshAsset_WhenAlreadyActiveWithMetadata_ShouldUpdateMetadata()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded
                {
                    AssetId = "VIN-1",
                    Status = AssetOwnershipStatus.Active,
                    RequestCount = 2,
                    Manufacturer = null,
                    Model = null,
                    Year = null,
                }
            ]
        };

        profile.ActivateOrRefreshAsset("VIN-1", "Winnebago", "View 24D", 2023);

        var asset = profile.AssetsOwned.First();
        asset.Manufacturer.Should().Be("Winnebago");
        asset.Model.Should().Be("View 24D");
        asset.Year.Should().Be(2023);
        asset.RequestCount.Should().Be(3);
    }

    [Fact]
    public void ActivateOrRefreshAsset_WhenAlreadyActiveWithNullMetadata_ShouldPreserveExistingValues()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded
                {
                    AssetId = "VIN-1",
                    Status = AssetOwnershipStatus.Active,
                    RequestCount = 2,
                    Manufacturer = "Winnebago",
                    Model = "View 24D",
                    Year = 2023,
                }
            ]
        };

        profile.ActivateOrRefreshAsset("VIN-1");

        var asset = profile.AssetsOwned.First();
        asset.Manufacturer.Should().Be("Winnebago");
        asset.Model.Should().Be("View 24D");
        asset.Year.Should().Be(2023);
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
                    AssetId = "VIN-1",
                    Status = AssetOwnershipStatus.Inactive,
                    RequestCount = 5,
                }
            ]
        };

        profile.ActivateOrRefreshAsset("VIN-1");

        profile.AssetsOwned.Should().HaveCount(2);
        profile.AssetsOwned.Should().ContainSingle(a => a.Status == AssetOwnershipStatus.Active);
        profile.AssetsOwned.First(a => a.Status == AssetOwnershipStatus.Active).RequestCount.Should().Be(1);
    }
}
