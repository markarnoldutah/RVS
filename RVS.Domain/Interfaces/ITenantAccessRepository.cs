using System.Security.Claims;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing tenant access and user roles.
/// </summary>
public interface ITenantAccessRepository
{
    /// <summary>
    /// Checks whether a user has access to a given tenant.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> HasAccessAsync(string userId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns roles/claims for session context; implementation
    /// can read from identity provider claims, DB, or a mix.
    /// </summary>
    /// <param name="user">The claims principal representing the authenticated user.</param>
    IReadOnlyList<string> GetRolesForUser(ClaimsPrincipal user);
}
