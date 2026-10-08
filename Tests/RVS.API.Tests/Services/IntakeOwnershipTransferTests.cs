using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using RVS.API.Options;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Packets;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Services;

/// <summary>
/// Ownership-transfer scenarios for <see cref="IntakeOrchestrationService"/>: an RV moving between
/// customers at the same or a different tenant and location, and the X-2 asset ledger staying
/// accurate through it. The repositories are backed by an in-memory store, so each scenario is a
/// sequence of real submissions and the invariants are checked against what was persisted.
/// </summary>
/// <remarks>
/// Invariants, checked after every scenario unless the scenario documents a known gap:
/// I1 within a tenant, at most one profile holds an Active entry for a key;
/// I2 ledger entries for a key equal the service requests submitted with it;
/// I3 within a tenant, the newest ledger entry's account is the Active owner's;
/// I4 a deactivated ownership entry is never reactivated.
/// </remarks>
public class IntakeOwnershipTransferTests
{
    // ── Fixtures ─────────────────────────────────────────────────────────────

    private const string TenantA = "ten_acme_rv";
    private const string TenantB = "ten_nova_rv";
    private const string SlugA1 = "acme-a1";
    private const string SlugA2 = "acme-a2";
    private const string SlugB1 = "nova-b1";

    private const string Vin = "1HGBH41JXMN109186";
    private const string Serial = "152263";
    private const string LanceKey = "LANCE:152263";

    private static readonly Customer Alice = new("Alice", "Archer", "alice@example.com", "801-555-0101");
    private static readonly Customer Bob = new("Bob", "Baker", "bob@example.com", "801-555-0102");
    private static readonly Customer Carol = new("Carol", "Carter", "carol@example.com", "801-555-0103");

    private sealed record Customer(string FirstName, string LastName, string Email, string Phone);

    private readonly InMemoryStore _store = new();
    private readonly Mock<ITenantConfigRepository> _tenantConfigRepoMock = new();
    private readonly IntakeOrchestrationService _sut;

    public IntakeOwnershipTransferTests()
    {
        _store.AddSlug(SlugA1, TenantA, "loc_a1", "Acme RV");
        _store.AddSlug(SlugA2, TenantA, "loc_a2", "Acme RV");
        _store.AddSlug(SlugB1, TenantB, "loc_b1", "Nova RV");

        var packetQueue = new Mock<IPacketGenerationQueue>();
        packetQueue.Setup(q => q.TryEnqueue(It.IsAny<PacketGenerationJob>())).Returns(true);

        _sut = new IntakeOrchestrationService(
            _store.SlugLookups.Object,
            _store.GlobalAccounts.Object,
            _store.Profiles.Object,
            _store.ServiceRequests.Object,
            _store.Ledger.Object,
            Mock.Of<ILocationRepository>(),
            Mock.Of<ILookupRepository>(),
            Mock.Of<ICategorizationService>(),
            Mock.Of<INotificationOrchestrator>(),
            packetQueue.Object,
            Mock.Of<IIntakeInviteRepository>(),
            _tenantConfigRepoMock.Object,
            Microsoft.Extensions.Options.Options.Create(new IntakeUrlOptions { BaseUrl = "https://rvintake.com" }),
            Mock.Of<ILogger<IntakeOrchestrationService>>());
    }

    // ── S: same owner, no transfer ───────────────────────────────────────────

    [Fact]
    public async Task S1_FirstSubmission_ShouldActivateOwnershipAndAppendOneLedgerEntry()
    {
        await SubmitAsync(Alice, SlugA1);

        var entry = ActiveEntry(TenantA, Alice, Vin);
        entry.RequestCount.Should().Be(1);
        _store.LedgerFor(Vin).Should().ContainSingle()
            .Which.GlobalCustomerAcctId.Should().Be(AccountId(Alice));
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task S2_SameOwnerSameLocation_ShouldRefreshWithoutTransfer()
    {
        await SubmitAsync(Alice, SlugA1);
        var firstSeen = ActiveEntry(TenantA, Alice, Vin).LastSeenAtUtc;

        await SubmitAsync(Alice, SlugA1);

        var entry = ActiveEntry(TenantA, Alice, Vin);
        entry.RequestCount.Should().Be(2);
        entry.LastSeenAtUtc.Should().BeOnOrAfter(firstSeen);
        Profile(TenantA, Alice).AssetsOwned.Should().ContainSingle();
        _store.LedgerFor(Vin).Should().HaveCount(2);
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task S3_SameOwnerOtherLocationSameTenant_ShouldUseTheSameProfile()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Alice, SlugA2);

        _store.ProfilesIn(TenantA).Should().ContainSingle();
        ActiveEntry(TenantA, Alice, Vin).RequestCount.Should().Be(2);
        _store.LedgerFor(Vin).Should().HaveCount(2).And.OnlyContain(e => e.TenantId == TenantA);
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task S4_SameOwnerOtherTenant_ShouldCreateASecondProfileAndLeaveTheFirstAlone()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Alice, SlugB1);

