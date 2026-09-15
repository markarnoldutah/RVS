using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for <see cref="Tenant"/> documents — the list of tenants the platform-admin tool
/// reads, and the billing details for hand-sent invoices (issue #563).
/// Stored in the <c>dealerships</c> container with <c>type = 'tenant'</c>; partition key
/// <c>/tenantId</c>, and <c>id == tenantId</c>.
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Gets a tenant by id. Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="tenantId">Tenant id (also the partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Tenant?> GetAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a tenant document. Create-only: never replaces an existing tenant.
    /// </summary>
    /// <param name="entity">The tenant to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="RVS.Domain.Exceptions.ConflictException">A tenant with this id already exists.</exception>
    Task<Tenant> CreateAsync(Tenant entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing tenant document.
    /// </summary>
    /// <param name="entity">The updated tenant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Tenant> UpdateAsync(Tenant entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every tenant. <b>Deliberately cross-partition</b> — the one exception to
    /// single-partition access, reachable only from the platform-admin endpoints.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Tenant>> ListAllAsync(CancellationToken cancellationToken = default);
}
