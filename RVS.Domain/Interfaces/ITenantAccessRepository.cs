using System.Security.Claims;

namespace RVS.Domain.Interfaces;


/// <summary>
/// Service for managing tenant access and user roles.
/// </summary>
public interface ITenantAccessRepository
{
    Task<bool> HasAccessAsync(string userId, string tenantId);

    /// <summary>
    /// Returns roles/claims for session context; implementation
    /// can read from identity provider claims, DB, or a mix.
    /// </summary>
    IReadOnlyList<string> GetRolesForUser(ClaimsPrincipal user);
}
