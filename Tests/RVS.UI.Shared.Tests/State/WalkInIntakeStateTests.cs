using FluentAssertions;
using RVS.Blazor.Intake.State;
using RVS.Blazor.Manager.State;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.State;

/// <summary>
/// Tests the Manager walk-in dialog's <see cref="WalkInIntakeState"/>.
/// Mirrors the most important <c>BuildCreateRequest</c> coverage from
/// <see cref="IntakeWizardStateTests"/> to pin parity between the two
/// <see cref="IIntakeWizardState"/> implementations — if either drifts,
/// both hosts hit the API with a different DTO shape.
/// </summary>
public class WalkInIntakeStateTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        var state = new WalkInIntakeState();

        state.Slug.Should().BeEmpty();
        state.Config.Should().BeNull();
        state.FirstName.Should().BeEmpty();
        state.LastName.Should().BeEmpty();
        state.Email.Should().BeEmpty();
        state.Phone.Should().BeNull();
        state.Vin.Should().BeEmpty();
        state.IssueCategory.Should().BeEmpty();
        state.IssueDescription.Should().BeEmpty();
        state.KnownAssets.Should().BeEmpty();
    }

    [Fact]
    public void Implements_IIntakeWizardState()
    {
        new WalkInIntakeState().Should().BeAssignableTo<IIntakeWizardState>();
    }

    [Fact]
    public void IsPrefilled_ShouldAlwaysBeFalse()
    {
        // Walk-ins never arrive with a magic-link prefill — this is a deliberate
        // invariant, not an initial value. Document it here.
        new WalkInIntakeState().IsPrefilled.Should().BeFalse();
    }

    [Fact]
    public async Task NotifyAndPersistAsync_ShouldFireOnChange()
    {
        var state = new WalkInIntakeState();
        var fired = false;
        state.OnChange += () => fired = true;

        await state.NotifyAndPersistAsync();

        fired.Should().BeTrue();
    }

    // ── BuildCreateRequest ───────────────────────────────────────────────

    [Fact]
    public void BuildCreateRequest_FullyPopulatedState_ShouldProduceExpectedDto()
    {
        var state = PopulatedState();

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
        request.IssueDescription.Should().Be("Lights flickering in the main cabin.");
        request.Urgency.Should().Be("High");
        request.RvUsage.Should().Be("Full-Time");
        request.HasExtendedWarranty.Should().Be("Yes");
        request.ApproxPurchaseDate.Should().Be("03/2023");
        request.SmsOptOut.Should().BeTrue();
        request.EmailOptOut.Should().BeTrue();
        // Walk-in dialog does not collect diagnostic responses (Step 6 is intake-only).
        request.DiagnosticResponses.Should().BeNull();
    }

    [Fact]
    public void BuildCreateRequest_ShouldTrimStrings()
    {
        var state = new WalkInIntakeState
        {
            FirstName = "  Jane  ",
            LastName = "  Doe  ",
            Email = "  jane@example.com  ",
            Phone = "  555-1234  ",
            Vin = "  1hgbh41jxmn109186  ",
            Manufacturer = "  Winnebago  ",
            Model = "  Vista  ",
            Year = 2023,
            IssueCategory = "  Electrical  ",
            IssueDescription = "  Lights flickering  ",
            Urgency = "  High  ",
            RvUsage = "  Full-Time  "
        };

        var request = state.BuildCreateRequest();

        request.Customer.FirstName.Should().Be("Jane");
        request.Customer.Email.Should().Be("jane@example.com");
        request.Asset.AssetId.Should().Be("1HGBH41JXMN109186");
        request.Asset.Manufacturer.Should().Be("Winnebago");
        request.IssueCategory.Should().Be("Electrical");
        request.IssueDescription.Should().Be("Lights flickering");
        request.Urgency.Should().Be("High");
        request.RvUsage.Should().Be("Full-Time");
    }

    [Fact]
    public void BuildCreateRequest_ShouldUppercaseVin()
    {
        var state = MinimalValidState();
        state.Vin = "1hgbh41jxmn109186";

        state.BuildCreateRequest().Asset.AssetId.Should().Be("1HGBH41JXMN109186");
    }

    [Fact]
    public void BuildCreateRequest_ShouldSetNullForEmptyOptionalFields()
    {
        var state = MinimalValidState();
        state.Phone = "   ";
        state.Manufacturer = "";
        state.Model = "  ";
        state.Urgency = "";
        state.RvUsage = "  ";
        state.HasExtendedWarranty = " ";
        state.ApproxPurchaseDate = "";

        var request = state.BuildCreateRequest();

        request.Customer.Phone.Should().BeNull();
        request.Asset.Manufacturer.Should().BeNull();
        request.Asset.Model.Should().BeNull();
        request.Urgency.Should().BeNull();
        request.RvUsage.Should().BeNull();
        request.HasExtendedWarranty.Should().BeNull();
        request.ApproxPurchaseDate.Should().BeNull();
    }

    [Fact]
    public void BuildCreateRequest_DefaultOptOuts_ShouldBeFalse()
    {
        var request = MinimalValidState().BuildCreateRequest();

        request.SmsOptOut.Should().BeFalse();
        request.EmailOptOut.Should().BeFalse();
    }

    // ── Parity with IntakeWizardState ───────────────────────────────────

    [Fact]
    public void BuildCreateRequest_ShouldMatchIntakeWizardState_ForIdenticalInputs()
    {
        // Any drift between the two state implementations will produce divergent
        // DTOs and break API contract parity for the two hosting apps.
        var walkIn = PopulatedState();
        var intake = PopulateIntakeState(new IntakeWizardState(new NullJSRuntime()));

        var walkInDto = walkIn.BuildCreateRequest();
        var intakeDto = intake.BuildCreateRequest();

        // Intake-only field (diagnostic responses) cleared for the comparison —
        // walk-in doesn't surface that UI, so its DTO will always have null here.
        var normalizedIntake = intakeDto with { DiagnosticResponses = null };

        walkInDto.Should().BeEquivalentTo(normalizedIntake);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static WalkInIntakeState MinimalValidState() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Vin = "1HGBH41JXMN109186",
        IssueCategory = "Electrical",
        IssueDescription = "Test"
    };

    private static WalkInIntakeState PopulatedState() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        SmsOptOut = true,
        EmailOptOut = true,
        Vin = "1HGBH41JXMN109186",
        Manufacturer = "Winnebago",
        Model = "Vista",
        Year = 2023,
        IssueCategory = "Electrical",
        IssueDescription = "Lights flickering in the main cabin.",
        Urgency = "High",
        RvUsage = "Full-Time",
        HasExtendedWarranty = "Yes",
        ApproxPurchaseDate = "03/2023"
    };

    private static IntakeWizardState PopulateIntakeState(IntakeWizardState state)
    {
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Phone = "555-1234";
        state.SmsOptOut = true;
        state.EmailOptOut = true;
        state.Vin = "1HGBH41JXMN109186";
        state.Manufacturer = "Winnebago";
        state.Model = "Vista";
        state.Year = 2023;
        state.IssueCategory = "Electrical";
        state.IssueDescription = "Lights flickering in the main cabin.";
        state.Urgency = "High";
        state.RvUsage = "Full-Time";
        state.HasExtendedWarranty = "Yes";
        state.ApproxPurchaseDate = "03/2023";
        return state;
    }
}
