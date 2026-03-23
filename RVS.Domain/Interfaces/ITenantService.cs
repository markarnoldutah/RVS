using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for managing tenant configuration and settings.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Creates the initial tenant configuration (bootstrap).
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="request">Configuration creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TenantConfig> CreateTenantConfigAsync(string tenantId, TenantConfigCreateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current tenant configuration.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tenant config is not found.</exception>
    Task<TenantConfig> GetTenantConfigAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the tenant configuration.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="request">Configuration update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tenant config is not found.</exception>
    Task<TenantConfig> UpdateTenantConfigAsync(string tenantId, TenantConfigUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the tenant access gate settings (login enabled/disabled state).
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tenant config is not found.</exception>
    Task<TenantAccessGateEmbedded> GetAccessGateAsync(string tenantId, CancellationToken cancellationToken = default);
}
