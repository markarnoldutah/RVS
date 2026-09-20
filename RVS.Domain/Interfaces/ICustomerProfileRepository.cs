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

    /// <summary>
    /// Finds the customer profile that has an active ownership entry for the specified asset.
    /// Returns <c>null</c> when no profile actively owns the asset in this tenant.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="assetId">Asset identifier (e.g. <c>1FTFW1ET5EKE12345</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerProfile?> GetByActiveAssetIdAsync(string tenantId, string assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the phone numbers, as stored, of every profile in the tenant that has opted out of
    /// SMS. Stored numbers are not normalised, so callers compare after normalising each one.
    /// Used to refuse an advisor intake invite to an opted-out number (<c>Spec A-14</c>, issue #663).
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> ListSmsOptedOutPhonesAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every profile, in every tenant, whose <see cref="CustomerProfile.PhoneE164"/>
    /// matches. This is the one deliberately cross-partition read on this container: an inbound
    /// carrier keyword (issue #665) arrives with a phone number and no tenant, and the shared
    /// toll-free number is blocked for every dealer at once, so the opt-out has to reach all of
    /// their records. Rare traffic, and bounded by how many dealers know one customer.
    /// </summary>
    /// <param name="phoneE164">The number in E.164, e.g. <c>+18015551234</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<CustomerProfile>> ListByPhoneE164AcrossTenantsAsync(
        string phoneE164, CancellationToken cancellationToken = default);
}
