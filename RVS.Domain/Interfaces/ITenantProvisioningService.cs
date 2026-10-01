using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Provisioning;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Platform-admin tenant provisioning (Spec P-1 … P-12, issues #563 and #647). Unlike every other service,
/// <c>tenantId</c> comes from the route, not the caller's claims — the caller is RVS staff acting
/// on another tenant, and the <c>PlatformAdmin</c> policy is what authorizes that.
/// </summary>
public interface ITenantProvisioningService
{
    /// <summary>Lists every tenant with its access gate and locations, ordered by name.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<TenantOverview>> ListTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>Edits a tenant's commercial details. Never touches the access gate.</summary>
    /// <param name="tenantId">Tenant to edit.</param>
    /// <param name="request">Fields to change; nulls are left as they are.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The tenant does not exist.</exception>
    Task<TenantOverview> UpdateTenantAsync(string tenantId, TenantUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a tenant: Cosmos documents first with fixed ids, the Auth0 user last. Safe to
    /// re-submit — existing pieces are reported as already existing, missing ones are created.
    /// Step failures are reported in the result rather than thrown.
    /// </summary>
    /// <param name="request">The create-tenant form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">The request is invalid.</exception>
    /// <exception cref="RVS.Domain.Exceptions.ConflictException">The tenant id is already used by a tenant with a different name.</exception>
    Task<TenantProvisioningResult> CreateTenantAsync(TenantCreateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Adds (or updates) an Auth0 user in a tenant and returns a set-password link.</summary>
    /// <param name="tenantId">Tenant to add the user to.</param>
    /// <param name="request">Who to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The tenant does not exist.</exception>
    /// <exception cref="RVS.Domain.Exceptions.ConflictException">The email belongs to a user in another tenant.</exception>
    Task<TenantUserProvisioningResult> AddUserAsync(string tenantId, TenantUserCreateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Issues a new set-password link for an existing user of the tenant.</summary>
    /// <param name="tenantId">Tenant the user must belong to.</param>
    /// <param name="userId">Auth0 user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The tenant or user does not exist, or the user belongs to another tenant.</exception>
    Task<PasswordTicket> CreatePasswordTicketAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Lists the tenant's Manager-app users with their roles, ordered by name then email (P-9).</summary>
    /// <param name="tenantId">Tenant whose users to list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The tenant does not exist.</exception>
    Task<IReadOnlyList<IdentityUser>> ListUsersAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Replaces a user's name, role and locations (P-10).</summary>
    /// <param name="tenantId">Tenant the user must belong to.</param>
    /// <param name="userId">Auth0 user id.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">The request is invalid, or names a location outside the tenant.</exception>
    /// <exception cref="KeyNotFoundException">The tenant or user does not exist, or the user belongs to another tenant.</exception>
    Task<IdentityUser> UpdateUserAsync(string tenantId, string userId, TenantUserUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Disables or re-enables one user's logins (P-11).</summary>
    /// <param name="tenantId">Tenant the user must belong to.</param>
    /// <param name="userId">Auth0 user id.</param>
    /// <param name="request">The new login state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The tenant or user does not exist, or the user belongs to another tenant.</exception>
    Task<IdentityUser> SetUserLoginsEnabledAsync(string tenantId, string userId, TenantUserAccessUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a user from the identity provider (P-12).</summary>
    /// <param name="tenantId">Tenant the user must belong to.</param>
    /// <param name="userId">Auth0 user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The tenant or user does not exist, or the user belongs to another tenant.</exception>
    Task DeleteUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables logins for a tenant. Never changes the commercial status.</summary>
    /// <param name="tenantId">Tenant to gate.</param>
    /// <param name="request">The new gate state and, when disabling, the reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The tenant or its config does not exist.</exception>
    Task<TenantOverview> SetAccessGateAsync(string tenantId, TenantAccessGateUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Adds a location to an existing tenant.</summary>
    /// <param name="tenantId">Tenant to add the location to.</param>
    /// <param name="request">The location form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The tenant does not exist.</exception>
    /// <exception cref="RVS.Domain.Exceptions.ConflictException">The slug is taken.</exception>
    Task<Location> AddLocationAsync(string tenantId, TenantLocationCreateRequestDto request, CancellationToken cancellationToken = default);
}
