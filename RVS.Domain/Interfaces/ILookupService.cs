using RVS.Domain.DTOs;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for retrieving lookup sets visible to a tenant.
/// For MVP, this always returns the global set only.
/// </summary>
public interface ILookupService
{
    /// <summary>
    /// Gets the lookup set visible to the given tenant for the specified category.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="category">Lookup category identifier (e.g. "issue-categories").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the lookup set is not found.</exception>
    Task<LookupSetDto> GetLookupSetAsync(string tenantId, string category, CancellationToken cancellationToken = default);
}