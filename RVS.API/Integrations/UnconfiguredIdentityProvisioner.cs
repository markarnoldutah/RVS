using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Registered in place of <see cref="Auth0ManagementProvisioner"/> when the
/// <c>Auth0Provisioner</c> settings are missing (Spec P-7, issue #563). Every call throws: unlike
/// the other integrations there is no silent no-op, because a tenant provisioned without its
/// first user must be reported as a failure, not look like a success.
/// </summary>
public sealed class UnconfiguredIdentityProvisioner : IIdentityProvisioner
{
    internal const string NotConfiguredMessage =
        "Auth0 provisioning is not configured. Set Auth0Provisioner:Domain, Auth0Provisioner:ClientId and " +
        "Auth0Provisioner:ClientSecret (Key Vault: Auth0Provisioner--Domain, --ClientId, --ClientSecret).";

    /// <inheritdoc />
    public Task<IdentityUserResult> EnsureUserAsync(IdentityUserRequest request, CancellationToken cancellationToken = default) =>
        Task.FromException<IdentityUserResult>(new InvalidOperationException(NotConfiguredMessage));

    /// <inheritdoc />
    public Task<IdentityUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromException<IdentityUser?>(new InvalidOperationException(NotConfiguredMessage));

    /// <inheritdoc />
    public Task<PasswordTicket> CreatePasswordTicketAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromException<PasswordTicket>(new InvalidOperationException(NotConfiguredMessage));

    /// <inheritdoc />
    public Task<IReadOnlyList<IdentityUser>> ListUsersAsync(string tenantId, CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<IdentityUser>>(new InvalidOperationException(NotConfiguredMessage));

    /// <inheritdoc />
    public Task<IdentityUser> UpdateUserAsync(string userId, IdentityUserUpdate changes, CancellationToken cancellationToken = default) =>
        Task.FromException<IdentityUser>(new InvalidOperationException(NotConfiguredMessage));

    /// <inheritdoc />
    public Task<IdentityUser> SetBlockedAsync(string userId, bool blocked, CancellationToken cancellationToken = default) =>
        Task.FromException<IdentityUser>(new InvalidOperationException(NotConfiguredMessage));

    /// <inheritdoc />
    public Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException(NotConfiguredMessage));
}
