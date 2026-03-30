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
        state.TotalSteps.Should().Be(7);
        state.Slug.Should().BeEmpty();
        state.Token.Should().BeNull();
        state.Config.Should().BeNull();
        state.FirstName.Should().BeEmpty();
        state.LastName.Should().BeEmpty();
        state.Email.Should().BeEmpty();
        state.Phone.Should().BeNull();
        state.IsPrefilled.Should().BeFalse();
        state.Vin.Should().BeEmpty();
        state.IsSubmitted.Should().BeFalse();
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

        state.CurrentStep.Should().Be(7);
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

        await state.GoToStepAsync(8);

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
            Phone = "555-1234"
        };

        state.ApplyPrefill(prefill);

        state.FirstName.Should().Be("Jane");
        state.LastName.Should().Be("Doe");
        state.Email.Should().Be("jane@example.com");
        state.Phone.Should().Be("555-1234");
        state.IsPrefilled.Should().BeTrue();
    }

    [Fact]
    public void ApplyPrefill_NullPrefill_ShouldThrow()
    {
        var state = CreateState();

        var act = () => state.ApplyPrefill(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyAssetPrefill_ShouldSetAssetFields()
    {
        var state = CreateState();
        var prefillAsset = new AssetInfoDto
        {
            AssetId = "RV:1HGBH41JXMN109186",
            Manufacturer = "Grand Design",
            Model = "Momentum 395G",
            Year = 2023
        };

        state.ApplyAssetPrefill(prefillAsset);

        state.Vin.Should().Be("RV:1HGBH41JXMN109186");
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

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
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
    public async Task ValidateCurrentStep_Step4_EmptyFields_ShouldReturnErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(4);

        var errors = state.ValidateCurrentStep();

        errors.Should().Contain(e => e.Contains("category"));
        errors.Should().Contain(e => e.Contains("description"));
    }

    [Fact]
    public async Task ValidateCurrentStep_Step4_DescriptionTooLong_ShouldReturnError()
    {
        var state = CreateState();
        await state.GoToStepAsync(4);
        state.IssueCategory = "Electrical";
        state.IssueDescription = new string('x', 2001);

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle()
            .Which.Should().Contain("2000");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step4_ValidFields_ShouldReturnNoErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(4);
        state.IssueCategory = "Electrical";
        state.IssueDescription = "The lights are flickering.";

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateCurrentStep_Step5_ShouldAlwaysReturnNoErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(5);

        var errors = state.ValidateCurrentStep();

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateCurrentStep_Step6_TooManyAttachments_ShouldReturnError()
    {
        var state = CreateState();
        await state.GoToStepAsync(6);
        for (var i = 0; i < 11; i++)
        {
            state.Attachments.Add(new AttachmentFileInfo { FileName = $"file{i}.jpg" });
        }

        var errors = state.ValidateCurrentStep();

        errors.Should().ContainSingle()
            .Which.Should().Contain("10");
    }

    [Fact]
    public async Task ValidateCurrentStep_Step7_ShouldAlwaysReturnNoErrors()
    {
        var state = CreateState();
        await state.GoToStepAsync(7);

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

        var request = state.BuildCreateRequest();

        request.Customer.FirstName.Should().Be("Jane");
        request.Customer.LastName.Should().Be("Doe");
        request.Customer.Email.Should().Be("jane@example.com");
        request.Customer.Phone.Should().Be("555-1234");
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
    public void Token_ShouldBeSettable()
    {
        var state = CreateState();

        state.Token = "dK3mRw9x:Xv2pLqN8aTcBfY7mZs4eWQ";

        state.Token.Should().Be("dK3mRw9x:Xv2pLqN8aTcBfY7mZs4eWQ");
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
        state.IsSubmitted = true;
        await state.GoToStepAsync(5);

        await state.ClearAsync();

        state.CurrentStep.Should().Be(1);
        state.Slug.Should().BeEmpty();
        state.FirstName.Should().BeEmpty();
        state.LastName.Should().BeEmpty();
        state.Email.Should().BeEmpty();
        state.Vin.Should().BeEmpty();
        state.IsSubmitted.Should().BeFalse();
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
}
