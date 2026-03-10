using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for managing payer entities.
/// </summary>
public interface IPayerRepository
{
    // TODO add CRUD methods for Payers

    /// <summary>
    /// Cosmos-style search; returns zero-or-more payer entities for the tenant.
    /// </summary>
    Task<List<Payer>> SearchAsync(string tenantId, string? planType, string? search);

    /// <summary>
    /// Returns a single payer or null if not found.
    /// </summary>
    Task<Payer?> GetByIdAsync(string tenantId, string payerId);
}
