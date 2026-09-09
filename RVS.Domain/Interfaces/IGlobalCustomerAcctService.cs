using RVS.Domain.Entities;
using RVS.Domain.Shared;

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
    /// Issues a fresh per-customer status token for the account identified by email (Spec X-5).
    /// The token is <c>base64url(SHA256(email)[0..8]):randomBase64Url</c>; only its SHA-256 hash is
    /// persisted (<see cref="GlobalCustomerAcct.MagicLinkTokenHash"/>). The raw token is returned in
    /// <see cref="MagicLinkIssueResult.RawToken"/> and is never available again.
    /// </summary>
    /// <param name="email">Customer email address (will be normalized internally).</param>
    /// <param name="expiresAtUtc">Token expiration timestamp. Defaults to 30 days from now (the TTL cap).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when no account exists for the email.</exception>
    Task<MagicLinkIssueResult> GenerateMagicLinkTokenAsync(string email, DateTime? expiresAtUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a raw status token for the status page: hashes the incoming token, looks the
    /// account up by hash, checks expiry, and extends the TTL on success (sliding renewal).
    /// Every attempt is audit-logged (hit / miss / expired). Returns the account when valid.
    /// </summary>
    /// <param name="token">The raw status token from the status-page URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when no account matches the token hash.</exception>
    /// <exception cref="Exceptions.MagicLinkExpiredException">Thrown when the token has expired.</exception>
    Task<GlobalCustomerAcct> ValidateMagicLinkTokenAsync(string token, CancellationToken cancellationToken = default);
}