        ActiveEntry(TenantA, Alice, Vin).RequestCount.Should().Be(1);
        ActiveEntry(TenantB, Alice, Vin).RequestCount.Should().Be(1);
        _store.LedgerFor(Vin).Select(e => e.TenantId).Should().Equal(TenantA, TenantB);
        AssertInvariants(Vin);
    }

    // ── T: sold to another person ────────────────────────────────────────────

    [Fact]
    public async Task T1_SoldSameTenantSameLocation_ShouldDeactivateSellerAndActivateBuyer()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Bob, SlugA1);

        var sold = Profile(TenantA, Alice).AssetsOwned.Should().ContainSingle().Subject;
        sold.Status.Should().Be(AssetOwnershipStatus.Inactive);
        sold.DeactivatedAtUtc.Should().NotBeNull();
        sold.DeactivationReason.Should().Be("OwnershipTransfer");

        ActiveEntry(TenantA, Bob, Vin).RequestCount.Should().Be(1);
        _store.LedgerFor(Vin).Select(e => e.GlobalCustomerAcctId)
            .Should().Equal(AccountId(Alice), AccountId(Bob));
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task T2_SoldSameTenantOtherLocation_ShouldTransferAcrossTheTenant()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Bob, SlugA2);

        Profile(TenantA, Alice).GetActiveInteraction(Vin).Should().BeNull();
        ActiveEntry(TenantA, Bob, Vin).Should().NotBeNull();
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task T3_SoldToBuyerAtOtherTenant_KnownGap_ShouldLeaveSellerActiveAtTheOldTenant()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Bob, SlugB1);

        // Gap 1: transfers are tenant-scoped, so Tenant A still believes Alice owns the rig.
        ActiveEntry(TenantA, Alice, Vin).Should().NotBeNull();
        ActiveEntry(TenantB, Bob, Vin).Should().NotBeNull();

        // Only the ledger records the sale.
        _store.LedgerFor(Vin).Select(e => (e.TenantId, e.GlobalCustomerAcctId))
            .Should().Equal((TenantA, AccountId(Alice)), (TenantB, AccountId(Bob)));
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task T4_BuyerLaterVisitsSellersTenant_ShouldDeactivateSellerOnlyThen()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Bob, SlugB1);
        var saleRecordedAt = _store.LedgerFor(Vin)[1].SubmittedAtUtc;

        await SubmitAsync(Bob, SlugA1);

        var sold = Profile(TenantA, Alice).AssetsOwned.Single();
        sold.Status.Should().Be(AssetOwnershipStatus.Inactive);
        sold.DeactivatedAtUtc.Should().BeOnOrAfter(saleRecordedAt,
            "the old tenant learns of the sale only when the buyer turns up there");
        ActiveEntry(TenantA, Bob, Vin).Should().NotBeNull();
        _store.LedgerFor(Vin).Should().HaveCount(3);
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task T5_ResaleChain_ShouldKeepExactlyOneActiveOwnerAtEachStep()
    {
        foreach (var owner in new[] { Alice, Bob, Carol })
        {
            await SubmitAsync(owner, SlugA1);

            ActiveOwners(TenantA, Vin).Should().ContainSingle().Which.Email.Should().Be(owner.Email);
            AssertInvariants(Vin);
        }

        Profile(TenantA, Alice).GetActiveInteraction(Vin).Should().BeNull();
        Profile(TenantA, Bob).GetActiveInteraction(Vin).Should().BeNull();
        _store.LedgerFor(Vin).Select(e => e.GlobalCustomerAcctId)
            .Should().Equal(AccountId(Alice), AccountId(Bob), AccountId(Carol));
    }

    [Fact]
    public async Task T6_BuyBack_ShouldAddANewActiveEntryBesideTheOldInactiveOne()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Carol, SlugA1);
        await SubmitAsync(Alice, SlugA1);

        var history = Profile(TenantA, Alice).AssetsOwned;
        history.Should().HaveCount(2);
        history[0].Status.Should().Be(AssetOwnershipStatus.Inactive);
        history[1].Status.Should().Be(AssetOwnershipStatus.Active);
        history[1].RequestCount.Should().Be(1);
        Profile(TenantA, Carol).GetActiveInteraction(Vin).Should().BeNull();
        AssertInvariants(Vin);
    }

    // ── E: identity edge cases ───────────────────────────────────────────────

    [Fact]
    public async Task E1_SameOwnerNewEmail_KnownBehavior_ShouldBeTreatedAsASale()
    {
        await SubmitAsync(Alice, SlugA1);
        var aliceAgain = Alice with { Email = "alice.new@example.com" };

        await SubmitAsync(aliceAgain, SlugA1);

        // Email is the identity (#679), so a new address is a new customer.
        Profile(TenantA, Alice).GetActiveInteraction(Vin).Should().BeNull();
        ActiveEntry(TenantA, aliceAgain, Vin).Should().NotBeNull();
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task E2_HouseholdSharesEmail_ShouldNotTransfer()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Alice with { FirstName = "Sam" }, SlugA1);

        _store.ProfilesIn(TenantA).Should().ContainSingle();
        ActiveEntry(TenantA, Alice, Vin).RequestCount.Should().Be(2);
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task E3_VinTypedInLowercaseWithSpaces_ShouldResolveToTheSameKey()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Alice, SlugA1, assetId: " 1hgbh41jxm n109186 ");

        ActiveEntry(TenantA, Alice, Vin).RequestCount.Should().Be(2);
        _store.LedgerFor(Vin).Should().HaveCount(2);
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task E4_SerialWithManufacturerSpellingVariants_ShouldTransferOnTheSameKey()
    {
        await SubmitAsync(Alice, SlugA1, assetId: Serial, manufacturer: "Lance");
        await SubmitAsync(Bob, SlugA1, assetId: Serial, manufacturer: "LANCE CAMPER MFG. CORP");

        Profile(TenantA, Alice).GetActiveInteraction(LanceKey).Should().BeNull();
        ActiveEntry(TenantA, Bob, LanceKey).Should().NotBeNull();
        _store.LedgerFor(LanceKey).Should().HaveCount(2);
        AssertInvariants(LanceKey);
    }

    [Fact]
    public async Task E5_SameSerialDifferentManufacturer_ShouldNotTransfer()
    {
        await SubmitAsync(Alice, SlugA1, assetId: Serial, manufacturer: "Lance");
        await SubmitAsync(Bob, SlugA1, assetId: Serial, manufacturer: "Arctic Fox");

        ActiveEntry(TenantA, Alice, LanceKey).Should().NotBeNull();
        ActiveEntry(TenantA, Bob, "ARCTICFOX:152263").Should().NotBeNull();
        _store.LedgerFor(LanceKey).Should().ContainSingle();
        _store.LedgerFor("ARCTICFOX:152263").Should().ContainSingle();
        AssertInvariants(LanceKey);
        AssertInvariants("ARCTICFOX:152263");
    }

    [Fact]
    public async Task E6_SerialWithoutManufacturer_ShouldRecordNoHistory()
    {
        await SubmitAsync(Alice, SlugA1, assetId: Serial, manufacturer: "Lance");
        await SubmitAsync(Bob, SlugA1, assetId: Serial, manufacturer: null);

        ActiveEntry(TenantA, Alice, LanceKey).Should().NotBeNull();
        Profile(TenantA, Bob).AssetsOwned.Should().BeEmpty();
        _store.AllLedgerEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task E7_SameRigByVinThenSerial_KnownBehavior_ShouldSplitHistory()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Bob, SlugA1, assetId: Serial, manufacturer: "Lance");

        ActiveEntry(TenantA, Alice, Vin).Should().NotBeNull("a serial key cannot match a VIN key");
        ActiveEntry(TenantA, Bob, LanceKey).Should().NotBeNull();
        AssertInvariants(Vin);
        AssertInvariants(LanceKey);
    }

    [Fact]
    public async Task E8_NoIdentifier_ShouldRecordNoHistory()
    {
        await SubmitAsync(Alice, SlugA1);
        await SubmitAsync(Bob, SlugA1, assetId: "", manufacturer: null);

        ActiveEntry(TenantA, Alice, Vin).Should().NotBeNull();
        Profile(TenantA, Bob).AssetsOwned.Should().BeEmpty();
        _store.AllLedgerEntries.Should().ContainSingle();
    }

    // ── F: volume, tenant state and failures ─────────────────────────────────

    [Fact]
    public async Task F1_MultiIssueSubmissionByBuyer_ShouldTransferOnceAndLedgerEachRequest()
    {
        await SubmitAsync(Alice, SlugA1);

        await SubmitAsync(Bob, SlugA1, issueCount: 3);

        Profile(TenantA, Alice).GetActiveInteraction(Vin).Should().BeNull();
        var bob = Profile(TenantA, Bob);
        bob.TotalRequestCount.Should().Be(3);
        _store.LedgerFor(Vin).Where(e => e.GlobalCustomerAcctId == AccountId(Bob)).Should().HaveCount(3);

        // Gap 6: the ownership entry counts submissions, the ledger and profile count requests.
        bob.GetActiveInteraction(Vin)!.RequestCount.Should().Be(1);
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task F2_TenantDisabledWithinCaptureWindow_ShouldStillTransferAndLedger()
    {
        await SubmitAsync(Alice, SlugA1);
        DisableTenant(TenantA, daysAgo: 59);

        await SubmitAsync(Bob, SlugA1);

        ActiveEntry(TenantA, Bob, Vin).Should().NotBeNull();
        _store.LedgerFor(Vin).Should().HaveCount(2);
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task F3_TenantIntakeExpired_ShouldRefuseAndLeaveOwnershipAndLedgerUntouched()
    {
        await SubmitAsync(Alice, SlugA1);
        DisableTenant(TenantA, daysAgo: 61);

        var act = () => SubmitAsync(Bob, SlugA1);

        await act.Should().ThrowAsync<IntakeExpiredException>();
        ActiveEntry(TenantA, Alice, Vin).Should().NotBeNull();
        _store.ProfileByEmail(TenantA, Bob.Email).Should().BeNull();
        _store.LedgerFor(Vin).Should().ContainSingle();
        AssertInvariants(Vin);
    }

    [Fact]
    public async Task F4_LedgerAppendFails_KnownGap_ShouldStillSubmitAndTransferWithAMissingEntry()
    {
        await SubmitAsync(Alice, SlugA1);
        _store.FailLedgerAppends = true;

        var result = await SubmitAsync(Bob, SlugA1);

        result.ServiceRequest.Id.Should().NotBeNullOrEmpty();
        ActiveEntry(TenantA, Bob, Vin).Should().NotBeNull();

        // Gap 3: the entry is lost with only a warning logged, so I2 and I3 no longer hold.
        _store.ServiceRequestsFor(Vin).Should().HaveCount(2);
        _store.LedgerFor(Vin).Should().ContainSingle()
            .Which.GlobalCustomerAcctId.Should().Be(AccountId(Alice));
    }

    [Fact]
    public async Task F5_ServiceRequestCreateFails_KnownGap_ShouldLeaveOwnershipMovedWithNoRequest()
    {
        await SubmitAsync(Alice, SlugA1);
        _store.FailServiceRequestCreates = true;

        var act = () => SubmitAsync(Bob, SlugA1);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // Gap 4: step 3 has already persisted the transfer before the request exists.
        Profile(TenantA, Alice).GetActiveInteraction(Vin).Should().BeNull();
        ActiveEntry(TenantA, Bob, Vin).Should().NotBeNull();
        Profile(TenantA, Bob).ServiceRequestIds.Should().BeEmpty();
        _store.ServiceRequestsFor(Vin).Should().ContainSingle();
        _store.LedgerFor(Vin).Should().ContainSingle()
            .Which.GlobalCustomerAcctId.Should().Be(AccountId(Alice));
    }

    [Fact]
    public async Task F6_ConcurrentBuyers_KnownGap_ShouldLeaveTwoActiveOwners()
    {
        await SubmitAsync(Alice, SlugA1);

        // Bob reads "Alice owns it"; Carol's whole submission lands before Bob writes. No ETag
        // guards the writes, so nothing stops Bob from also activating.
        _store.BeforeActiveOwnerLookupReturns = () => SubmitAsync(Carol, SlugA1);
        await SubmitAsync(Bob, SlugA1);

        // Gap 5: I1 is broken.
        ActiveOwners(TenantA, Vin).Select(p => p.Email)
            .Should().BeEquivalentTo(Bob.Email, Carol.Email);
        _store.LedgerFor(Vin).Should().HaveCount(3);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task<(ServiceRequest ServiceRequest, IReadOnlyList<ServiceRequest> ServiceRequests, string? MagicLinkToken, DateTime? MagicLinkExpiresAtUtc)> SubmitAsync(
        Customer customer,
        string slug,
        string assetId = Vin,
        string? manufacturer = "Grand Design",
        int issueCount = 1)
    {
        var request = new ServiceRequestCreateRequestDto
        {
            Customer = new CustomerInfoDto
            {
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Phone = customer.Phone,
                PreferredContact = "Email",
            },
            Asset = new AssetInfoDto { AssetId = assetId, Manufacturer = manufacturer, Model = "Momentum 395G", Year = 2023 },
            IssueCategory = "Slides",
            IssueDescription = "Slide won't retract",
            AdditionalIssues = issueCount > 1
                ? Enumerable.Range(2, issueCount - 1)
                    .Select(n => new IntakeIssueDto { IssueCategory = "Awning", IssueDescription = $"Issue {n}" })
                    .ToList()
                : null,
        };

        return _sut.ExecuteAsync(slug, request);
    }

    private void DisableTenant(string tenantId, int daysAgo) =>
        _tenantConfigRepoMock.Setup(r => r.GetAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantConfig
            {
                Id = tenantId,
                TenantId = tenantId,
                AccessGate = new TenantAccessGateEmbedded
                {
                    LoginsEnabled = false,
                    DisabledReason = "PastDue",
                    DisabledAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo),
                },
                CreatedByUserId = "admin",
            });

    private static string AccountId(Customer customer) => GlobalCustomerAcct.IdForEmail(customer.Email);

    private CustomerProfile Profile(string tenantId, Customer customer) =>
        _store.ProfileByEmail(tenantId, customer.Email)
            ?? throw new InvalidOperationException($"No profile for {customer.Email} in {tenantId}.");

    private AssetOwnershipEmbedded ActiveEntry(string tenantId, Customer customer, string key) =>
        Profile(tenantId, customer).GetActiveInteraction(key)
            ?? throw new InvalidOperationException($"{customer.Email} has no Active entry for {key} in {tenantId}.");

    private List<CustomerProfile> ActiveOwners(string tenantId, string key) =>
        _store.ProfilesIn(tenantId).Where(p => p.GetActiveInteraction(key) is not null).ToList();

    private void AssertInvariants(string key)
    {
        var ledger = _store.LedgerFor(key);

        // I2: one ledger entry per service request carrying this key, none missing or extra.
        ledger.Select(e => e.ServiceRequestId)
            .Should().BeEquivalentTo(_store.ServiceRequestsFor(key).Select(sr => sr.Id), "I2 for {0}", key);

        foreach (var tenantId in _store.TenantIds)
        {
            var owners = ActiveOwners(tenantId, key);

            // I1: at most one Active owner per tenant.
            owners.Should().HaveCountLessThanOrEqualTo(1, "I1 for {0} in {1}", key, tenantId);

            // I3: the newest entry written in this tenant names the Active owner.
            var newest = ledger.LastOrDefault(e => e.TenantId == tenantId);
            if (newest is not null)
            {
                owners.Should().ContainSingle("I3 for {0} in {1}", key, tenantId)
                    .Which.GlobalCustomerAcctId.Should().Be(newest.GlobalCustomerAcctId);
            }

            // I4: anything deactivated stays deactivated.
            _store.ProfilesIn(tenantId)
                .SelectMany(p => p.AssetsOwned)
                .Where(a => a.DeactivatedAtUtc is not null)
                .Should().OnlyContain(a => a.Status == AssetOwnershipStatus.Inactive, "I4 for {0}", key);
        }
    }

    /// <summary>
    /// Repository mocks backed by in-memory collections. Every read and write is a JSON round trip,
    /// as with Cosmos, so the service can't mutate stored state without writing it back.
    /// </summary>
    private sealed class InMemoryStore
    {
        private readonly Dictionary<string, SlugLookup> _slugs = [];
        private readonly Dictionary<string, string> _accounts = [];
        private readonly List<string> _profiles = [];
        private readonly List<ServiceRequest> _serviceRequests = [];
        private readonly List<string> _ledger = [];

        public Mock<ISlugLookupRepository> SlugLookups { get; } = new();
        public Mock<IGlobalCustomerAcctRepository> GlobalAccounts { get; } = new();
        public Mock<ICustomerProfileRepository> Profiles { get; } = new();
        public Mock<IServiceRequestRepository> ServiceRequests { get; } = new();
        public Mock<IAssetLedgerRepository> Ledger { get; } = new();

        public bool FailLedgerAppends { get; set; }
        public bool FailServiceRequestCreates { get; set; }

        /// <summary>Runs once, after the active-owner lookup has read and before it returns.</summary>
        public Func<Task>? BeforeActiveOwnerLookupReturns { get; set; }

        public IEnumerable<string> TenantIds => _slugs.Values.Select(s => s.TenantId).Distinct();

        public IReadOnlyList<AssetLedgerEntry> AllLedgerEntries => _ledger.Select(Read<AssetLedgerEntry>).ToList();

        public InMemoryStore()
        {
            SlugLookups.Setup(r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string slug, CancellationToken _) => _slugs.GetValueOrDefault(slug));

            GlobalAccounts.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string email, CancellationToken _) =>
                    _accounts.TryGetValue(email, out var json) ? Read<GlobalCustomerAcct>(json) : null);
            GlobalAccounts.Setup(r => r.CreateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlobalCustomerAcct a, CancellationToken _) =>
                {
                    if (_accounts.ContainsKey(a.Email)) throw new ConflictException("account exists");
                    _accounts[a.Email] = Write(a);
                    return Read<GlobalCustomerAcct>(_accounts[a.Email]);
                });
            GlobalAccounts.Setup(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlobalCustomerAcct a, CancellationToken _) =>
                {
                    _accounts[a.Email] = Write(a);
                    return Read<GlobalCustomerAcct>(_accounts[a.Email]);
                });

            Profiles.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tenantId, string email, CancellationToken _) => ProfileByEmail(tenantId, email));
            Profiles.Setup(r => r.CreateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CustomerProfile p, CancellationToken _) =>
                {
                    if (ProfileByEmail(p.TenantId, p.Email) is not null) throw new ConflictException("profile exists");
                    _profiles.Add(Write(p));
                    return Read<CustomerProfile>(_profiles[^1]);
                });
            Profiles.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CustomerProfile p, CancellationToken _) =>
                {
                    var index = _profiles.FindIndex(json => Read<CustomerProfile>(json) is var s && s.TenantId == p.TenantId && s.Id == p.Id);
                    _profiles[index] = Write(p);
                    return Read<CustomerProfile>(_profiles[index]);
                });
            Profiles.Setup(r => r.GetByActiveAssetIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (string tenantId, string assetId, CancellationToken _) =>
                {
                    var owner = ProfilesIn(tenantId).FirstOrDefault(p => p.GetActiveInteraction(assetId) is not null);
                    if (BeforeActiveOwnerLookupReturns is { } interleave)
                    {
                        BeforeActiveOwnerLookupReturns = null;
                        await interleave();
                    }
                    return owner;
                });

            ServiceRequests.Setup(r => r.CreateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceRequest sr, CancellationToken _) =>
                {
                    if (FailServiceRequestCreates) throw new InvalidOperationException("Cosmos unavailable");
                    _serviceRequests.Add(sr);
                    return sr;
                });

            Ledger.Setup(r => r.AppendAsync(It.IsAny<AssetLedgerEntry>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetLedgerEntry e, CancellationToken _) =>
                {
                    if (FailLedgerAppends) throw new InvalidOperationException("Cosmos unavailable");
                    _ledger.Add(Write(e));
                    return e;
                });
        }

        public void AddSlug(string slug, string tenantId, string locationId, string dealershipName) =>
            _slugs[slug] = new SlugLookup
            {
                Slug = slug,
                TenantId = tenantId,
                LocationId = locationId,
                DealershipName = dealershipName,
                LocationName = locationId,
            };

        public CustomerProfile? ProfileByEmail(string tenantId, string email) =>
            ProfilesIn(tenantId).FirstOrDefault(p => p.Email == email);

        public List<CustomerProfile> ProfilesIn(string tenantId) =>
            _profiles.Select(Read<CustomerProfile>).Where(p => p.TenantId == tenantId).ToList();

        public IReadOnlyList<AssetLedgerEntry> LedgerFor(string key) =>
            AllLedgerEntries.Where(e => e.AssetId == key).ToList();

        public IReadOnlyList<ServiceRequest> ServiceRequestsFor(string key) =>
            _serviceRequests
                .Where(sr => VehicleHistoryKey.For(sr.AssetInfo.AssetId, sr.AssetInfo.Manufacturer) == key)
                .ToList();

        private static string Write<T>(T value) => JsonConvert.SerializeObject(value);

        private static T Read<T>(string json) => JsonConvert.DeserializeObject<T>(json)!;
    }
}
