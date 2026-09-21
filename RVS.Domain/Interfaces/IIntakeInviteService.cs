using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Creates and lists advisor intake invites (<c>Spec A-14</c>, issue #663).
/// </summary>
public interface IIntakeInviteService
{
    /// <summary>
    /// Creates an invite for <paramref name="locationId"/> on behalf of the current advisor.
    ///
    /// A texted invite requires the caller's consent and a US/Canada phone number, and is refused
    /// while texting is disabled (<c>ConflictException</c>), for a number that has opted out of
    /// texts (<c>ConflictException</c>), and once a rate limit is reached
    /// (<c>RateLimitExceededException</c>). The invite is persisted before the text is sent, so
    /// a failed send still leaves the consent record.
    ///
    /// A self-entry invite texts nobody, works while texting is disabled, and returns the
    /// prefilled intake URL.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="locationId">Location whose intake form the invite opens.</param>
    /// <param name="request">The invite to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The location does not exist.</exception>
    Task<IntakeInviteCreateResult> CreateAsync(
        string tenantId, string locationId, IntakeInviteCreateRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one invite at a location, for the send dialog's delivery status.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="locationId">Location the invite belongs to.</param>
    /// <param name="id">Invite id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">No such invite at this location.</exception>
    Task<IntakeInvite> GetByIdAsync(
        string tenantId, string locationId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The current advisor's invites at <paramref name="locationId"/> for the current shift,
    /// newest first. Backs the send dialog's recent-sends list.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="locationId">Location the invites were created for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<IntakeInvite>> ListRecentForCurrentAdvisorAsync(
        string tenantId, string locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the send dialog can offer right now. Texting is an environment-level switch
    /// (<c>AzureCommunicationServices:Sms:Enabled</c>), not a tenant setting, so this takes no
    /// identifiers and reads the same for everyone in the environment.
    /// </summary>
    IntakeInviteCapability GetCapability();
}

/// <summary>
/// Whether the API can text an invite right now (<c>Spec A-14</c>, issue #666). While
/// <paramref name="SmsEnabled" /> is <c>false</c> the dialog offers only <i>Fill it in myself</i>.
/// </summary>
/// <param name="SmsEnabled">Whether an invite will actually be handed to ACS.</param>
public sealed record IntakeInviteCapability(bool SmsEnabled);

/// <summary>
/// A newly created invite, plus the prefilled intake URL when the advisor is to open it
/// themselves. The URL carries the raw token, which is never stored, so it exists only here.
/// </summary>
/// <param name="Invite">The persisted invite.</param>
/// <param name="IntakeUrl">The prefilled intake URL for a self-entry invite; <c>null</c> for a texted one.</param>
public sealed record IntakeInviteCreateResult(IntakeInvite Invite, string? IntakeUrl);
