using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing <see cref="Location"/> entities within a dealership.
/// All lookups are guaranteed to return a non-null value; a
/// <see cref="KeyNotFoundException"/> is thrown when the entity does not exist.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Gets a location by its identifier.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the location is not found.</exception>
    Task<Location> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all locations belonging to a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Location>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new location.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="entity">The location entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Location> CreateAsync(string tenantId, Location entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing location.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="entity">The updated location entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the location is not found.</exception>
    Task<Location> UpdateAsync(string tenantId, string id, Location entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a location.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the location is not found.</exception>
    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a hard bounce for one packet-email recipient at a location (<c>Spec B-4</c>,
    /// issue #439): moves <b>only that address</b> from the active recipient list to
    /// <see cref="RVS.Domain.Entities.PacketConfigEmbedded.DisabledRecipients"/>, never disabling
    /// the whole configuration. The remaining recipients are notified; if the bounce leaves the
    /// location with no active recipients an alert is logged. Idempotent — a repeat call for an
    /// address that is not an active recipient is a no-op.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="recipientEmail">The address that hard-bounced.</param>
    /// <param name="reason">Short, non-PII bounce reason to record. Optional.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the location is not found.</exception>
    Task<Location> DisableRecipientForBounceAsync(
        string tenantId, string id, string recipientEmail, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enables a previously bounced packet-email recipient (<c>Spec B-4</c>, issue #439):
    /// moves the address back from
    /// <see cref="RVS.Domain.Entities.PacketConfigEmbedded.DisabledRecipients"/> to the active
    /// recipient list. Idempotent — a no-op when the address is not currently disabled.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="id">Location identifier.</param>
    /// <param name="recipientEmail">The disabled address to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the location is not found.</exception>
    Task<Location> ReEnableRecipientAsync(
        string tenantId, string id, string recipientEmail, CancellationToken cancellationToken = default);
}
