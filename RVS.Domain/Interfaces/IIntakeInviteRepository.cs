using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Repository for <see cref="IntakeInvite"/> documents (<c>Spec A-14</c>, issue #663).
/// Container: <c>intake-invites</c>. Partition key: <c>/tenantId</c>. Document id is the
/// token's SHA-256 hash, so a lookup by token is a point read.
/// </summary>
public interface IIntakeInviteRepository
{
    /// <summary>
    /// Gets an invite by id (the token hash). Returns <c>null</c> when no matching document exists.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="id">Invite id: <c>InviteToken.Hash(token)</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IntakeInvite?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists one advisor's invites for one location created at or after <paramref name="sinceUtc"/>,
    /// newest first, at most <paramref name="maxItems"/>.
    /// </summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="locationId">Location the invites were created for.</param>
    /// <param name="advisorUserId">Advisor who created them.</param>
    /// <param name="sinceUtc">Earliest creation time to include.</param>
    /// <param name="maxItems">Maximum number of invites to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<IntakeInvite>> ListRecentByAdvisorAsync(
        string tenantId, string locationId, string advisorUserId, DateTime sinceUtc, int maxItems,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new invite document.
    /// </summary>
    /// <param name="entity">The invite to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IntakeInvite> CreateAsync(IntakeInvite entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing invite document.
    /// </summary>
    /// <param name="entity">The updated invite.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IntakeInvite> UpdateAsync(IntakeInvite entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the invite sent with this ACS message id, across tenants, so a delivery report can
    /// be matched back to it (issue #665). Reports arrive with no tenant context. Returns
    /// <c>null</c> for a message that is not an invite — an A-2 confirmation, for instance.
    /// </summary>
    /// <param name="acsMessageId">The ACS message id recorded at send time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IntakeInvite?> GetByAcsMessageIdAcrossTenantsAsync(
        string acsMessageId, CancellationToken cancellationToken = default);
}
