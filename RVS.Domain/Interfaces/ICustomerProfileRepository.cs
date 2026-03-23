using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="CustomerProfile"/> entities.
/// Partition key: <c>/tenantId</c>. Unique key: <c>/tenantId, /email</c>.
/// </summary>
public interface ICustomerProfileRepository
{
    /// <summary>
    /// Gets a customer profile by its identifier.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="id">Customer profile identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerProfile?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a customer profile by email address within a tenant.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="email">Customer email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerProfile?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new customer profile document.
    /// </summary>
    /// <param name="entity">The customer profile entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerProfile> CreateAsync(CustomerProfile entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing customer profile document.
    /// </summary>
    /// <param name="entity">The updated customer profile entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerProfile> UpdateAsync(CustomerProfile entity, CancellationToken cancellationToken = default);
}
