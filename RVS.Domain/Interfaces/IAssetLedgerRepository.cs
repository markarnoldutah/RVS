using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for persisting and retrieving <see cref="AssetLedgerEntry"/> entities.
/// Partition key: <c>/assetId</c>. Append-only — entries are immutable once created.
/// </summary>
public interface IAssetLedgerRepository
{
    /// <summary>
    /// Gets all ledger entries for a specific asset, ordered by submission date.
    /// </summary>
    /// <param name="assetId">Compound asset identifier (partition key), e.g. <c>RV:1FTFW1ET5EKE12345</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<AssetLedgerEntry>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single ledger entry by asset identifier and entry identifier.
    /// Returns <c>null</c> when no matching document is found.
    /// </summary>
    /// <param name="assetId">Compound asset identifier (partition key), e.g. <c>RV:1FTFW1ET5EKE12345</c>.</param>
    /// <param name="id">Ledger entry identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AssetLedgerEntry?> GetByIdAsync(string assetId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a new immutable ledger entry for the specified asset.
    /// </summary>
    /// <param name="entity">The ledger entry to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AssetLedgerEntry> AppendAsync(AssetLedgerEntry entity, CancellationToken cancellationToken = default);
}
