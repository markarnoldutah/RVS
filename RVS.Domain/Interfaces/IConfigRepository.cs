using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing tenant and payer configuration.
/// </summary>
public interface IConfigRepository
{
    // Tenant config (stored in Tenants container)
    Task<TenantConfig> CreateTenantConfigAsync(TenantConfig tenantConfigEntity);
    Task<TenantConfig?> GetTenantConfigAsync(string tenantId);
    Task SaveTenantConfigAsync(TenantConfig tenantConfigEntity);

    // Practice payer configs (stored in payerConfigs container, PK = tenantId)
    Task<List<PayerConfig>> GetPayerConfigsAsync(string tenantId, string practiceId);
    Task<PayerConfig?> GetPayerConfigAsync(string tenantId, string practiceId, string payerId);
    Task SavePayerConfigAsync(string tenantId, PayerConfig payerConfig);

}
