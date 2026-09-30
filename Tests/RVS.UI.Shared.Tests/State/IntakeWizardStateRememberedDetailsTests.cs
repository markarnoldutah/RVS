using FluentAssertions;
using RVS.Blazor.Intake.State;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.State;

/// <summary>
/// Opt-in device memory of the customer's contact details and VIN (issue #811, Spec A-7).
/// It fills only blank fields, is never offered on an advisor invite, and "Not you?" undoes it.
/// </summary>
public class IntakeWizardStateRememberedDetailsTests
{
    private const string InviteToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static readonly RememberedDetails Jane =
        new("Jane", "Doe", "jane@example.com", "(801) 555-1234", "1FTFW1ET5DFC10312");

    private static IntakeWizardState CreateState() => new(new NullJSRuntime());

    [Fact]
    public void Constructor_ShouldNotRememberDetailsByDefault()
    {
        var state = CreateState();

        state.RememberDetails.Should().BeFalse();
        state.IsRememberedPrefilled.Should().BeFalse();
    }

    [Fact]
    public void ApplyRememberedDetails_ShouldFillContactDetailsAndVin()
    {
        var state = CreateState();

        state.ApplyRememberedDetails(Jane);

        state.FirstName.Should().Be("Jane");
        state.LastName.Should().Be("Doe");
        state.Email.Should().Be("jane@example.com");
        state.Phone.Should().Be("(801) 555-1234");
        state.Vin.Should().Be("1FTFW1ET5DFC10312");
    }

    [Fact]
    public void ApplyRememberedDetails_ShouldMarkThePrefillAndKeepTheCustomerOptedIn()
    {
        // They ticked the box last time; showing it ticked is how they can untick it.
        var state = CreateState();

        state.ApplyRememberedDetails(Jane);

        state.IsRememberedPrefilled.Should().BeTrue();
        state.RememberDetails.Should().BeTrue();
    }

    [Fact]
    public void ApplyRememberedDetails_ShouldNotLookLikeAReturningCustomerMatch()
    {
        var state = CreateState();

        state.ApplyRememberedDetails(Jane);

        state.IsPrefilled.Should().BeFalse();
    }

    [Fact]
    public void ApplyRememberedDetails_ShouldNotOverwriteWhatIsAlreadyEntered()
    {
        var state = CreateState();
        state.FirstName = "Janet";
        state.Email = "janet@example.com";
        state.Vin = "SERIAL123";

        state.ApplyRememberedDetails(Jane);

        state.FirstName.Should().Be("Janet");
        state.Email.Should().Be("janet@example.com");
        state.Vin.Should().Be("SERIAL123");
        state.LastName.Should().Be("Doe");
    }

    [Fact]
    public void ApplyRememberedDetails_ShouldLeaveContactPreferencesForTheCustomer()
    {
        var state = CreateState();

        state.ApplyRememberedDetails(Jane);

        state.PreferredContact.Should().BeNull();
        state.SmsOptOut.Should().BeFalse();
        state.EmailOptOut.Should().BeFalse();
    }

    [Fact]
    public void ApplyRememberedDetails_WhenDetailsIsNull_ShouldThrowArgumentNullException()
    {
        var state = CreateState();

        var act = () => state.ApplyRememberedDetails(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CanRememberDetails_WithoutAnInvite_ShouldBeTrue()
    {
        var state = CreateState();

        state.CanRememberDetails.Should().BeTrue();
    }

    [Fact]
    public void CanRememberDetails_OnAnAdvisorInvite_ShouldBeFalse()
    {
        // A-14 self-entry runs in the advisor's own browser: remembering there would hand this
        // caller's details to the next one.
        var state = CreateState();
        state.InviteToken = InviteToken;

        state.CanRememberDetails.Should().BeFalse();
    }

    [Fact]
    public void ClearRememberedDetails_ShouldBlankWhatWasFilledAndOptOut()
    {
        var state = CreateState();
        state.ApplyRememberedDetails(Jane);

        state.ClearRememberedDetails();

        state.FirstName.Should().BeEmpty();
        state.LastName.Should().BeEmpty();
        state.Email.Should().BeEmpty();
        state.Phone.Should().BeNull();
        state.Vin.Should().BeEmpty();
        state.RememberDetails.Should().BeFalse();
        state.IsRememberedPrefilled.Should().BeFalse();
    }

    [Fact]
    public void ToRememberedDetails_ShouldCarryContactDetailsAndVin()
    {
        var state = CreateState();
        state.FirstName = " Jane ";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.Phone = "(801) 555-1234";
        state.Vin = "1FTFW1ET5DFC10312";

        var details = state.ToRememberedDetails();

        details.Should().Be(Jane);
    }

    [Fact]
    public void ToRememberedDetails_WhenTheVehicleWasSkipped_ShouldCarryNoVin()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.Email = "jane@example.com";
        state.Vin = "1FTFW1ET5DFC10312";
        state.VehicleSkipped = true;

        var details = state.ToRememberedDetails();

        details.Vin.Should().BeNull();
    }

    [Fact]
    public void ToRememberedDetails_WhenPhoneIsBlank_ShouldCarryNoPhone()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.Email = "jane@example.com";
        state.Phone = "  ";

        var details = state.ToRememberedDetails();

        details.Phone.Should().BeNull();
    }

    [Fact]
    public async Task PersistAndRestore_ShouldKeepTheRememberChoiceAcrossAReload()
    {
        var jsRuntime = new InMemoryWebStorageJSRuntime();
        var before = new IntakeWizardState(jsRuntime) { Slug = "test-slug" };
        before.ApplyRememberedDetails(Jane);
        await before.PersistAsync();

        var after = new IntakeWizardState(jsRuntime);
        await after.RestoreAsync();

        after.RememberDetails.Should().BeTrue();
        after.IsRememberedPrefilled.Should().BeTrue();
    }

    [Fact]
    public async Task ClearAsync_ShouldResetTheRememberChoice()
    {
        var state = CreateState();
        state.ApplyRememberedDetails(Jane);

        await state.ClearAsync();

        state.RememberDetails.Should().BeFalse();
        state.IsRememberedPrefilled.Should().BeFalse();
    }
}
