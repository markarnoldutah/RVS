using RVS.Domain.Entities;

namespace RVS.Domain.Shared;

/// <summary>
/// Result of issuing a per-customer status token (Spec X-5).
/// Carries the persisted account plus the <b>raw</b> token — the only point at which the raw
/// value is available. Hand <see cref="RawToken"/> to the customer; only its hash is stored.
/// </summary>
/// <param name="Account">The account after the new token hash and expiry were persisted.</param>
/// <param name="RawToken">The raw status token to deliver to the customer.</param>
public sealed record MagicLinkIssueResult(GlobalCustomerAcct Account, string RawToken);
