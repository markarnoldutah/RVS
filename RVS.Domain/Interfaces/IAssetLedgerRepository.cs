using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="AssetLedgerEntry"/> entities.
/// Partition key: <c>/vin</c>. Append-only — entries are immutable once created.
/// </summary>
public interface IAssetLedgerRepository
{
    /// <summary>
    /// Gets all ledger entries for a specific VIN, ordered by submission date.
    /// </summary>
    /// <param name="vin">Vehicle identification number (partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<AssetLedgerEntry>> GetByVinAsync(string vin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single ledger entry by VIN and entry identifier.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="vin">Vehicle identification number (partition key).</param>
    /// <param name="id">Ledger entry identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AssetLedgerEntry?> GetByIdAsync(string vin, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a new immutable ledger entry for the specified VIN.
    /// </summary>
    /// <param name="entity">The ledger entry to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AssetLedgerEntry> AppendAsync(AssetLedgerEntry entity, CancellationToken cancellationToken = default);
}
