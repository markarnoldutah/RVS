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
///
/// <see cref="KeepSignedInPolicy.ShouldForceLogin"/> is deliberately pure string logic with no
/// dependency on the OIDC types — the decision must be applied per interactive sign-in call
/// (<c>UnauthorizedAccess.razor</c>, <c>LoginDisplay.razor</c>), never to the provider's global
/// options, or it leaks into the library's automatic silent sign-in check. See the type's doc
/// comment for the failure that caused.
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
}
