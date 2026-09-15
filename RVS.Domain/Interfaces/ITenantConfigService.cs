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

    /// <summary>
    /// Enables or disables logins for a tenant (Spec P-4). Disabling records the reason and
    /// <c>DisabledAtUtc</c>; enabling clears both.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="loginsEnabled">The new gate state.</param>
    /// <param name="reason">Why logins are disabled. Ignored when enabling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tenant config is not found.</exception>
    Task<TenantConfig> SetAccessGateAsync(string tenantId, bool loginsEnabled, string? reason, CancellationToken cancellationToken = default);
}
