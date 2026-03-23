using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="CustomerIdentity"/> entities.
/// Partition key: <c>/normalizedEmail</c>. Cross-tenant — not scoped by tenantId.
/// </summary>
public interface IGlobalCustomerAcctRepository
{
    /// <summary>
    /// Gets a global customer identity by normalized email address.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="normalizedEmail">Lowercased, trimmed email address used as partition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerIdentity?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a global customer identity by its identifier.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="id">Customer identity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerIdentity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new global customer identity document.
    /// </summary>
    /// <param name="entity">The customer identity entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerIdentity> CreateAsync(CustomerIdentity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing global customer identity document.
    /// </summary>
    /// <param name="entity">The updated customer identity entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerIdentity> UpdateAsync(CustomerIdentity entity, CancellationToken cancellationToken = default);
}
