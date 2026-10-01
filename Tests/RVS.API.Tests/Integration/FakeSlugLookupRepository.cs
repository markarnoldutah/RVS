using System.Collections.Concurrent;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Integration;

/// <summary>
/// In-memory <see cref="ISlugLookupRepository"/> for the <c>go.rvintake.com</c> redirect
/// integration tests (<c>Spec A-13</c>, issue #599). Substituted for
/// <c>CosmosSlugLookupRepository</c> so the redirect can resolve a slug without a live Cosmos
/// account. Only the read path is implemented — the redirect never writes.
/// </summary>
public sealed class FakeSlugLookupRepository : ISlugLookupRepository
{
    private readonly ConcurrentDictionary<string, SlugLookup> _store = new();

    public void Seed(string slug, string tenantId, string locationId) =>
        _store[slug] = new SlugLookup
        {
            Slug = slug,
            TenantId = tenantId,
            LocationId = locationId,
            DealershipName = "Nova RV",
            LocationName = "Hurricane"
        };

    public Task<SlugLookup?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.GetValueOrDefault(slug));

    public Task<SlugLookup> CreateAsync(SlugLookup entity, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The redirect never writes slug lookups.");

    public Task<SlugLookup> UpsertAsync(SlugLookup entity, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The redirect never writes slug lookups.");

    public Task DeleteAsync(string slug, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The redirect never writes slug lookups.");
}
