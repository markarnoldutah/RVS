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
    /// Gets a global customer account by normalized email.
    /// </summary>
    /// <param name="normalizedEmail">Lowercased, trimmed email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the identity is not found.</exception>
    Task<GlobalCustomerAcct> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

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
}
