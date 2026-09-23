using FluentAssertions;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests for <see cref="KeepSignedInPolicy"/> — decides whether a sign-in must force Auth0's
/// password prompt (<c>prompt=login</c>) based on this device's "Keep me signed in" answer
/// (issue #498 hardening).
///
/// Contract under test: only a device that explicitly opted in may reuse Auth0's own session
/// cookie; a device that said no, has not been asked, or holds an unrecognised value always
/// re-authenticates. That closes the gap where a shared computer's user closed the tab without
/// signing out and Auth0 silently signed the next person back in.
/// </summary>
public class KeepSignedInPolicyTests
{
    [Fact]
    public void ShouldForceLogin_WhenDeviceOptedIn_ShouldBeFalse()
    {
        KeepSignedInPolicy.ShouldForceLogin(KeepSignedInPolicy.PreferenceYes).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no")]
    [InlineData("YES")]
    [InlineData(" yes ")]
    [InlineData("true")]
    public void ShouldForceLogin_WhenDeviceHasNotExplicitlyOptedIn_ShouldBeTrue(string? preference)
    {
        KeepSignedInPolicy.ShouldForceLogin(preference).Should().BeTrue();
    }

    [Fact]
    public void ApplyTo_WhenLoginMustBeForced_ShouldAddPromptLogin()
    {
        var parameters = new Dictionary<string, string>();

        KeepSignedInPolicy.ApplyTo(parameters, preference: "no");

        parameters.Should().ContainKey("prompt").WhoseValue.Should().Be("login");
    }

    [Fact]
    public void ApplyTo_WhenDeviceOptedIn_ShouldRemoveAnyPromptParameter()
    {
        var parameters = new Dictionary<string, string> { ["prompt"] = "login", ["audience"] = "https://api.example" };

        KeepSignedInPolicy.ApplyTo(parameters, preference: "yes");

        parameters.Should().NotContainKey("prompt");
        parameters.Should().ContainKey("audience");
    }

    [Fact]
    public void ApplyTo_WhenParametersIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => KeepSignedInPolicy.ApplyTo(null!, "yes");

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Profile-menu control (issue #616) ────────────────────────────────────

    [Theory]
    [InlineData("yes", KeepSignedInState.On)]
    [InlineData("no", KeepSignedInState.Off)]
    [InlineData(null, KeepSignedInState.NotSet)]
    [InlineData("", KeepSignedInState.NotSet)]
    [InlineData("YES", KeepSignedInState.NotSet)]
    [InlineData("No", KeepSignedInState.NotSet)]
    [InlineData(" yes ", KeepSignedInState.NotSet)]
    [InlineData("true", KeepSignedInState.NotSet)]
    public void ParseState_ShouldMapOnlyExactStoredAnswers(string? preference, KeepSignedInState expected)
    {
        KeepSignedInPolicy.ParseState(preference).Should().Be(expected);
    }

    [Fact]
    public void ParseState_ShouldAgreeWithShouldForceLogin()
    {
        // The menu must never show "On" for a value the startup policy treats as not opted in.
        foreach (var preference in new[] { "yes", "no", null, "", "YES", "true" })
        {
            var shownAsOn = KeepSignedInPolicy.ParseState(preference) == KeepSignedInState.On;
            shownAsOn.Should().Be(!KeepSignedInPolicy.ShouldForceLogin(preference), $"preference '{preference}'");
        }
    }

    [Theory]
    [InlineData(KeepSignedInState.On, true)]
    [InlineData(KeepSignedInState.Off, false)]
    public void ToPersist_WhenStateIsAnAnswer_ShouldReturnWhetherToKeepTheSignIn(KeepSignedInState state, bool expected)
    {
        KeepSignedInPolicy.ToPersist(state).Should().Be(expected);
    }

    [Fact]
    public void ToPersist_WhenStateIsNotSet_ShouldThrowArgumentOutOfRangeException()
    {
        // "Not set" is where a device starts, never something the menu stores.
        var act = () => KeepSignedInPolicy.ToPersist(KeepSignedInState.NotSet);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SharedComputerWarning_ShouldWarnAgainstSharedAndPublicComputers()
    {
        KeepSignedInPolicy.SharedComputerWarning.Should().Be("Don't choose this on a shared or public computer.");
    }
}
