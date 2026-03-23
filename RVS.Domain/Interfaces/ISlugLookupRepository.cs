using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="SlugLookup"/> entities.
/// Partition key: <c>/slug</c>. Enables O(1) location lookup by URL slug during intake.
/// </summary>
public interface ISlugLookupRepository
{
    /// <summary>
    /// Resolves a slug to its corresponding slug-lookup document.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="slug">Lowercase alphanumeric slug with hyphens.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SlugLookup?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces a slug-lookup document.
    /// </summary>
    /// <param name="entity">The slug-lookup entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SlugLookup> UpsertAsync(SlugLookup entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a slug-lookup document.
    /// </summary>
    /// <param name="slug">Slug to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string slug, CancellationToken cancellationToken = default);
}
