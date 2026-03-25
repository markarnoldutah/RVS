using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing <see cref="Dealership"/> entities.
/// All lookups are guaranteed to return a non-null value; a
/// <see cref="KeyNotFoundException"/> is thrown when the entity does not exist.
/// </summary>
public interface IDealershipService
{
    /// <summary>
    /// Gets a dealership by its identifier.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Dealership identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the dealership is not found.</exception>
    Task<Dealership> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a dealership by its unique slug.
    /// </summary>
    /// <param name="slug">URL-friendly dealership slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the dealership is not found.</exception>
    Task<Dealership> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all dealerships belonging to a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Dealership>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new dealership.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="entity">The dealership entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Dealership> CreateAsync(string tenantId, Dealership entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing dealership.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Dealership identifier.</param>
    /// <param name="entity">The updated dealership entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the dealership is not found.</exception>
    Task<Dealership> UpdateAsync(string tenantId, string id, Dealership entity, CancellationToken cancellationToken = default);
}
