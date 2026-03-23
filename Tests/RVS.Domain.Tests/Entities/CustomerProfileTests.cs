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
    public void ActiveVins_ShouldReturnOnlyActiveAssets()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded { Vin = "VIN-1", Status = AssetOwnershipStatus.Active },
                new AssetOwnershipEmbedded { Vin = "VIN-2", Status = AssetOwnershipStatus.Inactive },
                new AssetOwnershipEmbedded { Vin = "VIN-3", Status = AssetOwnershipStatus.Active }
            ]
        };

        profile.ActiveVins.Should().BeEquivalentTo(["VIN-1", "VIN-3"]);
    }

    [Fact]
    public void GetActiveInteraction_ShouldReturnActiveAssetForVin()
    {
        var active = new AssetOwnershipEmbedded { Vin = "VIN-1", Status = AssetOwnershipStatus.Active };
        var profile = new CustomerProfile
        {
            AssetsOwned = [active]
        };

        profile.GetActiveInteraction("VIN-1").Should().BeSameAs(active);
    }

    [Fact]
    public void GetActiveInteraction_ShouldReturnNullForInactiveVin()
    {
        var profile = new CustomerProfile
        {
            AssetsOwned =
            [
                new AssetOwnershipEmbedded { Vin = "VIN-1", Status = AssetOwnershipStatus.Inactive }
            ]
        };

        profile.GetActiveInteraction("VIN-1").Should().BeNull();
    }
}
