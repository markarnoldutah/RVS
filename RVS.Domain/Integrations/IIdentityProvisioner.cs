namespace RVS.Domain.Integrations;

/// <summary>
/// Creates and maintains Manager-app users in the identity provider (Auth0) for the
/// platform-admin provisioning tool (Spec P-2 / P-3 / P-7, issue #563; P-9 … P-12, issue #647).
///
/// Unlike the other integrations there is <b>no no-op fallback</b>: when the provider is not
/// configured every call throws, so a half-provisioned tenant is reported rather than hidden.
/// </summary>
public interface IIdentityProvisioner
{
    /// <summary>
    /// Creates the user (random password, never returned) or, when the email already belongs to
    /// a user in <see cref="IdentityUserRequest.TenantId"/>, updates it; then sets its
    /// <c>app_metadata</c> and assigns the role, removing any other <c>dealer:*</c> role.
    /// </summary>
    /// <param name="request">Who to provision and where.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="RVS.Domain.Exceptions.ConflictException">
    /// The email already belongs to a user in another tenant, or to a user with no tenant.
    /// </exception>
    Task<IdentityUserResult> EnsureUserAsync(IdentityUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by identity-provider id. Returns <c>null</c> when no such user exists.
    /// <see cref="IdentityUser.Roles"/> is left empty — roles cost a second call.
    /// </summary>
    /// <param name="userId">Identity-provider user id, e.g. <c>auth0|abc123</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IdentityUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a set-password link (7-day lifetime, marks the email verified, returns to the
    /// Manager app). The URL is a credential: callers must never store or log it.
    /// </summary>
    /// <param name="userId">Identity-provider user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PasswordTicket> CreatePasswordTicketAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every user whose <c>app_metadata.tenantId</c> is <paramref name="tenantId"/>, with
    /// their roles. The provider's search index is eventually consistent: a user created seconds
    /// ago may not be listed yet.
    /// </summary>
    /// <param name="tenantId">Tenant id; letters, digits, <c>_</c> and <c>-</c> only.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<IdentityUser>> ListUsersAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a user's display name, <c>app_metadata.locationIds</c> and <c>dealer:*</c> role.
    /// The new role is assigned before the old ones are removed, so the user is never left
    /// without one. Roles outside <c>dealer:*</c> and <c>app_metadata.tenantId</c> are untouched.
    /// </summary>
    /// <param name="userId">Identity-provider user id.</param>
    /// <param name="changes">The new values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user as it now stands, with roles.</returns>
    Task<IdentityUser> UpdateUserAsync(string userId, IdentityUserUpdate changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Blocks or unblocks a user. A blocked user cannot sign in; an access token already issued
    /// stays valid until it expires.
    /// </summary>
    /// <param name="userId">Identity-provider user id.</param>
    /// <param name="blocked"><c>true</c> to block.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user as it now stands, with roles.</returns>
    Task<IdentityUser> SetBlockedAsync(string userId, bool blocked, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a user. Deleting a user that no longer exists succeeds.</summary>
    /// <param name="userId">Identity-provider user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>A user to provision.</summary>
/// <param name="Email">Login email.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="TenantId"><c>app_metadata.tenantId</c> — the isolation boundary.</param>
/// <param name="OrgName"><c>app_metadata.orgName</c> — required by the Post-Login Action.</param>
/// <param name="LocationIds"><c>app_metadata.locationIds</c>; empty for tenant-wide roles.</param>
/// <param name="Role">Role name, e.g. <c>dealer:owner</c>.</param>
public sealed record IdentityUserRequest(
    string Email,
    string DisplayName,
    string TenantId,
    string OrgName,
    IReadOnlyList<string> LocationIds,
    string Role);

/// <summary>Result of <see cref="IIdentityProvisioner.EnsureUserAsync"/>.</summary>
/// <param name="UserId">Identity-provider user id.</param>
/// <param name="Created"><c>true</c> when a new user was created; <c>false</c> when an existing one was updated.</param>
public sealed record IdentityUserResult(string UserId, bool Created);

/// <summary>New values for <see cref="IIdentityProvisioner.UpdateUserAsync"/>.</summary>
/// <param name="DisplayName">Display name.</param>
/// <param name="LocationIds"><c>app_metadata.locationIds</c>; empty for tenant-wide roles.</param>
/// <param name="Role">The one <c>dealer:*</c> role the user should hold.</param>
public sealed record IdentityUserUpdate(string DisplayName, IReadOnlyList<string> LocationIds, string Role);

/// <summary>A user as the identity provider knows it.</summary>
/// <param name="UserId">Identity-provider user id.</param>
/// <param name="Email">Login email.</param>
/// <param name="TenantId"><c>app_metadata.tenantId</c>, or <c>null</c> when unset.</param>
public sealed record IdentityUser(string UserId, string Email, string? TenantId)
{
    public string? DisplayName { get; init; }

    /// <summary><c>app_metadata.locationIds</c>.</summary>
    public IReadOnlyList<string> LocationIds { get; init; } = [];

    /// <summary>Role names. Empty when the call that produced this user did not fetch roles.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Whether the user is blocked from signing in.</summary>
    public bool Blocked { get; init; }

    public DateTime? CreatedAtUtc { get; init; }

    public DateTime? LastLoginAtUtc { get; init; }
}

/// <summary>A set-password link. The URL is a credential.</summary>
/// <param name="Url">The link to hand to the user.</param>
/// <param name="ExpiresAtUtc">When the link stops working.</param>
public sealed record PasswordTicket(string Url, DateTime ExpiresAtUtc);
