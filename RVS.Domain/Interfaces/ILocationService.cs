using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing <see cref="Location"/> entities within a dealership.
/// All lookups are guaranteed to return a non-null value; a
/// <see cref="KeyNotFoundException"/> is thrown when the entity does not exist.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Gets a location by its identifier.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the location is not found.</exception>
    Task<Location> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all locations belonging to a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Location>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new location.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="entity">The location entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Location> CreateAsync(string tenantId, Location entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing location.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="entity">The updated location entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the location is not found.</exception>
    Task<Location> UpdateAsync(string tenantId, string id, Location entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a location.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the location is not found.</exception>
    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
