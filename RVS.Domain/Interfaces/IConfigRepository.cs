using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing tenant configuration.
/// </summary>
public interface IConfigRepository
{
    // Tenant config (stored in Tenants container)
    Task<TenantConfig> CreateTenantConfigAsync(TenantConfig tenantConfigEntity);
    Task<TenantConfig?> GetTenantConfigAsync(string tenantId);
    Task SaveTenantConfigAsync(TenantConfig tenantConfigEntity);
}
