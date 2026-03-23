using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing tenant configuration.
/// </summary>
public interface IConfigRepository
{
    /// <summary>Creates a new tenant configuration.</summary>
    Task<TenantConfig> CreateTenantConfigAsync(TenantConfig tenantConfigEntity);

    /// <summary>Gets a tenant configuration by tenant ID.</summary>
    Task<TenantConfig?> GetTenantConfigAsync(string tenantId);

    /// <summary>Saves (upserts) a tenant configuration.</summary>
    Task SaveTenantConfigAsync(TenantConfig tenantConfigEntity);
}
