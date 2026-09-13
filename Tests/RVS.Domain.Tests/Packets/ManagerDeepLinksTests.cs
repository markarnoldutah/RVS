using FluentAssertions;
using RVS.Domain.Packets;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="ManagerDeepLinks"/> — the one place the packet email's deep links into
/// the authenticated manager app are shaped and parsed (<c>Spec C-7</c>, issue #498).
///
/// Contract under test: <c>{base}/sr/{id}</c> opens the request; <c>{base}/sr/{id}?action={slug}</c>
/// pre-opens a one-tap confirm for In Progress, Waiting on Parts, or Completed. Links are plain
/// navigations — they carry no token and write nothing, so a mail-security scanner that fetches
/// them changes no state.
/// </summary>
public class ManagerDeepLinksTests
{
    private const string BaseUrl = "https://manager.example";

    // ── Build ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WhenBaseUrlIsBlank_ShouldReturnNull(string? baseUrl)
    {
        ManagerDeepLinks.Build(baseUrl, "sr_1").Should().BeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://manager.example")]
    [InlineData("manager.example")]
    public void Build_WhenBaseUrlIsNotHttp_ShouldReturnNull(string baseUrl)
    {
        ManagerDeepLinks.Build(baseUrl, "sr_1").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WhenServiceRequestIdIsBlank_ShouldThrowArgumentException(string? id)
    {
        var act = () => ManagerDeepLinks.Build(BaseUrl, id!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("https://manager.example")]
    [InlineData("https://manager.example/")]
    [InlineData("  https://manager.example/  ")]
    public void Build_ShouldPointTheRequestUrlAtTheSrRoute(string baseUrl)
    {
        var links = ManagerDeepLinks.Build(baseUrl, "sr_1");

        links.Should().NotBeNull();
        links!.RequestUrl.Should().Be("https://manager.example/sr/sr_1");
    }

    [Fact]
    public void Build_ShouldCarryTheThreeEmailActionsInPathOrder()
    {
        var links = ManagerDeepLinks.Build(BaseUrl, "sr_1")!;

        links.Actions.Select(a => (a.Label, a.Status, a.Url)).Should().Equal(
            ("In Progress", "InProgress", "https://manager.example/sr/sr_1?action=in-progress"),
            ("Waiting on Parts", "WaitingOnParts", "https://manager.example/sr/sr_1?action=waiting-on-parts"),
            ("Completed", "Completed", "https://manager.example/sr/sr_1?action=completed"));
    }

    [Fact]
    public void Build_ShouldEscapeTheServiceRequestId()
    {
        var links = ManagerDeepLinks.Build(BaseUrl, "a b/c?d")!;

        links.RequestUrl.Should().Be("https://manager.example/sr/a%20b%2Fc%3Fd");
    }

    // ── Action vocabulary ──────────────────────────────────────────────────

    [Fact]
    public void Actions_ShouldOnlyTargetKnownStatuses()
    {
        foreach (var action in ManagerDeepLinks.Actions)
        {
            StatusTransitions.GetAllowedTargets(action.Status).Should().NotBeEmpty(
                "'{0}' must be one of the fixed C-3 statuses", action.Status);
        }
    }

    [Theory]
    [InlineData("in-progress", "InProgress")]
    [InlineData("waiting-on-parts", "WaitingOnParts")]
    [InlineData("completed", "Completed")]
    [InlineData("IN-PROGRESS", "InProgress")]
    [InlineData("  completed  ", "Completed")]
    public void TryFindAction_WhenSlugIsKnown_ShouldReturnItsStatus(string slug, string expectedStatus)
    {
        var found = ManagerDeepLinks.TryFindAction(slug, out var action);

        found.Should().BeTrue();
        action!.Status.Should().Be(expectedStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cancelled")]
    [InlineData("new")]
    [InlineData("InProgress")]
    public void TryFindAction_WhenSlugIsUnknown_ShouldReturnFalse(string? slug)
    {
        var found = ManagerDeepLinks.TryFindAction(slug, out var action);

        found.Should().BeFalse();
        action.Should().BeNull();
    }
}
