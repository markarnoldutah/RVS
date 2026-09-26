using FluentAssertions;
using RVS.Blazor.Intake.State;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.State;

public class IntakeWizardStateTests
{
    private static IntakeWizardState CreateState() => new(new NullJSRuntime());

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        var state = CreateState();

        state.CurrentStep.Should().Be(1);
        state.TotalSteps.Should().Be(8);
        state.Slug.Should().BeEmpty();
        state.Token.Should().BeNull();
        state.Config.Should().BeNull();
        state.FirstName.Should().BeEmpty();
        state.LastName.Should().BeEmpty();
        state.Email.Should().BeEmpty();
        state.Phone.Should().BeNull();
        state.PreferredContact.Should().BeNull();
        state.SmsOptOut.Should().BeFalse();
        state.EmailOptOut.Should().BeFalse();
        state.IsPrefilled.Should().BeFalse();
        state.Vin.Should().BeEmpty();
        state.VinLookupSucceeded.Should().BeFalse();
        state.IsSubmitted.Should().BeFalse();
        state.SubmissionMagicLinkToken.Should().BeNull();
    }

    [Fact]
    public async Task GoToNextStepAsync_ShouldIncrementCurrentStep()
    {
        var state = CreateState();

        await state.GoToNextStepAsync();

        state.CurrentStep.Should().Be(2);
    }

    [Fact]
    public async Task GoToNextStepAsync_ShouldNotExceedTotalSteps()
    {
        var state = CreateState();

        for (var i = 0; i < 10; i++)
        {
            await state.GoToNextStepAsync();
        }

        state.CurrentStep.Should().Be(8);
    }

    [Fact]
    public async Task GoToPreviousStepAsync_ShouldDecrementCurrentStep()
    {
        var state = CreateState();
        await state.GoToNextStepAsync();
        await state.GoToNextStepAsync();

        await state.GoToPreviousStepAsync();

        state.CurrentStep.Should().Be(2);
    }

    [Fact]
    public async Task GoToPreviousStepAsync_ShouldNotGoBelowOne()
    {
        var state = CreateState();

        await state.GoToPreviousStepAsync();

        state.CurrentStep.Should().Be(1);
    }

    [Fact]
    public async Task GoToStepAsync_ShouldNavigateToSpecificStep()
    {
        var state = CreateState();

        await state.GoToStepAsync(5);

        state.CurrentStep.Should().Be(5);
    }

    [Fact]
    public async Task GoToStepAsync_ShouldRejectInvalidStep_TooLow()
    {
        var state = CreateState();
        await state.GoToStepAsync(3);

        await state.GoToStepAsync(0);

        state.CurrentStep.Should().Be(3);
    }

    [Fact]
    public async Task GoToStepAsync_ShouldRejectInvalidStep_TooHigh()
    {
        var state = CreateState();

        await state.GoToStepAsync(9);

        state.CurrentStep.Should().Be(1);
    }

    [Fact]
    public void ApplyPrefill_ShouldSetCustomerFields()
    {
        var state = CreateState();
        var prefill = new CustomerInfoDto
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "555-1234",
            PreferredContact = "Text"
        };

        state.ApplyPrefill(prefill);

        state.FirstName.Should().Be("Jane");
        state.LastName.Should().Be("Doe");
        state.Email.Should().Be("jane@example.com");
        state.Phone.Should().Be("555-1234");
        state.PreferredContact.Should().Be("Text");
        state.IsPrefilled.Should().BeTrue();
    }

    [Fact]
    public void ApplyPrefill_NullPrefill_ShouldThrow()
    {
        var state = CreateState();

        var act = () => state.ApplyPrefill(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Spec A-14: invite prefill ────────────────────────────────────────────

    [Fact]
    public void ApplyInvitePrefill_ShouldSetFirstNameAndPhone()
    {
        var state = CreateState();

        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane", Phone = "+18015551234" });

        state.FirstName.Should().Be("Jane");
        state.Phone.Should().Be("+18015551234");
        state.IsInvitePrefilled.Should().BeTrue();
    }

    [Fact]
    public void ApplyInvitePrefill_ShouldLeaveEverythingElseForTheCustomer()
    {
        var state = CreateState();

        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane", Phone = "+18015551234" });

        state.LastName.Should().BeEmpty();
        state.Email.Should().BeEmpty();
        state.PreferredContact.Should().BeNull();
    }

    [Fact]
    public void ApplyInvitePrefill_ShouldNotLookLikeAReturningCustomerMatch()
    {
        // A-7's magic-link prefill (IsPrefilled) greets a known customer; an invite knows only
        // what the advisor typed, and is a separate path.
        var state = CreateState();

        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane", Phone = "+18015551234" });

        state.IsPrefilled.Should().BeFalse();
    }

    [Fact]
    public void ApplyInvitePrefill_ShouldNotOverwriteWhatTheCustomerAlreadyEntered()
    {
        // Config is not persisted, so a reload re-fetches the invite; what the customer typed
        // (or corrected) since must survive it.
        var state = CreateState();
        state.FirstName = "Janet";
        state.Phone = "801-555-9999";

        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane", Phone = "+18015551234" });

        state.FirstName.Should().Be("Janet");
        state.Phone.Should().Be("801-555-9999");
    }

    [Fact]
    public void ApplyInvitePrefill_WhenTheInviteWasEmailed_ShouldSetTheEmail()
    {
        var state = CreateState();

        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane", Email = "jane@example.com" });

        state.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public void ApplyInvitePrefill_ShouldNotOverwriteAnEmailTheCustomerAlreadyEntered()
    {
        var state = CreateState();
        state.Email = "janet@example.com";

        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane", Email = "jane@example.com" });

        state.Email.Should().Be("janet@example.com");
    }

    [Fact]
    public void ApplyInvitePrefill_WhenInviteHasNoPhone_ShouldLeavePhoneBlank()
    {
        var state = CreateState();

        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane", Phone = null });

        state.FirstName.Should().Be("Jane");
        state.Phone.Should().BeNull();
    }

    [Fact]
    public void ApplyInvitePrefill_NullPrefill_ShouldThrow()
    {
        var state = CreateState();

        var act = () => state.ApplyInvitePrefill(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyInvitePrefill_ShouldNotifySubscribers()
    {
        var state = CreateState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane" });

        fired.Should().BeTrue();
    }

    [Fact]
    public void BuildCreateRequest_ShouldCarryTheInviteToken()
    {
        var state = CreateState();
        state.InviteToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        var request = state.BuildCreateRequest();

        request.InviteToken.Should().Be("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
    }

    [Fact]
    public void BuildCreateRequest_WithoutAnInvite_ShouldSendNoToken()
    {
        var request = CreateState().BuildCreateRequest();

        request.InviteToken.Should().BeNull();
    }

    [Fact]
    public async Task PersistAndRestore_ShouldKeepTheInviteAcrossAReload()
    {
        // The invite is spent on submission, so losing the token on a reload mid-wizard would
        // cost the request its advisor attribution.
        var jsRuntime = new InMemoryWebStorageJSRuntime();
        var before = new IntakeWizardState(jsRuntime) { Slug = "test-slug", InviteToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" };
        before.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane", Phone = "+18015551234" });
        await before.PersistAsync();

        var after = new IntakeWizardState(jsRuntime);
        await after.RestoreAsync();

        after.InviteToken.Should().Be("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        after.IsInvitePrefilled.Should().BeTrue();
        after.FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task PersistAndRestore_ShouldKeepTheVinLookupResultAcrossAReload()
    {
        // Issue #758: without it, a refresh on Step 4 kept the decoded vehicle but told the
        // customer no vehicle information was found for the VIN.
        var jsRuntime = new InMemoryWebStorageJSRuntime();
        var before = new IntakeWizardState(jsRuntime) { Slug = "test-slug", Vin = "1HGBH41JXMN109186", VinLookupSucceeded = true };
        await before.PersistAsync();

        var after = new IntakeWizardState(jsRuntime);
        await after.RestoreAsync();

        after.VinLookupSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task ClearAsync_ShouldResetTheVinLookupResult()
    {
        var state = CreateState();
        state.VinLookupSucceeded = true;

        await state.ClearAsync();

        state.VinLookupSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_ShouldDropTheInvite()
    {
        var state = CreateState();
        state.InviteToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        state.ApplyInvitePrefill(new IntakeInvitePrefillResponseDto { FirstName = "Jane" });

        await state.ClearAsync();

        state.InviteToken.Should().BeNull();
        state.IsInvitePrefilled.Should().BeFalse();
    }

    [Fact]
    public void ApplyAssetPrefill_ShouldSetAssetFields()
    {
        var state = CreateState();
        var prefillAsset = new AssetInfoDto
        {
            AssetId = "1HGBH41JXMN109186",
            Manufacturer = "Grand Design",
            Model = "Momentum 395G",
            Year = 2023
        };

        state.ApplyAssetPrefill(prefillAsset);

        state.Vin.Should().Be("1HGBH41JXMN109186");
        state.Manufacturer.Should().Be("Grand Design");
        state.Model.Should().Be("Momentum 395G");
        state.Year.Should().Be(2023);
    }

    [Fact]
    public void ApplyAssetPrefill_NullPrefill_ShouldThrow()
    {
        var state = CreateState();

        var act = () => state.ApplyAssetPrefill(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateCurrentStep_Step1_NoConfig_ShouldReturnError()
    {
        var state = CreateState();

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle()
            .Which.Should().Contain("configuration");
    }

    [Fact]
    public void ValidateCurrentStep_Step1_WithConfig_ShouldReturnNoErrors()
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test Location",
            LocationSlug = "test",
            DealershipName = "Test Dealer"
        };

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateCurrentStep_Step2_EmptyFields_ShouldReturnErrors()
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test", LocationSlug = "test", DealershipName = "Test"
        };
        await state.GoToNextStepAsync();

        var errors = state.ValidateCurrentStep();

        errors.Should().Contain(e => e.Contains("First name"));
        errors.Should().Contain(e => e.Contains("Last name"));
        errors.Should().Contain(e => e.Contains("Email"));
    }

    [Fact]
    public async Task ValidateCurrentStep_Step2_InvalidEmail_ShouldReturnError()
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test", LocationSlug = "test", DealershipName = "Test"
        };
        await state.GoToNextStepAsync();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "not-an-email";
        state.Phone = "801-555-1234";
        state.PreferredContact = "Email";

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle()
            .Which.Should().Contain("@");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step2_ValidFields_ShouldReturnNoErrors()
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test", LocationSlug = "test", DealershipName = "Test"
        };
        await state.GoToNextStepAsync();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Phone = "801-555-1234";
        state.PreferredContact = "Email";

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    // The API holds the same line (issue #679): phone is required whatever the preference.
    [Theory]
    [InlineData(null, "Phone number is required")]
    [InlineData("555-1234", "Phone number must have at least 10 digits")]
    public async Task ValidateCurrentStep_Step2_PreferredContactEmail_WithoutAValidPhone_ShouldReturnError(
        string? phone, string expected)
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test", LocationSlug = "test", DealershipName = "Test"
        };
        await state.GoToNextStepAsync();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Phone = phone;
        state.PreferredContact = "Email";

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle().Which.Should().Be(expected);
        state.FieldErrors.Should().ContainKey("Phone");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step2_NoPreferredContact_ShouldReturnError()
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test", LocationSlug = "test", DealershipName = "Test"
        };
        await state.GoToNextStepAsync();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.PreferredContact = null;

        var errors = state.ValidateCurrentStep();

        errors.Should().Contain(e => e.Contains("contact", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateCurrentStep_Step2_PreferredContactPhone_WithoutPhoneNumber_ShouldReturnError()
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test", LocationSlug = "test", DealershipName = "Test"
        };
        await state.GoToNextStepAsync();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.PreferredContact = "Phone";
        state.Phone = null;

        var errors = state.ValidateCurrentStep();

        errors.Should().Contain(e => e.Contains("phone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateCurrentStep_Step2_PreferredContactText_WithPhoneNumber_ShouldReturnNoErrors()
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test", LocationSlug = "test", DealershipName = "Test"
        };
        await state.GoToNextStepAsync();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.PreferredContact = "Text";
        state.Phone = "801-555-1234";

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    // ── Step 2: opt-outs veto the preferred contact method (Spec A-2, issue #662) ──

    [Theory]
    [InlineData("Phone", true)]
    [InlineData("Text", false)]
    [InlineData("Email", true)]
    public void IsContactMethodAvailable_WhenSmsOptOut_ShouldDisableTextOnly(string method, bool expected)
    {
        var state = CreateState();
        state.SmsOptOut = true;

        state.IsContactMethodAvailable(method).Should().Be(expected);
    }

    [Theory]
    [InlineData("Phone", true)]
    [InlineData("Text", true)]
    [InlineData("Email", false)]
    public void IsContactMethodAvailable_WhenEmailOptOut_ShouldDisableEmailOnly(string method, bool expected)
    {
        var state = CreateState();
        state.EmailOptOut = true;

        state.IsContactMethodAvailable(method).Should().Be(expected);
    }

    [Fact]
    public void IsContactMethodAvailable_WhenBothOptedOut_ShouldLeavePhoneAvailable()
    {
        var state = CreateState();
        state.SmsOptOut = true;
        state.EmailOptOut = true;

        state.IsContactMethodAvailable("Phone").Should().BeTrue();
        state.IsContactMethodAvailable("Text").Should().BeFalse();
        state.IsContactMethodAvailable("Email").Should().BeFalse();
    }

    [Fact]
    public void SmsOptOut_WhenTextIsSelected_ShouldClearTheSelection()
    {
        var state = CreateState();
        state.PreferredContact = "Text";

        state.SmsOptOut = true;

        state.PreferredContact.Should().BeNull();
    }

    [Fact]
    public void EmailOptOut_WhenEmailIsSelected_ShouldClearTheSelection()
    {
        var state = CreateState();
        state.PreferredContact = "Email";

        state.EmailOptOut = true;

        state.PreferredContact.Should().BeNull();
    }

    [Theory]
    [InlineData("Phone")]
    [InlineData("Email")]
    public void SmsOptOut_WhenSelectionDoesNotConflict_ShouldKeepIt(string method)
    {
        var state = CreateState();
        state.PreferredContact = method;

        state.SmsOptOut = true;

        state.PreferredContact.Should().Be(method);
    }

    [Theory]
    [InlineData("Phone")]
    [InlineData("Text")]
    public void EmailOptOut_WhenSelectionDoesNotConflict_ShouldKeepIt(string method)
    {
        var state = CreateState();
        state.PreferredContact = method;

        state.EmailOptOut = true;

        state.PreferredContact.Should().Be(method);
    }

    [Theory]
    [InlineData("Text", true, false)]
    [InlineData("Email", false, true)]
    public async Task ValidateCurrentStep_Step2_PreferredContactOptedOut_ShouldReturnError(
        string preferredContact, bool smsOptOut, bool emailOptOut)
    {
        var state = CreateState();
        state.Config = new IntakeConfigResponseDto
        {
            LocationName = "Test", LocationSlug = "test", DealershipName = "Test"
        };
        await state.GoToNextStepAsync();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Phone = "801-555-1234";
        state.SmsOptOut = smsOptOut;
        state.EmailOptOut = emailOptOut;
        // Set after the opt-out, which would otherwise clear it — the shape a hand-built state takes.
        state.PreferredContact = preferredContact;

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle(e => e.Contains("opted out", StringComparison.OrdinalIgnoreCase));
        state.FieldErrors.Should().ContainKey("PreferredContact");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step3_EmptyVin_ShouldReturnError()
    {
        var state = CreateState();
        await state.GoToStepAsync(3);

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle()
            .Which.Should().Contain("VIN");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step3_InvalidVin_ShouldReturnError()
    {
        var state = CreateState();
        await state.GoToStepAsync(3);
        state.Vin = "INVALID";

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle()
            .Which.Should().Contain("17");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step3_ValidVin_ShouldReturnNoErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(3);
        state.Vin = "1HGBH41JXMN109186";

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateCurrentStep_Step4_VehicleDetails_EmptyFields_ShouldReturnErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(4);

        var errors = state.ValidateCurrentStep();

        errors.Should().Contain(e => e.Contains("warranty", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(e => e.Contains("purchase date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateCurrentStep_Step4_VehicleDetails_BothFieldsSet_ShouldReturnNoErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(4);
        state.HasExtendedWarranty = "No";
        state.ApproxPurchaseDate = "03/2023";

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateCurrentStep_Step5_EmptyFields_ShouldReturnErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(5);

        var errors = state.ValidateCurrentStep();

        errors.Should().Contain(e => e.Contains("category"));
        errors.Should().Contain(e => e.Contains("description"));
    }

    [Fact]
    public async Task ValidateCurrentStep_Step5_DescriptionTooLong_ShouldReturnError()
    {
        var state = CreateState();
        await state.GoToStepAsync(5);
        state.IssueCategory = "Electrical";
        state.IssueDescription = new string('x', 2001);
        state.HasExtendedWarranty = "No";
        state.ApproxPurchaseDate = "01/2023";

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle()
            .Which.Should().Contain("2000");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step5_ValidFields_ShouldReturnNoErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(5);
        state.IssueCategory = "Electrical";
        state.IssueDescription = "The lights are flickering.";
        state.HasExtendedWarranty = "No";
        state.ApproxPurchaseDate = "03/2023";

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateCurrentStep_Step6_ShouldAlwaysReturnNoErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(6);

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateCurrentStep_Step7_TooManyAttachments_ShouldReturnError()
    {
        var state = CreateState();
        await state.GoToStepAsync(7);
        for (var i = 0; i < 11; i++)
        {
            state.Attachments.Add(new AttachmentFileInfo { FileName = $"file{i}.jpg" });
        }

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle()
            .Which.Should().Contain("10");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step8_ShouldAlwaysReturnNoErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(8);

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    [Fact]
    public void BuildCreateRequest_ShouldTrimStrings()
    {
        var state = CreateState();
        state.FirstName = "  Jane  ";
        state.LastName = "  Doe  ";
        state.Email = "  jane@example.com  ";
        state.Phone = "  555-1234  ";
        state.Vin = "  1hgbh41jxmn109186  ";
        state.Manufacturer = "  Winnebago  ";
        state.Model = "  Vista  ";
        state.Year = 2023;
        state.IssueCategory = "  Electrical  ";
        state.IssueDescription = "  Lights flickering  ";
        state.Urgency = "  High  ";
        state.RvUsage = "  Full-Time  ";
        state.PreferredContact = "  Text  ";

        var request = state.BuildCreateRequest();

        request.Customer.FirstName.Should().Be("Jane");
        request.Customer.LastName.Should().Be("Doe");
        request.Customer.Email.Should().Be("jane@example.com");
        request.Customer.Phone.Should().Be("555-1234");
        request.Customer.PreferredContact.Should().Be("Text");
        request.Asset.AssetId.Should().Be("1HGBH41JXMN109186");
        request.Asset.Manufacturer.Should().Be("Winnebago");
        request.Asset.Model.Should().Be("Vista");
        request.Asset.Year.Should().Be(2023);
        request.IssueCategory.Should().Be("Electrical");
        request.IssueDescription.Should().Be("Lights flickering");
        request.Urgency.Should().Be("High");
        request.RvUsage.Should().Be("Full-Time");
    }

    [Fact]
    public void BuildCreateRequest_ShouldUppercaseVin()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1hgbh41jxmn109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";

        var request = state.BuildCreateRequest();

        request.Asset.AssetId.Should().Be("1HGBH41JXMN109186");
    }

    [Fact]
    public void BuildCreateRequest_ShouldSetNullForEmptyOptionalFields()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";
        state.Phone = "   ";
        state.Manufacturer = "";
        state.Model = "  ";
        state.Urgency = "";
        state.RvUsage = "  ";

        var request = state.BuildCreateRequest();

        request.Customer.Phone.Should().BeNull();
        request.Asset.Manufacturer.Should().BeNull();
        request.Asset.Model.Should().BeNull();
        request.Urgency.Should().BeNull();
        request.RvUsage.Should().BeNull();
    }

    [Fact]
    public void BuildCreateRequest_DefaultOptOuts_ShouldBeFalse()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";

        var request = state.BuildCreateRequest();

        request.SmsOptOut.Should().BeFalse();
        request.EmailOptOut.Should().BeFalse();
    }

    [Fact]
    public void BuildCreateRequest_WithSmsOptOut_ShouldIncludeOptOut()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";
        state.SmsOptOut = true;

        var request = state.BuildCreateRequest();

        request.SmsOptOut.Should().BeTrue();
        request.EmailOptOut.Should().BeFalse();
    }

    [Fact]
    public void BuildCreateRequest_WithEmailOptOut_ShouldIncludeOptOut()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";
        state.EmailOptOut = true;

        var request = state.BuildCreateRequest();

        request.SmsOptOut.Should().BeFalse();
        request.EmailOptOut.Should().BeTrue();
    }

    [Fact]
    public void BuildCreateRequest_WithDiagnosticResponses_ShouldIncludeThem()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";
        state.DiagnosticResponses =
        [
            new DiagnosticResponseDto
            {
                QuestionText = "Q1",
                SelectedOptions = ["Yes"],
                FreeTextResponse = "Details"
            }
        ];

        var request = state.BuildCreateRequest();

        request.DiagnosticResponses.Should().HaveCount(1);
        request.DiagnosticResponses![0].QuestionText.Should().Be("Q1");
    }

    [Fact]
    public void BuildCreateRequest_NoDiagnosticResponses_ShouldSetNull()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";

        var request = state.BuildCreateRequest();

        request.DiagnosticResponses.Should().BeNull();
    }

    [Fact]
    public void BuildCreateRequest_WithWarrantyFields_ShouldIncludeThem()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";
        state.HasExtendedWarranty = "Yes";
        state.ApproxPurchaseDate = "March 2023";

        var request = state.BuildCreateRequest();

        request.HasExtendedWarranty.Should().Be("Yes");
        request.ApproxPurchaseDate.Should().Be("March 2023");
    }

    [Fact]
    public void BuildCreateRequest_WithEmptyWarrantyFields_ShouldSetNull()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";
        state.HasExtendedWarranty = "  ";
        state.ApproxPurchaseDate = "";

        var request = state.BuildCreateRequest();

        request.HasExtendedWarranty.Should().BeNull();
        request.ApproxPurchaseDate.Should().BeNull();
    }

    [Fact]
    public void BuildCreateRequest_WithWarrantyFields_ShouldTrimValues()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";
        state.HasExtendedWarranty = "  Not Sure  ";
        state.ApproxPurchaseDate = "  2022  ";

        var request = state.BuildCreateRequest();

        request.HasExtendedWarranty.Should().Be("Not Sure");
        request.ApproxPurchaseDate.Should().Be("2022");
    }

    [Fact]
    public void Token_ShouldBeSettable()
    {
        var state = CreateState();

        state.Token = "dK3mRw9x:Xv2pLqN8aTcBfY7mZs4eWQ";

        state.Token.Should().Be("dK3mRw9x:Xv2pLqN8aTcBfY7mZs4eWQ");
    }

    [Fact]
    public void SubmissionMagicLinkToken_ShouldDefaultToNull()
    {
        var state = CreateState();

        state.SubmissionMagicLinkToken.Should().BeNull();
    }

    [Fact]
    public void SubmissionMagicLinkToken_ShouldBeSettable()
    {
        var state = CreateState();

        state.SubmissionMagicLinkToken = "abc123:xyz789";

        state.SubmissionMagicLinkToken.Should().Be("abc123:xyz789");
    }

    [Fact]
    public async Task ClearAsync_ShouldResetAllFields()
    {
        var state = CreateState();
        state.Slug = "test-location";
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Vin = "1HGBH41JXMN109186";
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Test";
        state.HasExtendedWarranty = "Yes";
        state.ApproxPurchaseDate = "March 2023";
        state.IsSubmitted = true;
        state.SubmissionMagicLinkToken = "abc123:xyz789";
        state.FailedUploadCount = 3;
        state.IsSubmitting = true;
        state.SmsOptOut = true;
        state.EmailOptOut = true;
        state.PreferredContact = "Phone";
        await state.GoToStepAsync(5);

        await state.ClearAsync();

        state.CurrentStep.Should().Be(1);
        state.Slug.Should().BeEmpty();
        state.FirstName.Should().BeEmpty();
        state.LastName.Should().BeEmpty();
        state.Email.Should().BeEmpty();
        state.Vin.Should().BeEmpty();
        state.HasExtendedWarranty.Should().BeNull();
        state.ApproxPurchaseDate.Should().BeNull();
        state.IsSubmitted.Should().BeFalse();
        state.SubmissionMagicLinkToken.Should().BeNull();
        state.FailedUploadCount.Should().Be(0);
        state.IsSubmitting.Should().BeFalse();
        state.SmsOptOut.Should().BeFalse();
        state.EmailOptOut.Should().BeFalse();
        state.PreferredContact.Should().BeNull();
    }

    [Fact]
    public void IsSubmitting_ShouldDefaultToFalse()
    {
        var state = CreateState();

        state.IsSubmitting.Should().BeFalse();
    }

    [Fact]
    public void OnChange_ShouldFireWhenStepChanges()
    {
        var state = CreateState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.ApplyPrefill(new CustomerInfoDto
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com"
        });

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task OnChange_ShouldFireOnNavigation()
    {
        var state = CreateState();
        var fireCount = 0;
        state.OnChange += () => fireCount++;

        await state.GoToNextStepAsync();
        await state.GoToPreviousStepAsync();

        fireCount.Should().Be(2);
    }

    [Fact]
    public void BuildStartOverUrl_ShouldReturnIntakeUrlWithSlug()
    {
        var url = IntakeWizardState.BuildStartOverUrl("camping-world-slc", null);

        url.Should().Be("/camping-world-slc");
    }

    [Fact]
    public void BuildStartOverUrl_WithToken_ShouldIncludeTokenQueryParam()
    {
        var url = IntakeWizardState.BuildStartOverUrl("camping-world-slc", "dK3mRw9x:Xv2pLqN8aTcBfY7mZs4eWQ");

        url.Should().Be("/camping-world-slc?token=dK3mRw9x%3AXv2pLqN8aTcBfY7mZs4eWQ");
    }

    [Fact]
    public void BuildStartOverUrl_WithWhitespaceToken_ShouldOmitTokenQueryParam()
    {
        var url = IntakeWizardState.BuildStartOverUrl("camping-world-slc", "   ");

        url.Should().Be("/camping-world-slc");
    }

    [Fact]
    public void BuildStartOverUrl_WithEmptyToken_ShouldOmitTokenQueryParam()
    {
        var url = IntakeWizardState.BuildStartOverUrl("camping-world-slc", "");

        url.Should().Be("/camping-world-slc");
    }

    [Fact]
    public void BuildStartOverUrl_ShouldEncodeSlug()
    {
        var url = IntakeWizardState.BuildStartOverUrl("slug with spaces", null);

        url.Should().Be("/slug%20with%20spaces");
    }

    [Fact]
    public void BuildStartOverUrl_NullSlug_ShouldThrow()
    {
        var act = () => IntakeWizardState.BuildStartOverUrl(null!, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildStartOverUrl_EmptySlug_ShouldThrow()
    {
        var act = () => IntakeWizardState.BuildStartOverUrl("", null);

        act.Should().Throw<ArgumentException>();
    }

    // ── Issue #740: the config survives a refresh, and the category list is alphabetized ──

    private static IntakeConfigResponseDto ConfigWithCategories(params LookupItemDto[] categories) => new()
    {
        LocationName = "Test", LocationSlug = "test-slug", DealershipName = "Test",
        IssueCategories = [.. categories]
    };

    [Fact]
    public async Task EnsureConfigAsync_WhenARestoredSessionIsPastStepOne_ShouldFetchTheConfig()
    {
        // Config is not persisted, so a refresh on Step 5 comes back with none — and Step 1,
        // the only step that fetched it, is never shown again.
        var jsRuntime = new InMemoryWebStorageJSRuntime();
        var before = new IntakeWizardState(jsRuntime) { Slug = "test-slug" };
        await before.GoToStepAsync(5);

        var after = new IntakeWizardState(jsRuntime);
        await after.RestoreAsync();
        var config = ConfigWithCategories(new LookupItemDto("slides", "Slide-outs", null, 1, true));

        await after.EnsureConfigAsync(_ => Task.FromResult(config));

        after.Config.Should().BeSameAs(config);
        after.SelectableIssueCategories.Should().ContainSingle(c => c.Code == "slides");
    }

    [Fact]
    public async Task EnsureConfigAsync_WhenTheConfigIsAlreadyLoaded_ShouldNotFetchAgain()
    {
        var state = CreateState();
        var loaded = ConfigWithCategories();
        state.Config = loaded;
        await state.GoToStepAsync(5);
        var fetches = 0;

        await state.EnsureConfigAsync(_ => { fetches++; return Task.FromResult(ConfigWithCategories()); });

        fetches.Should().Be(0);
        state.Config.Should().BeSameAs(loaded);
    }

    [Fact]
    public async Task EnsureConfigAsync_OnStepOne_ShouldLeaveTheFetchToTheLandingStep()
    {
        // Step 1 fetches the config itself and applies the prefills that come with it.
        var state = CreateState();
        var fetches = 0;

        await state.EnsureConfigAsync(_ => { fetches++; return Task.FromResult(ConfigWithCategories()); });

        fetches.Should().Be(0);
        state.Config.Should().BeNull();
    }

    [Fact]
    public async Task EnsureConfigAsync_WhenTheFetchFails_ShouldLeaveTheConfigUnset()
    {
        var state = CreateState();
        await state.GoToStepAsync(5);

        await state.EnsureConfigAsync(_ => Task.FromException<IntakeConfigResponseDto>(new HttpRequestException("offline")));

        state.Config.Should().BeNull();
    }

    [Fact]
    public async Task EnsureConfigAsync_WhenFetchConfigIsNull_ShouldThrowArgumentNullException()
    {
        var state = CreateState();

        var act = () => state.EnsureConfigAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void SelectableIssueCategories_WithNoConfig_ShouldBeEmpty()
    {
        CreateState().SelectableIssueCategories.Should().BeEmpty();
    }

    [Fact]
    public void SelectableIssueCategories_ShouldBeAlphabetizedByNameAndOmitUnselectableItems()
    {
        var state = CreateState();
        state.Config = ConfigWithCategories(
            new LookupItemDto("slides", "Slide-outs", null, 1, true),
            new LookupItemDto("appliances", "appliances", null, 2, true),
            new LookupItemDto("other", "Other", null, 3, false),
            new LookupItemDto("electrical", "Electrical", null, 4, true));

        state.SelectableIssueCategories.Select(c => c.Name).Should()
            .Equal("appliances", "Electrical", "Slide-outs");
    }

    // ---- Furthest step reached, and history-driven step changes -----------------------------
    // The step now rides in the URL so the browser's Back button walks the wizard. That makes
    // the step something a customer can ask for directly, so the state has to know which steps
    // they have legitimately reached.

    [Fact]
    public void MaxStepReached_OnAFreshWizard_ShouldBeStepOne()
    {
        CreateState().MaxStepReached.Should().Be(1);
    }

    [Fact]
    public async Task GoToNextStepAsync_ShouldRaiseMaxStepReached()
    {
        var state = CreateState();

        await state.GoToNextStepAsync();
        await state.GoToNextStepAsync();

        state.MaxStepReached.Should().Be(3);
    }

    [Fact]
    public async Task GoToPreviousStepAsync_ShouldNotLowerMaxStepReached()
    {
        var state = CreateState();
        await state.GoToStepAsync(4);

        await state.GoToPreviousStepAsync();

        state.CurrentStep.Should().Be(3);
        state.MaxStepReached.Should().Be(4);
    }

    [Fact]
    public async Task GoToStepAsync_ShouldRaiseMaxStepReached()
    {
        var state = CreateState();

        await state.GoToStepAsync(6);

        state.MaxStepReached.Should().Be(6);
    }

    [Fact]
    public async Task GoToStepAsync_WhenRejected_ShouldLeaveMaxStepReachedAlone()
    {
        var state = CreateState();
        await state.GoToStepAsync(3);

        await state.GoToStepAsync(9);

        state.MaxStepReached.Should().Be(3);
    }

    [Fact]
    public async Task GoToStepFromHistoryAsync_ShouldMoveToTheStep()
    {
        var state = CreateState();
        await state.GoToStepAsync(5);

        await state.GoToStepFromHistoryAsync(2);

        state.CurrentStep.Should().Be(2);
    }

    [Fact]
    public async Task GoToStepFromHistoryAsync_ShouldNotGoPastTheStepReached()
    {
        // A hand-typed ?step=8 must not skip the steps in between.
        var state = CreateState();
        await state.GoToStepAsync(3);
        await state.GoToStepFromHistoryAsync(1);

        await state.GoToStepFromHistoryAsync(8);

        state.CurrentStep.Should().Be(3);
    }

    [Fact]
    public async Task GoToStepFromHistoryAsync_ShouldNotRaiseMaxStepReached()
    {
        var state = CreateState();
        await state.GoToStepAsync(3);

        await state.GoToStepFromHistoryAsync(3);

        state.MaxStepReached.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GoToStepFromHistoryAsync_BelowStepOne_ShouldLandOnStepOne(int step)
    {
        var state = CreateState();
        await state.GoToStepAsync(4);

        await state.GoToStepFromHistoryAsync(step);

        state.CurrentStep.Should().Be(1);
    }

    [Fact]
    public async Task GoToStepFromHistoryAsync_ShouldCancelAPendingEditReturn()
    {
        // Review sent them to Step 3 to edit, then they pressed Back instead of Continue. The
        // promise to return to Review belonged to that Continue, and the customer overrode it.
        var state = CreateState();
        await state.GoToStepAsync(8);
        state.ReturnToStepAfterEdit = 8;

        await state.GoToStepFromHistoryAsync(3);

        state.ReturnToStepAfterEdit.Should().BeNull();
    }

    [Fact]
    public async Task PersistAndRestore_ShouldKeepTheStepReachedAcrossAReload()
    {
        var js = new InMemoryWebStorageJSRuntime();
        var state = new IntakeWizardState(js);
        state.Slug = "acme-rv";
        await state.GoToStepAsync(6);
        await state.GoToStepFromHistoryAsync(2);

        var restored = new IntakeWizardState(js);
        await restored.RestoreAsync();

        restored.CurrentStep.Should().Be(2);
        restored.MaxStepReached.Should().Be(6);
    }

    [Fact]
    public async Task RestoreAsync_ForASessionSavedBeforeTheStepWasTracked_ShouldTrustTheCurrentStep()
    {
        // A session persisted by the previous build has no maxStepReached. Reading it as 0 would
        // clamp a customer on Step 5 back to Step 1 and lose their place.
        var js = new InMemoryWebStorageJSRuntime();
        js.Items["rvs_intake_wizard_state"] =
            """{"CurrentStep":5,"Slug":"acme-rv","FirstName":"Dana"}""";
        var state = new IntakeWizardState(js);

        await state.RestoreAsync();

        state.CurrentStep.Should().Be(5);
        state.MaxStepReached.Should().Be(5);
    }

    [Fact]
    public async Task ClearAsync_ShouldResetTheStepReached()
    {
        var state = CreateState();
        await state.GoToStepAsync(7);

        await state.ClearAsync();

        state.MaxStepReached.Should().Be(1);
    }
}
