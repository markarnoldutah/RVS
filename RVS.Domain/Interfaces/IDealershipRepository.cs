using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="Dealership"/> entities.
/// Partition key: <c>/tenantId</c>.
/// </summary>
public interface IDealershipRepository
{
    /// <summary>
    /// Gets a dealership by tenant and identifier.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="id">Dealership identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Dealership?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a dealership by its unique slug.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="slug">URL-friendly dealership slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Dealership?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all dealerships belonging to a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Dealership>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new dealership document.
    /// </summary>
    /// <param name="entity">The dealership entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Dealership> CreateAsync(Dealership entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing dealership document.
    /// </summary>
    /// <param name="entity">The updated dealership entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Dealership> UpdateAsync(Dealership entity, CancellationToken cancellationToken = default);
}
