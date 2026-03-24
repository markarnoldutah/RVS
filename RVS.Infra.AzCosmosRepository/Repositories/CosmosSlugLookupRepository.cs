using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="SlugLookup"/> entities.
/// Container: <c>slug-lookups</c>. Partition key: <c>/id</c>.
/// <para>
/// Convention: document <c>id</c> = <c>slug_{slug}</c> (e.g. "slug_blue-compass-slc").
/// The partition key equals the document id, enabling O(1) point reads by slug.
/// </para>
/// </summary>
public sealed class CosmosSlugLookupRepository : CosmosRepositoryBase, ISlugLookupRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosSlugLookupRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosSlugLookupRepository"/>.
    /// </summary>
    public CosmosSlugLookupRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosSlugLookupRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "slug-lookups");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SlugLookup?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        // O(1) point read — document id and partition key both equal "slug_{slug}".
        var docId = BuildId(slug);

        try
        {
            var response = await _container.ReadItemAsync<SlugLookup>(
                docId,
                new PartitionKey(docId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetBySlugAsync [slug={Slug}] — RequestCharge: {Charge} RU", slug, response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SlugLookup> UpsertAsync(SlugLookup entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

        var response = await _container.UpsertItemAsync(
            entity,
            new PartitionKey(entity.Id),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpsertAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var docId = BuildId(slug);

        var response = await _container.DeleteItemAsync<SlugLookup>(
            docId,
            new PartitionKey(docId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("DeleteAsync [slug={Slug}] — RequestCharge: {Charge} RU", slug, response.RequestCharge);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Derives the Cosmos document id from a slug using the <c>slug_{slug}</c> convention.
    /// </summary>
    private static string BuildId(string slug) => $"slug_{slug}";
}
