using System.Collections.Concurrent;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Integration;

/// <summary>
/// In-memory <see cref="ITenantConfigRepository"/> for the tenant access gate integration
/// tests. Substituted for <c>CosmosTenantConfigRepository</c> so the middleware →
/// <see cref="RVS.API.Services.TenantConfigService"/> → repository chain runs for real
/// without a live Cosmos account. Seed via <see cref="Seed"/>.
/// </summary>
public sealed class FakeTenantConfigRepository : ITenantConfigRepository
{
    private readonly ConcurrentDictionary<string, TenantConfig> _store = new();

    public void Seed(string tenantId, bool loginsEnabled)
    {
        _store[tenantId] = new TenantConfig
        {
            Id = tenantId,
            TenantId = tenantId,
            AccessGate = new TenantAccessGateEmbedded { LoginsEnabled = loginsEnabled }
        };
    }

    public Task<TenantConfig?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.GetValueOrDefault(tenantId));

    public Task<TenantConfig> CreateAsync(TenantConfig entity, CancellationToken cancellationToken = default)
    {
        _store[entity.TenantId] = entity;
        return Task.FromResult(entity);
    }

    public Task SaveAsync(TenantConfig entity, CancellationToken cancellationToken = default)
    {
        _store[entity.TenantId] = entity;
        return Task.CompletedTask;
    }
}
