using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing <see cref="TenantConfig"/> entities.
/// All lookups are guaranteed to return a non-null value; a
/// <see cref="KeyNotFoundException"/> is thrown when the entity does not exist.
/// </summary>
public interface ITenantConfigService
{
    /// <summary>
    /// Creates tenant configuration for a new tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="request">Configuration creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TenantConfig> CreateTenantConfigAsync(string tenantId, TenantConfigCreateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configuration for a specific tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tenant config is not found.</exception>
    Task<TenantConfig> GetTenantConfigAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the configuration for a specific tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="request">Configuration update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tenant config is not found.</exception>
    Task<TenantConfig> UpdateTenantConfigAsync(string tenantId, TenantConfigUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the access gate configuration for a specific tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tenant config is not found.</exception>
    Task<TenantAccessGateEmbedded> GetAccessGateAsync(string tenantId, CancellationToken cancellationToken = default);
}
