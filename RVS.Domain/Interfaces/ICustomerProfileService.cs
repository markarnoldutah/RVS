using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing <see cref="CustomerProfile"/> entities.
/// All lookups are guaranteed to return a non-null value; a
/// <see cref="KeyNotFoundException"/> is thrown when the entity does not exist.
/// </summary>
public interface ICustomerProfileService
{
    /// <summary>
    /// Gets a customer profile by its identifier.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Customer profile identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the profile is not found.</exception>
    Task<CustomerProfile> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a customer profile by email address within a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="email">Customer email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the profile is not found.</exception>
    Task<CustomerProfile> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing profile or auto-creates a shadow profile on first intake.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="email">Customer email address.</param>
    /// <param name="firstName">Customer first name.</param>
    /// <param name="lastName">Customer last name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerProfile> GetOrCreateAsync(string tenantId, string email, string firstName, string lastName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing customer profile.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Customer profile identifier.</param>
    /// <param name="entity">The updated customer profile entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the profile is not found.</exception>
    Task<CustomerProfile> UpdateAsync(string tenantId, string id, CustomerProfile entity, CancellationToken cancellationToken = default);
}
