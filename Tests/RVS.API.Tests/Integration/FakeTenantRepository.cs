using System.Collections.Concurrent;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Integration;

/// <summary>
/// In-memory <see cref="ITenantRepository"/> for the admin endpoint integration tests.
/// Substituted for <c>CosmosTenantRepository</c> so <c>GET api/admin/tenants</c> can reach the
/// controller without a live Cosmos account. Left empty by default: listing a seeded tenant
/// would also read its locations, which are not faked.
/// </summary>
public sealed class FakeTenantRepository : ITenantRepository
{
    private readonly ConcurrentDictionary<string, Tenant> _store = new();

    public Task<Tenant?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.GetValueOrDefault(tenantId));

    public Task<Tenant> CreateAsync(Tenant entity, CancellationToken cancellationToken = default)
    {
        if (!_store.TryAdd(entity.Id, entity))
        {
            throw new ConflictException($"Tenant '{entity.Id}' already exists.");
        }

        return Task.FromResult(entity);
    }

    public Task<Tenant> UpdateAsync(Tenant entity, CancellationToken cancellationToken = default)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<Tenant>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Tenant>>([.. _store.Values]);
}
