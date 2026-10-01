using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

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

    // ---- Spec A-2 inbound keywords (issue #665) --------------------------------------------

    private static CustomerProfile ProfileForKeywords() => new()
    {
        Id = "cp-1",
        TenantId = "ten_acme_rv",
        Email = "kim@example.com",
        Phone = "(801) 555-1234",
        PhoneE164 = "+18015551234",
    };

    [Fact]
    public void ApplySmsKeyword_WhenOptOut_ShouldSetFlagAndStampBothTimes()
    {
        var profile = ProfileForKeywords();
        var at = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);

        var changed = profile.ApplySmsKeyword(SmsKeyword.OptOut, at);

        changed.Should().BeTrue();
        profile.SmsOptOut.Should().BeTrue();
        profile.SmsOptOutAtUtc.Should().Be(at);
        profile.SmsKeywordAtUtc.Should().Be(at);
    }

    [Fact]
    public void ApplySmsKeyword_WhenOptIn_ShouldClearOptOutAndStampOptIn()
    {
        var profile = ProfileForKeywords();
        profile.SmsOptOut = true;
        profile.SmsOptOutAtUtc = new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);
        var at = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);

        var changed = profile.ApplySmsKeyword(SmsKeyword.OptIn, at);

        changed.Should().BeTrue();
        profile.SmsOptOut.Should().BeFalse();
        profile.SmsOptOutAtUtc.Should().BeNull();
        profile.SmsOptInAtUtc.Should().Be(at);
        profile.SmsKeywordAtUtc.Should().Be(at);
    }

    [Fact]
    public void ApplySmsKeyword_WhenAlreadyOptedOut_ShouldKeepTheOriginalOptOutTime()
    {
        // The first opt-out is the one that matters for evidence; a repeat does not reset it.
        var first = new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);
        var profile = ProfileForKeywords();
        profile.ApplySmsKeyword(SmsKeyword.OptOut, first);

        profile.ApplySmsKeyword(SmsKeyword.OptOut, first.AddHours(1));

        profile.SmsOptOutAtUtc.Should().Be(first);
        profile.SmsKeywordAtUtc.Should().Be(first.AddHours(1));
    }

    [Fact]
    public void ApplySmsKeyword_WhenHelp_ShouldChangeNothing()
    {
        // HELP is answered with a fixed reply; it is not consent and not a revocation.
        var profile = ProfileForKeywords();
        profile.SmsOptOut = true;
        profile.SmsOptOutAtUtc = new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);

        var changed = profile.ApplySmsKeyword(SmsKeyword.Help, new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc));

        changed.Should().BeFalse();
        profile.SmsOptOut.Should().BeTrue();
        profile.SmsKeywordAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApplySmsKeyword_WhenNone_ShouldChangeNothing()
    {
        var profile = ProfileForKeywords();

        var changed = profile.ApplySmsKeyword(SmsKeyword.None, DateTime.UtcNow);

        changed.Should().BeFalse();
        profile.SmsOptOut.Should().BeFalse();
        profile.SmsKeywordAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApplySmsKeyword_WhenEventIsOlderThanTheLastKeyword_ShouldBeIgnored()
    {
        // Event Grid is unordered: a STOP then START pair can arrive reversed.
        var profile = ProfileForKeywords();
        var newer = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
        profile.ApplySmsKeyword(SmsKeyword.OptIn, newer);

        var changed = profile.ApplySmsKeyword(SmsKeyword.OptOut, newer.AddMinutes(-5));

        changed.Should().BeFalse();
        profile.SmsOptOut.Should().BeFalse();
        profile.SmsKeywordAtUtc.Should().Be(newer);
    }

    [Fact]
    public void ApplySmsKeyword_WhenTheSameEventArrivesTwice_ShouldBeANoOpTheSecondTime()
    {
        // Event Grid delivers at least once, so a duplicate must not count as a change.
        var profile = ProfileForKeywords();
        var at = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
        profile.ApplySmsKeyword(SmsKeyword.OptOut, at);

        var changed = profile.ApplySmsKeyword(SmsKeyword.OptOut, at);

        changed.Should().BeFalse();
        profile.SmsOptOut.Should().BeTrue();
    }

    [Fact]
    public void ApplySmsKeyword_WhenOptInFollowsOptOut_ShouldWinOnTheLaterEvent()
    {
        var profile = ProfileForKeywords();
        var at = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
        profile.ApplySmsKeyword(SmsKeyword.OptOut, at);

        var changed = profile.ApplySmsKeyword(SmsKeyword.OptIn, at.AddSeconds(30));

        changed.Should().BeTrue();
        profile.SmsOptOut.Should().BeFalse();
    }

    // ---- Spec A-2 intake opt-outs: set, never clear (issue #673) ----------------------------

    [Fact]
    public void ApplyIntakeOptOuts_WhenSmsBoxTickedAndNotOptedOut_ShouldSetFlagAndStampTime()
    {
        var profile = ProfileForKeywords();
        var at = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);

        profile.ApplyIntakeOptOuts(smsOptOut: true, emailOptOut: false, at);

        profile.SmsOptOut.Should().BeTrue();
        profile.SmsOptOutAtUtc.Should().Be(at);
    }

    [Fact]
    public void ApplyIntakeOptOuts_WhenSmsBoxUntickedAndStoredOptOut_ShouldLeaveItSet()
    {
        // The form never shows a stored opt-out, so an unticked box is not a request to clear it.
        var stored = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var profile = ProfileForKeywords();
        profile.SmsOptOut = true;
        profile.SmsOptOutAtUtc = stored;

        profile.ApplyIntakeOptOuts(smsOptOut: false, emailOptOut: false, stored.AddDays(18));

        profile.SmsOptOut.Should().BeTrue();
        profile.SmsOptOutAtUtc.Should().Be(stored);
    }

    [Fact]
    public void ApplyIntakeOptOuts_WhenSmsBoxTickedAndAlreadyOptedOut_ShouldKeepTheOriginalTime()
    {
        var stored = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var profile = ProfileForKeywords();
        profile.SmsOptOut = true;
        profile.SmsOptOutAtUtc = stored;

        profile.ApplyIntakeOptOuts(smsOptOut: true, emailOptOut: false, stored.AddDays(18));

        profile.SmsOptOutAtUtc.Should().Be(stored);
    }

    [Fact]
    public void ApplyIntakeOptOuts_WhenEmailBoxTickedAndNotOptedOut_ShouldSetFlagAndStampTime()
    {
        var profile = ProfileForKeywords();
        var at = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);

        profile.ApplyIntakeOptOuts(smsOptOut: false, emailOptOut: true, at);

        profile.EmailOptOut.Should().BeTrue();
        profile.EmailOptOutAtUtc.Should().Be(at);
    }

    [Fact]
    public void ApplyIntakeOptOuts_WhenEmailBoxUntickedAndStoredOptOut_ShouldLeaveItSet()
    {
        var stored = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var profile = ProfileForKeywords();
        profile.EmailOptOut = true;
        profile.EmailOptOutAtUtc = stored;

        profile.ApplyIntakeOptOuts(smsOptOut: false, emailOptOut: false, stored.AddDays(18));

        profile.EmailOptOut.Should().BeTrue();
        profile.EmailOptOutAtUtc.Should().Be(stored);
    }

    [Fact]
    public void ApplyIntakeOptOuts_WhenBothBoxesUnticked_ShouldChangeNothing()
    {
        var profile = ProfileForKeywords();

        profile.ApplyIntakeOptOuts(smsOptOut: false, emailOptOut: false, DateTime.UtcNow);

        profile.SmsOptOut.Should().BeFalse();
        profile.SmsOptOutAtUtc.Should().BeNull();
        profile.EmailOptOut.Should().BeFalse();
        profile.EmailOptOutAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApplyIntakeOptOuts_ShouldNotTouchTheKeywordClock()
    {
        // Only an inbound keyword advances SmsKeywordAtUtc; a later START must still be able to clear.
        var profile = ProfileForKeywords();

        profile.ApplyIntakeOptOuts(smsOptOut: true, emailOptOut: false, DateTime.UtcNow);

        profile.SmsKeywordAtUtc.Should().BeNull();
    }
}
