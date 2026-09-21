using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RVS.API.Options;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Packets;
using RVS.Domain.Security;
using RVS.Domain.Validation;

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
    private readonly Mock<INotificationOrchestrator> _notificationOrchestratorMock = new();
    private readonly Mock<IPacketGenerationQueue> _packetQueueMock = new();
    private readonly Mock<IIntakeInviteRepository> _inviteRepoMock = new();
    private readonly IntakeOrchestrationService _sut;

    public IntakeOrchestrationServiceTests()
    {
        _packetQueueMock.Setup(q => q.TryEnqueue(It.IsAny<PacketGenerationJob>())).Returns(true);

        var intakeUrlOptions = Microsoft.Extensions.Options.Options.Create(new IntakeUrlOptions { BaseUrl = "https://rvintake.com" });

        _sut = new IntakeOrchestrationService(
            _slugLookupRepoMock.Object,
            _globalAcctRepoMock.Object,
            _profileRepoMock.Object,
            _srRepoMock.Object,
            _ledgerRepoMock.Object,
            _locationRepoMock.Object,
            _lookupRepoMock.Object,
            _categorizationMock.Object,
            _notificationOrchestratorMock.Object,
            _packetQueueMock.Object,
            _inviteRepoMock.Object,
            intakeUrlOptions,
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

        result.ServiceRequest.TenantId.Should().Be("ten_test");
        result.ServiceRequest.LocationId.Should().Be("loc_test");
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

    // ── Step 2: Opt-outs are not written to GlobalCustomerAcct (issue #673) ──
    // Nothing reads that copy — the invite check and confirmations both use CustomerProfile.

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ExecuteAsync_WhenNewGlobalAcct_ShouldNotWriteOptOuts(bool smsOptOut, bool emailOptOut)
    {
        SetupFullHappyPath(globalAcctExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(smsOptOut: smsOptOut, emailOptOut: emailOptOut));

        _globalAcctRepoMock.Verify(r => r.CreateAsync(
            It.Is<GlobalCustomerAcct>(a => !a.SmsOptOut && !a.SmsOptOutAtUtc.HasValue
                                           && !a.EmailOptOut && !a.EmailOptOutAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistingGlobalAcct_WhenBoxesTicked_ShouldNotWriteOptOuts()
    {
        SetupFullHappyPath(globalAcctExists: true);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(smsOptOut: true, emailOptOut: true));

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a => !a.SmsOptOut && !a.SmsOptOutAtUtc.HasValue
                                           && !a.EmailOptOut && !a.EmailOptOutAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistingGlobalAcct_WhenBoxesUnticked_ShouldLeaveLegacyOptOutsUnchanged()
    {
        // Accounts written before #673 may still carry opt-outs; a submission leaves them alone.
        var smsAt = DateTime.UtcNow.AddDays(-30);
        var emailAt = DateTime.UtcNow.AddDays(-10);
        SetupFullHappyPath(globalAcctExists: true);
        var existing = BuildGlobalAcct();
        existing.SmsOptOut = true;
        existing.SmsOptOutAtUtc = smsAt;
        existing.EmailOptOut = true;
        existing.EmailOptOutAtUtc = emailAt;
        _globalAcctRepoMock.Setup(r => r.GetByEmailAsync("jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(smsOptOut: false, emailOptOut: false));

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a => a.SmsOptOut && a.SmsOptOutAtUtc == smsAt
                                           && a.EmailOptOut && a.EmailOptOutAtUtc == emailAt),
            It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task ExecuteAsync_WhenProfileIsCreated_ShouldStoreThePhoneInE164()
    {
        // An inbound STOP arrives with a phone number and no tenant (issue #665), so the number
        // has to be stored in a form a lookup can match. The typed form is kept as well.
        SetupFullHappyPath(profileExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.CreateAsync(
            It.Is<CustomerProfile>(p => p.PhoneE164 == "+18015551234" && p.Phone == "801-555-1234"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProfileExists_ShouldRefreshThePhoneInE164()
    {
        SetupFullHappyPath(profileExists: true);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.PhoneE164 == "+18015551234"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPhoneCannotNormalise_ShouldLeaveE164Null()
    {
        // The profile still keeps what the customer typed; only the matchable form is absent.
        SetupFullHappyPath(profileExists: false);
        var request = BuildValidRequest();
        request = request with { Customer = request.Customer with { Phone = "555-1234" } };

        await _sut.ExecuteAsync("test-slug", request);

        _profileRepoMock.Verify(r => r.CreateAsync(
            It.Is<CustomerProfile>(p => p.PhoneE164 == null && p.Phone == "555-1234"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Step 3: Opt-out Timestamp Stamping (CustomerProfile) ─────────────────

    [Fact]
    public async Task ExecuteAsync_WhenNewProfile_WithSmsOptOut_ShouldStampSmsOptOutAtUtc()
    {
        SetupFullHappyPath(profileExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(smsOptOut: true));

        _profileRepoMock.Verify(r => r.CreateAsync(
            It.Is<CustomerProfile>(p => p.SmsOptOut && p.SmsOptOutAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewProfile_WithEmailOptOut_ShouldStampEmailOptOutAtUtc()
    {
        SetupFullHappyPath(profileExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(emailOptOut: true));

        _profileRepoMock.Verify(r => r.CreateAsync(
            It.Is<CustomerProfile>(p => p.EmailOptOut && p.EmailOptOutAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewProfile_WithNoOptOut_ShouldNotSetOptOutTimestamps()
    {
        SetupFullHappyPath(profileExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(smsOptOut: false, emailOptOut: false));

        _profileRepoMock.Verify(r => r.CreateAsync(
            It.Is<CustomerProfile>(p => !p.SmsOptOutAtUtc.HasValue && !p.EmailOptOutAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistingProfile_WhenSmsOptOutFirstSet_ShouldStampSmsOptOutAtUtc()
    {
        SetupFullHappyPath(profileExists: true);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(smsOptOut: true));

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.SmsOptOut && p.SmsOptOutAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistingProfile_WhenStoredSmsOptOutAndBoxUnticked_ShouldStayOptedOut()
    {
        // Intake sets an opt-out but never clears one: the form never shows the stored value,
        // so an unticked box is not a choice to opt back in (issue #673).
        var storedAt = DateTime.UtcNow.AddDays(-5);
        var existingProfile = BuildProfile();
        existingProfile.SmsOptOut = true;
        existingProfile.SmsOptOutAtUtc = storedAt;

        SetupFullHappyPath(profileExists: true);
        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProfile);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(smsOptOut: false));

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.SmsOptOut && p.SmsOptOutAtUtc == storedAt),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistingProfile_WhenEmailOptOutFirstSet_ShouldStampEmailOptOutAtUtc()
    {
        SetupFullHappyPath(profileExists: true);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(emailOptOut: true));

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.EmailOptOut && p.EmailOptOutAtUtc.HasValue),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistingProfile_WhenStoredEmailOptOutAndBoxUnticked_ShouldStayOptedOut()
    {
        // Intake sets an opt-out but never clears one: the form never shows the stored value,
        // so an unticked box is not a choice to opt back in (issue #673).
        var storedAt = DateTime.UtcNow.AddDays(-5);
        var existingProfile = BuildProfile();
        existingProfile.EmailOptOut = true;
        existingProfile.EmailOptOutAtUtc = storedAt;

        SetupFullHappyPath(profileExists: true);
        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProfile);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(emailOptOut: false));

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.EmailOptOut && p.EmailOptOutAtUtc == storedAt),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssetOwnedByDifferentProfile_ShouldTransferOwnership()
    {
        var existingOwner = BuildProfile("cp_other");
        existingOwner.AssetsOwned.Add(new AssetOwnershipEmbedded
        {
            AssetId = "1HGBH41JXMN109186",
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
            AssetId = "1HGBH41JXMN109186",
            Status = AssetOwnershipStatus.Active,
            RequestCount = 1,
        });

        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _profileRepoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_test", "1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.Id == "cp_other"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPopulateAssetMetadataOnCustomerProfile()
    {
        SetupFullHappyPath();

        CustomerProfile? capturedProfile = null;
        _profileRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerProfile, CancellationToken>((p, _) => capturedProfile = p)
            .ReturnsAsync((CustomerProfile p, CancellationToken _) => p);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        capturedProfile.Should().NotBeNull();
        var asset = capturedProfile!.AssetsOwned.First(a => a.AssetId == "1HGBH41JXMN109186" && a.Status == AssetOwnershipStatus.Active);
        asset.Manufacturer.Should().Be("Grand Design");
        asset.Model.Should().Be("Momentum 395G");
        asset.Year.Should().Be(2023);
    }

    // ── Step 4: ServiceRequest Creation ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldCreateServiceRequestWithCustomerSnapshot()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.ServiceRequest.CustomerSnapshot.FirstName.Should().Be("Jane");
        result.ServiceRequest.CustomerSnapshot.LastName.Should().Be("Doe");
        result.ServiceRequest.CustomerSnapshot.Email.Should().Be("jane@example.com");
        result.ServiceRequest.CustomerSnapshot.Phone.Should().Be("801-555-1234");
        result.ServiceRequest.CustomerSnapshot.PreferredContact.Should().Be("Phone");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewGlobalAcct_ShouldSavePhone()
    {
        SetupFullHappyPath(globalAcctExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.CreateAsync(
            It.Is<GlobalCustomerAcct>(a => a.Phone == "801-555-1234"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistingGlobalAcct_ShouldUpdatePhone()
    {
        SetupFullHappyPath(globalAcctExists: true);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a => a.Phone == "801-555-1234"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewProfile_ShouldSavePhone()
    {
        SetupFullHappyPath(profileExists: false);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.CreateAsync(
            It.Is<CustomerProfile>(p => p.Phone == "801-555-1234"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistingProfile_ShouldUpdatePhone()
    {
        SetupFullHappyPath(profileExists: true);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _profileRepoMock.Verify(r => r.UpdateAsync(
            It.Is<CustomerProfile>(p => p.Phone == "801-555-1234"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistTheCustomerSubmittedCategory_AndNotReRunAiCategorization()
    {
        // A-5: the AI suggestion is advisory and was already offered in the wizard. The
        // server stores what the customer submitted and never re-runs categorization here.
        SetupFullHappyPath();
        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HVAC");

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.ServiceRequest.IssueCategory.Should().Be("Slides");
        _categorizationMock.Verify(
            c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSubmittedCategoryIsNotInTheVocabulary_ShouldStoreOther()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest();
        request = request with { IssueCategory = "Transmission Fluid" };

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.IssueCategory.Should().Be(IssueCategoryVocabulary.FallbackCode);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNormalizeSubmittedCategoryCasing()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest() with { IssueCategory = "  slides  " };

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.IssueCategory.Should().Be("Slides");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistTheVerbatimIssueDescription()
    {
        // Issue #601: the pre-curation text is what the packet's "Complaint — word for word"
        // section renders, so it has to survive intake alongside the curated description.
        SetupFullHappyPath();
        var request = BuildValidRequest() with
        {
            IssueDescriptionVerbatim = "  um so like the slide it uh wont retract  ",
        };

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.IssueDescriptionVerbatim.Should().Be("um so like the slide it uh wont retract");
        result.ServiceRequest.IssueDescription.Should().Be("Slide won't retract");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenNoVerbatimDescriptionSupplied_ShouldLeaveItNull(string? verbatim)
    {
        SetupFullHappyPath();
        var request = BuildValidRequest() with { IssueDescriptionVerbatim = verbatim };

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.IssueDescriptionVerbatim.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCapabilityMismatchNote_TechnicianSummaryShouldBeNull()
    {
        // Issue #601: the packet's Preliminary assessment section (labelled AI-generated)
        // must never echo the literal issue description — that duplicates the Complaint
        // section, which shows the customer's words verbatim. With no capability-mismatch
        // note there is nothing distinct to say, so the seed text is null.
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.ServiceRequest.TechnicianSummary.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCapabilityMismatchNoteProvided_TechnicianSummaryShouldBeTheNoteOnly()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest() with
        {
            CapabilityMismatchNote = "Capability 'diesel-service' was requested but is not available at this location. User was advised to contact the location directly."
        };

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.TechnicianSummary.Should()
            .Be("Capability 'diesel-service' was requested but is not available at this location. User was advised to contact the location directly.");
        result.ServiceRequest.TechnicianSummary.Should().NotContain("Slide won't retract");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeDiagnosticResponsesInServiceRequest()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest(includeDiagnostics: true);

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.DiagnosticResponses.Should().HaveCount(1);
        result.ServiceRequest.DiagnosticResponses[0].QuestionText.Should().Be("How long has this been happening?");
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

        result.ServiceRequest.CustomerSnapshot.IsReturningCustomer.Should().BeTrue();
        result.ServiceRequest.CustomerSnapshot.PriorRequestCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetNewCustomerFlagWhenNoPriorRequests()
    {
        SetupFullHappyPath(profileExists: false);

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.ServiceRequest.CustomerSnapshot.IsReturningCustomer.Should().BeFalse();
        result.ServiceRequest.CustomerSnapshot.PriorRequestCount.Should().Be(0);
    }

    // ── Step 5: AssetLedgerEntry (non-blocking) ──────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldAppendAssetLedgerEntry()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _ledgerRepoMock.Verify(r => r.AppendAsync(
            It.Is<AssetLedgerEntry>(e =>
                e.AssetId == "1HGBH41JXMN109186" &&
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

        result.ServiceRequest.Should().NotBeNull();
        result.ServiceRequest.TenantId.Should().Be("ten_test");
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
    public async Task ExecuteAsync_WhenValidTokenExists_ShouldNotRotateMagicLinkToken()
    {
        SetupFullHappyPath();

        // Arrange: pre-populate a valid (non-expired) token on the global account
        var existingToken = "existing:token";
        var existingExpiry = DateTime.UtcNow.AddDays(60);
        _globalAcctRepoMock.Setup(r => r.GetByEmailAsync("jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalCustomerAcct
            {
                Id = "gca_test",
                Email = "jane@example.com",
                FirstName = "Jane",
                LastName = "Doe",
                CreatedByUserId = "intake",
                MagicLinkToken = existingToken,
                MagicLinkExpiresAtUtc = existingExpiry,
            });

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a =>
                a.MagicLinkToken == existingToken &&
                a.MagicLinkExpiresAtUtc == existingExpiry),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenIsNull_ShouldGenerateNewMagicLinkToken()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a =>
                a.MagicLinkToken != null &&
                a.MagicLinkExpiresAtUtc.HasValue &&
                a.MagicLinkExpiresAtUtc.Value > DateTime.UtcNow.AddDays(89)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenIsExpired_ShouldGenerateNewMagicLinkToken()
    {
        SetupFullHappyPath();

        // Arrange: pre-populate an expired token on the global account
        _globalAcctRepoMock.Setup(r => r.GetByEmailAsync("jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalCustomerAcct
            {
                Id = "gca_test",
                Email = "jane@example.com",
                FirstName = "Jane",
                LastName = "Doe",
                CreatedByUserId = "intake",
                MagicLinkToken = "old:expiredtoken",
                MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
            });

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _globalAcctRepoMock.Verify(r => r.UpdateAsync(
            It.Is<GlobalCustomerAcct>(a =>
                a.MagicLinkToken != "old:expiredtoken" &&
                a.MagicLinkToken != null &&
                a.MagicLinkExpiresAtUtc.HasValue &&
                a.MagicLinkExpiresAtUtc.Value > DateTime.UtcNow.AddDays(89)),
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
            It.Is<GlobalCustomerAcct>(a => a.AllKnownAssetIds.Contains("1HGBH41JXMN109186")),
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

    [Fact]
    public async Task ExecuteAsync_ShouldNotifyWithTheTenantLocationAndE164Phone()
    {
        SetupFullHappyPath();

        await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        // BuildValidRequest submits "801-555-1234"; ACS only accepts E.164 (issue #661).
        _notificationOrchestratorMock.Verify(n => n.SendServiceRequestConfirmationAsync(
            "ten_test", "loc_test", It.IsAny<string?>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>(),
            "+18015551234",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotifyWithTheNormalisedPreferredContactAndOptOuts()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest(emailOptOut: true);
        request = request with { Customer = request.Customer with { PreferredContact = "  text " } };

        await _sut.ExecuteAsync("test-slug", request);

        // PreferredContact chooses the confirmation channel; the opt-outs veto it (issue #662).
        _notificationOrchestratorMock.Verify(n => n.SendServiceRequestConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), "Text",
            false, true, It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoredSmsOptOutAndBoxUnticked_ShouldNotifyWithTheStoredOptOut()
    {
        // The confirmation follows the profile after the write, not this submission's boxes (issue #673).
        var existingProfile = BuildProfile();
        existingProfile.SmsOptOut = true;
        existingProfile.SmsOptOutAtUtc = DateTime.UtcNow.AddDays(-5);
        SetupFullHappyPath(profileExists: true);
        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProfile);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(smsOptOut: false));

        _notificationOrchestratorMock.Verify(n => n.SendServiceRequestConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            true, false, It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoredEmailOptOutAndBoxUnticked_ShouldNotifyWithTheStoredOptOut()
    {
        var existingProfile = BuildProfile();
        existingProfile.EmailOptOut = true;
        existingProfile.EmailOptOutAtUtc = DateTime.UtcNow.AddDays(-5);
        SetupFullHappyPath(profileExists: true);
        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProfile);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(emailOptOut: false));

        _notificationOrchestratorMock.Verify(n => n.SendServiceRequestConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            false, true, It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTextPreferredButStoredSmsOptOut_ShouldAcceptAndNotifyWithTheOptOut()
    {
        // The customer cannot see the stored opt-out, so the submission is accepted rather than
        // refused; NotificationOrchestrator routes the confirmation to email (issue #673).
        var existingProfile = BuildProfile();
        existingProfile.SmsOptOut = true;
        existingProfile.SmsOptOutAtUtc = DateTime.UtcNow.AddDays(-5);
        SetupFullHappyPath(profileExists: true);
        _profileRepoMock.Setup(r => r.GetByEmailAsync("ten_test", "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProfile);
        var request = BuildValidRequest();
        request = request with { Customer = request.Customer with { PreferredContact = "Text" } };

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.Should().NotBeNull();
        _notificationOrchestratorMock.Verify(n => n.SendServiceRequestConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), "Text",
            true, false, "jane@example.com", It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThePhoneCannotBeNormalised_ShouldNotifyWithoutAPhone()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest();
        request = request with { Customer = request.Customer with { Phone = "555-1234" } };

        await _sut.ExecuteAsync("test-slug", request);

        _notificationOrchestratorMock.Verify(n => n.SendServiceRequestConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>(),
            null,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Step 8: Enqueue packet generation (non-blocking) ─────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldEnqueuePacketGenerationForTheCreatedRequest()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        _packetQueueMock.Verify(q => q.TryEnqueue(
            It.Is<PacketGenerationJob>(j =>
                j.TenantId == "ten_test" &&
                j.ServiceRequestId == result.ServiceRequest.Id &&
                j.Trigger == "intake")),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPacketEnqueueThrows_ShouldNotFailIntake()
    {
        SetupFullHappyPath();
        _packetQueueMock.Setup(q => q.TryEnqueue(It.IsAny<PacketGenerationJob>()))
            .Throws(new InvalidOperationException("queue disposed"));

        var act = () => _sut.ExecuteAsync("test-slug", BuildValidRequest());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPacketQueueIsFull_ShouldStillReturnServiceRequest()
    {
        SetupFullHappyPath();
        _packetQueueMock.Setup(q => q.TryEnqueue(It.IsAny<PacketGenerationJob>())).Returns(false);

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.ServiceRequest.Should().NotBeNull();
        result.ServiceRequest.TenantId.Should().Be("ten_test");
    }

    // ── Expected attachment count (issue #516) ───────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldRecordHowManyAttachmentsIntakePromised()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest() with { ExpectedAttachmentCount = 3 };

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.PacketGeneration.ExpectedAttachmentCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoAttachmentsArePromised_ShouldRecordZero()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.ServiceRequest.PacketGeneration.ExpectedAttachmentCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPromisedAttachmentCountIsNegative_ShouldClampToZero()
    {
        SetupFullHappyPath();
        var request = BuildValidRequest() with { ExpectedAttachmentCount = -2 };

        var result = await _sut.ExecuteAsync("test-slug", request);

        result.ServiceRequest.PacketGeneration.ExpectedAttachmentCount.Should().Be(
            0, "a negative promise must never stall packet generation");
    }

    // ── Full Orchestration ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FullOrchestration_ShouldCreateServiceRequestWithAllFields()
    {
        SetupFullHappyPath();

        var request = BuildValidRequest(includeDiagnostics: true);
        var result = await _sut.ExecuteAsync("test-slug", request);

        result.Should().NotBeNull();
        result.ServiceRequest.Should().NotBeNull();
        result.ServiceRequest.TenantId.Should().Be("ten_test");
        result.ServiceRequest.LocationId.Should().Be("loc_test");
        result.ServiceRequest.Status.Should().Be("New");
        result.ServiceRequest.IssueCategory.Should().Be("Slides");
        result.ServiceRequest.IssueDescription.Should().Be("Slide won't retract");
        result.ServiceRequest.CustomerSnapshot.FirstName.Should().Be("Jane");
        result.ServiceRequest.CustomerSnapshot.LastName.Should().Be("Doe");
        result.ServiceRequest.AssetInfo.AssetId.Should().Be("1HGBH41JXMN109186");
        result.ServiceRequest.TechnicianSummary.Should().BeNull();
        result.ServiceRequest.DiagnosticResponses.Should().HaveCount(1);
        result.MagicLinkToken.Should().NotBeNullOrWhiteSpace();
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

    // ── Spec A-13: channel attribution ───────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldPersistTheChannelOnTheServiceRequest()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "qr"));

        result.ServiceRequest.IntakeSource.Should().Be(IntakeSourceVocabulary.Qr);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoChannelSupplied_ShouldRecordItAsPrint()
    {
        // A submission that arrived without a src came from printed material, which cannot
        // carry a query string — not from an unknown channel.
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: null));

        result.ServiceRequest.IntakeSource.Should().Be(IntakeSourceVocabulary.Print);
    }

    [Fact]
    public async Task ExecuteAsync_WhenChannelUnknown_ShouldKeepItRatherThanRejectTheSubmission()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "nfc"));

        result.ServiceRequest.IntakeSource.Should().Be("nfc");
    }

    [Fact]
    public async Task ExecuteAsync_WhenChannelMalformed_ShouldCoerceItRatherThanRejectTheSubmission()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "<script>"));

        result.ServiceRequest.IntakeSource.Should().Be(IntakeSourceVocabulary.Other);
    }

    // ── Spec A-14: invite prefill (open) ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetInvitePrefillAsync_WhenSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var act = () => _sut.GetInvitePrefillAsync(slug!, InviteTokenValue);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetInvitePrefillAsync_WhenSlugNotFound_ShouldThrowKeyNotFoundException()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("unknown-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);

        var act = () => _sut.GetInvitePrefillAsync("unknown-slug", InviteTokenValue);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetInvitePrefillAsync_WhenInviteIsUsable_ShouldReturnFirstNameAndPhone()
    {
        SetupInvite(BuildInvite());

        var result = await _sut.GetInvitePrefillAsync("test-slug", InviteTokenValue);

        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Jane");
        result.Phone.Should().Be("+18015551234");
    }

    [Fact]
    public async Task GetInvitePrefillAsync_ShouldPointReadByTheTokenHashInTheSlugsTenant()
    {
        SetupInvite(BuildInvite());

        await _sut.GetInvitePrefillAsync("test-slug", InviteTokenValue);

        _inviteRepoMock.Verify(r => r.GetByIdAsync("ten_test", InviteToken.Hash(InviteTokenValue), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvitePrefillAsync_ShouldNotRedeemTheInvite()
    {
        // Messaging clients fetch the link to build a preview; opening must never spend the token.
        SetupInvite(BuildInvite());

        await _sut.GetInvitePrefillAsync("test-slug", InviteTokenValue);

        _inviteRepoMock.Verify(r => r.UpdateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetInvitePrefillAsync_WhenExpired_ShouldReturnNull()
    {
        SetupInvite(BuildInvite(expiresAtUtc: DateTime.UtcNow.AddMinutes(-1)));

        var result = await _sut.GetInvitePrefillAsync("test-slug", InviteTokenValue);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvitePrefillAsync_WhenAlreadyRedeemed_ShouldReturnNull()
    {
        SetupInvite(BuildInvite(redeemedAtUtc: DateTime.UtcNow.AddHours(-1)));

        var result = await _sut.GetInvitePrefillAsync("test-slug", InviteTokenValue);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvitePrefillAsync_WhenUnknown_ShouldReturnNull()
    {
        SetupInvite(null);

        var result = await _sut.GetInvitePrefillAsync("test-slug", InviteTokenValue);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvitePrefillAsync_WhenInviteBelongsToAnotherLocation_ShouldReturnNull()
    {
        SetupInvite(BuildInvite(locationId: "loc_other"));

        var result = await _sut.GetInvitePrefillAsync("test-slug", InviteTokenValue);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task GetInvitePrefillAsync_WhenTokenIsMalformed_ShouldReturnNullWithoutAStorageRead(string? token)
    {
        SetupInvite(BuildInvite());

        var result = await _sut.GetInvitePrefillAsync("test-slug", token!);

        result.Should().BeNull();
        _inviteRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetInvitePrefillAsync_WhenLookupFails_ShouldReturnNullSoTheFormStillLoads()
    {
        // A-13's "the redirect never fails" extends to invites: a storage fault costs the
        // customer the prefill, never the form.
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSlugLookup());
        _inviteRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));

        var result = await _sut.GetInvitePrefillAsync("test-slug", InviteTokenValue);

        result.Should().BeNull();
    }

    // ── Spec A-14: invite redemption (submit) ────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithUsableInvite_ShouldAttributeTheRequestToTheAdvisorAndInvite()
    {
        SetupFullHappyPath();
        var invite = BuildInvite();
        SetupInvite(invite);

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "advisor", inviteToken: InviteTokenValue));

        result.ServiceRequest.IntakeSource.Should().Be(IntakeSourceVocabulary.Advisor);
        result.ServiceRequest.IntakeInviteId.Should().Be(invite.Id);
        result.ServiceRequest.AdvisorUserId.Should().Be("auth0|advisor");
    }

    [Fact]
    public async Task ExecuteAsync_WithUsableInvite_ShouldTagAdvisorEvenWhenTheSubmittedSourceDiffers()
    {
        // The invite is the proof of the channel; a src lost or rewritten on the way wins nothing.
        SetupFullHappyPath();
        SetupInvite(BuildInvite());

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: null, inviteToken: InviteTokenValue));

        result.ServiceRequest.IntakeSource.Should().Be(IntakeSourceVocabulary.Advisor);
    }

    [Fact]
    public async Task ExecuteAsync_WithUsableInvite_ShouldMarkTheInviteRedeemedWithTheServiceRequestId()
    {
        SetupFullHappyPath();
        SetupInvite(BuildInvite());

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(inviteToken: InviteTokenValue));

        _inviteRepoMock.Verify(r => r.UpdateAsync(
            It.Is<IntakeInvite>(i =>
                i.RedeemedAtUtc.HasValue &&
                i.ServiceRequestId == result.ServiceRequest.Id &&
                i.UpdatedByUserId == "intake"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithUsableInvite_ShouldRedeemOnlyAfterTheServiceRequestIsCreated()
    {
        SetupFullHappyPath();
        SetupInvite(BuildInvite());
        var order = new List<string>();
        _srRepoMock.Setup(r => r.CreateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("sr"))
            .ReturnsAsync((ServiceRequest sr, CancellationToken _) => sr);
        _inviteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("invite"))
            .ReturnsAsync((IntakeInvite i, CancellationToken _) => i);

        await _sut.ExecuteAsync("test-slug", BuildValidRequest(inviteToken: InviteTokenValue));

        order.Should().Equal("sr", "invite");
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredInvite_ShouldSubmitWithoutInviteAttribution()
    {
        SetupFullHappyPath();
        SetupInvite(BuildInvite(expiresAtUtc: DateTime.UtcNow.AddMinutes(-1)));

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "advisor", inviteToken: InviteTokenValue));

        AssertSubmittedWithoutInvite(result.ServiceRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WithRedeemedInvite_ShouldSubmitWithoutInviteAttribution()
    {
        SetupFullHappyPath();
        SetupInvite(BuildInvite(redeemedAtUtc: DateTime.UtcNow.AddHours(-1)));

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "advisor", inviteToken: InviteTokenValue));

        AssertSubmittedWithoutInvite(result.ServiceRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownInvite_ShouldSubmitWithoutInviteAttribution()
    {
        SetupFullHappyPath();
        SetupInvite(null);

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "advisor", inviteToken: InviteTokenValue));

        AssertSubmittedWithoutInvite(result.ServiceRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WithInviteForAnotherLocation_ShouldSubmitWithoutInviteAttribution()
    {
        SetupFullHappyPath();
        SetupInvite(BuildInvite(locationId: "loc_other"));

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "advisor", inviteToken: InviteTokenValue));

        AssertSubmittedWithoutInvite(result.ServiceRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WithMalformedInviteToken_ShouldSubmitWithoutAStorageRead()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(inviteToken: "not-a-token"));

        result.ServiceRequest.IntakeInviteId.Should().BeNull();
        _inviteRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInviteLookupFails_ShouldStillSubmit()
    {
        SetupFullHappyPath();
        _inviteRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(intakeSource: "advisor", inviteToken: InviteTokenValue));

        AssertSubmittedWithoutInvite(result.ServiceRequest);
        _srRepoMock.Verify(r => r.CreateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMarkingTheInviteRedeemedFails_ShouldStillSubmitWithAttribution()
    {
        SetupFullHappyPath();
        var invite = BuildInvite();
        SetupInvite(invite);
        _inviteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest(inviteToken: InviteTokenValue));

        result.ServiceRequest.IntakeInviteId.Should().Be(invite.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutInviteToken_ShouldNotTouchInvites()
    {
        SetupFullHappyPath();

        var result = await _sut.ExecuteAsync("test-slug", BuildValidRequest());

        result.ServiceRequest.IntakeInviteId.Should().BeNull();
        result.ServiceRequest.AdvisorUserId.Should().BeNull();
        _inviteRepoMock.VerifyNoOtherCalls();
    }

    private const string InviteTokenValue = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static IntakeInvite BuildInvite(
        string locationId = "loc_test", DateTime? expiresAtUtc = null, DateTime? redeemedAtUtc = null) => new()
    {
        Id = InviteToken.Hash(InviteTokenValue),
        TenantId = "ten_test",
        LocationId = locationId,
        AdvisorUserId = "auth0|advisor",
        FirstName = "Jane",
        Phone = "+18015551234",
        ConsentCapturedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddHours(72),
        RedeemedAtUtc = redeemedAtUtc,
        CreatedByUserId = "auth0|advisor",
    };

    private void SetupInvite(IntakeInvite? invite)
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSlugLookup());
        _inviteRepoMock.Setup(r => r.GetByIdAsync("ten_test", InviteToken.Hash(InviteTokenValue), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);
        _inviteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntakeInvite i, CancellationToken _) => i);
    }

    private void AssertSubmittedWithoutInvite(ServiceRequest serviceRequest)
    {
        // The link still says where the customer came from; only the invite's own attribution
        // (and the single use it would spend) is withheld.
        serviceRequest.IntakeSource.Should().Be(IntakeSourceVocabulary.Advisor);
        serviceRequest.IntakeInviteId.Should().BeNull();
        serviceRequest.AdvisorUserId.Should().BeNull();
        _inviteRepoMock.Verify(r => r.UpdateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ServiceRequestCreateRequestDto BuildValidRequest(
        bool includeDiagnostics = false,
        bool smsOptOut = false,
        bool emailOptOut = false,
        string? intakeSource = null,
        string? inviteToken = null)
    {
        return new ServiceRequestCreateRequestDto
        {
            Customer = new CustomerInfoDto
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane@example.com",
                Phone = "801-555-1234",
                PreferredContact = "Phone",
            },
            Asset = new AssetInfoDto
            {
                AssetId = "1HGBH41JXMN109186",
                Manufacturer = "Grand Design",
                Model = "Momentum 395G",
                Year = 2023,
            },
            IssueCategory = "Slides",
            IssueDescription = "Slide won't retract",
            Urgency = "This week",
            RvUsage = "Full-time",
            SmsOptOut = smsOptOut,
            EmailOptOut = emailOptOut,
            IntakeSource = intakeSource,
            InviteToken = inviteToken,
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
            _profileRepoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_test", "1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
                .ReturnsAsync(assetOwner);
        }
        else
        {
            _profileRepoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_test", "1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
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
            .ReturnsAsync("Slides");
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
        var acct = BuildGlobalAcctWithMagicLink(expired: true, assetIds: ["1HGBH41JXMN109186"]);
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
        var acct = BuildGlobalAcctWithMagicLink(expired: false, assetIds: ["OLD_VIN", "1HGBH41JXMN109186"]);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        var oldLedgerEntries = new List<AssetLedgerEntry>
        {
            new()
            {
                AssetId = "OLD_VIN",
                Manufacturer = "Thor",
                Model = "Aria 4000",
                Year = 2019,
                GlobalCustomerAcctId = acct.Id,
            }
        };
        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("OLD_VIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldLedgerEntries);

        // The most recently added asset ID is the last in the list
        var ledgerEntries = new List<AssetLedgerEntry>
        {
            new()
            {
                AssetId = "1HGBH41JXMN109186",
                Manufacturer = "Grand Design",
                Model = "Momentum 395G",
                Year = 2023,
                GlobalCustomerAcctId = acct.Id,
            }
        };
        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ledgerEntries);

        var result = await _sut.GetIntakeConfigAsync("test-slug", "valid-token");

        result.PrefillAsset.Should().NotBeNull();
        result.PrefillAsset!.AssetId.Should().Be("1HGBH41JXMN109186");
        result.PrefillAsset.Manufacturer.Should().Be("Grand Design");
        result.PrefillAsset.Model.Should().Be("Momentum 395G");
        result.PrefillAsset.Year.Should().Be(2023);
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenTokenValidWithMultipleAssets_ShouldReturnAllKnownAssets()
    {
        SetupConfigHappyPath();
        var acct = BuildGlobalAcctWithMagicLink(expired: false, assetIds: ["OLD_VIN", "1HGBH41JXMN109186"]);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("OLD_VIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>
            {
                new() { AssetId = "OLD_VIN", Manufacturer = "Thor", Model = "Aria 4000", Year = 2019, GlobalCustomerAcctId = acct.Id }
            });
        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>
            {
                new() { AssetId = "1HGBH41JXMN109186", Manufacturer = "Grand Design", Model = "Momentum 395G", Year = 2023, GlobalCustomerAcctId = acct.Id }
            });

        var result = await _sut.GetIntakeConfigAsync("test-slug", "valid-token");

        result.KnownAssets.Should().HaveCount(2);
        result.KnownAssets[0].AssetId.Should().Be("OLD_VIN");
        result.KnownAssets[0].Manufacturer.Should().Be("Thor");
        result.KnownAssets[1].AssetId.Should().Be("1HGBH41JXMN109186");
        result.KnownAssets[1].Manufacturer.Should().Be("Grand Design");
        result.PrefillAsset.Should().NotBeNull();
        result.PrefillAsset!.AssetId.Should().Be("1HGBH41JXMN109186");
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenAssetLedgerEmpty_ShouldReturnNullPrefillAssetButIncludeKnownAsset()
    {
        SetupConfigHappyPath();
        var acct = BuildGlobalAcctWithMagicLink(expired: false, assetIds: ["1HGBH41JXMN109186"]);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>());

        var result = await _sut.GetIntakeConfigAsync("test-slug", "valid-token");

        result.PrefillCustomer.Should().NotBeNull();
        result.PrefillAsset.Should().BeNull();
        result.KnownAssets.Should().HaveCount(1);
        result.KnownAssets[0].AssetId.Should().Be("1HGBH41JXMN109186");
        result.KnownAssets[0].Manufacturer.Should().BeNull();
    }

    [Fact]
    public async Task GetIntakeConfigAsync_WhenSomeAssetsHaveNoLedgerEntries_ShouldIncludeAllKnownAssets()
    {
        SetupConfigHappyPath();
        var acct = BuildGlobalAcctWithMagicLink(expired: false, assetIds: ["OLD_VIN", "1HGBH41JXMN109186", "NO_LEDGER"]);
        _globalAcctRepoMock.Setup(r => r.GetByMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("OLD_VIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>
            {
                new() { AssetId = "OLD_VIN", Manufacturer = "Thor", Model = "Aria 4000", Year = 2019, GlobalCustomerAcctId = acct.Id }
            });
        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("1HGBH41JXMN109186", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>
            {
                new() { AssetId = "1HGBH41JXMN109186", Manufacturer = "Grand Design", Model = "Momentum 395G", Year = 2023, GlobalCustomerAcctId = acct.Id }
            });
        _ledgerRepoMock.Setup(r => r.GetByAssetIdAsync("NO_LEDGER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetLedgerEntry>());

        var result = await _sut.GetIntakeConfigAsync("test-slug", "valid-token");

        result.KnownAssets.Should().HaveCount(3);
        result.KnownAssets[0].AssetId.Should().Be("OLD_VIN");
        result.KnownAssets[0].Manufacturer.Should().Be("Thor");
        result.KnownAssets[1].AssetId.Should().Be("1HGBH41JXMN109186");
        result.KnownAssets[1].Manufacturer.Should().Be("Grand Design");
        result.KnownAssets[2].AssetId.Should().Be("NO_LEDGER");
        result.KnownAssets[2].Manufacturer.Should().BeNull();
        result.PrefillAsset.Should().NotBeNull();
        result.PrefillAsset!.AssetId.Should().Be("1HGBH41JXMN109186");
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

    [Fact]
    public async Task GetIntakeConfigAsync_WhenLocationHasPhone_ShouldIncludeLocationPhone()
    {
        SetupConfigHappyPath();
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_test", "loc_test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location
            {
                Id = "loc_test",
                TenantId = "ten_test",
                Name = "Test Location",
                Phone = "(555) 123-4567",
                CreatedByUserId = "admin",
            });

        var result = await _sut.GetIntakeConfigAsync("test-slug");

        result.LocationPhone.Should().Be("(555) 123-4567");
    }

    // ── AssessCapabilitiesAsync ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AssessCapabilitiesAsync_WhenSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var act = () => _sut.AssessCapabilitiesAsync(slug!, "battery is dead");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AssessCapabilitiesAsync_WhenIssueDescriptionIsNullOrWhiteSpace_ShouldThrowArgumentException(string? description)
    {
        var act = () => _sut.AssessCapabilitiesAsync("test-slug", description!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenSlugNotFound_ShouldThrowKeyNotFoundException()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("unknown-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);

        var act = () => _sut.AssessCapabilitiesAsync("unknown-slug", "battery is dead");

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*unknown-slug*");
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenLocationCoversRequiredCapability_ShouldReturnMatched()
    {
        SetupConfigHappyPath();
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_test", "loc_test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location
            {
                Id = "loc_test",
                TenantId = "ten_test",
                Name = "Test Location",
                Phone = "(555) 999-1111",
                EnabledCapabilities = ["electrical", "plumbing"],
                CreatedByUserId = "admin",
            });
        _categorizationMock.Setup(c => c.CategorizeAsync("battery is dead", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Electrical");

        var result = await _sut.AssessCapabilitiesAsync("test-slug", "battery is dead");

        result.Matched.Should().BeTrue();
        result.IssueCategory.Should().Be("Electrical");
        result.RequiredCapabilities.Should().BeEquivalentTo(["electrical"]);
        result.MissingCapabilities.Should().BeEmpty();
        result.LocationPhone.Should().Be("(555) 999-1111");
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenLocationMissesRequiredCapability_ShouldReturnNotMatchedWithMissing()
    {
        SetupConfigHappyPath();
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_test", "loc_test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location
            {
                Id = "loc_test",
                TenantId = "ten_test",
                Name = "Test Location",
                Phone = "(555) 999-1111",
                EnabledCapabilities = ["plumbing"],
                CreatedByUserId = "admin",
            });
        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Electrical");

        var result = await _sut.AssessCapabilitiesAsync("test-slug", "battery is dead");

        result.Matched.Should().BeFalse();
        result.IssueCategory.Should().Be("Electrical");
        result.RequiredCapabilities.Should().BeEquivalentTo(["electrical"]);
        result.MissingCapabilities.Should().BeEquivalentTo(["electrical"]);
        result.LocationPhone.Should().Be("(555) 999-1111");
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenCategoryHasNoMappedCapabilities_ShouldReturnMatchedWithEmptyRequired()
    {
        SetupConfigHappyPath();
        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IssueCategoryVocabulary.FallbackCode);

        var result = await _sut.AssessCapabilitiesAsync("test-slug", "general issue");

        result.Matched.Should().BeTrue();
        result.IssueCategory.Should().Be(IssueCategoryVocabulary.FallbackCode);
        result.RequiredCapabilities.Should().BeEmpty();
        result.MissingCapabilities.Should().BeEmpty();
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenIssueCategorySupplied_ShouldSkipCategorization()
    {
        SetupConfigHappyPath();
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_test", "loc_test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location
            {
                Id = "loc_test",
                TenantId = "ten_test",
                Name = "Test Location",
                EnabledCapabilities = ["hvac"],
                CreatedByUserId = "admin",
            });

        var result = await _sut.AssessCapabilitiesAsync("test-slug", "anything", "HVAC");

        result.Matched.Should().BeTrue();
        result.IssueCategory.Should().Be("HVAC");
        result.RequiredCapabilities.Should().BeEquivalentTo(["hvac"]);
        _categorizationMock.Verify(
            c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenCategorizationThrows_ShouldTreatAsUnknownAndMatch()
    {
        SetupConfigHappyPath();
        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ai down"));

        var result = await _sut.AssessCapabilitiesAsync("test-slug", "something is broken");

        result.Matched.Should().BeTrue();
        result.IssueCategory.Should().BeNull();
        result.RequiredCapabilities.Should().BeEmpty();
        result.MissingCapabilities.Should().BeEmpty();
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenSlidesCategoryAndCapabilityMissing_ShouldReportMissing()
    {
        SetupConfigHappyPath();
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_test", "loc_test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location
            {
                Id = "loc_test",
                TenantId = "ten_test",
                Name = "Test Location",
                // Has body-repair and roof-repair but is MISSING slide-out-repair
                EnabledCapabilities = ["body-repair", "roof-repair"],
                CreatedByUserId = "admin",
            });

        var result = await _sut.AssessCapabilitiesAsync("test-slug", "slide-out is stuck", "Slides");

        result.Matched.Should().BeFalse();
        result.MissingCapabilities.Should().BeEquivalentTo(["slide-out-repair"]);
        result.RequiredCapabilities.Should().BeEquivalentTo(["slide-out-repair"]);
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenAiReturnsOutOfVocabularyCategory_ShouldTreatAsUnknownAndMatch()
    {
        SetupConfigHappyPath();
        _categorizationMock.Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Structural"); // retired alias — no longer in the vocabulary

        var result = await _sut.AssessCapabilitiesAsync("test-slug", "slide-out is stuck");

        result.Matched.Should().BeTrue();
        result.RequiredCapabilities.Should().BeEmpty();
        result.MissingCapabilities.Should().BeEmpty();
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
