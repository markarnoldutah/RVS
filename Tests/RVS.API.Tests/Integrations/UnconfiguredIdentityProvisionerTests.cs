using FluentAssertions;
using RVS.API.Integrations;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Spec P-7: when the Auth0 provisioner settings are missing the endpoints fail loudly —
/// there is deliberately no silent no-op fallback.
/// </summary>
public sealed class UnconfiguredIdentityProvisionerTests
{
    private readonly UnconfiguredIdentityProvisioner _sut = new();

    [Fact]
    public async Task EnsureUserAsync_ShouldThrowNamingTheMissingSettings()
    {
        var act = () => _sut.EnsureUserAsync(new IdentityUserRequest("a@b.example.com", "A", "ten_a", "A", [], "dealer:owner"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Auth0Provisioner*");
    }

    [Fact]
    public async Task GetUserAsync_ShouldThrowNamingTheMissingSettings()
    {
        var act = () => _sut.GetUserAsync("auth0|u1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Auth0Provisioner*");
    }

    [Fact]
    public async Task CreatePasswordTicketAsync_ShouldThrowNamingTheMissingSettings()
    {
        var act = () => _sut.CreatePasswordTicketAsync("auth0|u1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Auth0Provisioner*");
    }

    [Fact]
    public async Task ListUsersAsync_ShouldThrowNamingTheMissingSettings()
    {
        var act = () => _sut.ListUsersAsync("ten_a");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Auth0Provisioner*");
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldThrowNamingTheMissingSettings()
    {
        var act = () => _sut.UpdateUserAsync("auth0|u1", new IdentityUserUpdate("A", [], "dealer:owner"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Auth0Provisioner*");
    }

    [Fact]
    public async Task SetBlockedAsync_ShouldThrowNamingTheMissingSettings()
    {
        var act = () => _sut.SetBlockedAsync("auth0|u1", blocked: true);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Auth0Provisioner*");
    }

    [Fact]
    public async Task DeleteUserAsync_ShouldThrowNamingTheMissingSettings()
    {
        var act = () => _sut.DeleteUserAsync("auth0|u1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Auth0Provisioner*");
    }
}
