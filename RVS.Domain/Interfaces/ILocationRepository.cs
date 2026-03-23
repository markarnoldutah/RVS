using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="Location"/> entities.
/// Partition key: <c>/tenantId</c>. Unique key: <c>/tenantId, /slug</c>.
/// </summary>
public interface ILocationRepository
{
    /// <summary>
    /// Gets a location by tenant and identifier.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Location?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a location by its slug within a tenant.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="slug">URL-friendly location slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Location?> GetBySlugAsync(string tenantId, string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all locations belonging to a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Location>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new location document.
    /// </summary>
    /// <param name="entity">The location entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Location> CreateAsync(Location entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing location document.
    /// </summary>
    /// <param name="entity">The updated location entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Location> UpdateAsync(Location entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a location document.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
