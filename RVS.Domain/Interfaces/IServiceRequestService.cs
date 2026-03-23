using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing <see cref="ServiceRequest"/> entities.
/// All lookups are guaranteed to return a non-null value; a
/// <see cref="KeyNotFoundException"/> is thrown when the entity does not exist.
/// </summary>
public interface IServiceRequestService
{
    /// <summary>
    /// Gets a service request by its identifier.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Service request identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    Task<ServiceRequest> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches service requests with paging support.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="pageSize">Maximum items to return.</param>
    /// <param name="continuationToken">Optional continuation token for the next page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<ServiceRequest>> SearchAsync(string tenantId, int pageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new service request.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="entity">The service request entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ServiceRequest> CreateAsync(string tenantId, ServiceRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing service request.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Service request identifier.</param>
    /// <param name="entity">The updated service request entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    Task<ServiceRequest> UpdateAsync(string tenantId, string id, ServiceRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions the status of a service request, validated by <see cref="RVS.Domain.Validation.StatusTransitions"/>.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Service request identifier.</param>
    /// <param name="newStatus">Target status value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    /// <exception cref="ArgumentException">Thrown when the status transition is invalid.</exception>
    Task<ServiceRequest> UpdateStatusAsync(string tenantId, string id, string newStatus, CancellationToken cancellationToken = default);
}
