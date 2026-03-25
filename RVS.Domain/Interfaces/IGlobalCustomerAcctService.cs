using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing <see cref="GlobalCustomerAcct"/> (global customer account) entities.
/// Cross-tenant — links dealership-scoped profiles to a single human identity.
/// All lookups are guaranteed to return a non-null value; a
/// <see cref="KeyNotFoundException"/> is thrown when the entity does not exist.
/// </summary>
public interface IGlobalCustomerAcctService
{
    /// <summary>
    /// Gets a global customer account by email address.
    /// Email will be normalized internally (trimmed, lowercased).
    /// </summary>
    /// <param name="email">Customer email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the identity is not found.</exception>
    Task<GlobalCustomerAcct> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing identity or creates one for a new customer.
    /// </summary>
    /// <param name="email">Customer email address (will be normalized internally).</param>
    /// <param name="firstName">Customer first name.</param>
    /// <param name="lastName">Customer last name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GlobalCustomerAcct> GetOrCreateAsync(string email, string firstName, string lastName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links a tenant-scoped customer profile to this global identity.
    /// </summary>
    /// <param name="identityId">Global customer identity identifier.</param>
    /// <param name="tenantId">Tenant that owns the linked profile.</param>
    /// <param name="profileId">Customer profile identifier within the tenant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the identity is not found.</exception>
    Task<GlobalCustomerAcct> LinkProfileAsync(string identityId, string tenantId, string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a magic-link token for the global customer account identified by email.
    /// Token format: <c>base64url(SHA256(email)[0..8]):random_bytes</c>.
    /// The email-hash prefix enables O(1) partition-key derivation on read.
    /// </summary>
    /// <param name="email">Customer email address (will be normalized internally).</param>
    /// <param name="expiresAtUtc">Token expiration timestamp. Defaults to 30 days from now if not specified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when no account exists for the email.</exception>
    Task<GlobalCustomerAcct> GenerateMagicLinkTokenAsync(string email, DateTime? expiresAtUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a magic-link token for the status page.
    /// Returns the account if the token is valid and not expired; throws otherwise.
    /// </summary>
    /// <param name="token">The magic-link token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when no account matches the token.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the token has expired.</exception>
    Task<GlobalCustomerAcct> ValidateMagicLinkTokenAsync(string token, CancellationToken cancellationToken = default);
}
