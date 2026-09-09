using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="GlobalCustomerAcct"/> entities.
/// Partition key: <c>/email</c>. Cross-tenant — not scoped by tenantId.
/// Email is always stored in normalized form (trimmed, lowercased).
/// </summary>
public interface IGlobalCustomerAcctRepository
{
    /// <summary>
    /// Gets a global customer account by email address.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="email">Customer email address (expected to be already normalized).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GlobalCustomerAcct?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

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
    /// Finds a global customer account by the SHA-256 hash (base64url) of its status token.
    /// Returns <c>null</c> when no account matches. The raw token is never stored, so callers
    /// must hash the incoming token (<c>AnonymousTokenHelper.ComputeHash</c>) before calling.
    /// </summary>
    /// <param name="tokenHash">The hashed status token value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GlobalCustomerAcct?> GetByMagicLinkTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
