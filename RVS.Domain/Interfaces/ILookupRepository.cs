using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing lookup data (issue categories, service types, etc.).
/// For MVP, only <c>GLOBAL</c> tenant-level sets are supported.
/// </summary>
public interface ILookupRepository
{
    /// <summary>
    /// Gets the global lookup set for the specified category.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="category">Lookup category identifier (e.g. "issue-categories").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LookupSet?> GetGlobalAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the global lookup set for seeding and maintenance.
    /// </summary>
    /// <param name="entity">The lookup set entity to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertGlobalAsync(LookupSet entity, CancellationToken cancellationToken = default);
}
