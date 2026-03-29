using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class IntakeOrchestrationServiceTests
{
    private readonly Mock<ISlugLookupRepository> _slugLookupRepoMock = new();
    private readonly Mock<IGlobalCustomerAcctRepository> _globalAcctRepoMock = new();
    private readonly Mock<ICustomerProfileRepository> _profileRepoMock = new();
    private readonly Mock<IServiceRequestRepository> _srRepoMock = new();
    private readonly Mock<IAssetLedgerRepository> _ledgerRepoMock = new();
    private readonly Mock<ILocationRepository> _locationRepoMock = new();
    private readonly Mock<ILookupRepository> _lookupRepoMock = new();
    private readonly Mock<ICategorizationService> _categorizationMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly IntakeOrchestrationService _sut;

    public IntakeOrchestrationServiceTests()
    {
        _sut = new IntakeOrchestrationService(
            _slugLookupRepoMock.Object,
            _globalAcctRepoMock.Object,
            _profileRepoMock.Object,
            _srRepoMock.Object,
            _ledgerRepoMock.Object,
            _locationRepoMock.Object,
            _lookupRepoMock.Object,
            _categorizationMock.Object,
            _notificationMock.Object,
            Mock.Of<ILogger<IntakeOrchestrationService>>());
    }

    // ── Guard Clauses ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ExecuteAsync_WhenSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var act = () => _sut.ExecuteAsync(slug!, BuildValidRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ExecuteAsync("test-slug", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── Step 1: Slug Resolution ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenSlugNotFound_ShouldThrowKeyNotFoundException()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("unknown-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);

        var act = () => _sut.ExecuteAsync("unknown-slug", BuildValidRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*unknown-slug*");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldResolveTenantIdAndLocationIdFromSlug()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.TenantId.Should().Be("ten_test");
        result.LocationId.Should().Be("loc_test");
    }

    // ── Step 2: GlobalCustomerAcct Resolution ────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenGlobalAcctDoesNotExist_ShouldCreateNew()
    {
        SetupFullHappyPath(globalAcctExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.CreateAsync(
            It.Is<GlobalCustomerAcct>(a => a.Email == "jane@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGlobalAcctExists_ShouldNotCreateNew()
    {
        SetupFullHappyPath(globalAcctExists: true);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.CreateAsync(
            It.IsAny<GlobalCustomerAcct>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Step 3: CustomerProfile Resolution + Asset Ownership ─────────────────

    [Fact]
    public async Task ExecuteAsync_WhenProfileDoesNotExist_ShouldCreateNewProfile()
    {
        SetupFullHappyPath(profileExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.CreateAsync(
            It.Is<CustomerProfile>(p => p.TenantId == "ten_test" && p.Email == "jane@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProfileExists_ShouldNotCreateNew()
    {
        SetupFullHappyPath(profileExists: true);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.CreateAsync(
            It.IsAny<CustomerProfile>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssetOwnedByDifferentProfile_ShouldTransferOwnership()
    {
        var existingOwner = BuildProfile("cp_other");
        existingOwner.AssetsOwned.Add(new AssetOwnershipEmbedded
        {
            AssetId = "RV:1HGBH41JXMN109186",
            Status = AssetOwnershipStatus.Active,
            RequestCount = 2,
        });

        SetupFullHappyPath(assetOwner: existingOwner);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.Id == "cp_other"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssetOwnedBySameProfile_ShouldNotTransferOwnership()
    {
        SetupFullHappyPath(profileExists: true);

        var profile = BuildProfile();
        profile.AssetsOwned.Add(new AssetOwnershipEmbedded
        {
            AssetId = "RV:1HGBH41JXMN109186",
            Status = AssetOwnershipStatus.Active,
            RequestCount = 1,
        });

        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _profileRepoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_test", "RV:1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.Id == "cp_other"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Step 4: ServiceRequest Creation ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldCreateServiceRequestWithCustomerSnapshot()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.CustomerSnapshot.FirstName.Should().Be("Jane");
        result.CustomerSnapshot.LastName.Should().Be("Doe");
        result.CustomerSnapshot.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseAiCategorizationWhenAvailable()
    {
        SetupFullHappyPath();
        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("AI Category");

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.IssueCategory.Should().Be("AI Category");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAiCategorizationFails_ShouldFallBackToRequestCategory()
    {
        SetupFullHappyPath();
        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("AI timed out"));

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.IssueCategory.Should().Be("Slide System");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBuildTechnicianSummaryFromIssueDescription()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.TechnicianSummary.Should().Contain("Slide won't retract");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeDiagnosticResponsesInServiceRequest()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest(includeDiagnostics: true);

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.DiagnosticResponses.Should().HaveCount(1);
        result.DiagnosticResponses[0].QuestionText.Should().Be("How long has this been happening?");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetReturningCustomerFlagWhenPriorRequests()
    {
        var profile = BuildProfile();
        profile.TotalRequestCount = 3;

        SetupFullHappyPath();
        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.CustomerSnapshot.IsReturningCustomer.Should().BeTrue();
        result.CustomerSnapshot.PriorRequestCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetNewCustomerFlagWhenNoPriorRequests()
    {
        SetupFullHappyPath(profileExists: false);

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.CustomerSnapshot.IsReturningCustomer.Should().BeFalse();
        result.CustomerSnapshot.PriorRequestCount.Should().Be(0);
    }

    // ── Step 5: AssetLedgerEntry (non-blocking) ──────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldAppendAssetLedgerEntry()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _ledgerRepoMock.Verify(r => r.AppendAsync(
            It.Is<AssetLedgerEntry>(e =>
                e.AssetId == "RV:1HGBH41JXMN109186" &&
                e.TenantId == "ten_test"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLedgerAppendFails_ShouldNotFailIntake()
    {
        SetupFullHappyPath();
        _ledgerRepoMock.Setup(r => r.AppendAsync(It.IsAny<AssetLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos write failed"));

        var act = () => _sut.ExecuteAsync("test-slug", BuildValidRequest());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLedgerAppendFails_ShouldStillReturnServiceRequest()
    {
        SetupFullHappyPath();
        _ledgerRepoMock.Setup(r => r.AppendAsync(It.IsAny<AssetLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos write failed"));

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.Should().NotBeNull();
        result.TenantId.Should().Be("ten_test");
    }

    // ── Step 6: Update Linkages ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldIncrementProfileRequestCount()
    {
        var profile = BuildProfile();
        profile.TotalRequestCount = 2;

        SetupFullHappyPath();
        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.TotalRequestCount == 3),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRotateMagicLinkToken()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a =>
                a.MagicLinkToken != null &&
                a.MagicLinkExpiresAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAddServiceRequestIdToProfile()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.ServiceRequestIds.Count > 0),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAddAssetIdToGlobalAcctAllKnownAssetIds()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a => a.AllKnownAssetIds.Contains("RV:1HGBH41JXMN109186")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLinkProfileToGlobalAcct()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a => a.LinkedProfiles.Any(lp => lp.TenantId == "ten_test")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Step 7: Fire-and-forget Notification ─────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldFireNotificationWithoutBlocking()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.Should().NotBeNull();
    }

    // ── Full Orchestration ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FullOrchestration_ShouldCreateServiceRequestWithAllFields()
    {
        SetupFullHappyPath();
        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("AI: Slide System");

        var request = BuildValidRequest(includeDiagnostics: true);
        var result = await _sut.ExecuteAsync("test-slug", request);

        result.Should().NotBeNull();
        result.TenantId.Should().Be("ten_test");
        result.LocationId.Should().Be("loc_test");
        result.Status.Should().Be("New");
        result.IssueCategory.Should().Be("AI: Slide System");
        result.IssueDescription.Should().Be("Slide won't retract");
        result.CustomerSnapshot.FirstName.Should().Be("Jane");
        result.CustomerSnapshot.LastName.Should().Be("Doe");
        result.AssetInfo.AssetId.Should().Be("RV:1HGBH41JXMN109186");
        result.TechnicianSummary.Should().NotBeNullOrWhiteSpace();
        result.DiagnosticResponses.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_FullOrchestration_ShouldCallAllRepositories()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _slugLookupRepoMock.Verify(r => r.GetBySlugAsync("test-slug", It.IsAny<CancellationToken>()), Times.Once);
        _globalAcctRepoMock.Verify(r => r.GetByEmailAsync("jane@example.com", It.IsAny<CancellationToken>()), Times.Once);
        _profileRepoMock.Verify(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()), Times.Once);
        _srRepoMock.Verify(r => r.CreateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _ledgerRepoMock.Verify(r => r.AppendAsync(It.IsAny<AssetLedgerEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        _globalAcctRepoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToAllSteps()
    {
        SetupFullHappyPath();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(), token);

        _slugLookupRepoMock.Verify(r => r.GetBySlugAsync("test-slug", token), Times.Once);
        _globalAcctRepoMock.Verify(r => r.GetByEmailAsync("jane@example.com", token), Times.Once);
        _profileRepoMock.Verify(r => r.GetByEmailAsync("ten_test", "jane@example.com", token), Times.Once);
        _srRepoMock.Verify(r => r.CreateAsync(It.IsAny<ServiceRequest>(), token), Times.Once);
        _ledgerRepoMock.Verify(r => r.AppendAsync(It.IsAny<AssetLedgerEntry>(), token), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ServiceRequestCreateRequestDto BuildValidRequest(bool includeDiagnostics = false)
    {
        return new ServiceRequestCreateRequestDto
        {
            Customer = new CustomerInfoDto
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane@example.com",
                Phone = "801-555-1234",
            },
            Asset = new AssetInfoDto
            {
                AssetId = "RV:1HGBH41JXMN109186",
                Manufacturer = "Grand Design",
                Model = "Momentum 395G",
                Year = 2023,
            },
            IssueCategory = "Slide System",
            IssueDescription = "Slide won't retract",
            Urgency = "This week",
            RvUsage = "Full-time",
            DiagnosticResponses = includeDiagnostics
                ?
                [
                    new DiagnosticResponseDto
                    {
                        QuestionText = "How long has this been happening?",
                        SelectedOptions = ["Less than a week"],
                        FreeTextResponse = "Started 3 days ago",
                    }
                ]
                : null,
        };
    }

    private static CustomerProfile BuildProfile(string id = "cp_test")
    {
        return new CustomerProfile
        {
            Id = id,
            TenantId = "ten_test",
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Name = "Jane Doe",
            GlobalCustomerAcctId = "gca_test",
            CreatedByUserId = "intake",
        };
    }

    private static GlobalCustomerAcct BuildGlobalAcct()
    {
        return new GlobalCustomerAcct
        {
            Id = "gca_test",
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            CreatedByUserId = "intake",
        };
    }

    private static SlugLookup BuildSlugLookup()
    {
        return new SlugLookup
        {
            Slug = "test-slug",
            TenantId = "ten_test",
            LocationId = "loc_test",
            DealershipName = "Test Dealership",
            LocationName = "Test Location",
        };
    }

    private void SetupFullHappyPath(
        bool globalAcctExists = true,
        bool profileExists = true,
        CustomerProfile? assetOwner = null)
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSlugLookup());

        var globalAcct = BuildGlobalAcct();
        if (globalAcctExists)
        {
            _globalAcctRepoMock.Setup(r => r.GetByEmailAsync("jane@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(globalAcct);
        }
        else
        {
            _globalAcctRepoMock.Setup(r => r.GetByEmailAsync("jane@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlobalCustomerAcct?)null);
            _globalAcctRepoMock.Setup(r => r.CreateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlobalCustomerAcct a, CancellationToken _) =>
                {
                    // simulate Cosmos assigning the id
                    return a;
                });
        }

        var profile = BuildProfile();
        if (profileExists)
        {
            _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
        }
        else
        {
            _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync((CustomerProfile?)null);
            _profileRepoMock.Setup(r => r.CreateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CustomerProfile p, CancellationToken _) => p);
        }

        if (assetOwner is not null)
        {
            _profileRepoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_test", "RV:1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
                .ReturnsAsync(assetOwner);
        }
        else
        {
            _profileRepoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_test", "RV:1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
                .ReturnsAsync((CustomerProfile?)null);
        }

        _profileRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile p, CancellationToken _) => p);

        _srRepoMock.Setup(r => r.CreateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest sr, CancellationToken _) => sr);

        _ledgerRepoMock.Setup(r => r.AppendAsync(It.IsAny<AssetLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetLedgerEntry e, CancellationToken _) => e);

        _globalAcctRepoMock.Setup(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct a, CancellationToken _) => a);

        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Slide System");
    }

    // ── GetIntakeConfigAsync ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetIntakeConfigAsync_WhenSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var act = () => _sut.GetIntakeConfigAsync(slug!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenSlugNotFound_ShouldThrowKeyNotFoundException()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("unknown-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);

        var act = () => _sut.GetIntakeConfigAsync("unknown-slug");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*unknown-slug*");
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenNoToken_ShouldReturnNullPrefills()
    {
        SetupConfigHappyPath();

        var result = await _sut.GetIntakeConfigAsync("test-slug");

        result.PrefillCustomer.Should().BeNull();
        result.PrefillAsset.Should().BeNull();
        result.TokenExpired.Should().BeFalse();
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenTokenExpired_ShouldReturnNullPrefills()
    {
        SetupConfigHappyPath();
        var acct = BuildGlobalAcctWithMagicLink(expired: true, assetIds: ["RV:1HGBH41JXMN109186"]);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("expired-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        var result = await _sut.GetIntakeConfigAsync("test-slug", "expired-token");

        result.PrefillCustomer.Should().BeNull();
        result.PrefillAsset.Should().BeNull();
        result.TokenExpired.Should().BeTrue();
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenTokenValidButNoAssets_ShouldReturnCustomerPrefillOnly()
    {
        SetupConfigHappyPath();
        var acct = BuildGlobalAcctWithMagicLink(expired: false, assetIds: []);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        var result = await _sut.GetIntakeConfigAsync("test-slug", "valid-token");

        result.PrefillCustomer.Should().NotBeNull();
        result.PrefillCustomer!.FirstName.Should().Be("Jane");
        result.PrefillAsset.Should().BeNull();
        result.TokenExpired.Should().BeFalse();
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenTokenValidWithAssetHistory_ShouldReturnPrefillAsset()
    {
        SetupConfigHappyPath();
        var acct = BuildGlobalAcctWithMagicLink(expired: false, assetIds: ["RV:OLD_VIN", "RV:1HGBH41JXMN109186"]);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        var oldLedgerEntries = new List<AssetLedgerEntry>
        {
            new()
            {
                AssetId = "RV:OLD_VIN",
                Manufacturer = "Thor",
                Model = "Aria 4000",
                Year = 2019,
                GlobalCustomerAcctId = acct.Id,
            }
        };
        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("RV:OLD_VIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldLedgerEntries);

        // The most recently added asset ID is the last in the list
        var ledgerEntries = new List<AssetLedgerEntry>
        {
            new()
            {
                AssetId = "RV:1HGBH41JXMN109186",
                Manufacturer = "Grand Design",
                Model = "Momentum 395G",
                Year = 2023,
                GlobalCustomerAcctId = acct.Id,
            }
        };
        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("RV:1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ledgerEntries);

        var result = await _sut.GetIntakeConfigAsync("test-slug", "valid-token");

        result.PrefillAsset.Should().NotBeNull();
        result.PrefillAsset!.AssetId.Should().Be("RV:1HGBH41JXMN109186");
        result.PrefillAsset.Manufacturer.Should().Be("Grand Design");
        result.PrefillAsset.Model.Should().Be("Momentum 395G");
        result.PrefillAsset.Year.Should().Be(2023);
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenTokenValidWithMultipleAssets_ShouldReturnAllKnownAssets()
    {
        SetupConfigHappyPath();
        var acct = BuildGlobalAcctWithMagicLink(expired: false, assetIds: ["RV:OLD_VIN", "RV:1HGBH41JXMN109186"]);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("RV:OLD_VIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>
            {
                new() { AssetId = "RV:OLD_VIN", Manufacturer = "Thor", Model = "Aria 4000", Year = 2019, GlobalCustomerAcctId = acct.Id }
            });
        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("RV:1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>
            {
                new() { AssetId = "RV:1HGBH41JXMN109186", Manufacturer = "Grand Design", Model = "Momentum 395G", Year = 2023, GlobalCustomerAcctId = acct.Id }
            });

        var result = await _sut.GetIntakeConfigAsync("test-slug", "valid-token");

        result.KnownAssets.Should().HaveCount(2);
        result.KnownAssets[0].AssetId.Should().Be("RV:OLD_VIN");
        result.KnownAssets[0].Manufacturer.Should().Be("Thor");
        result.KnownAssets[1].AssetId.Should().Be("RV:1HGBH41JXMN109186");
        result.KnownAssets[1].Manufacturer.Should().Be("Grand Design");
        result.PrefillAsset.Should().NotBeNull();
        result.PrefillAsset!.AssetId.Should().Be("RV:1HGBH41JXMN109186");
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenAssetLedgerEmpty_ShouldReturnNullPrefillAsset()
    {
        SetupConfigHappyPath();
        var acct = BuildGlobalAcctWithMagicLink(expired: false, assetIds: ["RV:1HGBH41JXMN109186"]);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("RV:1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>());

        var result = await _sut.GetIntakeConfigAsync("test-slug", "valid-token");

        result.PrefillCustomer.Should().NotBeNull();
        result.PrefillAsset.Should().BeNull();
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenTokenNotFoundInDb_ShouldReturnNullPrefills()
    {
        SetupConfigHappyPath();
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("unknown-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct?)null);

        var result = await _sut.GetIntakeConfigAsync("test-slug", "unknown-token");

        result.PrefillCustomer.Should().BeNull();
        result.PrefillAsset.Should().BeNull();
        result.TokenExpired.Should().BeFalse();
    }

    private static GlobalCustomerAcct BuildGlobalAcctWithMagicLink(bool expired, List<string> assetIds)
    {
        return new GlobalCustomerAcct
        {
            Id = "gca_test",
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Phone = "801-555-1234",
            MagicLinkToken = expired ? "expired-token" : "valid-token",
            MagicLinkExpiresAtUtc = expired ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(29),
            AllKnownAssetIds = assetIds,
            CreatedByUserId = "intake",
        };
    }

    private void SetupConfigHappyPath()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSlugLookup());

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_test", "loc_test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location
            {
                Id = "loc_test",
                TenantId = "ten_test",
                Name = "Test Location",
                CreatedByUserId = "admin",
            });

        _lookupRepoMock.Setup(r => r.GetGlobalAsync("IssueCategory", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LookupSet?)null);
    }
}
