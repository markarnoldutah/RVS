using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="TenantConfig"/> entities.
/// Partition key: <c>/tenantId</c>.
/// </summary>
public interface ITenantConfigRepository
{
    /// <summary>
    /// Gets the configuration for a specific tenant.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TenantConfig?> GetAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tenant configuration document.
    /// </summary>
    /// <param name="entity">The tenant config entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TenantConfig> CreateAsync(TenantConfig entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves (replaces) an existing tenant configuration document.
    /// </summary>
    /// <param name="entity">The updated tenant config entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(TenantConfig entity, CancellationToken cancellationToken = default);
}
