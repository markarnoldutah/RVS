using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="GlobalCustomerAcct"/> entities.
/// Partition key: <c>/normalizedEmail</c>. Cross-tenant — not scoped by tenantId.
/// </summary>
public interface IGlobalCustomerAcctRepository
{
    /// <summary>
    /// Gets a global customer account by normalized email address.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="normalizedEmail">Lowercased, trimmed email address used as partition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GlobalCustomerAcct?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a global customer account by its identifier.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="id">Customer account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GlobalCustomerAcct?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new global customer account document.
    /// </summary>
    /// <param name="entity">The global customer account entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GlobalCustomerAcct> CreateAsync(GlobalCustomerAcct entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing global customer account document.
    /// </summary>
    /// <param name="entity">The updated global customer account entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GlobalCustomerAcct> UpdateAsync(GlobalCustomerAcct entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a global customer account by its magic-link token.
    /// Returns <c>null</c> when no account matches.
    /// </summary>
    /// <param name="token">The magic-link token value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GlobalCustomerAcct?> GetByMagicLinkTokenAsync(string token, CancellationToken cancellationToken = default);
}
