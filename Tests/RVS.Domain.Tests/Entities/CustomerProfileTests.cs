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
}
