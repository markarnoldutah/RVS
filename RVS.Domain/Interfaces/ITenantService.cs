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
    Task<TenantConfig> CreateTenantConfigAsync(string tenantId, TenantConfigCreateRequestDto request);

    /// <summary>
    /// Retrieves the current tenant configuration.
    /// </summary>
    Task<TenantConfig> GetTenantConfigAsync(string tenantId);

    /// <summary>
    /// Updates the tenant configuration.
    /// </summary>
    Task<TenantConfig> UpdateTenantConfigAsync(string tenantId, TenantConfigUpdateRequestDto request);

    /// <summary>
    /// Retrieves the tenant access gate settings (login enabled/disabled state).
    /// </summary>
    Task<TenantAccessGateEmbedded> GetAccessGateAsync(string tenantId);
}
