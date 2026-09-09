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
    /// Searches service requests using up to 10 filter parameters with continuation-token pagination.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="request">Filter criteria and page size (keyword, status, category, location, etc.).</param>
    /// <param name="continuationToken">Optional continuation token for the next page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<ServiceRequest>> SearchAsync(string tenantId, ServiceRequestSearchRequestDto request, string? continuationToken = null, CancellationToken cancellationToken = default);

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
    /// <param name="request">The update request DTO containing changed values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    Task<ServiceRequest> UpdateAsync(string tenantId, string id, ServiceRequestUpdateRequestDto request, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Applies a shared repair outcome to up to 25 service requests in a single batch.
    /// All service requests must belong to the specified tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="request">Batch outcome request containing SR IDs and outcome fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when the batch exceeds 25 items.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    Task<BatchOutcomeResponseDto> BatchOutcomeAsync(string tenantId, BatchOutcomeRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a service request.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Service request identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or clears the manager-authored customer status note (<c>Spec C-9</c>). The note renders
    /// on the anonymous customer status page alongside the fixed status. Passing a null, empty, or
    /// whitespace-only <paramref name="note"/> clears the current note. The text is validated
    /// (length cap + blocked characters) and is never written to application logs.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Service request identifier.</param>
    /// <param name="note">The note text, or null/blank to clear.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tenantId"/> or <paramref name="id"/> is blank, or when the note
    /// exceeds the length cap or contains a blocked character.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    Task<ServiceRequest> SetCustomerStatusNoteAsync(string tenantId, string id, string? note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests on-demand regeneration of the request's service packet (<c>Spec B-1</c>, issue #434):
    /// resets the packet-generation state to <c>Pending</c> and enqueues a fresh generation job.
    /// Returns as soon as the job is enqueued — generation runs in the background.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Service request identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the service request is not found.</exception>
    Task RegeneratePacketAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
