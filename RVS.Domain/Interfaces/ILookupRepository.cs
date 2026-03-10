using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing lookup data (visit types, COB reasons, etc.).
/// </summary>
public interface ILookupRepository
{
    /// <summary>
    /// Gets the global lookup set for the specified category.
    /// For MVP, only "GLOBAL" sets are supported.
    /// Returns null if not found.
    /// </summary>
    Task<LookupSet?> GetGlobalAsync(string category);

    /// <summary>
    /// MVP seeding/maintenance API: create or update the global lookup set.
    /// </summary>
    Task UpsertGlobalAsync(LookupSet entity);
}
