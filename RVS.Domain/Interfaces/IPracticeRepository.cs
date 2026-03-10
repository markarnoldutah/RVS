using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing practice entities.
/// </summary>
public interface IPracticeRepository
{
    // TODO add CRUD for Practices including embedded Locations

    /// <summary>
    /// Returns all practices for a tenant. includeLocations can drive projection or joins.
    /// </summary>
    Task<List<Practice>> GetPracticesForTenantAsync(string tenantId, bool includeLocations);

    /// <summary>
    /// Returns a single practice or null if not found.
    /// Service layer will throw KeyNotFoundException.
    /// </summary>
    Task<Practice?> GetByIdAsync(string tenantId, string practiceId);
}
