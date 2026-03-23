using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="ServiceRequest"/> entities.
/// Partition key: <c>/tenantId</c>.
/// </summary>
public interface IServiceRequestRepository
{
    /// <summary>
    /// Gets a single service request by its identifier.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="id">Service request identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ServiceRequest?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists service requests associated with a specific location.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="locationId">Location identifier to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ServiceRequest>> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches service requests with paging support.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="pageSize">Maximum items to return.</param>
    /// <param name="continuationToken">Optional Cosmos DB continuation token for the next page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<ServiceRequest>> SearchAsync(string tenantId, int pageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new service request document.
    /// </summary>
    /// <param name="entity">The service request entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ServiceRequest> CreateAsync(ServiceRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing service request document.
    /// </summary>
    /// <param name="entity">The updated service request entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ServiceRequest> UpdateAsync(ServiceRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a service request document.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="id">Service request identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
