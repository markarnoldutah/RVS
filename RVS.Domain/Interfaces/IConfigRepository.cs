using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing tenant configuration.
/// </summary>
public interface IConfigRepository
{
    /// <summary>Creates a new tenant configuration.</summary>
    /// <param name="tenantConfigEntity">The tenant config entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TenantConfig> CreateTenantConfigAsync(TenantConfig tenantConfigEntity, CancellationToken cancellationToken = default);

    /// <summary>Gets a tenant configuration by tenant ID. Returns <c>null</c> if not found.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TenantConfig?> GetTenantConfigAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Saves (upserts) a tenant configuration.</summary>
    /// <param name="tenantConfigEntity">The tenant config entity to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveTenantConfigAsync(TenantConfig tenantConfigEntity, CancellationToken cancellationToken = default);
}
